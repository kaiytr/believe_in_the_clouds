using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public enum PlayerAttackKind { Direct, Projectile, Skill, DamageOverTime, Summon }

/// <summary>Create once per attack; reuse across colliders, with a distinct index for each real hit.</summary>
public sealed class PlayerAttackContext : IDisposable
{
    private sealed class TargetHits
    {
        public readonly int Generation;
        public readonly Dictionary<int, bool> Hits = new Dictionary<int, bool>();
        public TargetHits(int generation) { Generation = generation; }
    }
    private static long nextId;
    private readonly Dictionary<IDamageable, TargetHits> targets = new Dictionary<IDamageable, TargetHits>();
    private bool completed;
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
    public bool TryHit(IDamageable target, float amount, int hitIndex = 0) =>
        amount > 0f && !float.IsNaN(amount) && !float.IsInfinity(amount) && hitIndex >= 0 &&
        Begin(target) && target.TakeDamage(Damage(amount, hitIndex));

    public bool Begin(IDamageable target, bool publishAttempt = true)
    {
        if (completed || Actor == null || target == null || target is Component component && component == null)
            return false;
        if (targets.TryGetValue(target, out TargetHits hits))
            return hits.Generation == GenerationOf(target);
        targets.Add(target, new TargetHits(GenerationOf(target)));
        if (publishAttempt) Publish(target, PlayerCombatSignalType.AttackAttempt, 0f);
        return !completed && targets.TryGetValue(target, out hits) && hits.Generation == GenerationOf(target);
    }

    public bool TryConsumeHit(IDamageable target, int hitIndex)
    {
        if (hitIndex < 0 || !Begin(target)) return false;
        return targets[target].Hits.TryAdd(hitIndex, false);
    }

    public bool ConfirmHit(IDamageable target, float appliedDamage, int hitIndex = 0)
    {
        if (hitIndex < 0 || appliedDamage <= 0f || !Begin(target)) return false;
        TargetHits hits = targets[target];
        if (hits.Hits.TryGetValue(hitIndex, out bool confirmed) && confirmed) return false;
        hits.Hits[hitIndex] = true;
        Publish(target, PlayerCombatSignalType.HitConfirmed, appliedDamage);
        return true;
    }

    private void Publish(IDamageable target, PlayerCombatSignalType type, float damage)
    {
        if (CountsForBelief && target is Component component && component != null && Actor != null)
            Actor.GetComponent<PlayerCombatEvents>()?.Publish(new PlayerCombatSignal(type, Id, Actor, component.gameObject, damage));
    }

    private static int GenerationOf(IDamageable target) => target is ICombatGeneration owner ? owner.CombatGeneration : 0;
    public void Dispose() { completed = true; targets.Clear(); }
}

/// <summary>Encounter resets invalidate attacks captured before the reset.</summary>
public interface ICombatGeneration { int CombatGeneration { get; } }

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
