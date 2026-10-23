using System.Collections;
using UnityEngine;

public class MagicCircleAttack : EnemyAttack
{
    [Header("Range")]
    [SerializeField, Min(0f)] private float attackRange = 7f;

    [Header("Magic Circle")]
    [SerializeField] private MagicCircle magicCirclePrefab;
    [SerializeField, Min(0.01f)] private float circleRadius = 1.5f;
    [SerializeField, Min(0f)] private float circleVisualDuration = 0.8f;

    [Header("Slow")]
    [SerializeField, Range(0f, 1f)] private float slowMultiplier = 0.5f;
    [SerializeField, Min(0f)] private float slowDuration = 2f;

    [Header("Danger Indicator")]
    [SerializeField] private DangerIndicator dangerIndicatorPrefab;

    private Vector2 lockedTargetPosition;

    public float AttackRange => attackRange;
    public override bool IsConfigured => base.IsConfigured && magicCirclePrefab != null &&
                                         dangerIndicatorPrefab != null && circleRadius > 0f;

    protected override bool CanAttack(Transform target)
    {
        return target != null && Vector2.Distance(transform.position, target.position) <= attackRange;
    }

    protected override void PrepareAttack(Transform target)
    {
        lockedTargetPosition = target.position;
    }

    protected override DangerIndicator ShowDangerIndicator()
    {
        if (dangerIndicatorPrefab == null)
            return null;

        float diameter = circleRadius * 2f;
        DangerIndicator indicator = Instantiate(dangerIndicatorPrefab);
        indicator.Setup(lockedTargetPosition, 0f, new Vector2(diameter, diameter), windupTime);
        return indicator;
    }

    protected override bool IsPlayerInThreatRange(Collider2D bodyCollider)
    {
        return ColliderOverlapsCircle(bodyCollider, lockedTargetPosition, circleRadius);
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
            damage,
            slowMultiplier,
            slowDuration,
            circleVisualDuration,
            PlayerBodyCollider);

        if (harmfulEffectApplied)
            ReportOutcome(AttackOutcome.Hit);
        else if (WasPlayerThreatenedDuringWarning && IsPlayerOutsideThreatRange())
            ReportOutcome(AttackOutcome.EvadedByPlayer);
        else
            ReportOutcome(AttackOutcome.Missed);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
