using System.Collections;
using UnityEngine;

public class MagicCircleAttack : EnemyAttack
{
    [Header("Range")]
    [SerializeField]
    private float attackRange = 7f;

    [Header("Magic Circle")]
    [SerializeField]
    private MagicCircle magicCirclePrefab;

    [SerializeField]
    private float circleRadius = 1.5f;

    [SerializeField]
    private float circleVisualDuration = 0.8f;

    [Header("Slow")]
    [Range(0f, 1f)]
    [SerializeField]
    private float slowMultiplier = 0.5f;

    [SerializeField]
    private float slowDuration = 2f;

    [Header("Danger Indicator")]
    [SerializeField]
    private DangerIndicator dangerIndicatorPrefab;

    private Vector2 lockedTargetPosition;

    public float AttackRange =>
        attackRange;

    protected override bool CanAttack(
        Transform target
    )
    {
        if (target == null)
            return false;

        float distance =
            Vector2.Distance(
                transform.position,
                target.position
            );

        return distance <= attackRange;
    }

    protected override void PrepareAttack(
        Transform target
    )
    {
        /*
         * 선딜 시작 순간의 Player 위치 고정.
         *
         * 이후 Player가 움직이거나
         * 간부가 넉백되어도 변경되지 않는다.
         */
        lockedTargetPosition =
            target.position;
    }

    protected override void ShowDangerIndicator()
    {
        if (dangerIndicatorPrefab == null)
            return;

        DangerIndicator indicator =
            Instantiate(
                dangerIndicatorPrefab
            );

        float diameter =
            circleRadius * 2f;

        /*
         * 마법진은 공격자를 따라가는 공격이 아니라
         * 월드 위치에 설치되는 공격.
         *
         * 따라서 SetupFollowing이 아니라 Setup 사용.
         */
        indicator.Setup(
            lockedTargetPosition,
            0f,
            new Vector2(
                diameter,
                diameter
            ),
            windupTime
        );
    }

    protected override IEnumerator ExecuteAttackRoutine()
    {
        if (magicCirclePrefab == null)
        {
            Debug.LogError(
                $"{name}: MagicCircle Prefab이 지정되지 않았습니다."
            );

            yield break;
        }

        MarkAttackAsLaunched();
        MagicCircle circle =
            Instantiate(
                magicCirclePrefab,
                lockedTargetPosition,
                Quaternion.identity
            );

        circle.Activate(
            circleRadius,
            damage,
            slowMultiplier,
            slowDuration,
            circleVisualDuration,
            this
        );

        yield break;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );
    }
}