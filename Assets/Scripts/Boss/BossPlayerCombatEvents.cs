using System;
using UnityEngine;

/// <summary>
/// Adapter point for the future player attack and dodge systems.
/// Attack IDs identify one logical player attack across attempt and hit signals.
/// </summary>
public sealed class BossPlayerCombatEvents : MonoBehaviour
{
    private static int nextAttackId = 1;

    public event Action<BossPlayerCombatSignal> SignalPublished;

    public int PublishAttackAttempt(GameObject target)
    {
        if (target == null)
            return 0;

        int attackId = nextAttackId++;
        Publish(BossPlayerCombatSignalType.AttackAttempt, attackId, target, 0f);
        return attackId;
    }

    public void PublishHitConfirmed(int attackId, GameObject target, float damage)
    {
        if (attackId <= 0 || target == null || damage <= 0f)
            return;

        Publish(BossPlayerCombatSignalType.HitConfirmed, attackId, target, damage);
    }

    /// <summary>Reports that a specific boss attack was avoided by this player.</summary>
    public void PublishBossAttackEvaded(int bossAttackId, GameObject boss)
    {
        if (bossAttackId <= 0 || boss == null)
            return;

        Publish(BossPlayerCombatSignalType.AttackEvaded, bossAttackId, boss, 0f);
    }

    private void Publish(
        BossPlayerCombatSignalType type,
        int attackId,
        GameObject target,
        float damage)
    {
        SignalPublished?.Invoke(new BossPlayerCombatSignal(
            type,
            attackId,
            gameObject,
            target,
            damage));
    }
}
