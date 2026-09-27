using UnityEngine;

[RequireComponent(typeof(MagicCircleAttack))]
public class ExecutiveEnemy : EnemyUnit
{
    [Header("Distance Control")]
    [SerializeField]
    private float retreatStartDistance = 3f;

    [SerializeField]
    private float retreatEndDistance = 5f;

    private MagicCircleAttack magicCircleAttack;

    private bool isRetreating;

    protected override void Awake()
    {
        base.Awake();

        magicCircleAttack =
            GetComponent<MagicCircleAttack>();
    }

    protected override void UpdateAI()
    {
        if (magicCircleAttack == null)
        {
            StopMovement();
            return;
        }

        /*
         * 마법진 시전 중에는 정지.
         */
        if (magicCircleAttack.IsAttacking)
        {
            StopMovement();
            return;
        }

        float horizontalDistance =
            GetHorizontalDistanceToTarget();

        /*
         * 플레이어가 가까이 접근하면
         * 후퇴 모드 진입.
         */
        if (horizontalDistance <
            retreatStartDistance)
        {
            isRetreating = true;
        }

        /*
         * 후퇴 시작 후에는
         * retreatEndDistance까지 확실하게 이동.
         *
         * 이렇게 해야 3 근처에서
         * 앞뒤로 덜덜 떠는 현상을 방지함.
         */
        if (isRetreating)
        {
            if (horizontalDistance <
                retreatEndDistance)
            {
                ChangeState(
                    EnemyState.Retreat
                );

                MoveAwayFromTarget(
                    true
                );

                return;
            }

            isRetreating = false;

            StopMovement();
        }

        /*
         * 기본적으로는 이동하지 않는다.
         *
         * 신자와 달리 공격 사거리 밖의
         * Player를 쫓아가지 않음.
         */
        ChangeState(
            EnemyState.Idle
        );

        StopMovement();
        FaceTarget();

        /*
         * Player가 공격 범위 내이고
         * 쿨타임이 끝났을 때만 마법진 시전.
         *
         * 실제 범위 검사는 TryAttack 내부의
         * CanAttack에서 수행.
         */
        if (magicCircleAttack.IsReady)
        {
            magicCircleAttack.TryAttack(
                target
            );
        }
    }
}