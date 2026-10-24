using System;
using UnityEngine;

public enum BossPhase
{
    One,
    Two
}

public enum BossPatternKind
{
    MeleeCombo,
    Dash,
    Projectile,
    AttackIntentBelief,
    CloudBelief,
    BurnBelief,
    AttackAndDodgeBelief,
    NoAttackBelief,
    ShockwaveBelief
}

public enum BossBeliefKind
{
    None,
    AttackIntent,
    Cloud,
    Burn,
    AttackAndDodge,
    NoAttack,
    Shockwave
}

public enum BossAttackSignalMode
{
    Attempt,
    HitConfirmed,
    Either
}

public enum BossAttackKind
{
    Melee,
    Dash,
    Projectile,
    Shockwave
}

[Serializable]
public struct BossPlayerCombatSignal
{
    public BossPlayerCombatSignalType Type { get; }
    public int AttackId { get; }
    public GameObject Actor { get; }
    public GameObject Target { get; }
    public float Damage { get; }
    public float OccurredAt { get; }

    public BossPlayerCombatSignal(
        BossPlayerCombatSignalType type,
        int attackId,
        GameObject actor,
        GameObject target,
        float damage)
    {
        Type = type;
        AttackId = attackId;
        Actor = actor;
        Target = target;
        Damage = damage;
        OccurredAt = Time.time;
    }
}

public enum BossPlayerCombatSignalType
{
    AttackAttempt,
    HitConfirmed,
    AttackEvaded
}

[Serializable]
public sealed class BossPatternWeight
{
    [SerializeField] private BossPatternKind pattern;
    [SerializeField, Min(0f)] private float baseWeight = 1f;
    [SerializeField, Min(0f)] private float repeatPenaltyPerUse = 0.5f;

    public BossPatternKind Pattern => pattern;
    public float BaseWeight => baseWeight;
    public float RepeatPenaltyPerUse => repeatPenaltyPerUse;

    public BossPatternWeight() { }

    public BossPatternWeight(BossPatternKind pattern, float baseWeight, float repeatPenaltyPerUse)
    {
        this.pattern = pattern;
        this.baseWeight = baseWeight;
        this.repeatPenaltyPerUse = repeatPenaltyPerUse;
    }

    public float EffectiveWeight(int timesUsed)
    {
        return baseWeight / (1f + Mathf.Max(0, timesUsed) * repeatPenaltyPerUse);
    }
}
