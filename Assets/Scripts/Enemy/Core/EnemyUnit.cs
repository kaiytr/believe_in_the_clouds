using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BeliefController))]
public abstract class EnemyUnit : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    [SerializeField] protected float maxHp = 30f;
    [SerializeField] protected float moveSpeed = 2.5f;

    [Header("Target")]
    [SerializeField] private float targetSearchInterval = 0.5f;

    [Header("Body")]
    [SerializeField] private Collider2D bodyCollider;

    [Header("Death")]
    [SerializeField] private float destroyDelay = 1.5f;

    protected Rigidbody2D rb;
    protected Animator animator;
    protected Transform target;
    protected float currentHp;
    protected EnemyState currentState = EnemyState.Idle;

    private BeliefController beliefController;
    private EnemyAttack[] attacks;
    private EnemyContactDamage[] contactDamageComponents;
    private BeliefView beliefView;
    private Collider2D targetBodyCollider;
    private float moveInput;
    private bool movementLocked;
    private bool configurationValid;
    private float nextTargetSearchTime;

    public bool IsDead => currentState == EnemyState.Dead;
    public bool IsVulnerable => beliefController != null && beliefController.IsVulnerable;
    public bool IsMovementLocked => movementLocked;
    public EnemyState CurrentState => currentState;
    public Transform Target => target;
    public Collider2D BodyCollider => bodyCollider;
    public Collider2D TargetBodyCollider => targetBodyCollider;
    public BeliefController Belief => beliefController;
    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public float FacingDirection => GetActiveAttackDirection();

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        beliefController = GetComponent<BeliefController>();
        attacks = GetComponents<EnemyAttack>();
        contactDamageComponents = GetComponentsInChildren<EnemyContactDamage>(true);
        beliefView = GetComponent<BeliefView>();
        currentHp = maxHp;

        if (bodyCollider == null)
            bodyCollider = GetComponent<Collider2D>();
    }

    protected virtual void Start()
    {
        TryAcquirePlayer();
        if (!ValidateConfiguration())
        {
            gameObject.SetActive(false);
            return;
        }

        beliefController.StateChanged += HandleBeliefStateChanged;
        if (!beliefView.Initialize(beliefController))
        {
            Debug.LogError($"{name}: BeliefView failed to create its phrase and gauge UI.", this);
            gameObject.SetActive(false);
            return;
        }
        if (IsVulnerable)
            HandleBeliefStateChanged(beliefController, BeliefState.Vulnerable);
    }

    protected virtual void Update()
    {
        if (IsDead || !configurationValid)
            return;

        if (IsVulnerable)
        {
            StopMovement();
            ChangeState(EnemyState.Vulnerable);
            return;
        }

        if (!HasValidTarget())
        {
            StopMovement();
            ChangeState(EnemyState.Idle);
            TryAcquirePlayer();
            return;
        }

        UpdateAI();
    }

    protected virtual void FixedUpdate()
    {
        if (IsDead || movementLocked)
            return;

        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
    }

    protected abstract void UpdateAI();

    private bool ValidateConfiguration()
    {
        if (beliefController == null || !beliefController.IsConfigured)
            return FailConfiguration("BeliefController or a valid profile is missing.");

        if (bodyCollider == null || bodyCollider.isTrigger || bodyCollider.gameObject != gameObject)
            return FailConfiguration("Assign exactly one non-trigger body Collider2D on the enemy root.");

        if (target == null || targetBodyCollider == null)
            return FailConfiguration("A tagged Player with PlayerHealth and a root body Collider2D is required.");

        BeliefCondition[] conditions = GetComponents<BeliefCondition>();
        if (conditions.Length != 1)
            return FailConfiguration("Exactly one belief condition must be attached to the enemy root.");

        bool conditionMatchesType = beliefController.Type switch
        {
            BeliefType.RearPosition => conditions[0] is RearPositionBeliefCondition,
            BeliefType.DodgeAttack => conditions[0] is DodgeAttackBeliefCondition,
            _ => false
        };
        if (!conditionMatchesType)
            return FailConfiguration("Belief type and condition component do not match.");

        if (attacks == null || attacks.Length != 1 || attacks[0] == null)
            return FailConfiguration($"Exactly one concrete EnemyAttack must be attached to the enemy root (found {attacks?.Length ?? -1}).");
        if (!attacks[0].IsConfigured)
            return FailConfiguration("EnemyAttack is missing required warning or attack assets.");

        if (beliefView == null)
            return FailConfiguration("BeliefView is required on the enemy root.");

        configurationValid = true;
        return true;
    }

    private bool FailConfiguration(string message)
    {
        Debug.LogError($"{name}: {message} Disabling this enemy.", this);
        return false;
    }

    private void HandleBeliefStateChanged(BeliefController controller, BeliefState state)
    {
        bool vulnerable = state == BeliefState.Vulnerable;
        foreach (EnemyContactDamage contact in contactDamageComponents)
        {
            if (contact != null)
                contact.SetSuppressed(vulnerable || IsDead);
        }

        foreach (EnemyAttack attack in attacks)
        {
            if (attack != null)
                attack.SetVulnerable(vulnerable);
        }

        if (vulnerable)
        {
            LockMovement();
            ChangeState(EnemyState.Vulnerable);
        }
        else if (!IsDead)
        {
            bool attackStillRunning = false;
            foreach (EnemyAttack attack in attacks)
                attackStillRunning |= attack != null && attack.IsAttacking;

            if (!attackStillRunning)
                UnlockMovement();
            ChangeState(EnemyState.Idle);
        }
    }

    private bool HasValidTarget()
    {
        return target != null && target.gameObject.activeInHierarchy &&
               targetBodyCollider != null && targetBodyCollider.enabled;
    }

    protected bool TryAcquirePlayer()
    {
        if (Time.time < nextTargetSearchTime)
            return false;

        nextTargetSearchTime = Time.time + targetSearchInterval;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return false;

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth == null || playerHealth.BodyCollider == null || playerHealth.BodyCollider.isTrigger)
        {
            target = player.transform;
            targetBodyCollider = null;
            return false;
        }

        target = player.transform;
        targetBodyCollider = playerHealth.BodyCollider;
        IgnorePhysicalCollisionWithPlayer(target);
        return true;
    }

    protected float GetDistanceToTarget()
    {
        return target == null ? float.MaxValue : Vector2.Distance(transform.position, target.position);
    }

    protected float GetHorizontalDistanceToTarget()
    {
        return target == null ? float.MaxValue : Mathf.Abs(target.position.x - transform.position.x);
    }

    protected void MoveTowardTarget()
    {
        if (target == null)
        {
            StopMovement();
            return;
        }

        float direction = Mathf.Sign(target.position.x - transform.position.x);
        SetHorizontalMove(direction);
        SetFacingDirection(direction);
    }

    protected void MoveAwayFromTarget(bool keepFacingTarget = true)
    {
        if (target == null)
        {
            StopMovement();
            return;
        }

        float targetDirection = Mathf.Sign(target.position.x - transform.position.x);
        float moveDirection = -targetDirection;
        SetHorizontalMove(moveDirection);
        SetFacingDirection(keepFacingTarget ? targetDirection : moveDirection);
    }

    protected void SetHorizontalMove(float direction)
    {
        if (!movementLocked && !IsVulnerable)
            moveInput = Mathf.Clamp(direction, -1f, 1f);
    }

    public void StopMovement()
    {
        moveInput = 0f;
    }

    public void LockMovement()
    {
        movementLocked = true;
        moveInput = 0f;
        if (rb != null)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public void UnlockMovement()
    {
        if (IsDead || IsVulnerable)
            return;
        movementLocked = false;
    }

    public void FaceTarget()
    {
        if (target != null && !IsVulnerable)
            SetFacingDirection(target.position.x - transform.position.x);
    }

    public void SetFacingDirection(float direction)
    {
        if (Mathf.Abs(direction) < 0.01f)
            return;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction);
        transform.localScale = scale;
    }

    private float GetActiveAttackDirection()
    {
        if (attacks != null)
        {
            foreach (EnemyAttack attack in attacks)
            {
                if (attack != null && attack.IsAttacking)
                    return attack.LockedFacingDirection;
            }
        }

        return transform.localScale.x >= 0f ? 1f : -1f;
    }

    public void ChangeState(EnemyState newState)
    {
        if (!IsDead)
            currentState = newState;
    }

    public bool TakeDamage(DamageInfo damageInfo)
    {
        if (IsDead || damageInfo.Amount <= 0f)
            return false;

        float finalDamage = beliefController != null
            ? beliefController.ApplyIncomingDamageModifier(damageInfo)
            : damageInfo.Amount;
        if (finalDamage <= 0f)
            return false;

        currentHp = Mathf.Max(0f, currentHp - finalDamage);
        beliefView?.UpdateHealth(currentHp, maxHp);
        if (currentHp <= 0f)
            Die();
        return true;
    }

    protected virtual void Die()
    {
        if (beliefController != null)
        {
            beliefController.StateChanged -= HandleBeliefStateChanged;
            beliefController.StopBeliefSystem();
        }

        foreach (EnemyAttack attack in attacks)
        {
            if (attack != null)
                attack.CancelForOwnerDeath();
        }
        foreach (EnemyContactDamage contact in contactDamageComponents)
        {
            if (contact != null)
                contact.SetSuppressed(true);
        }

        beliefView?.Hide();
        currentState = EnemyState.Dead;
        movementLocked = true;
        StopMovement();
        if (rb != null)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        // Doctrine: enemy/boss OnDeath doctrines trigger when the death motion ends.
        // PlayerHealth raises at Die() entry (no motion there); enemies raise just
        // before destroy so "explode on death" lands at motion end.
        // (A coroutine on this object cannot run past Destroy, so raise slightly early.)
        float deathRaiseDelay = Mathf.Max(0f, destroyDelay - 0.05f);
        StartCoroutine(RaiseDeathDoctrineAfterMotion(deathRaiseDelay));
        Destroy(gameObject, destroyDelay);
    }

    private System.Collections.IEnumerator RaiseDeathDoctrineAfterMotion(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (DoctrineEventBus.HasListeners)
            DoctrineEventBus.Raise(ConditionType.OnDeath, gameObject, null);
    }

    private void IgnorePhysicalCollisionWithPlayer(Transform playerRoot)
    {
        if (playerRoot == null)
            return;

        SetPhysicalCollisionWithPlayerEnabled(false);
    }

    public void SetPhysicalCollisionWithPlayerEnabled(bool enabled)
    {
        if (target == null)
            return;

        Collider2D[] enemyColliders = GetComponentsInChildren<Collider2D>(true);
        Collider2D[] playerColliders = target.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D enemyCollider in enemyColliders)
        {
            if (enemyCollider.isTrigger)
                continue;

            foreach (Collider2D playerCollider in playerColliders)
            {
                if (!playerCollider.isTrigger)
                    Physics2D.IgnoreCollision(enemyCollider, playerCollider, !enabled);
            }
        }
    }

    private void OnDestroy()
    {
        if (beliefController != null)
            beliefController.StateChanged -= HandleBeliefStateChanged;
    }
}
