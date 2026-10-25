using System.Collections;
using UnityEngine;

public class DogChargeAttack : EnemyAttack
{
    [Header("Charge")]
    [SerializeField, Min(0f)] private float chargeTriggerRange = 4f;
    [SerializeField, Min(0f)] private float verticalAttackTolerance = 1.5f;
    [SerializeField, Min(0.01f)] private float chargeDistance = 5f;
    [SerializeField, Min(0.01f)] private float chargeSpeed = 12f;

    [Header("Danger Indicator")]
    [SerializeField] private DangerIndicator dangerIndicatorPrefab;
    [SerializeField, Min(0.01f)] private float indicatorHeight = 1f;

    [Header("Obstacle")]
    [SerializeField] private LayerMask obstacleLayer;

    private Rigidbody2D body;
    private float lockedDirectionX;
    private Vector2 chargeStartPosition;
    private bool isCharging;
    private bool hasResolvedPlayerContact;
    private bool playerContactAppliedHarm;
    private bool blockedByObstacle;
    private bool chargeCompleted;

    public float ChargeTriggerRange => chargeTriggerRange;
    public bool IsCharging => isCharging;
    public override bool IsConfigured => base.IsConfigured && dangerIndicatorPrefab != null &&
                                         chargeDistance > 0f && indicatorHeight > 0f && chargeSpeed > 0f;

    protected override void Awake()
    {
        base.Awake();
        body = GetComponent<Rigidbody2D>();
    }

    protected override bool CanAttack(Transform target)
    {
        if (target == null)
            return false;

        float horizontalDistance = Mathf.Abs(target.position.x - transform.position.x);
        float verticalDistance = Mathf.Abs(target.position.y - transform.position.y);
        return horizontalDistance <= chargeTriggerRange && verticalDistance <= verticalAttackTolerance;
    }

    protected override void PrepareAttack(Transform target)
    {
        lockedDirectionX = LockedFacingDirection;
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
        indicator.SetupFollowing(
            transform,
            new Vector2(lockedDirectionX, 0f),
            new Vector2(chargeDistance, indicatorHeight),
            windupTime);
        return indicator;
    }

    protected override bool IsPlayerInThreatRange(Collider2D bodyCollider)
    {
        Vector2 origin = transform.position;
        return IsBodyInChargeCorridor(bodyCollider, origin);
    }

    private bool IsBodyInChargeCorridor(Collider2D bodyCollider, Vector2 origin)
    {
        Vector2 center = origin + new Vector2(lockedDirectionX * chargeDistance * 0.5f, 0f);
        return ColliderOverlapsBox(bodyCollider, center, new Vector2(chargeDistance, indicatorHeight));
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

        float maximumChargeTime = chargeDistance / Mathf.Max(chargeSpeed, 0.01f) + 0.25f;
        float elapsed = 0f;
        while (!blockedByObstacle && !hasResolvedPlayerContact &&
               Mathf.Abs(body.position.x - chargeStartPosition.x) < chargeDistance &&
               elapsed < maximumChargeTime)
        {
            if (owner == null || owner.IsDead)
                break;

            body.linearVelocity = new Vector2(lockedDirectionX * chargeSpeed, body.linearVelocity.y);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        chargeCompleted = !blockedByObstacle && !hasResolvedPlayerContact &&
                          Mathf.Abs(body.position.x - chargeStartPosition.x) >= chargeDistance - 0.02f;
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        owner.SetPhysicalCollisionWithPlayerEnabled(false);
        isCharging = false;

        if (playerContactAppliedHarm)
        {
            ReportOutcome(AttackOutcome.Hit);
        }
        else if (hasResolvedPlayerContact)
        {
            ReportOutcome(AttackOutcome.Missed);
        }
        else if (blockedByObstacle)
        {
            ReportOutcome(AttackOutcome.Blocked);
        }
        else if (chargeCompleted)
        {
            Collider2D playerBody = PlayerBodyCollider;
            bool outsideOriginalCorridor = playerBody != null &&
                                           !IsBodyInChargeCorridor(playerBody, chargeStartPosition);
            ReportOutcome(WasPlayerThreatenedDuringWarning && outsideOriginalCorridor
                ? AttackOutcome.EvadedByPlayer
                : AttackOutcome.Missed);
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
        playerContactAppliedHarm = damageable != null &&
                                   damageable.TakeDamage(new DamageInfo(damage, DamageSource.Enemy));

        // Table rule: OnAttack triggers only when the hit lands.
        if (playerContactAppliedHarm && owner != null && DoctrineEventBus.HasListeners)
        {
            DoctrineEventBus.Raise(
                ConditionType.OnAttack,
                owner.gameObject,
                player != null ? player.gameObject : null);
        }
    }

    private bool IsObstacle(int layer)
    {
        return (obstacleLayer.value & (1 << layer)) != 0;
    }

    protected override void OnOwnerDeath()
    {
        isCharging = false;
        owner?.SetPhysicalCollisionWithPlayerEnabled(false);
        if (body != null)
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, chargeTriggerRange);
    }
}
