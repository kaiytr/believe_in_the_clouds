using System.Collections;
using UnityEngine;

public class MagicCircleAttack : EnemyAttack
{
    [Header("Range")]
    [SerializeField, Min(0f)] private float attackRange = 7f;

    [Header("Magic Circle")]
    [SerializeField] private MagicCircle magicCirclePrefab;
    [SerializeField, Min(0.01f)] private float circleRadius = 1.5f;
    [Tooltip("Height of the zone (and of the warning column) above the floor.")]
    [SerializeField, Min(0.1f)] private float zoneHeight = 1.6f;
    [Tooltip("How long the floor zone stays after it is laid.")]
    [SerializeField, Min(0.1f)] private float zoneDuration = 4f;
    [Tooltip("Seconds between damage ticks while standing in the zone.")]
    [SerializeField, Min(0.05f)] private float damageInterval = 1f;

    [Header("Slow")]
    [SerializeField, Range(0f, 1f)] private float slowMultiplier = 0.5f;
    [Tooltip("The slow is refreshed while standing in the zone and wears off this long after leaving it.")]
    [SerializeField, Min(0f)] private float slowLinger = 0.4f;

    [Header("Danger Indicator")]
    [SerializeField] private DangerIndicator dangerIndicatorPrefab;

    private Vector2 lockedTargetPosition; // floor point under the target, where the zone is laid

    public float AttackRange => attackRange;
    // The rear-entry challenge needs an opportunity to reach the rear before the AI can turn.
    public override float WindupDuration => owner != null && owner.Belief != null &&
        owner.Belief.Profile != null && owner.Belief.Profile.Rule == BeliefRule.RearEntry
        ? Mathf.Max(windupTime, owner.Belief.Profile.RearInterval + 1f) : windupTime;
    public override bool IsConfigured => base.IsConfigured && magicCirclePrefab != null &&
                                         dangerIndicatorPrefab != null && circleRadius > 0f;

    protected override bool CanAttack(Transform target)
    {
        return target != null && Vector2.Distance(transform.position, target.position) <= attackRange;
    }

    protected override void PrepareAttack(Transform target)
    {
        lockedTargetPosition = FindFloorPoint(target);
    }

    // The zone lies on the floor under the target even when the target is airborne when the warning starts.
    private Vector2 FindFloorPoint(Transform target)
    {
        Collider2D body = PlayerBodyCollider;
        if (body == null) return target.position;
        Bounds bounds = body.bounds;
        PlayerMove move = target.GetComponentInParent<PlayerMove>();
        int mask = move != null ? move.GroundLayer.value : Physics2D.DefaultRaycastLayers;
        Vector2 probe = new Vector2(bounds.center.x, bounds.min.y + 0.25f);
        RaycastHit2D hit = Physics2D.Raycast(probe, Vector2.down, 8f, mask);
        return new Vector2(bounds.center.x, hit.collider != null ? hit.point.y : bounds.min.y);
    }

    private Vector2 ZoneCenter => lockedTargetPosition + new Vector2(0f, zoneHeight * 0.5f);
    private Vector2 ZoneSize => new Vector2(circleRadius * 2f, zoneHeight);

    // The warning column is placed at the locked spot on the ground, not around the executive.
    public override bool WarningFollowsOwner => false;

    protected override DangerIndicator ShowDangerIndicator()
    {
        if (dangerIndicatorPrefab == null)
            return null;

        DangerIndicator indicator = Instantiate(dangerIndicatorPrefab);
        indicator.UseGroundColumn();
        indicator.Setup(ZoneCenter, 0f, ZoneSize);
        return indicator;
    }

    protected override bool IsPlayerInThreatRange(Collider2D bodyCollider)
    {
        return ColliderOverlapsBox(bodyCollider, ZoneCenter, ZoneSize);
    }

    protected override IEnumerator ExecuteAttackRoutine()
    {
        if (magicCirclePrefab == null)
        {
            Debug.LogError($"{name}: MagicCircleAttack has no MagicCircle prefab.", this);
            yield break;
        }

        MarkAttackAsLaunched();
        MagicCircle circle = Instantiate(magicCirclePrefab, lockedTargetPosition, Quaternion.identity);
        bool harmfulEffectApplied = circle.Activate(
            circleRadius,
            zoneHeight,
            damage * DoctrineAttackMultiplier,
            slowMultiplier,
            slowLinger,
            damageInterval,
            zoneDuration,
            PlayerBodyCollider,
            out bool hpDamageApplied, gameObject);
        if (hpDamageApplied) NotifyPlayerHpDamageApplied(CurrentAttackId);

        if (circle.AvoidedByDash) NoteAvoidedByDash(CurrentAttackId);

        if (harmfulEffectApplied)
            ReportOutcome(AttackOutcome.Hit, completedNormally: true);
        else if (WasPlayerThreatenedDuringWarning && IsPlayerOutsideThreatRange())
            ReportOutcome(AttackOutcome.EvadedByPlayer, completedNormally: true);
        else
            ReportOutcome(AttackOutcome.Missed, completedNormally: true);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
