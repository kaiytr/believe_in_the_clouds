using System.Collections;
using UnityEngine;

public class DogChargeAttack : EnemyAttack
{
    [Header("Charge")]
    [SerializeField, Min(0f)] private float chargeTriggerRange = 4f;
    [SerializeField, Min(0f)] private float verticalAttackTolerance = 1.5f;
    [SerializeField, Min(0.01f)] private float chargeDistance = 5f;
    [SerializeField, Min(0.01f)] private float chargeSpeedRatio = 1.4f;

    [Header("Danger Indicator")]
    [SerializeField] private DangerIndicator dangerIndicatorPrefab;
    [SerializeField, Min(0.01f)] private float indicatorHeight = 1f;

    [Header("Afterimage")]
    [SerializeField] private CombatAfterimage afterimagePrefab;

    [Header("Obstacle")]
    [SerializeField] private LayerMask obstacleLayer;

    private Rigidbody2D body;
    private SpriteRenderer bodyVisual;
    private CombatAfterimageEmitter afterimages;
    private static readonly WaitForFixedUpdate PhysicsTick = new WaitForFixedUpdate();
    private float lockedDirectionX;
    private Vector2 lockedChargeOrigin;
    private float lockedChargeDistance;
    private bool lockedChargeBlockedByObstacle;
    private Vector2 chargeStartPosition;
    private bool isCharging;
    private bool hasResolvedPlayerContact;
    private bool playerContactAppliedHarm;
    private bool blockedByObstacle;
    private bool chargeCompleted;

    public float ChargeTriggerRange => chargeTriggerRange;
    public bool IsCharging => isCharging;
    public override bool IsConfigured => base.IsConfigured && dangerIndicatorPrefab != null &&
                                         afterimagePrefab != null && chargeDistance > 0f && indicatorHeight > 0f && chargeSpeedRatio > 0f;

    protected override void Awake()
    {
        base.Awake();
        body = GetComponent<Rigidbody2D>();
        bodyVisual = GetComponentInChildren<SpriteRenderer>();
        afterimages = GetComponent<CombatAfterimageEmitter>();
        if (afterimages == null) afterimages = gameObject.AddComponent<CombatAfterimageEmitter>();
    }

    protected override bool CanAttack(Transform target)
    {
        if (target == null)
            return false;

        float horizontalOffset = target.position.x - transform.position.x;
        float direction = Mathf.Abs(horizontalOffset) < 0.01f
            ? (transform.localScale.x >= 0f ? 1f : -1f)
            : Mathf.Sign(horizontalOffset);
        return CanAttackFromDirection(target, direction);
    }

    protected override bool IsWindupTargetValid(Transform target)
    {
        // The charge commits to its original direction once the warning starts.
        // Only a newly blocked path cancels it; moving out of range is an evade.
        return target != null && HasClearChargePath(target, LockedFacingDirection);
    }

    private bool CanAttackFromDirection(Transform target, float direction)
    {
        if (target == null)
            return false;

        float horizontalOffset = target.position.x - transform.position.x;
        float horizontalDistance = Mathf.Abs(horizontalOffset);
        float verticalDistance = Mathf.Abs(target.position.y - transform.position.y);
        if (horizontalDistance > chargeTriggerRange || verticalDistance > verticalAttackTolerance ||
            horizontalOffset * direction < -0.01f)
            return false;

        Collider2D targetCollider = PlayerBodyCollider;
        float clearDistance = GetClearChargeDistance(direction, out _);
        return targetCollider != null &&
               IsBodyInChargeCorridor(targetCollider, ChargeOrigin(direction), direction, clearDistance) &&
               HasClearChargePath(target, direction);
    }

    // The corridor starts at the dog's leading edge so the warning never reaches back over or behind its body.
    private Vector2 ChargeOrigin(float direction)
    {
        Collider2D dogCollider = owner != null ? owner.BodyCollider : null;
        if (dogCollider == null)
            return transform.position;

        Bounds bounds = dogCollider.bounds;
        return new Vector2(direction > 0f ? bounds.max.x : bounds.min.x, transform.position.y);
    }

