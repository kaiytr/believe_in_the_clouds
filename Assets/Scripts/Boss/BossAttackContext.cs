using System.Collections.Generic;

/// <summary>
/// One whole regular attack (a three-hit combo is one context). Created when the regular lane starts the
/// attack, so burn enhancement and a linked dodge prophecy are decided once and never re-read mid-attack.
/// </summary>
public sealed class BossRegularAttackContext
{
    public int PatternId { get; }
    public BossPatternKind Kind { get; }
    /// <summary>Burn enhancement was active when this attack started.</summary>
    public bool BurnEnhanced { get; }
    /// <summary>"공격하고 피할 것이다" prophecy linked to this attack, or null.</summary>
    public BossProphecy Dodge { get; internal set; }
    public readonly List<BossAttackRecord> Records = new List<BossAttackRecord>(3);

    public BossRegularAttackContext(int patternId, BossPatternKind kind, bool burnEnhanced, BossProphecy dodge = null)
    { PatternId = patternId; Kind = kind; BurnEnhanced = burnEnhanced; Dodge = dodge; }

    public BossBeliefKind DisplayBelief =>
        Dodge != null ? BossBeliefKind.AttackAndDodge : BurnEnhanced ? BossBeliefKind.Burn : BossBeliefKind.None;
}

/// <summary>Unresolved prophecy. Observation kinds close at the first valid regular attack start; the dodge kind links to one attack.</summary>
public sealed class BossProphecy
{
    public BossBeliefKind Kind { get; }
    public float StartedAt { get; }
    public bool PlayerActed { get; internal set; }
    public bool Reduced { get; internal set; }
    public BossRegularAttackContext Linked { get; internal set; }
    internal readonly HashSet<long> AttackIds = new HashSet<long>();
    public bool IsObservation => Kind == BossBeliefKind.AttackIntent || Kind == BossBeliefKind.NoAttack;
    public BossProphecy(BossBeliefKind kind, float startedAt) { Kind = kind; StartedAt = startedAt; }
}
