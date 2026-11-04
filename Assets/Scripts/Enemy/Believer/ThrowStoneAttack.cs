using System.Collections;
using UnityEngine;

public class ThrowStoneAttack : EnemyAttack
{
    [Header("Range")]
    [SerializeField, Min(0.01f)] private float attackRange = 100f;

    [Header("Projectile")]
    [SerializeField] private StoneProjectile projectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField, Min(0.01f)] private float projectileSpeed = 8f;

    [Header("Danger Indicator")]
    [SerializeField] private DangerIndicator dangerIndicatorPrefab;
    [SerializeField, Min(0.01f)] private float indicatorWidth = 0.3f;
    [SerializeField, Min(0f)] private float verticalAttackTolerance = 1.5f;

    private Vector2 launchOrigin;
    private float lockedProjectileDistance;

    public float AttackRange => attackRange;
    public bool CanAttackFromCurrentPosition(Transform target) => CanAttack(target);
    public bool CanEngageTargetFromCurrentPosition(Transform target)
    {
        BelieverEnemy believer = owner as BelieverEnemy;
        return target != null && believer != null &&
               believer.CanEngageTargetOnCurrentPlatform(target, GetSpawnPosition());
    }

    public override bool IsConfigured => base.IsConfigured && projectilePrefab != null &&
                                         dangerIndicatorPrefab != null && attackRange > 0f &&
                                         projectileSpeed > 0f && indicatorWidth > 0f;

    protected override bool CanAttack(Transform target)
    {
        BelieverEnemy believer = owner as BelieverEnemy;
        if (target == null || believer == null)
            return false;

        float horizontalDistance = Mathf.Abs(target.position.x - transform.position.x);
        float verticalDistance = Mathf.Abs(target.position.y - transform.position.y);
        return horizontalDistance <= attackRange && verticalDistance <= verticalAttackTolerance &&
               CanEngageTargetFromCurrentPosition(target);
    }

    protected override void PrepareAttack(Transform target)
    {
        lockedProjectileDistance = attackRange;
    }

    protected override DangerIndicator ShowDangerIndicator()
    {
        if (dangerIndicatorPrefab == null)
            return null;

        Transform origin = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
        DangerIndicator indicator = Instantiate(dangerIndicatorPrefab);
        indicator.SetupFollowing(origin, LockedDirection, new Vector2(lockedProjectileDistance, indicatorWidth));
        return indicator;
    }

    protected override bool IsPlayerInThreatRange(Collider2D bodyCollider)
    {
        return IsBodyInThrowLane(bodyCollider, GetSpawnPosition(), lockedProjectileDistance);
    }

    private bool IsBodyInThrowLane(Collider2D bodyCollider, Vector2 origin, float length)
    {
        Vector2 center = origin + LockedDirection * (length * 0.5f);
        return ColliderOverlapsBox(bodyCollider, center, new Vector2(length, indicatorWidth));
    }

    public AttackOutcome GetOutcomeAtMaximumRange()
    {
        Collider2D body = PlayerBodyCollider;
        return WasPlayerThreatenedDuringWarning && body != null &&
               !IsBodyInThrowLane(body, launchOrigin, lockedProjectileDistance)
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
        projectile.Initialize(LockedDirection, projectileSpeed, damage, lockedProjectileDistance, this);

        // Keep the body committed through the throw, then allow spacing adjustments
        // while the launched projectile is still travelling.
        yield return new WaitForSeconds(.25f);
        owner.UnlockMovement();
    }

    private Vector2 GetSpawnPosition()
    {
        return projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
        float direction = transform.localScale.x < 0f ? -1f : 1f;
        Gizmos.DrawLine(origin, origin + Vector3.right * direction * attackRange);
    }
}
