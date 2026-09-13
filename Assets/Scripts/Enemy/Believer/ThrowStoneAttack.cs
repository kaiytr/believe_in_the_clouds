using UnityEngine;

public class ThrowStoneAttack : EnemyAttack
{
    [Header("Range")]
    [SerializeField]
    private float attackRange = 6f;

    [Header("Projectile")]
    [SerializeField]
    private StoneProjectile projectilePrefab;

    [SerializeField]
    private Transform projectileSpawnPoint;

    [SerializeField]
    private float projectileSpeed = 8f;

    [Header("Danger Indicator")]
    [SerializeField]
    private DangerIndicator dangerIndicatorPrefab;

    [SerializeField]
    private float indicatorWidth = 0.3f;

    [SerializeField]
    private float verticalAttackTolerance = 1.5f;

    private Vector2 lockedDirection;

    public float AttackRange => attackRange;

    protected override bool CanAttack(Transform target)
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
            horizontalDistance <= attackRange &&
            verticalDistance <= verticalAttackTolerance;
    }

    protected override void PrepareAttack(Transform target)
    {
        if (target == null)
            return;

        float directionX =
            Mathf.Sign(
                target.position.x -
                transform.position.x
            );

        if (Mathf.Abs(directionX) < 0.01f)
        {
            directionX =
                transform.localScale.x >= 0f
                    ? 1f
                    : -1f;
        }

        lockedDirection =
            directionX > 0f
                ? Vector2.right
                : Vector2.left;
    }

    protected override void ShowDangerIndicator()
    {
        if (dangerIndicatorPrefab == null)
            return;

        Vector2 origin =
            GetSpawnPosition();

        Vector2 center =
            origin +
            lockedDirection *
            (attackRange * 0.5f);

        float angle =
            Mathf.Atan2(
                lockedDirection.y,
                lockedDirection.x
            ) *
            Mathf.Rad2Deg;

        DangerIndicator indicator =
            Instantiate(
                dangerIndicatorPrefab
            );

        indicator.Setup(
            center,
            angle,
            new Vector2(
                attackRange,
                indicatorWidth
            ),
            windupTime
        );
    }

    protected override void ExecuteAttack()
    {
        if (projectilePrefab == null)
        {
            Debug.LogError(
                $"{name}: StoneProjectile Prefab이 지정되지 않았습니다."
            );

            return;
        }

        Vector2 spawnPosition =
            GetSpawnPosition();

        float angle =
            Mathf.Atan2(
                lockedDirection.y,
                lockedDirection.x
            ) *
            Mathf.Rad2Deg;

        StoneProjectile projectile =
            Instantiate(
                projectilePrefab,
                spawnPosition,
                Quaternion.Euler(
                    0f,
                    0f,
                    angle
                )
            );

        projectile.Initialize(
            lockedDirection,
            projectileSpeed,
            damage,
            attackRange
        );
    }

    private Vector2 GetSpawnPosition()
    {
        if (projectileSpawnPoint != null)
        {
            return projectileSpawnPoint.position;
        }

        return transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );
    }
}