    private float GetClearChargeDistance(float direction, out bool blocked)
    {
        blocked = false;
        Collider2D dogCollider = owner != null ? owner.BodyCollider : null;
        if (dogCollider == null || obstacleLayer.value == 0)
            return 0f;

        Bounds bounds = dogCollider.bounds;
        Vector2 center = bounds.center;
        Vector2 size = bounds.size;
        float clearDistance = chargeDistance;
        var hits = CombatPhysics.Cast(center, size, Vector2.right * direction, chargeDistance,
            obstacleLayer.value, circle: false);
        for (int i = 0; i < hits.Count; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.isTrigger ||
                hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
                continue;

            // The supporting floor is beneath the dog, not a wall in its charge path.
            if (hitCollider.bounds.max.y <= center.y - size.y * 0.5f + 0.04f ||
                Vector2.Dot(hits[i].normal, Vector2.right * direction) >= -0.05f)
                continue;

            if (hits[i].distance < clearDistance)
            {
                clearDistance = Mathf.Max(0f, hits[i].distance - 0.01f);
                blocked = true;
            }
        }

        return clearDistance;
    }

    private bool HasClearChargePath(Transform target, float direction)
    {
        Collider2D dogCollider = owner != null ? owner.BodyCollider : null;
        Collider2D targetCollider = PlayerBodyCollider;
        if (dogCollider == null || targetCollider == null || obstacleLayer.value == 0)
            return false;

        Bounds dogBounds = dogCollider.bounds;
        Bounds targetBounds = targetCollider.bounds;
        float startX = direction > 0f ? dogBounds.max.x : dogBounds.min.x;
        float endX = direction > 0f ? targetBounds.min.x : targetBounds.max.x;
        float distance = (endX - startX) * direction;
        if (distance <= 0.02f)
            return true;

        Vector2 origin = new Vector2(startX, dogBounds.center.y);
        var hits = CombatPhysics.Raycast(origin, Vector2.right * direction, distance, obstacleLayer);
        for (int i = 0; i < hits.Count; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider == dogCollider || hitCollider == targetCollider ||
                hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform) ||
                hitCollider.transform == target || hitCollider.transform.IsChildOf(target))
                continue;

