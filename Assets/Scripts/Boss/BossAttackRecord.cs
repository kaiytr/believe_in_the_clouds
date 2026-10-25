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
    public BossAttackGeometry Geometry { get; }
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
    private readonly List<BossAttackGeometry> warningGeometry = new List<BossAttackGeometry>();
    public IReadOnlyList<BossAttackGeometry> WarningGeometry => warningGeometry;

    public BossAttackRecord(int generation, int patternId, int id, int hitIndex, BossAttackKind kind,
        BossBeliefKind belief, BossAttackGeometry geometry, float damage, bool allowsBurn)
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

    public void AddWarningGeometry(BossAttackGeometry geometry) { if (!Activated && !IsComplete) warningGeometry.Add(geometry); }

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
            DamageAccepted ? AttackOutcome.Hit : reason == BossAttackEndReason.Wall ? AttackOutcome.Blocked :
            Activated && WasThreatened && EscapedByMovement && !Contacted ? AttackOutcome.EvadedByPlayer : AttackOutcome.Missed;
        return true;
    }
}

/// <summary>All sizes are world units. The same shape drives warning, threat and active queries.</summary>
public readonly struct BossAttackGeometry
{
    public Vector2 Center { get; }
    public Vector2 Size { get; }
    public float Angle { get; }
    public bool IsCircle { get; }
    public BossAttackGeometry(Vector2 center, Vector2 size, float angle = 0f, bool circle = false)
    { Center = center; Size = size; Angle = angle; IsCircle = circle; }

    public static BossAttackGeometry Corridor(Vector2 origin, Vector2 direction, float distance, float thickness)
    {
        direction.Normalize();
        return new BossAttackGeometry(origin + direction * distance * 0.5f,
            new Vector2(distance, thickness), Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    public bool Overlaps(Collider2D target)
    {
        if (target == null || !target.enabled || !target.gameObject.activeInHierarchy) return false;
        Collider2D[] hits = IsCircle ? Physics2D.OverlapCircleAll(Center, Size.x * 0.5f) : Physics2D.OverlapBoxAll(Center, Size, Angle);
        foreach (Collider2D hit in hits) if (hit == target) return true;
        return false;
    }
}
