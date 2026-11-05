using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ThrowStoneAttack))]
public class BelieverEnemy : EnemyUnit
{
    private const float PlatformSampleSpacing = 0.4f;
    private const float WalkableNormalMinimum = 0.65f;
    private const float PlayerGroundProbeDrop = 5f;
    private const float CurrentGroundProbeDrop = 0.7f;
    private const float PerceptionCacheDuration = 0.1f;
    private const float PerceptionCachePositionSqr = 0.04f;

    [Header("Distance Control")]
    [SerializeField] private float preferredMinDistance = 4.5f;
    [SerializeField] private float preferredMaxDistance = 5.5f;

    [Header("Platform Awareness")]
    [SerializeField] private LayerMask platformMask = 1 << 6;
    [SerializeField, Min(0.05f)] private float maximumPlatformStep = 0.45f;
    [SerializeField, Min(0.01f)] private float edgeProbeDistance = 0.18f;
    [SerializeField, Min(0.01f)] private float groundProbeLift = 0.25f;

    private ThrowStoneAttack stoneAttack;
    private bool retreatAfterAttack;
    private bool hasPerceptionCache;
    private bool cachedCanEngage;
    private float perceptionCacheExpiresAt;
    private Vector2 cachedEnemyPosition;
    private Vector2 cachedTargetPosition;
    private Vector2 cachedAttackOrigin;

    public float RetreatTriggerDistance => preferredMinDistance;
    public bool IsRetreating => CurrentState == EnemyState.Retreat;

    protected override void Awake()
    {
        base.Awake();
        stoneAttack = GetComponent<ThrowStoneAttack>();
    }

    protected override void UpdateAI()
    {
        if (stoneAttack == null)
        {
            StopMovement();
            return;
        }

        if (IsMovementLocked)
        {
            StopMovement();
            return;
        }

        if (!stoneAttack.CanPursueTargetOnCurrentPlatform(target))
        {
            retreatAfterAttack = false;
            StopMovement();
            FaceTarget();
            ChangeMovementState(EnemyState.Idle);
            if (stoneAttack.IsReady)
                stoneAttack.TryAttack(target);
            return;
        }

        float horizontalDistance = GetHorizontalDistanceToTarget();

        if (retreatAfterAttack)
        {
            if (horizontalDistance < preferredMaxDistance)
            {
                RetreatSafely();
                return;
            }

            retreatAfterAttack = false;
        }

        if (horizontalDistance < preferredMinDistance)
        {
            if (stoneAttack.IsReady)
            {
                StopMovement();
                FaceTarget();
                if (stoneAttack.TryAttack(target))
                    retreatAfterAttack = true;
                return;
            }

            RetreatSafely();
            return;
        }

        if (horizontalDistance > preferredMaxDistance)
        {
            ApproachSafely();
            return;
        }

        StopMovement();
        FaceTarget();
        ChangeMovementState(EnemyState.Idle);

        if (stoneAttack.IsReady)
            stoneAttack.TryAttack(target);
    }

    private void ApproachSafely()
    {
        float direction = Mathf.Sign(target.position.x - transform.position.x);
        if (!CanMoveSafelyOnPlatform(direction))
        {
            StopAtPlatformEdge();
            return;
        }

        ChangeMovementState(EnemyState.Chase);
        MoveTowardTarget();
    }

    private void ChangeMovementState(EnemyState state)
    {
        if (stoneAttack == null || !stoneAttack.IsAttacking)
            ChangeState(state);
    }

    public bool CanMoveSafelyToTargetOnCurrentPlatform(Transform candidate, Vector2 attackOrigin)
    {
        Collider2D enemyBody = BodyCollider;
        Collider2D playerBody = TargetBodyCollider;
        if (candidate == null || candidate != Target || !candidate.gameObject.activeInHierarchy ||
            enemyBody == null || playerBody == null || !playerBody.enabled)
            return false;

        Vector2 enemyPosition = enemyBody.bounds.center;
        Vector2 playerPosition = playerBody.bounds.center;
        if (hasPerceptionCache && Time.time < perceptionCacheExpiresAt &&
            (enemyPosition - cachedEnemyPosition).sqrMagnitude < PerceptionCachePositionSqr &&
            (playerPosition - cachedTargetPosition).sqrMagnitude < PerceptionCachePositionSqr &&
            (attackOrigin - cachedAttackOrigin).sqrMagnitude < PerceptionCachePositionSqr)
            return cachedCanEngage;

        cachedEnemyPosition = enemyPosition;
        cachedTargetPosition = playerPosition;
        cachedAttackOrigin = attackOrigin;
        perceptionCacheExpiresAt = Time.time + PerceptionCacheDuration;
        hasPerceptionCache = true;
        cachedCanEngage = EvaluateTargetOnCurrentPlatform(enemyBody, playerBody, enemyPosition,
            playerPosition);
        return cachedCanEngage;
    }

    public bool HasClearProjectilePath(Transform candidate, Vector2 attackOrigin, Vector2 targetPoint)
    {
        Collider2D playerBody = TargetBodyCollider;
        return candidate != null && candidate == Target && candidate.gameObject.activeInHierarchy &&
               playerBody != null && playerBody.enabled && !IsBlockedByPlatform(attackOrigin, targetPoint);
    }