            if (hits[i].distance < distance - 0.01f)
                return false;
        }

        return true;
    }

    protected override void PrepareAttack(Transform target)
    {
        lockedDirectionX = LockedFacingDirection;
        lockedChargeOrigin = ChargeOrigin(lockedDirectionX);
        lockedChargeDistance = GetClearChargeDistance(lockedDirectionX, out lockedChargeBlockedByObstacle);
        hasResolvedPlayerContact = false;
        playerContactAppliedHarm = false;
        blockedByObstacle = false;
        chargeCompleted = false;
    }

    protected override DangerIndicator ShowDangerIndicator()
    {
        if (dangerIndicatorPrefab == null)
            return null;

        DangerIndicator indicator = Instantiate(dangerIndicatorPrefab);
        Vector2 center = lockedChargeOrigin + Vector2.right * (lockedDirectionX * lockedChargeDistance * 0.5f);
        indicator.Setup(center, 0f, new Vector2(lockedChargeDistance, indicatorHeight));
        return indicator;
    }

    protected override bool IsPlayerInThreatRange(Collider2D bodyCollider)
    {
        return IsBodyInChargeCorridor(bodyCollider, lockedChargeOrigin, lockedDirectionX, lockedChargeDistance);
    }

    private bool IsBodyInChargeCorridor(Collider2D bodyCollider, Vector2 origin, float direction, float distance)
    {
        if (bodyCollider == null || distance <= 0f)
            return false;

        Vector2 center = origin + Vector2.right * (direction * distance * 0.5f);
        return ColliderOverlapsBox(bodyCollider, center, new Vector2(distance, indicatorHeight));
    }

    protected override IEnumerator ExecuteAttackRoutine()
    {
        if (body == null)
        {
            ReportOutcome(AttackOutcome.Missed);
            yield break;
        }

        MarkAttackAsLaunched();
        isCharging = true;
        chargeStartPosition = body.position;
        owner.SetPhysicalCollisionWithPlayerEnabled(true);

        float chargeSpeed = owner.ReferencePlayerMoveSpeed * chargeSpeedRatio;
        if (chargeSpeed <= 0f)
        {
            owner.SetPhysicalCollisionWithPlayerEnabled(false);
            isCharging = false;
            ReportOutcome(AttackOutcome.Missed);
            yield break;
        }

        float maximumChargeTime = lockedChargeDistance / chargeSpeed + 0.25f;
        float elapsed = 0f;
        while (!blockedByObstacle && !hasResolvedPlayerContact &&
               Mathf.Abs(body.position.x - chargeStartPosition.x) < lockedChargeDistance &&
               elapsed < maximumChargeTime)
        {
            if (owner == null || owner.IsDead)
                break;

            float travelled = Mathf.Abs(body.position.x - chargeStartPosition.x);
            float stepDistance = Mathf.Min(lockedChargeDistance - travelled, chargeSpeed * Time.fixedDeltaTime);
            float stepSpeed = stepDistance / Time.fixedDeltaTime;
            body.linearVelocity = new Vector2(lockedDirectionX * stepSpeed, body.linearVelocity.y);
            SpawnAfterimage();
            elapsed += Time.fixedDeltaTime;
            yield return PhysicsTick;
        }

        if (!blockedByObstacle && lockedChargeBlockedByObstacle &&
            Mathf.Abs(body.position.x - chargeStartPosition.x) >= lockedChargeDistance - 0.02f)
            blockedByObstacle = true;

        chargeCompleted = !blockedByObstacle && !hasResolvedPlayerContact &&
                          Mathf.Abs(body.position.x - chargeStartPosition.x) >= lockedChargeDistance - 0.02f;
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        owner.SetPhysicalCollisionWithPlayerEnabled(false);
        isCharging = false;

        if (playerContactAppliedHarm)
        {
            ReportOutcome(AttackOutcome.Hit, completedNormally: true);
        }
        else if (hasResolvedPlayerContact)
        {
            ReportOutcome(AttackOutcome.Missed, completedNormally: true);
        }
        else if (blockedByObstacle)
        {
            ReportOutcome(AttackOutcome.Blocked);
        }
        else if (chargeCompleted)
        {
            Collider2D playerBody = PlayerBodyCollider;
            bool outsideOriginalCorridor = playerBody != null &&
                !IsBodyInChargeCorridor(playerBody, lockedChargeOrigin, lockedDirectionX, lockedChargeDistance);
            ReportOutcome(WasPlayerThreatenedDuringWarning && outsideOriginalCorridor
                ? AttackOutcome.EvadedByPlayer
                : AttackOutcome.Missed, completedNormally: true);
        }
        else
        {
            ReportOutcome(AttackOutcome.Missed);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleChargeCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleChargeCollision(collision);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleChargeCollider(other);
    }

    private void HandleChargeCollision(Collision2D collision)
    {
        Collider2D other = collision.collider;
        if (!isCharging || other == null)
            return;

        if (IsPlayerCollider(other))
        {
            ResolvePlayerContact();
            return;
        }

        if (IsObstacle(other.gameObject.layer) && IsBlockingSurface(collision))
            blockedByObstacle = true;
    }

    private bool IsBlockingSurface(Collision2D collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            float normalAlongCharge = collision.GetContact(i).normal.x * lockedDirectionX;
            if (normalAlongCharge < -0.5f)
                return true;
        }

        return false;
    }

    private void HandleChargeCollider(Collider2D other)
    {
        if (isCharging && IsPlayerCollider(other))
            ResolvePlayerContact();
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        return owner != null && owner.Target != null &&
               (other == owner.TargetBodyCollider ||
                other.attachedRigidbody != null && other.attachedRigidbody.transform == owner.Target ||
                other.transform.root == owner.Target);
    }

    private void ResolvePlayerContact()
    {
        if (hasResolvedPlayerContact)
            return;

        hasResolvedPlayerContact = true;
        Transform player = owner != null ? owner.Target : null;
        IDamageable damageable = player != null ? player.GetComponent<IDamageable>() : null;
        if (damageable == null && player != null)
            damageable = player.GetComponentInChildren<IDamageable>();
        float attackDamage = damage * DoctrineAttackMultiplier;
        playerContactAppliedHarm = damageable != null &&
                                   damageable.TakeDamage(new DamageInfo(attackDamage, DamageSource.Enemy, gameObject, "돌진"));
        if (playerContactAppliedHarm) NotifyPlayerHpDamageApplied(CurrentAttackId);
    }

    private bool IsObstacle(int layer)
    {
        return (obstacleLayer.value & (1 << layer)) != 0;
    }

    private void SpawnAfterimage()
    {
        if (afterimagePrefab == null || bodyVisual == null || owner == null || owner.BodyCollider == null)
            return;

        afterimages.Emit(afterimagePrefab, bodyVisual, bodyVisual.sortingOrder - 1);
    }

    protected override void OnAttackCancelled()
    {
        isCharging = false;
        afterimages?.Clear();
        owner?.SetPhysicalCollisionWithPlayerEnabled(false);
        if (body != null)
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, chargeTriggerRange);
    }
}
