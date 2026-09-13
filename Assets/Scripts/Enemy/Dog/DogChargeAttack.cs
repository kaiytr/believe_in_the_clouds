using System.Collections;
using UnityEngine;

public class DogChargeAttack : EnemyAttack
{
    [Header("Charge")]
    [SerializeField]
    private float chargeTriggerRange = 4f;

    [SerializeField]
    private float verticalAttackTolerance = 1.5f;

    [SerializeField]
    private float chargeDistance = 5f;

    [SerializeField]
    private float chargeSpeed = 12f;

    [Header("Danger Indicator")]
    [SerializeField]
    private DangerIndicator dangerIndicatorPrefab;

    [SerializeField]
    private float indicatorHeight = 1f;

    [Header("Obstacle")]
    [SerializeField]
    private LayerMask obstacleLayer;

    private Rigidbody2D rb;

    private float lockedDirectionX;

    private bool isCharging;
    private bool hasDamagedPlayer;
    private bool chargeInterrupted;

    public float ChargeTriggerRange =>
        chargeTriggerRange;

    public bool IsCharging =>
        isCharging;

    protected override void Awake()
    {
        base.Awake();

        rb = GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            Debug.LogError(
                $"{name}: DogChargeAttack에는 Rigidbody2D가 필요합니다."
            );
        }
    }

    protected override bool CanAttack(
        Transform target
    )
    {
        if (target == null)
            return false;

        float horizontalDistance =
            Mathf.Abs(
                target.position.x -
                transform.position.x
            );

        float verticalDistance =
            Mathf.Abs(
                target.position.y -
                transform.position.y
            );

        return
            horizontalDistance <=
                chargeTriggerRange &&
            verticalDistance <=
                verticalAttackTolerance;
    }

    protected override void PrepareAttack(
        Transform target
    )
    {
        float direction =
            Mathf.Sign(
                target.position.x -
                transform.position.x
            );

        if (Mathf.Abs(direction) <
            0.01f)
        {
            direction =
                transform.localScale.x >= 0f
                    ? 1f
                    : -1f;
        }

        lockedDirectionX = direction;

        hasDamagedPlayer = false;
        chargeInterrupted = false;
    }

    protected override void ShowDangerIndicator()
    {
        if (dangerIndicatorPrefab == null)
            return;

        Vector2 direction =
            new Vector2(
                lockedDirectionX,
                0f
            );

        DangerIndicator indicator =
            Instantiate(
                dangerIndicatorPrefab
            );

        indicator.SetupFollowing(
            transform,
            direction,
            new Vector2(
                chargeDistance,
                indicatorHeight
            ),
            windupTime
        );
    }

    protected override IEnumerator ExecuteAttackRoutine()
    {
        if (rb == null)
            yield break;

        isCharging = true;
        chargeInterrupted = false;

        float startX =
            rb.position.x;

        /*
         * 벽 등에 막혀 이동거리를 채우지 못하는 경우
         * 무한 돌진하지 않도록 안전 제한시간 설정.
         */
        float maximumChargeTime =
            chargeDistance /
            Mathf.Max(
                chargeSpeed,
                0.01f
            )
            + 0.25f;

        float elapsed = 0f;

        while (!chargeInterrupted &&
               Mathf.Abs(
                   rb.position.x - startX
               ) < chargeDistance &&
               elapsed < maximumChargeTime)
        {
            if (owner == null ||
                owner.IsDead)
            {
                break;
            }

            rb.linearVelocity =
                new Vector2(
                    lockedDirectionX *
                    chargeSpeed,
                    rb.linearVelocity.y
                );

            elapsed +=
                Time.fixedDeltaTime;

            yield return
                new WaitForFixedUpdate();
        }

        rb.linearVelocity =
            new Vector2(
                0f,
                rb.linearVelocity.y
            );

        isCharging = false;
    }

    private void OnCollisionEnter2D(
        Collision2D collision
    )
    {
        if (!isCharging)
            return;

        Collider2D other =
            collision.collider;

        if (TryDamagePlayer(other))
        {
            /*
             * 플레이어를 물었으면
             * 그 자리에서 돌진 종료.
             */
            chargeInterrupted = true;

            return;
        }

        /*
         * 벽 등에 충돌하면 돌진 종료.
         *
         * 평소 바닥 접촉은 이미 시작 전에
         * 발생한 충돌이므로 문제가 되지 않는다.
         */
        if (IsObstacle(
            other.gameObject.layer
        ))
        {
            chargeInterrupted = true;
        }
    }

    private void OnCollisionStay2D(
        Collision2D collision
    )
    {
        if (!isCharging)
            return;

        /*
         * 돌진 시작 시점에 이미 플레이어와
         * 접촉 중인 경우도 대응.
         */
        if (TryDamagePlayer(
            collision.collider
        ))
        {
            chargeInterrupted = true;
        }
    }

    private void OnTriggerEnter2D(
        Collider2D other
    )
    {
        if (!isCharging)
            return;

        if (TryDamagePlayer(other))
        {
            chargeInterrupted = true;
        }
    }

    private bool TryDamagePlayer(
        Collider2D other
    )
    {
        if (hasDamagedPlayer)
            return false;

        Transform playerRoot =
            GetPlayerRoot(other);

        if (playerRoot == null)
            return false;

        IDamageable damageable =
            playerRoot
                .GetComponent<IDamageable>();

        if (damageable == null)
        {
            damageable =
                playerRoot
                    .GetComponentInChildren<IDamageable>();
        }

        if (damageable == null)
            return false;

        damageable.TakeDamage(
            damage
        );

        hasDamagedPlayer = true;

        return true;
    }

    private Transform GetPlayerRoot(
        Collider2D other
    )
    {
        if (other.CompareTag("Player"))
        {
            return other.transform;
        }

        if (other.attachedRigidbody != null &&
            other.attachedRigidbody.CompareTag(
                "Player"
            ))
        {
            return
                other.attachedRigidbody.transform;
        }

        Transform root =
            other.transform.root;

        if (root != null &&
            root.CompareTag("Player"))
        {
            return root;
        }

        return null;
    }

    private bool IsObstacle(int layer)
    {
        return
            (obstacleLayer.value &
             (1 << layer))
            != 0;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            chargeTriggerRange
        );
    }
}