    public float GetProjectileClearDistance(Vector2 attackOrigin, Vector2 direction, float maxDistance,
        float projectileDiameter)
    {
        if (direction.sqrMagnitude <= 0.0001f || maxDistance <= 0f)
            return 0f;

        direction.Normalize();
        float clearDistance = maxDistance;
        List<RaycastHit2D> hits = CombatPhysics.Cast(attackOrigin, Vector2.one * projectileDiameter,
            direction, maxDistance, platformMask, circle: true);
        for (int i = 0; i < hits.Count; i++)
        {
            if (hits[i].collider == null ||
                (platformMask.value & (1 << hits[i].collider.gameObject.layer)) == 0)
                continue;

            if (hits[i].distance < clearDistance)
                clearDistance = Mathf.Max(0f, hits[i].distance - 0.01f);
        }

        return clearDistance;
    }

    private bool EvaluateTargetOnCurrentPlatform(Collider2D enemyBody, Collider2D playerBody,
        Vector2 enemyPosition, Vector2 playerPosition)
    {
        if (!TryFindGroundUnder(enemyBody, CurrentGroundProbeDrop, out Vector2 enemyGround) ||
            !TryFindGroundUnder(playerBody, PlayerGroundProbeDrop, out Vector2 playerGround) ||
            Mathf.Abs(enemyGround.y - playerGround.y) > maximumPlatformStep ||
            !HasConnectedGroundPath(enemyGround, playerGround))
            return false;

        return !IsBlockedByPlatform(enemyPosition, playerPosition);
    }

    private bool HasConnectedGroundPath(Vector2 start, Vector2 end)
    {
        float horizontalDistance = Mathf.Abs(end.x - start.x);
        if (horizontalDistance <= PlatformSampleSpacing)
            return Mathf.Abs(end.y - start.y) <= maximumPlatformStep;

        int sampleCount = Mathf.CeilToInt(horizontalDistance / PlatformSampleSpacing);
        float direction = Mathf.Sign(end.x - start.x);
        float groundY = start.y;
        for (int i = 1; i <= sampleCount; i++)
        {
            float x = i == sampleCount ? end.x : start.x + direction * PlatformSampleSpacing * i;
            if (!TryFindGroundNear(x, groundY, out Vector2 nextGround))
                return false;
            groundY = nextGround.y;
        }

        return Mathf.Abs(groundY - end.y) <= maximumPlatformStep;
    }

    private bool CanMoveSafelyOnPlatform(float direction)
    {
        if (Mathf.Abs(direction) < 0.01f || BodyCollider == null ||
            !TryFindGroundUnder(BodyCollider, CurrentGroundProbeDrop, out Vector2 currentGround))
            return false;

        Bounds bounds = BodyCollider.bounds;
        float probeX = bounds.center.x + Mathf.Sign(direction) * (bounds.extents.x + edgeProbeDistance);
        return TryFindGroundNear(probeX, currentGround.y, out Vector2 groundAhead) &&
               Mathf.Abs(groundAhead.y - currentGround.y) <= maximumPlatformStep;
    }

    private bool TryFindGroundUnder(Collider2D body, float maximumDrop, out Vector2 groundPoint)
    {
        Bounds bounds = body.bounds;
        Vector2 origin = new Vector2(bounds.center.x, bounds.min.y + groundProbeLift);
        return TryFindWalkableGround(origin, groundProbeLift + maximumDrop, out groundPoint);
    }

    private bool TryFindGroundNear(float x, float expectedY, out Vector2 groundPoint)
    {
        float tolerance = maximumPlatformStep;
        Vector2 origin = new Vector2(x, expectedY + tolerance + 0.02f);
        if (!TryFindWalkableGround(origin, tolerance * 2f + 0.04f, out groundPoint) ||
            Mathf.Abs(groundPoint.y - expectedY) > tolerance)
        {
            groundPoint = default;
            return false;
        }

        return true;
    }

    private bool TryFindWalkableGround(Vector2 origin, float distance, out Vector2 groundPoint)
    {
        List<RaycastHit2D> hits = CombatPhysics.Raycast(origin, Vector2.down, distance, platformMask);
        float closestDistance = float.MaxValue;
        groundPoint = default;
        bool found = false;
        for (int i = 0; i < hits.Count; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider == null || hit.normal.y < WalkableNormalMinimum || hit.distance >= closestDistance)
                continue;

            closestDistance = hit.distance;
            groundPoint = hit.point;
            found = true;
        }

        return found;
    }

    private bool IsBlockedByPlatform(Vector2 start, Vector2 end)
    {
        if ((end - start).sqrMagnitude < 0.0001f)
            return false;

        Vector2 direction = (end - start).normalized;
        List<RaycastHit2D> hits = CombatPhysics.Raycast(start, direction, Vector2.Distance(start, end), platformMask);
        return hits.Count > 0;
    }

    private void RetreatSafely()
    {
        float direction = target.position.x < transform.position.x ? 1f : -1f;
        if (!CanMoveSafelyOnPlatform(direction))
        {
            StopAtPlatformEdge();
            return;
        }

        ChangeMovementState(EnemyState.Retreat);
        MoveAwayFromTarget(true);
    }

    private void StopAtPlatformEdge()
    {
        StopMovement();
        FaceTarget();
        ChangeMovementState(EnemyState.Idle);
    }
}
