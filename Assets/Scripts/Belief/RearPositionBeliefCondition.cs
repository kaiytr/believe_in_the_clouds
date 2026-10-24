using UnityEngine;

public sealed class RearPositionBeliefCondition : BeliefCondition
{
    [SerializeField] private Collider2D enemyBodyCollider;
    [SerializeField, Min(0f)] private float maxRearDistance = 4f;
    [SerializeField, Min(0f)] private float maxVerticalDifference = 1.5f;

    private EnemyUnit owner;

    public override BeliefType SupportedType => BeliefType.RearPosition;

    public float MaxRearDistance => maxRearDistance;
    public float MaxVerticalDifference => maxVerticalDifference;

    protected override void Awake()
    {
        base.Awake();
        owner = GetComponent<EnemyUnit>();
        if (enemyBodyCollider == null)
            enemyBodyCollider = GetComponent<Collider2D>();
    }

    private void Update()
    {
        if (owner == null || owner.IsDead || Controller == null || Controller.Type != SupportedType ||
            Controller.IsVulnerable || Controller.Profile == null)
        {
            return;
        }

        Collider2D playerBodyCollider = owner.TargetBodyCollider;
        if (enemyBodyCollider == null || playerBodyCollider == null)
            return;

        if (!IsWithinRearRange(
                enemyBodyCollider.bounds,
                playerBodyCollider.bounds,
                owner.FacingDirection,
                maxRearDistance,
                maxVerticalDifference))
        {
            return;
        }

        ReduceBelief(Controller.Profile.ReductionPerSecond * Time.deltaTime);
    }

    public static bool IsWithinRearRange(
        Bounds enemyBounds,
        Bounds playerBounds,
        float facingDirection,
        float rearDistance,
        float verticalDifference)
    {
        if (Mathf.Approximately(enemyBounds.center.x, playerBounds.center.x) ||
            Mathf.Abs(enemyBounds.center.y - playerBounds.center.y) > verticalDifference)
        {
            return false;
        }

        bool playerOnLeft = playerBounds.center.x < enemyBounds.center.x;
        bool playerIsBehind = facingDirection >= 0f ? playerOnLeft : !playerOnLeft;
        if (!playerIsBehind)
            return false;

        float horizontalGap = facingDirection >= 0f
            ? Mathf.Max(0f, enemyBounds.min.x - playerBounds.max.x)
            : Mathf.Max(0f, playerBounds.min.x - enemyBounds.max.x);

        return horizontalGap <= rearDistance;
    }
}
