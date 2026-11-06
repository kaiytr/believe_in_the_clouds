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

    private Vector2 lockedOrigin;
    private Vector2 launchOrigin;
    private float lockedProjectileDistance;

    public float AttackRange => attackRange;
    public bool CanAttackFromCurrentPosition(Transform target) => CanAttack(target);
    public bool CanPursueTargetOnCurrentPlatform(Transform target)
    {
        BelieverEnemy believer = owner as BelieverEnemy;
        return target != null && believer != null &&
               believer.CanMoveSafelyToTargetOnCurrentPlatform(target, GetSpawnPosition());
    }

    public override bool IsConfigured => base.IsConfigured && projectilePrefab != null &&
                                         dangerIndicatorPrefab != null && attackRange > 0f &&
                                         projectileSpeed > 0f && indicatorWidth > 0f;

    protected override bool CanAttack(Transform target)
    {
        BelieverEnemy believer = owner as BelieverEnemy;
        if (target == null || believer == null)
            return false;

        Collider2D body = PlayerBodyCollider;
        if (body == null || !body.enabled)
            return false;

        Vector2 origin = GetSpawnPosition();
        Vector2 targetPoint = body.ClosestPoint(origin);
        Vector2 toTarget = targetPoint - origin;
        float targetDistance = toTarget.magnitude;
        Vector2 direction = targetDistance > 0.0001f
            ? toTarget / targetDistance
            : new Vector2(transform.localScale.x >= 0f ? 1f : -1f, 0f);
        float clearDistance = believer.GetProjectileClearDistance(origin, direction, attackRange,
            projectilePrefab != null ? projectilePrefab.CollisionDiameter : 0f);

        return targetDistance <= attackRange && targetDistance <= clearDistance + 0.01f &&
               believer.HasClearProjectilePath(target, origin, targetPoint);
    }

    protected override Vector2 GetLockedAttackDirection(Transform target)
    {
        Vector2 origin = GetSpawnPosition();
        Collider2D body = PlayerBodyCollider;
        Vector2 targetPoint = body != null ? body.ClosestPoint(origin) : (Vector2)target.position;
        Vector2 direction = targetPoint - origin;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = (Vector2)target.position - origin;
        return direction;
    }

    protected override void PrepareAttack(Transform target)
    {
        lockedOrigin = GetSpawnPosition();
        BelieverEnemy believer = owner as BelieverEnemy;
        lockedProjectileDistance = believer != null
            ? believer.GetProjectileClearDistance(lockedOrigin, LockedDirection, attackRange,
                projectilePrefab != null ? projectilePrefab.CollisionDiameter : 0f)
            : attackRange;
    }

    protected override DangerIndicator ShowDangerIndicator()
    {
        if (dangerIndicatorPrefab == null)
            return null;

        DangerIndicator indicator = Instantiate(dangerIndicatorPrefab);
        indicator.UseThinningLine();
        float angle = Mathf.Atan2(LockedDirection.y, LockedDirection.x) * Mathf.Rad2Deg;
        Vector2 center = lockedOrigin + LockedDirection * (lockedProjectileDistance * 0.5f);
        indicator.Setup(center, angle, new Vector2(lockedProjectileDistance, indicatorWidth));
        return indicator;
    }

    protected override bool IsPlayerInThreatRange(Collider2D bodyCollider)
    {
        return IsBodyInThrowLane(bodyCollider, lockedOrigin, lockedProjectileDistance);
    }

    private bool IsBodyInThrowLane(Collider2D bodyCollider, Vector2 origin, float length)
    {
        Vector2 center = origin + LockedDirection * (length * 0.5f);
        float angle = Mathf.Atan2(LockedDirection.y, LockedDirection.x) * Mathf.Rad2Deg;
        return ColliderOverlapsBox(bodyCollider, center, new Vector2(length, indicatorWidth), angle);
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
        Vector2 spawnPosition = lockedOrigin;
        launchOrigin = lockedOrigin;
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

    /// <summary>Where a real throw leaves the believer. Cutscene throws start from the same point.</summary>
    public Vector2 ProjectileSpawnWorldPosition => GetSpawnPosition();

    private Vector2 GetSpawnPosition()
    {
        return projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
        Gizmos.DrawWireSphere(origin, attackRange);
    }
}
