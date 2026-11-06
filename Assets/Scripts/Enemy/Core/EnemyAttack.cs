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

    [Header("Attack Sounds")]
    [Tooltip("공격 준비(선딜)가 시작되는 순간 재생. 비어 있으면 소리 없이 동작한다.")]
    [SerializeField] protected AudioClip attackWindupSound;
    [Tooltip("공격 시전(실제 판정이 시작되는 순간)에 재생.")]
    [SerializeField] protected AudioClip attackCastSound;
    [Tooltip("이 공격이 플레이어를 실제로 맞혔을 때 재생.")]
    [SerializeField] protected AudioClip attackHitSound;
    [Tooltip("공격 사운드 볼륨 (0~1).")]
    [SerializeField, Range(0f, 1f)] protected float attackSoundVolume = 0.9f;

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
    private AudioSource attackAudioSource;

    public event Action<EnemyAttack, AttackOutcome> OutcomeReported;
    public event Action<EnemyAttack, EnemyAttackResult> AttackResolved;

    protected float DoctrineAttackMultiplier
    {
        get
        {
            DoctrineStats stats = owner != null ? owner.GetComponent<DoctrineStats>() : null;
            return stats != null ? stats.AttackMultiplier : 1f;
        }
    }
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
    // 컷씬 연출 돌이 실제 공격과 같은 피해를 입히기 위한 값 (기본 피해 × 교리 공격 배율).
    public float ScriptedHitDamage => damage * DoctrineAttackMultiplier;
    public bool WasPlayerThreatenedDuringWarning => playerThreatenedDuringWarning;
    protected Collider2D PlayerBodyCollider => owner != null ? owner.TargetBodyCollider : null;
    protected Vector2 LockedDirection => lockedDirection;

    protected virtual void Awake()
    {
        owner = GetComponent<EnemyUnit>();
        if (owner == null)
            Debug.LogError($"{name}: EnemyAttack requires EnemyUnit on the same root.", this);

        // 공격 사운드는 적 오브젝트에 2D AudioSource 하나를 두고 겹쳐 재생한다.
        attackAudioSource = GetComponent<AudioSource>();
        if (attackAudioSource == null)
            attackAudioSource = gameObject.AddComponent<AudioSource>();
        attackAudioSource.playOnAwake = false;
        attackAudioSource.loop = false;
        attackAudioSource.spatialBlend = 0f;
        if (attackAudioSource.volume <= 0f)
            attackAudioSource.volume = 1f;
    }

    private void Update()
    {
        if (owner == null || owner.IsDead)
            return;

        if (phase != AttackPhase.Idle &&
            (!HasAttackTarget() || (phase == AttackPhase.Windup && !IsWindupTargetValid(attackTarget))))
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

        lockedDirection = GetLockedAttackDirection(target);
        if (lockedDirection.sqrMagnitude <= 0.0001f)
            lockedDirection = new Vector2(transform.localScale.x >= 0f ? 1f : -1f, 0f);
        else
            lockedDirection.Normalize();
        playerThreatenedDuringWarning = false;
        playerHpDamaged = false;
        attackTarget = target;
        attackId++;
        outcomeReported = false;
        attackLaunched = false;
        pendingHitResults = 0;
        phase = AttackPhase.Windup;

        owner.LockMovement();
        owner.SetFacingDirection(Mathf.Abs(lockedDirection.x) > 0.01f
            ? lockedDirection.x
            : transform.localScale.x);
        owner.ChangeState(EnemyState.AttackWindup);
        PrepareAttack(target);
        activeIndicator = ShowDangerIndicator();
        if (activeIndicator != null)
            activeIndicator.SetSortingBehind(owner.GetComponentInChildren<SpriteRenderer>());
        // 공격준비시전: 선딜(예고)이 시작되는 순간.
        PlayAttackSound(attackWindupSound);
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
        // 공격시전: 실제 판정이 시작되는 순간.
        PlayAttackSound(attackCastSound);
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
    protected virtual Vector2 GetLockedAttackDirection(Transform target)
    {
        float direction = target.position.x - transform.position.x;
        if (Mathf.Abs(direction) < 0.01f)
            direction = transform.localScale.x >= 0f ? 1f : -1f;
        return new Vector2(Mathf.Sign(direction), 0f);
    }

    protected virtual bool IsWindupTargetValid(Transform target) => true;
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

    /// <summary>Called once accepted HP damage is known; records belief data before notifying doctrines.</summary>
    public void NotifyPlayerHpDamageApplied(int sourceAttackId)
    {
        if (sourceAttackId != attackId || outcomeReported || !attackLaunched || playerHpDamaged)
            return;

        playerHpDamaged = true;
        // 공격피격: 이 공격이 플레이어에게 실제 피해를 넣은 순간.
        PlayAttackSound(attackHitSound);
        if (owner != null && DoctrineEventBus.HasListeners)
            DoctrineEventBus.Raise(ConditionType.OnAttack, owner.gameObject,
                attackTarget != null ? attackTarget.gameObject : null);
    }

    private void PlayAttackSound(AudioClip clip)
    {
        if (clip == null || attackAudioSource == null)
            return;

        attackAudioSource.PlayOneShot(clip, attackSoundVolume);
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
