using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class StoneProjectile : MonoBehaviour
{
    [Header("Collision")]
    [SerializeField] private LayerMask obstacleLayer;

    private Rigidbody2D rb;

    private Vector2 spawnPosition;

    private float damage;
    private float maxDistance;
    private EnemyAttack sourceAttack;

    private bool initialized;
    private bool attackResultResolved;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Initialize(
        Vector2 direction,
        float speed,
        float damage,
        float maxDistance,
        EnemyAttack sourceAttack = null
    )
    {
        this.damage = damage;
        this.maxDistance = maxDistance;
        this.sourceAttack = sourceAttack;

        spawnPosition = rb.position;

        Vector2 normalizedDirection =
            direction.normalized;

        if (speed <= 0f || maxDistance <= 0f || normalizedDirection.sqrMagnitude <= 0.0001f)
        {
            initialized = true;
            ResolveAttackResult(false);
            Destroy(gameObject);
            return;
        }

        rb.linearVelocity =
            normalizedDirection * speed;

        initialized = true;

        float safetyLifetime =
            maxDistance /
            Mathf.Max(speed, 0.01f)
            + 1f;

        Destroy(
            gameObject,
            safetyLifetime
        );
    }

    private void FixedUpdate()
    {
        if (!initialized)
            return;

        float traveledDistance =
            Vector2.Distance(
                spawnPosition,
                rb.position
            );

        if (traveledDistance >= maxDistance)
        {
            ResolveAttackResult(false);
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(
        Collider2D other
    )
    {
        if (TryGetPlayer(
            other,
            out Transform playerRoot))
        {
            IDamageable damageable =
                playerRoot.GetComponent<IDamageable>();

            if (damageable == null)
            {
                damageable =
                    playerRoot
                        .GetComponentInChildren<IDamageable>();
            }

            if (damageable != null)
            {
                damageable.TakeDamage(
                    damage
                );

                ResolveAttackResult(true);
            }

            Destroy(gameObject);

            return;
        }

        if (IsObstacle(
            other.gameObject.layer))
        {
            ResolveAttackResult(false);
            Destroy(gameObject);
        }
    }

    private void ResolveAttackResult(bool playerHit)
    {
        if (attackResultResolved)
            return;
        attackResultResolved = true;
        if (sourceAttack != null)
            sourceAttack.ResolvePendingHitResult(playerHit);
    }

    private void OnDestroy()
    {
        if (initialized && !attackResultResolved)
            ResolveAttackResult(false);
    }

    private bool TryGetPlayer(
        Collider2D other,
        out Transform playerRoot
    )
    {
        playerRoot = null;

        // Collider 자체가 Player인 경우
        if (other.CompareTag("Player"))
        {
            playerRoot = other.transform;

            return true;
        }

        // Rigidbody 루트가 Player인 경우
        if (other.attachedRigidbody != null &&
            other.attachedRigidbody.CompareTag(
                "Player"
            ))
        {
            playerRoot =
                other.attachedRigidbody.transform;

            return true;
        }

        // 계층 최상단이 Player인 경우
        Transform root =
            other.transform.root;

        if (root != null &&
            root.CompareTag("Player"))
        {
            playerRoot = root;

            return true;
        }

        return false;
    }

    private bool IsObstacle(int layer)
    {
        return
            (obstacleLayer.value &
             (1 << layer))
            != 0;
    }
}