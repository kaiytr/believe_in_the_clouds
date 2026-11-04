using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class StoneProjectile : MonoBehaviour
{
    [Header("Collision")]
    [SerializeField] private LayerMask obstacleLayer;

    private Rigidbody2D body;
    private Vector2 spawnPosition;
    private Vector2 direction;
    private float damage;
    private float maxDistance;
    private ThrowStoneAttack sourceAttack;
    private bool initialized;
    private bool resultResolved;
    private int sourceAttackId;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
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
            if (!resultResolved && sourceAttack != null && sourceAttack.Owner != null &&
                sourceAttack.Owner.HasCombatTarget && sourceAttack.IsResolvingAttack(sourceAttackId))
                Resolve(AttackOutcome.Blocked);

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
