using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adapter point for the future player attack and dodge systems.
/// Attack IDs identify one logical player attack across attempt and hit signals.
/// </summary>
public sealed class BossPlayerCombatEvents : MonoBehaviour
{
    private static int nextAttackId = 1;
    private readonly HashSet<int> appliedHitIds = new HashSet<int>();
    private readonly Dictionary<int, (GameObject target, int generation)> attempts = new Dictionary<int, (GameObject, int)>();

    public event Action<BossPlayerCombatSignal> SignalPublished;

    public int PublishAttackAttempt(GameObject target)
    {
        if (target == null)
            return 0;

        int attackId = nextAttackId++;
        var boss = target.GetComponent<BossCombatController>();
        attempts[attackId] = (target, boss != null ? boss.Generation : 0);
        Publish(BossPlayerCombatSignalType.AttackAttempt, attackId, target, 0f);
        return attackId;
    }

    public void PublishHitConfirmed(int attackId, GameObject target, float damage)
    {
        if (!IsCurrentAttempt(attackId, target) || damage <= 0f)
            return;

        Publish(BossPlayerCombatSignalType.HitConfirmed, attackId, target, damage);
    }

    /// <summary>Test/future attack adapter: apply damage once, then notify. Notifications never apply damage themselves.</summary>
    public bool TryApplyHitAndPublish(int attackId, GameObject target, float damage)
    {
        if (!IsCurrentAttempt(attackId, target) || damage <= 0f || appliedHitIds.Contains(attackId)) return false;
        IDamageable receiver = target.GetComponent<IDamageable>();
        if (receiver == null) return false;
        appliedHitIds.Add(attackId);
        bool accepted = receiver.TakeDamage(new DamageInfo(damage, DamageSource.PlayerOwned));
        if (accepted) PublishHitConfirmed(attackId, target, damage);
        return accepted;
    }

    private bool IsCurrentAttempt(int id, GameObject target)
    {
        if (id <= 0 || target == null || !attempts.TryGetValue(id, out var attempt) || attempt.target != target) return false;
        var boss = target.GetComponent<BossCombatController>();
        return boss == null || attempt.generation == boss.Generation;
    }
    public void ResetTestHistory() { appliedHitIds.Clear(); attempts.Clear(); }

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
