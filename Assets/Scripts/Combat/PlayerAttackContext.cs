using System;
using System.Threading;
using UnityEngine;

public enum PlayerAttackKind { Direct, Projectile, Skill, DamageOverTime, Summon }

/// <summary>Create once per attack; reuse across colliders, with a distinct index for each real hit.</summary>
public sealed class PlayerAttackContext
{
    private static long nextId;
    public long Id { get; }
    public GameObject Actor { get; }
    public PlayerAttackKind Kind { get; }
    public bool CountsForBelief => Kind == PlayerAttackKind.Direct || Kind == PlayerAttackKind.Projectile ||
                                  Kind == PlayerAttackKind.Skill;

    public PlayerAttackContext(GameObject actor, PlayerAttackKind kind = PlayerAttackKind.Direct)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        Id = Interlocked.Increment(ref nextId);
        Actor = actor;
        Kind = kind;
    }

    public DamageInfo Damage(float amount, int hitIndex = 0) => new DamageInfo(amount, this, hitIndex);
    public bool TryHit(EnemyUnit target, float amount, int hitIndex = 0) =>
        target != null && target.TakeDamage(Damage(amount, hitIndex));
}

public readonly struct PlayerHitResult
{
    public long AttackId { get; }
    public int HitIndex { get; }
    public float AppliedDamage { get; }
    public float OccurredAt { get; }
    public Bounds EnemyBounds { get; }
    public Bounds PlayerBounds { get; }
    public float EnemyFacing { get; }

    public PlayerHitResult(long attackId, int hitIndex, float appliedDamage, float occurredAt,
        Bounds enemyBounds, Bounds playerBounds, float enemyFacing)
    {
        AttackId = attackId;
        HitIndex = hitIndex;
        AppliedDamage = appliedDamage;
        OccurredAt = occurredAt;
        EnemyBounds = enemyBounds;
        PlayerBounds = playerBounds;
        EnemyFacing = enemyFacing;
    }
}
