using System;
using System.Collections;
using UnityEngine;

public abstract class EnemyAttack : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] protected float damage = 10f;
    [SerializeField] protected float cooldown = 2.5f;
    [SerializeField] protected float windupTime = 0.8f;
    [SerializeField] protected float recoveryTime = 0.5f;

    private enum AttackPhase
    {
        Idle,
        Windup,
        Resolving,
        Recovery
    }

    protected EnemyUnit owner;
    private AttackPhase phase;
    private Coroutine attackRoutine;
    private DangerIndicator activeIndicator;
    private float cooldownRemaining;
    private int pendingHitResults;
    private bool outcomeReported;
    private bool attackLaunched;
    private bool playerHpDamaged;
    private Transform attackTarget;
    private int attackId;
    private bool playerThreatenedDuringWarning;
    private Vector2 lockedDirection;

    public event Action<EnemyAttack, AttackOutcome> OutcomeReported;
    public event Action<EnemyAttack, EnemyAttackResult> AttackResolved;
    public int CurrentAttackId => attackId;
    public bool IsWindingUp => phase == AttackPhase.Windup;
    public virtual float WindupDuration => windupTime;
    public bool IsResolvingAttack(int id) => id == attackId && phase == AttackPhase.Resolving && !outcomeReported;
    public bool IsAttacking => phase != AttackPhase.Idle;
    public bool IsReady => isActiveAndEnabled && owner != null && !owner.IsDead &&
                           phase == AttackPhase.Idle && cooldownRemaining <= 0f;
    public float LockedFacingDirection => lockedDirection.x < 0f ? -1f : 1f;
    public EnemyUnit Owner => owner;
    public virtual bool IsConfigured => owner != null;
    public bool WasPlayerThreatenedDuringWarning => playerThreatenedDuringWarning;
    protected Collider2D PlayerBodyCollider => owner != null ? owner.TargetBodyCollider : null;
    protected Vector2 LockedDirection => lockedDirection;

    protected virtual void Awake()
    {
        owner = GetComponent<EnemyUnit>();
        if (owner == null)
            Debug.LogError($"{name}: EnemyAttack requires EnemyUnit on the same root.", this);
    }

    private void Update()
    {
        if (owner == null || owner.IsDead)
            return;

        if (phase != AttackPhase.Idle && !HasAttackTarget())
        {
            CancelAttack(true);
            return;
        }

        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);

        if (phase == AttackPhase.Windup)
            TrackThreatDuringWarning();
    }

    public bool TryAttack(Transform target)
    {
        if (!IsReady || target == null || !CanAttack(target))
            return false;

        float direction = target.position.x - transform.position.x;
        if (Mathf.Abs(direction) < 0.01f)
            direction = transform.localScale.x >= 0f ? 1f : -1f;
        lockedDirection = new Vector2(Mathf.Sign(direction), 0f);
        playerThreatenedDuringWarning = false;
        playerHpDamaged = false;
        attackTarget = target;
        attackId++;
        outcomeReported = false;
        attackLaunched = false;
        pendingHitResults = 0;
        phase = AttackPhase.Windup;

        owner.LockMovement();
        owner.SetFacingDirection(lockedDirection.x);
        owner.ChangeState(EnemyState.AttackWindup);
        PrepareAttack(target);
        activeIndicator = ShowDangerIndicator();
        attackRoutine = StartCoroutine(AttackRoutine(target));
        return true;
    }

    private IEnumerator AttackRoutine(Transform target)
    {
        yield return WaitWindup(WindupDuration);
        if (owner == null || owner.IsDead || phase != AttackPhase.Windup)
            yield break;

        phase = AttackPhase.Resolving;
        owner.ChangeState(EnemyState.Attack);
        yield return ExecuteAttackRoutine();

        while (pendingHitResults > 0 && owner != null && !owner.IsDead)
            yield return null;

        if (owner == null || owner.IsDead)
        {
            CancelForOwnerDeath();
            yield break;
        }

        if (!outcomeReported)
            ReportOutcome(AttackOutcome.Missed);

        cooldownRemaining = Mathf.Max(0f, cooldown);
        phase = AttackPhase.Recovery;
        owner.ChangeState(EnemyState.Recovery);
        yield return WaitGameTime(recoveryTime);

        if (owner != null && !owner.IsDead)
        {
            phase = AttackPhase.Idle;
            attackRoutine = null;
            owner.UnlockMovement();
            owner.ChangeState(EnemyState.Idle);
        }
    }

    private IEnumerator WaitGameTime(float duration)
    {
        float remaining = Mathf.Max(0f, duration);
        while (remaining > 0f)
        {
            if (owner == null || owner.IsDead)
                yield break;

            remaining -= Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitWindup(float duration)
    {
        float remaining = Mathf.Max(0f, duration);
        while (remaining > 0f)
        {
            if (owner == null || owner.IsDead)
                yield break;

            remaining -= Time.deltaTime;

            if (activeIndicator != null)
                activeIndicator.SetProgress(duration > 0f ? 1f - remaining / duration : 1f);
            yield return null;
        }

        if (activeIndicator != null)
        {
            activeIndicator.SetProgress(1f);
            activeIndicator.Cancel();
        }
        activeIndicator = null;
    }

    private void TrackThreatDuringWarning()
    {
        Collider2D body = PlayerBodyCollider;
        if (!playerThreatenedDuringWarning && body != null && IsPlayerInThreatRange(body))
            playerThreatenedDuringWarning = true;
    }

    protected abstract bool CanAttack(Transform target);
    protected virtual void PrepareAttack(Transform target) { }
    protected abstract DangerIndicator ShowDangerIndicator();
    protected abstract bool IsPlayerInThreatRange(Collider2D bodyCollider);
    protected abstract IEnumerator ExecuteAttackRoutine();

    protected bool IsPlayerOutsideThreatRange()
    {
        Collider2D body = PlayerBodyCollider;
        return body != null && !IsPlayerInThreatRange(body);
    }

    protected void MarkAttackAsLaunched()
    {
        attackLaunched = true;
    }

    protected void TrackPendingHitResult()
    {
        pendingHitResults++;
    }

    public void ResolvePendingHitResult(int sourceAttackId, AttackOutcome outcome, bool completedNormally)
    {
        if (sourceAttackId != attackId || pendingHitResults <= 0 || owner == null || owner.IsDead)
            return;

        pendingHitResults--;
        ReportOutcome(outcome, completedNormally);
    }

    public void RecordPlayerHpDamage(int sourceAttackId)
    {
        if (sourceAttackId == attackId && !outcomeReported && attackLaunched)
            playerHpDamaged = true;
    }

    private bool HasAttackTarget() => owner != null && owner.HasCombatTarget && owner.Target == attackTarget;

    protected void ReportOutcome(AttackOutcome outcome, bool completedNormally = false)
    {
        if (outcomeReported || phase == AttackPhase.Idle)
            return;

        if (outcome == AttackOutcome.EvadedByPlayer && !attackLaunched)
            outcome = AttackOutcome.Missed;

        if (!HasAttackTarget() && outcome != AttackOutcome.Cancelled)
        {
            outcome = AttackOutcome.Cancelled;
            completedNormally = false;
        }
        var result = new EnemyAttackResult(attackId, outcome, attackLaunched, playerHpDamaged,
            completedNormally, Time.time);
        outcomeReported = true;
        OutcomeReported?.Invoke(this, outcome);
        AttackResolved?.Invoke(this, result);
    }

    public void SetVulnerable(bool vulnerable)
    {
        if (vulnerable && phase == AttackPhase.Windup)
            CancelWindup();
    }

    private void CancelWindup() => CancelAttack(true);

    public void CancelForOwnerDeath() => CancelAttack(false);

    private void CancelAttack(bool startCooldown)
    {
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        attackRoutine = null;
        if (activeIndicator != null) activeIndicator.Cancel();
        activeIndicator = null;
        if (phase != AttackPhase.Idle) ReportOutcome(AttackOutcome.Cancelled);
        phase = AttackPhase.Idle;
        pendingHitResults = 0;
        if (startCooldown) cooldownRemaining = Mathf.Max(0f, cooldown);
        OnAttackCancelled();
        if (owner != null && !owner.IsDead)
        {
            owner.UnlockMovement();
            owner.ChangeState(EnemyState.Idle);
        }
    }

    protected virtual void OnAttackCancelled() { }

    protected static bool ColliderOverlapsBox(Collider2D body, Vector2 center, Vector2 size, float angle = 0f) =>
        CombatPhysics.OverlapsBox(body, center, size, angle);

    protected static bool ColliderOverlapsCircle(Collider2D body, Vector2 center, float radius) =>
        CombatPhysics.OverlapsCircle(body, center, radius);

    protected virtual void OnDisable() => CancelAttack(false);
}
