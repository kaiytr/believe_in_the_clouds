using UnityEngine;
using System.Collections.Generic;

/// <summary>One logical hit, including both branches of a ground wave. Never reused after cancellation.</summary>
public sealed class BossAttackRecord
{
    public int Generation { get; }
    public int PatternId { get; }
    public int Id { get; }
    public int HitIndex { get; }
    public BossAttackKind Kind { get; }
    public BossBeliefKind Belief { get; }
    public AttackGeometry Geometry { get; }
    public float Damage { get; }
    public bool AllowsBurn { get; }
    public bool Activated { get; private set; }
    public bool WasThreatened { get; private set; }
    public bool EscapedByMovement { get; private set; }
    public bool Contacted { get; private set; }
    public bool DamageAccepted { get; private set; }
    public bool IsComplete { get; private set; }
    public AttackOutcome Outcome { get; private set; }
    public BossAttackEndReason EndReason { get; private set; }
    private Vector2 threatenedPosition;
    private readonly List<AttackGeometry> warningGeometry = new List<AttackGeometry>();
    public IReadOnlyList<AttackGeometry> WarningGeometry => warningGeometry;

    public BossAttackRecord(int generation, int patternId, int id, int hitIndex, BossAttackKind kind,
        BossBeliefKind belief, AttackGeometry geometry, float damage, bool allowsBurn)
    {
        Generation = generation;
        PatternId = patternId;
        Id = id;
        HitIndex = hitIndex;
        Kind = kind;
        Belief = belief;
        Geometry = geometry;
        warningGeometry.Add(geometry);
        Damage = Mathf.Max(0f, damage);
        AllowsBurn = allowsBurn;
    }

    public void AddWarningGeometry(AttackGeometry geometry) { if (!Activated && !IsComplete) warningGeometry.Add(geometry); }

    public void Observe(bool overlaps, Vector2 targetPosition)
    {
        if (IsComplete) return;
        if (overlaps && !WasThreatened) threatenedPosition = targetPosition;
        WasThreatened |= overlaps;
        if (WasThreatened && !overlaps && Vector2.SqrMagnitude(targetPosition - threatenedPosition) > 0.0025f)
            EscapedByMovement = true;
    }

    public void Activate() { if (!IsComplete) Activated = true; }
    public bool TryContact()
    {
        if (!Activated || IsComplete || Contacted) return false;
        Contacted = true;
        return true;
    }
    public void AcceptDamage() { if (Contacted && !IsComplete) DamageAccepted = true; }

    public bool Finish(BossAttackEndReason reason)
    {
        if (IsComplete) return false;
        IsComplete = true;
        EndReason = reason;
        Outcome = reason == BossAttackEndReason.Cancelled || reason == BossAttackEndReason.TargetLost ? AttackOutcome.Cancelled :
            DamageAccepted ? AttackOutcome.Hit :
            Activated && WasThreatened && EscapedByMovement && !Contacted ? AttackOutcome.EvadedByPlayer :
            reason == BossAttackEndReason.Wall ? AttackOutcome.Blocked : AttackOutcome.Missed;
        return true;
    }
}
