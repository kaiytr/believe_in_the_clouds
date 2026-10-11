using System.Collections.Generic;
using UnityEngine;

// 효과 실행 계층. 교리마다 분기문을 추가하는 대신,
// 모든 Result가 이 공통 경로를 통과한다.
// 호출 측(전투 코드, PlayerMove 등)에 교리별 분기를 두지 않는다.
public static class DoctrineEffectExecutor
{
    public static bool VerboseLogs = false;

    private static readonly HashSet<Health> OverlapSeen = new HashSet<Health>();
    public const float DefaultHealAmount = 20f;
    public const float DefaultDamageAmount = 10f;
    public const float DefaultExplodeDamage = 30f;
    public const float DefaultExplodeRadius = 3f;
    public const float DefaultSlowMultiplier = 0.5f;
    public const float DefaultBuffMultiplier = 1.5f;
    public const float DefaultDebuffDuration = 3f;
    public const float DefaultBuffDuration = 5f;

    public static bool IsSupported(ResultType result)
    {
        switch (result)
        {
            case ResultType.Heal:
            case ResultType.Explode:
            case ResultType.Slow:
            case ResultType.IncreaseSpeed:
            case ResultType.IncreaseAttack:
            case ResultType.TakeDamage:
                return true;
            default:
                return false;
        }
    }

    // target: 효과가 적용될 객체. 일반적으로 이벤트의 SubjectObject.
    // source: 원인이 된 객체(가해자 등). TakeDamage/Explode의 source로 전달된다.
    public static bool Execute(Doctrine doctrine, GameObject target, GameObject source = null)
    {
        if (doctrine == null || target == null)
        {
            return false;
        }

        switch (doctrine.Result)
        {
            case ResultType.Heal:
                return ExecuteHeal(target, ResolveMagnitude(doctrine.Magnitude, DefaultHealAmount));

            case ResultType.TakeDamage:
                return ExecuteTakeDamage(target, ResolveMagnitude(doctrine.Magnitude, DefaultDamageAmount), source);

            case ResultType.Slow:
                return ExecuteSpeedBuff(
                    target,
                    ResolveSlowMultiplier(doctrine.Magnitude),
                    ResolveDuration(doctrine.Duration, DefaultDebuffDuration));

            case ResultType.IncreaseSpeed:
                return ExecuteSpeedBuff(
                    target,
                    ResolveBoostMultiplier(doctrine.Magnitude),
                    ResolveDuration(doctrine.Duration, DefaultBuffDuration));

            case ResultType.IncreaseAttack:
                return ExecuteAttackBuff(
                    target,
                    ResolveBoostMultiplier(doctrine.Magnitude),
                    ResolveDuration(doctrine.Duration, DefaultBuffDuration));

            case ResultType.Explode:
                return ExecuteExplode(
                    target,
                    ResolveMagnitude(doctrine.Magnitude, DefaultExplodeDamage),
                    ResolveRadius(doctrine.Radius),
                    source);

            default:
                Debug.LogWarning($"[Doctrine] 미구현 Result: {doctrine.Result}.");
                return false;
        }
    }

    public static bool ExecuteHeal(GameObject target, float amount)
    {
        if (target == null)
        {
            return false;
        }

        Health health = target.GetComponent<Health>();
        if (health == null)
        {
            Debug.LogWarning($"[Doctrine] Heal 실패: {target.name}에 Health가 없습니다.");
            return false;
        }

        health.Heal(amount);
        return true;
    }

    public static bool ExecuteTakeDamage(GameObject target, float amount, GameObject source = null)
    {
        if (target == null)
        {
            return false;
        }

        Health health = target.GetComponent<Health>();
        if (health == null)
        {
            Debug.LogWarning($"[Doctrine] TakeDamage 실패: {target.name}에 Health가 없습니다.");
            return false;
        }

        health.TakeDamage(amount, source);
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
            Debug.LogWarning($"[Doctrine] IncreaseAttack 실패: {target.name}에 DoctrineStats가 없습니다.");
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
            Debug.LogWarning($"[Doctrine] 속도 변경 실패: {target.name}에 DoctrineStats가 없습니다.");
            return false;
        }

        stats.ApplySpeedBuff(multiplier, duration);
        return true;
    }

    public static bool ExecuteExplode(GameObject center, float damage, float radius, GameObject source = null)
    {
        if (center == null)
        {
            return false;
        }

        List<Health> targets = FindTargetsInRadius(center, radius);
        Health self = center.GetComponent<Health>();
        return ApplyExplodeDamage(targets, damage, source, self);
    }

    public static List<Health> FindTargetsInRadius(GameObject center, float radius)
    {
        List<Health> result = new List<Health>();
        if (center == null || radius <= 0f)
        {
            return result;
        }

        // NonAlloc/ContactFilter 오버로드가 이 프로젝트 설정에서
        // 오버로드 해석에 실패하므로 확실하게 컴파일되는 OverlapCircleAll 사용.
        Collider2D[] hits = Physics2D.OverlapCircleAll((Vector2)center.transform.position, radius);
        OverlapSeen.Clear();
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            if (!hit.TryGetComponent(out Health health) || health == null)
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

    // 물리 쿼리와 분리된 순수 피해 적용부. 테스트에서 직접 호출 가능하다.
    public static bool ApplyExplodeDamage(
        List<Health> targets,
        float damage,
        GameObject source,
        Health exclude)
    {
        if (targets == null)
        {
            return false;
        }

        int hitCount = 0;
        foreach (Health health in targets)
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
            Debug.Log($"[Doctrine] Explode 실행: {hitCount}기 타격 (피해 {damage}).");
        }
        return true;
    }

    private static float ResolveMagnitude(float value, float defaultValue)
    {
        return value > 0f ? value : defaultValue;
    }

    private static float ResolveDuration(float value, float defaultValue)
    {
        return value > 0f ? value : defaultValue;
    }

    private static float ResolveRadius(float value)
    {
        return value > 0f ? value : DefaultExplodeRadius;
    }

    private static float ResolveBoostMultiplier(float value)
    {
        return value > 1f ? value : DefaultBuffMultiplier;
    }

    private static float ResolveSlowMultiplier(float value)
    {
        return value > 0f && value < 1f ? value : DefaultSlowMultiplier;
    }
}
