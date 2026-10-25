using System;
using UnityEngine;

public enum PlayerCombatSignalType { AttackAttempt, HitConfirmed, AttackEvaded }

public readonly struct PlayerCombatSignal
{
    public PlayerCombatSignalType Type { get; }
    public long AttackId { get; }
    public GameObject Actor { get; }
    public GameObject Target { get; }
    public float Damage { get; }
    public float OccurredAt { get; }
    public PlayerCombatSignal(PlayerCombatSignalType type, long attackId, GameObject actor, GameObject target, float damage)
    { Type = type; AttackId = attackId; Actor = actor; Target = target; Damage = damage; OccurredAt = Time.time; }
}

/// <summary>Shared attack signals. The caller owns each attack context and its lifetime; no global attack history.</summary>
public sealed class PlayerCombatEvents : MonoBehaviour
{
    public event Action<PlayerCombatSignal> SignalPublished;

    public PlayerAttackContext PublishAttackAttempt(GameObject target)
    {
        IDamageable receiver = target != null ? target.GetComponent<IDamageable>() : null;
        if (receiver == null) return null;
        var attack = new PlayerAttackContext(gameObject);
        return attack.Begin(receiver) ? attack : null;
    }

    public void PublishHitConfirmed(PlayerAttackContext attack, GameObject target, float damage, int hitIndex = 0)
    {
        if (attack == null || attack.Actor != gameObject || target == null) return;
        IDamageable receiver = target.GetComponent<IDamageable>();
        if (receiver != null) attack.ConfirmHit(receiver, damage, hitIndex);
    }

    public bool TryApplyHitAndPublish(PlayerAttackContext attack, GameObject target, float damage, int hitIndex = 0) =>
        attack != null && attack.Actor == gameObject && target != null &&
        attack.TryHit(target.GetComponent<IDamageable>(), damage, hitIndex);

    public void PublishAttackEvaded(int attackId, GameObject target)
    {
        if (attackId > 0 && target != null)
            Publish(new PlayerCombatSignal(PlayerCombatSignalType.AttackEvaded, attackId, gameObject, target, 0f));
    }

    internal void Publish(PlayerCombatSignal signal) => SignalPublished?.Invoke(signal);
}
