using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class StoneProjectile : MonoBehaviour
{
    [Header("Collision")]
    [SerializeField] private LayerMask obstacleLayer;

    private Rigidbody2D body;
    private Collider2D projectileCollider;
    private CircleCollider2D circleCollider;
    private Vector2 spawnPosition;
    private Vector2 direction;
    private float damage;
    private float maxDistance;
    private ThrowStoneAttack sourceAttack;
    private bool initialized;
    private bool resultResolved;
    private int sourceAttackId;

    public float CollisionDiameter
    {
        get
        {
            CircleCollider2D circle = circleCollider != null ? circleCollider : GetComponent<CircleCollider2D>();
            if (circle != null)
            {
                Vector3 scale = transform.lossyScale;
                return circle.radius * 2f * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            }

            Collider2D collider = projectileCollider != null ? projectileCollider : GetComponent<Collider2D>();
            return collider != null ? Mathf.Max(collider.bounds.size.x, collider.bounds.size.y) : 0f;
        }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        projectileCollider = GetComponent<Collider2D>();
        circleCollider = GetComponent<CircleCollider2D>();
    }

    public void Initialize(Vector2 direction, float speed, float damage, float maxDistance, ThrowStoneAttack sourceAttack)
    {
        this.direction = direction.normalized;
        this.damage = damage;
        this.maxDistance = maxDistance;
        this.sourceAttack = sourceAttack;
        sourceAttackId = sourceAttack != null ? sourceAttack.CurrentAttackId : 0;
        spawnPosition = body.position;
        initialized = true;

        if (speed <= 0f || maxDistance <= 0f || this.direction.sqrMagnitude <= 0.0001f)
        {
            Resolve(AttackOutcome.Missed);
            Destroy(gameObject);
            return;
        }

        body.linearVelocity = this.direction * speed;
        float safetyLifetime = maxDistance / speed + 1f;
        Destroy(gameObject, safetyLifetime);
    }

    private void FixedUpdate()
    {
        if (!initialized)
            return;

        if (!resultResolved && (sourceAttack == null || sourceAttack.Owner == null || sourceAttack.Owner.IsDead ||
            !sourceAttack.Owner.HasCombatTarget || !sourceAttack.IsResolvingAttack(sourceAttackId)))
        {
            Resolve(AttackOutcome.Cancelled);
            Destroy(gameObject);
            return;
        }

        float travelled = Vector2.Dot(body.position - spawnPosition, direction);
        float remainingDistance = maxDistance - travelled;
        float stepDistance = Mathf.Min(
            Mathf.Max(0f, Vector2.Dot(body.linearVelocity, direction) * Time.fixedDeltaTime),
            remainingDistance);

        // Trigger callbacks can be skipped when a fast projectile crosses a thin wall
        // between physics steps, so sweep its collider along the upcoming movement.
        if (HasObstacleInPath(stepDistance))
        {
            ResolveBlocked();
            Destroy(gameObject);
            return;
        }

        // Dodged: the stone is already farther from the believer than the player is, and it did not hit.
        // The stone keeps flying, but the result is settled now instead of at maximum range.
        if (!resultResolved && sourceAttack != null && sourceAttack.HasStonePassedPlayer(travelled))
            Resolve(AttackOutcome.EvadedByPlayer, completedNormally: true);

        if (travelled >= maxDistance)
        {
            if (!resultResolved && sourceAttack != null)
                Resolve(sourceAttack.GetOutcomeAtMaximumRange(), completedNormally: true);
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
            return;

        if ((obstacleLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            ResolveBlocked();
            Destroy(gameObject);
            return;
        }

        if (resultResolved || sourceAttack == null || sourceAttack.Owner == null ||
            !sourceAttack.Owner.HasCombatTarget || !sourceAttack.IsResolvingAttack(sourceAttackId) ||
            other != sourceAttack.Owner.TargetBodyCollider)
            return;

        Transform player = sourceAttack.Owner.Target;
        IDamageable damageable = player != null ? player.GetComponent<IDamageable>() : null;
        if (damageable == null && player != null)
            damageable = player.GetComponentInChildren<IDamageable>();

        float attackDamage = damage;
        DoctrineStats ownerStats = sourceAttack.Owner.GetComponent<DoctrineStats>();
        if (ownerStats != null)
            attackDamage *= ownerStats.AttackMultiplier;
        bool applied = damageable != null &&
                       damageable.TakeDamage(new DamageInfo(attackDamage, DamageSource.Enemy, sourceAttack.Owner.gameObject, "돌 투척"));
        if (applied) sourceAttack.NotifyPlayerHpDamageApplied(sourceAttackId);
        Resolve(applied ? AttackOutcome.Hit : AttackOutcome.Missed, completedNormally: true);
        Destroy(gameObject);
    }

    private bool HasObstacleInPath(float distance)
    {
        if (distance <= 0f || obstacleLayer.value == 0 || projectileCollider == null)
            return false;

        float diameter = CollisionDiameter;
        if (diameter <= 0f)
            return false;

        List<RaycastHit2D> hits = CombatPhysics.Cast(
            body.position,
            Vector2.one * diameter,
            direction,
            distance,
            obstacleLayer.value,
            circle: true);

        for (int i = 0; i < hits.Count; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider != null && (obstacleLayer.value & (1 << hitCollider.gameObject.layer)) != 0)
                return true;
        }

        return false;
    }

    private void ResolveBlocked()
    {
        if (!resultResolved && sourceAttack != null && sourceAttack.Owner != null &&
            sourceAttack.Owner.HasCombatTarget && sourceAttack.IsResolvingAttack(sourceAttackId))
            Resolve(AttackOutcome.Blocked);
    }

    private void Resolve(AttackOutcome outcome, bool completedNormally = false)
    {
        if (resultResolved)
            return;

        resultResolved = true;
        if (sourceAttack != null)
            sourceAttack.ResolvePendingHitResult(sourceAttackId, outcome, completedNormally);
    }

    private void OnDestroy()
    {
        if (initialized && !resultResolved)
            Resolve(AttackOutcome.Missed);
    }
}
