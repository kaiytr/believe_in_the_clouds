using System.Collections;
using UnityEngine;

public abstract class EnemyAttack : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] protected float damage = 10f;
    [SerializeField] protected float cooldown = 2.5f;
    [SerializeField] protected float windupTime = 0.8f;
    [SerializeField] protected float recoveryTime = 0.5f;

    protected EnemyUnit owner;

    private bool isAttacking;
    private float nextAttackTime;

    private Coroutine attackRoutine;
    private bool damagedPlayerDuringAttack;
    private bool dodgeNotified;
    private bool attackWasLaunched;
    private int pendingHitResults;

    public event System.Action<EnemyAttack> OnAttackStarted;
    public event System.Action<EnemyAttack> OnAttackExecuted;
    public event System.Action<EnemyAttack> OnPlayerHit;
    public event System.Action<EnemyAttack> OnPlayerDodged;
    public event System.Action<EnemyAttack, bool> OnAttackFinished;

    public bool IsAttacking => isAttacking;
    public bool DamagedPlayerDuringAttack =>
        damagedPlayerDuringAttack;

    public bool IsReady =>
        !isAttacking &&
        Time.time >= nextAttackTime;

    protected virtual void Awake()
    {
        owner = GetComponent<EnemyUnit>();

        if (owner == null)
        {
            Debug.LogError(
                $"{name}: EnemyAttack에는 같은 오브젝트의 EnemyUnit이 필요합니다."
            );
        }
    }

    public bool TryAttack(Transform target)
    {
        if (owner == null)
            return false;

        if (owner.IsDead)
            return false;

        if (!IsReady)
            return false;

        if (!CanAttack(target))
            return false;

        attackRoutine =
            StartCoroutine(
                AttackRoutine(target)
            );

        return true;
    }

    private IEnumerator AttackRoutine(Transform target)
    {
        isAttacking = true;
        damagedPlayerDuringAttack = false;
        dodgeNotified = false;
        attackWasLaunched = false;
        pendingHitResults = 0;

        owner.LockMovement();
        owner.FaceTarget();

        owner.ChangeState(
            EnemyState.AttackWindup
        );

        PrepareAttack(target);

        OnAttackStarted?.Invoke(this);

        ShowDangerIndicator();

        yield return new WaitForSeconds(
            windupTime
        );

        if (owner == null || owner.IsDead)
        {
            FinishAttack(true);
            yield break;
        }

        owner.ChangeState(
            EnemyState.Attack
        );

        OnAttackExecuted?.Invoke(this);

        /*
         * 공격별 실제 실행.
         *
         * 신자:
         * 돌을 생성하고 즉시 종료.
         *
         * 개:
         * 돌진이 끝날 때까지 대기.
         */
        yield return ExecuteAttackRoutine();

        while (pendingHitResults > 0 &&
               owner != null &&
               !owner.IsDead)
        {
            yield return null;
        }

        if (owner == null || owner.IsDead)
        {
            FinishAttack(true);
            yield break;
        }

        /*
         * 실제 공격 종료 시점부터 쿨타임 시작.
         */
        nextAttackTime =
            Time.time + cooldown;

        owner.ChangeState(
            EnemyState.Recovery
        );

        if (attackWasLaunched && !damagedPlayerDuringAttack && !dodgeNotified)
        {
            dodgeNotified = true;
            OnPlayerDodged?.Invoke(this);
        }

        yield return new WaitForSeconds(
            recoveryTime
        );

        if (owner != null &&
            !owner.IsDead)
        {
            owner.UnlockMovement();

            owner.ChangeState(
                EnemyState.Idle
            );
        }

        FinishAttack(true);
    }

    protected abstract bool CanAttack(
        Transform target
    );

    protected virtual void PrepareAttack(
        Transform target
    )
    {
    }

    protected virtual void ShowDangerIndicator()
    {
    }

    protected abstract IEnumerator ExecuteAttackRoutine();

    private void FinishAttack(bool notify)
    {
        if (!isAttacking && attackRoutine == null)
            return;

        isAttacking = false;
        attackRoutine = null;

        if (notify)
        {
            OnAttackFinished?.Invoke(
                this,
                damagedPlayerDuringAttack
            );
        }
    }

    protected void NotifyPlayerHit()
    {
        if (damagedPlayerDuringAttack)
            return;

        damagedPlayerDuringAttack = true;
        dodgeNotified = true;

        OnPlayerHit?.Invoke(this);
    }

    protected void MarkAttackAsLaunched()
    {
        attackWasLaunched = true;
    }

    protected void TrackPendingHitResult()
    {
        pendingHitResults++;
    }

    public void ResolvePendingHitResult(bool playerHit)
    {
        if (pendingHitResults <= 0)
            return;
        pendingHitResults--;
        if (playerHit)
            NotifyPlayerHit();
    }

    public void NotifyProjectilePlayerHit()
    {
        NotifyPlayerHit();
    }

    protected virtual void OnDisable()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);

            attackRoutine = null;
        }

        FinishAttack(false);

        damagedPlayerDuringAttack = false;
        dodgeNotified = false;
        attackWasLaunched = false;
        pendingHitResults = 0;

        if (owner != null &&
            !owner.IsDead)
        {
            owner.UnlockMovement();
        }
    }
}