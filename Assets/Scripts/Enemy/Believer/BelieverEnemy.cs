using UnityEngine;

[RequireComponent(typeof(ThrowStoneAttack))]
public class BelieverEnemy : EnemyUnit
{
    [Header("Distance Control")]
    [SerializeField]
    private float preferredMinDistance = 4.5f;

    [SerializeField]
    private float preferredMaxDistance = 5.5f;

    private ThrowStoneAttack stoneAttack;

    private bool retreatAfterAttack;

    protected override void Awake()
    {
        base.Awake();

        stoneAttack =
            GetComponent<ThrowStoneAttack>();
    }

    protected override void UpdateAI()
    {
        if (stoneAttack == null)
        {
            StopMovement();
            return;
        }

        /*
         * 공격 중에는 이동하지 않는다.
         */
        if (stoneAttack.IsAttacking)
        {
            StopMovement();
            return;
        }

        float horizontalDistance =
            GetHorizontalDistanceToTarget();

        /*
         * 가까이 붙은 상태에서 공격했다면
         * 공격 종료 후 선호 거리까지 후퇴한다.
         */
        if (retreatAfterAttack)
        {
            if (horizontalDistance <
                preferredMaxDistance)
            {
                ChangeState(
                    EnemyState.Retreat
                );

                MoveAwayFromTarget(true);

                return;
            }

            retreatAfterAttack = false;
        }

        /*
         * 너무 멀다.
         *
         * 공격 사거리 안쪽의 선호 거리까지 접근한다.
         */
        if (horizontalDistance >
            preferredMaxDistance)
        {
            ChangeState(
                EnemyState.Chase
            );

            MoveTowardTarget();

            return;
        }

        /*
         * 너무 가까움.
         */
        if (horizontalDistance <
            preferredMinDistance)
        {
            /*
             * 공격 준비가 되어 있다면
             * 바로 도망치지 않고 먼저 공격한다.
             *
             * 기획:
             * "가까이 접근하면 도망치기보다
             * 잠시 멈춰 공격한 뒤 다시 거리를 벌린다."
             */
            if (stoneAttack.IsReady)
            {
                StopMovement();
                FaceTarget();

                bool attackStarted =
                    stoneAttack.TryAttack(
                        target
                    );

                if (attackStarted)
                {
                    retreatAfterAttack = true;
                }

                return;
            }

            /*
             * 공격 쿨타임 중이라면
             * 그냥 후퇴해서 거리 확보.
             */
            ChangeState(
                EnemyState.Retreat
            );

            MoveAwayFromTarget(true);

            return;
        }

        /*
         * 선호 거리 안.
         *
         * 이동하지 않고 공격한다.
         */
        StopMovement();
        FaceTarget();

        ChangeState(
            EnemyState.Idle
        );

        if (stoneAttack.IsReady)
        {
            stoneAttack.TryAttack(
                target
            );
        }
    }
}