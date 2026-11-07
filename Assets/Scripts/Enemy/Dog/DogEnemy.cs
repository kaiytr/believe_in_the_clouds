using UnityEngine;

[RequireComponent(typeof(DogChargeAttack))]
public class DogEnemy : EnemyUnit
{
    [Header("Movement")]
    [SerializeField]
    private float chaseStopDistance = 0.6f;

    private DogChargeAttack chargeAttack;

    protected override void Awake()
    {
        base.Awake();

        chargeAttack =
            GetComponent<DogChargeAttack>();
    }

    protected override void UpdateAI()
    {
        if (chargeAttack == null)
        {
            StopMovement();
            return;
        }

        /*
         * 선딜 / 돌진 / 후딜 중에는
         * 일반 AI 이동을 하지 않는다.
         */
        if (chargeAttack.IsAttacking)
        {
            StopMovement();
            return;
        }

        /*
         * 공격 가능한 상황이라면
         * DogChargeAttack 내부에서
         * 사거리와 높이 차이를 검사한다.
         */
        if (chargeAttack.IsReady)
        {
            StopMovement();
            FaceTarget();

            if (chargeAttack.TryAttack(target))
                return;
        }

        float horizontalDistance =
            GetHorizontalDistanceToTarget();

        /*
         * 플레이어와 거의 겹친 상황에서
         * 공격 쿨타임이라면 밀어붙이지 않고
         * 잠시 멈춘다.
         */
        if (horizontalDistance <=
            chaseStopDistance)
        {
            ChangeState(
                EnemyState.Idle
            );

            StopMovement();
            FaceTarget();

            return;
        }

        /*
         * 그 외에는 계속 추적.
         */
        ChangeState(
            EnemyState.Chase
        );

        MoveTowardTarget();
    }
}