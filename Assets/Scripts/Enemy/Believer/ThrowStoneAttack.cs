using System.Collections;
using UnityEngine;

public class ThrowStoneAttack : EnemyAttack
{
    [Header("Range")]
    [SerializeField, Min(0.01f)] private float attackRange = 6f;

    [Header("Projectile")]
    [SerializeField] private StoneProjectile projectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField, Min(0.01f)] private float projectileSpeed = 8f;

    [Header("Danger Indicator")]
    [SerializeField] private DangerIndicator dangerIndicatorPrefab;
    [SerializeField, Min(0.01f)] private float indicatorWidth = 0.3f;
    [SerializeField, Min(0f)] private float verticalAttackTolerance = 1.5f;

    private Vector2 launchOrigin;

    public float AttackRange => attackRange;
    public override bool IsConfigured => base.IsConfigured && projectilePrefab != null &&
                                         dangerIndicatorPrefab != null && attackRange > 0f &&
                                         projectileSpeed > 0f && indicatorWidth > 0f;

    protected override bool CanAttack(Transform target)
    {
        if (target == null)
            return false;

        float horizontalDistance = Mathf.Abs(target.position.x - transform.position.x);
        float verticalDistance = Mathf.Abs(target.position.y - transform.position.y);
        return horizontalDistance <= attackRange && verticalDistance <= verticalAttackTolerance;
    }

    protected override DangerIndicator ShowDangerIndicator()
    {
        if (dangerIndicatorPrefab == null)
            return null;

        Transform origin = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
        DangerIndicator indicator = Instantiate(dangerIndicatorPrefab);
        indicator.SetupFollowing(origin, LockedDirection, new Vector2(attackRange, indicatorWidth));
        return indicator;
    }

    protected override bool IsPlayerInThreatRange(Collider2D bodyCollider)
    {
        return IsBodyInThrowLane(bodyCollider, GetSpawnPosition());
    }

    private bool IsBodyInThrowLane(Collider2D bodyCollider, Vector2 origin)
    {
        Vector2 center = origin + LockedDirection * (attackRange * 0.5f);
        return ColliderOverlapsBox(bodyCollider, center, new Vector2(attackRange, indicatorWidth));
    }

    public AttackOutcome GetOutcomeAtMaximumRange()
    {
        Collider2D body = PlayerBodyCollider;
        return WasPlayerThreatenedDuringWarning && body != null &&
               !IsBodyInThrowLane(body, launchOrigin)
            ? AttackOutcome.EvadedByPlayer
            : AttackOutcome.Missed;
    }

    protected override IEnumerator ExecuteAttackRoutine()
    {
        if (projectilePrefab == null || projectileSpeed <= 0f || attackRange <= 0f)
        {
            Debug.LogError($"{name}: ThrowStoneAttack has an invalid projectile configuration.", this);
            yield break;
        }

        MarkAttackAsLaunched();
        Vector2 spawnPosition = GetSpawnPosition();
        launchOrigin = spawnPosition;
        float angle = Mathf.Atan2(LockedDirection.y, LockedDirection.x) * Mathf.Rad2Deg;
        StoneProjectile projectile = Instantiate(
            projectilePrefab,
            spawnPosition,
            Quaternion.Euler(0f, 0f, angle));

        TrackPendingHitResult();
        projectile.Initialize(LockedDirection, projectileSpeed, damage, attackRange, this);
    }

    private Vector2 GetSpawnPosition()
    {
        return projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
