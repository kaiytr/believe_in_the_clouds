using System.Collections.Generic;
using UnityEngine;

// Effect execution layer. Every Result goes through this common path
// instead of adding branches per doctrine.
// Table-fixed values (DoctrineTable) are the defaults:
//   Explode       = 30% of exploder max HP, friends and foes alike (incl. self)
//   IncreaseSpeed = x1.4 persistent (re-applied by manager while equipped)
//   IncreaseAttack= x1.2 for 5s
//   Heal          = 10% of max HP
// Doctrine.Magnitude (>0) acts as an explicit damage/amount override when set.
public static class DoctrineEffectExecutor
{
    public static bool VerboseLogs = false;

    private static readonly HashSet<IHealth> OverlapSeen = new HashSet<IHealth>();
    public const float DefaultExplodeRadius = 3f;
    public const float DefaultAttackBuffDuration = 5f;

    public static bool IsSupported(ResultType result)
    {
        switch (result)
        {
            case ResultType.Heal:
            case ResultType.Explode:
            case ResultType.IncreaseSpeed:
            case ResultType.IncreaseAttack:
                return true;
            default:
                return false;
        }
    }

    // target: object the effect applies to. Usually the event's SubjectObject.
    // source: cause object (attacker etc). Passed as source of Explode damage.
    public static bool Execute(Doctrine doctrine, GameObject target, GameObject source = null)
    {
        if (doctrine == null || target == null)
        {
            return false;
        }

        switch (doctrine.Result)
        {
            case ResultType.Heal:
                return ExecuteHealMaxRatio(target, DoctrineTable.HealMaxHpRatio, doctrine.Magnitude);

            case ResultType.IncreaseSpeed:
                return ExecuteSpeedBuff(
                    target,
                    1f + DoctrineTable.SpeedIncreaseRatio,
                    0f); // persistent (no revert)

            case ResultType.IncreaseAttack:
                return ExecuteAttackBuff(
                    target,
                    1f + DoctrineTable.AttackIncreaseRatio,
                    ResolveDuration(doctrine.Duration, DoctrineTable.AttackBuffDuration));

            case ResultType.Explode:
                return ExecuteExplodeMaxRatio(
                    target,
                    DoctrineTable.ExplodeMaxHpRatio,
                    ResolveRadius(doctrine.Radius),
                    source,
                    doctrine.Magnitude);

            default:
                Debug.LogWarning($"[Doctrine] Unimplemented Result: {doctrine.Result}.");
                return false;
        }
    }

    public static bool ExecuteHeal(GameObject target, float amount)
    {
        if (target == null)
        {
            return false;
        }

        IHealth health = target.GetComponent<IHealth>();
        if (health == null)
        {
            Debug.LogWarning($"[Doctrine] Heal failed: {target.name} has no IHealth.");
            return false;
        }

        health.Heal(amount);
        return true;
    }

    // Table heal: 10% of max HP. amountOverride (>0) replaces the ratio amount.
    public static bool ExecuteHealMaxRatio(GameObject target, float ratio, float amountOverride = 0f)
    {
        if (target == null)
        {
            return false;
        }

        IHealth health = target.GetComponent<IHealth>();
        if (health == null)
        {
            Debug.LogWarning($"[Doctrine] Heal failed: {target.name} has no IHealth.");
            return false;
        }

        float amount = amountOverride > 0f ? amountOverride : health.MaxHealth * ratio;
        if (amount <= 0f)
        {
            return false;
        }

        health.Heal(amount);
        return true;
    }

    public static bool ExecuteAttackBuff(GameObject target, float multiplier, float duration)
    {
        if (target == null)
        {
            return false;
        }

        DoctrineStats stats = target.GetComponent<DoctrineStats>();
        if (stats == null)
        {
            Debug.LogWarning($"[Doctrine] IncreaseAttack failed: {target.name} has no DoctrineStats.");
            return false;
        }

        stats.ApplyAttackBuff(multiplier, duration);
        return true;
    }

    public static bool ExecuteSpeedBuff(GameObject target, float multiplier, float duration)
    {
        if (target == null)
        {
            return false;
        }

        DoctrineStats stats = target.GetComponent<DoctrineStats>();
        if (stats == null)
        {
            Debug.LogWarning($"[Doctrine] Speed change failed: {target.name} has no DoctrineStats.");
            return false;
        }

        stats.ApplySpeedBuff(multiplier, duration);
        return true;
    }

    // Table explode: ratio of the exploder's max HP, friends and foes alike (incl. self).
    // damageOverride (>0) replaces the ratio damage (legacy params / tuning).
    public static bool ExecuteExplodeMaxRatio(
        GameObject center,
        float ratio,
        float radius,
        GameObject source = null,
        float damageOverride = 0f)
    {
        if (center == null)
        {
            return false;
        }

        float damage = damageOverride;
        if (damage <= 0f)
        {
            IHealth selfHealth = center.GetComponent<IHealth>();
            float maxHp = selfHealth != null ? selfHealth.MaxHealth : 0f;
            damage = maxHp * ratio;
        }

        if (damage <= 0f)
        {
            return false;
        }

        List<IHealth> targets = FindTargetsInRadius(center, radius);
        return ApplyExplodeDamage(targets, damage, source, null);
    }

    public static bool ExecuteExplode(GameObject center, float damage, float radius, GameObject source = null)
    {
        if (center == null)
        {
            return false;
        }

        List<IHealth> targets = FindTargetsInRadius(center, radius);
        return ApplyExplodeDamage(targets, damage, source, null);
    }

    public static List<IHealth> FindTargetsInRadius(GameObject center, float radius)
    {
        List<IHealth> result = new List<IHealth>();
        if (center == null || radius <= 0f)
        {
            return result;
        }

        // OverlapCircleAll: NonAlloc/ContactFilter overloads fail overload
        // resolution in this project setup, so use the reliable overload.
        Collider2D[] hits = Physics2D.OverlapCircleAll((Vector2)center.transform.position, radius);
        OverlapSeen.Clear();
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            IHealth health = hit.GetComponent<IHealth>();
            if (health == null)
            {
                continue;
            }

            if (OverlapSeen.Add(health))
            {
                result.Add(health);
            }
        }

        return result;
    }

    // Pure damage application separated from physics query. Callable from tests.
    // Table rule: friends and foes alike, so exclude is normally null (self included).
    // exclude is kept for explicit cases only.
    public static bool ApplyExplodeDamage(
        List<IHealth> targets,
        float damage,
        GameObject source,
        IHealth exclude)
    {
        if (targets == null)
        {
            return false;
        }

        int hitCount = 0;
        foreach (IHealth health in targets)
        {
            if (health == null || health == exclude)
            {
                continue;
            }

            health.TakeDamage(damage, source);
            hitCount++;
        }

        if (VerboseLogs)
        {
            Debug.Log($"[Doctrine] Explode executed: {hitCount} hit (damage {damage}).");
        }
        return true;
    }

    private static float ResolveDuration(float value, float defaultValue)
    {
        return value > 0f ? value : defaultValue;
    }

    private static float ResolveRadius(float value)
    {
        return value > 0f ? value : DefaultExplodeRadius;
    }
}
