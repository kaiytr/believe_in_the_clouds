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
    Shockwave,
    Spike
}

public enum BossAttackEndReason { Completed, Contact, Wall, Cancelled, TargetLost }
public enum BossPresentationState { Idle, Stare, Windup, Active, Recovery, Charging, Vulnerable, Dead }

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
