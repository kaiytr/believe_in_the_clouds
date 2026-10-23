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
    private bool playerThreatenedDuringWarning;
    private Vector2 lockedDirection;

    public event Action<EnemyAttack, AttackOutcome> OutcomeReported;
    public bool IsAttacking => phase != AttackPhase.Idle;
    public bool IsReady => owner != null && !owner.IsDead && !owner.IsVulnerable &&
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

        if (!owner.IsVulnerable)
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
        yield return WaitGameTime(windupTime, pauseWhileVulnerable: true);
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
        yield return WaitGameTime(recoveryTime, pauseWhileVulnerable: true);

        if (owner != null && !owner.IsDead)
        {
            phase = AttackPhase.Idle;
            attackRoutine = null;
            if (!owner.IsVulnerable)
            {
                owner.UnlockMovement();
                owner.ChangeState(EnemyState.Idle);
            }
        }
    }

    private IEnumerator WaitGameTime(float duration, bool pauseWhileVulnerable)
    {
        float remaining = Mathf.Max(0f, duration);
        while (remaining > 0f)
        {
            if (owner == null || owner.IsDead)
                yield break;

            if (!pauseWhileVulnerable || !owner.IsVulnerable)
                remaining -= Time.deltaTime;
            yield return null;
        }
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

    public void ResolvePendingHitResult(AttackOutcome outcome)
    {
        if (pendingHitResults <= 0 || owner == null || owner.IsDead)
            return;

        pendingHitResults--;
        ReportOutcome(outcome);
    }

    protected void ReportOutcome(AttackOutcome outcome)
    {
        if (outcomeReported || phase == AttackPhase.Idle)
            return;

        if (outcome == AttackOutcome.EvadedByPlayer && !attackLaunched)
            outcome = AttackOutcome.Missed;

        outcomeReported = true;
        OutcomeReported?.Invoke(this, outcome);
    }

    public void SetVulnerable(bool vulnerable)
    {
        if (vulnerable && phase == AttackPhase.Windup)
            CancelWindup();
    }

    private void CancelWindup()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (activeIndicator != null)
            activeIndicator.Cancel();
        activeIndicator = null;
        ReportOutcome(AttackOutcome.Cancelled);
        cooldownRemaining = Mathf.Max(0f, cooldown);
        phase = AttackPhase.Idle;
    }

    public void CancelForOwnerDeath()
    {
        if (phase == AttackPhase.Idle && attackRoutine == null)
            return;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (activeIndicator != null)
            activeIndicator.Cancel();
        activeIndicator = null;
        ReportOutcome(AttackOutcome.Cancelled);
        pendingHitResults = 0;
        phase = AttackPhase.Idle;
        OnOwnerDeath();
    }

    protected virtual void OnOwnerDeath() { }

    protected static bool ColliderOverlapsBox(Collider2D body, Vector2 center, Vector2 size, float angle = 0f)
    {
        if (body == null || !body.enabled || !body.gameObject.activeInHierarchy)
            return false;

        Collider2D[] overlaps = Physics2D.OverlapBoxAll(center, size, angle, Physics2D.AllLayers);
        foreach (Collider2D overlap in overlaps)
        {
            if (overlap == body)
                return true;
        }
        return false;
    }

    protected static bool ColliderOverlapsCircle(Collider2D body, Vector2 center, float radius)
    {
        if (body == null || !body.enabled || !body.gameObject.activeInHierarchy)
            return false;

        Vector2 closest = body.ClosestPoint(center);
        return (closest - center).sqrMagnitude <= radius * radius;
    }

    protected virtual void OnDisable()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (activeIndicator != null)
            activeIndicator.Cancel();
        activeIndicator = null;
        if (phase != AttackPhase.Idle)
            ReportOutcome(AttackOutcome.Cancelled);
        phase = AttackPhase.Idle;
        pendingHitResults = 0;

        if (owner != null && !owner.IsDead && !owner.IsVulnerable)
            owner.UnlockMovement();
    }
}
