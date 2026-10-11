using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyUnit : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    [SerializeField] protected float maxHp = 30f;
    [SerializeField] protected float moveSpeed = 2.5f;

    [Header("Target")]
    [SerializeField] private float targetSearchInterval = 0.5f;

    [Header("Death")]
    [SerializeField] private float destroyDelay = 1.5f;

    protected Rigidbody2D rb;
    protected Animator animator;
    protected Transform target;

    protected float currentHp;
    protected EnemyState currentState = EnemyState.Idle;

    private BeliefController beliefController;

    private float moveInput;
    private bool movementLocked;

    private float nextTargetSearchTime;

    public bool IsDead => currentState == EnemyState.Dead;

    public bool IsMovementLocked => movementLocked;

    public EnemyState CurrentState => currentState;

    public Transform Target => target;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        beliefController = GetComponent<BeliefController>();

        currentHp = maxHp;
    }

    protected virtual void Start()
    {
        TryAcquirePlayer();
    }

    protected virtual void Update()
    {
        if (IsDead)
            return;

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
        if (IsDead)
            return;

        if (movementLocked)
            return;

        rb.linearVelocity = new Vector2(
            moveInput * moveSpeed,
            rb.linearVelocity.y
        );
    }

    protected abstract void UpdateAI();

    private bool HasValidTarget()
    {
        return target != null &&
               target.gameObject.activeInHierarchy;
    }

    protected bool TryAcquirePlayer()
    {
        if (Time.time < nextTargetSearchTime)
            return false;

        nextTargetSearchTime =
            Time.time + targetSearchInterval;

        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return false;

        target = player.transform;

        IgnorePhysicalCollisionWithPlayer(
            target
        );

        return true;
    }

    protected float GetDistanceToTarget()
    {
        if (target == null)
            return float.MaxValue;

        return Vector2.Distance(
            transform.position,
            target.position
        );
    }

    protected float GetHorizontalDistanceToTarget()
    {
        if (target == null)
            return float.MaxValue;

        return Mathf.Abs(
            target.position.x -
            transform.position.x
        );
    }

    protected void MoveTowardTarget()
    {
        if (target == null)
        {
            StopMovement();
            return;
        }

        float direction = Mathf.Sign(
            target.position.x -
            transform.position.x
        );

        SetHorizontalMove(direction);

        FaceDirection(direction);
    }

    protected void MoveAwayFromTarget(
        bool keepFacingTarget = true
    )
    {
        if (target == null)
        {
            StopMovement();
            return;
        }

        float targetDirection = Mathf.Sign(
            target.position.x -
            transform.position.x
        );

        float moveDirection = -targetDirection;

        SetHorizontalMove(moveDirection);

        if (keepFacingTarget)
            FaceDirection(targetDirection);
        else
            FaceDirection(moveDirection);
    }

    protected void SetHorizontalMove(float direction)
    {
        if (movementLocked)
            return;

        moveInput =
            Mathf.Clamp(direction, -1f, 1f);
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
        {
            rb.linearVelocity = new Vector2(
                0f,
                rb.linearVelocity.y
            );
        }
    }

    public void UnlockMovement()
    {
        if (IsDead)
            return;

        movementLocked = false;
    }

    public void FaceTarget()
    {
        if (target == null)
            return;

        float direction = Mathf.Sign(
            target.position.x -
            transform.position.x
        );

        FaceDirection(direction);
    }

    protected void FaceDirection(float direction)
    {
        if (Mathf.Abs(direction) < 0.01f)
            return;

        Vector3 scale = transform.localScale;

        scale.x =
            Mathf.Abs(scale.x) *
            Mathf.Sign(direction);

        transform.localScale = scale;
    }

    public void ChangeState(EnemyState newState)
    {
        if (IsDead)
            return;

        currentState = newState;
    }

    public virtual void TakeDamage(float damage)
    {
        if (IsDead)
            return;

        if (damage <= 0f)
            return;

        Vector2 hitDirection = target != null
            ? (Vector2)target.position - (Vector2)transform.position
            : Vector2.zero;
        foreach (SequentialFlankAttackBeliefCondition condition in
                 GetComponents<SequentialFlankAttackBeliefCondition>())
        {
            condition.RegisterPlayerHit(hitDirection);
        }

        float finalDamage = damage;

        if (beliefController != null)
        {
            finalDamage =
                beliefController
                    .ApplyIncomingDamageModifier(damage);
        }

        if (finalDamage <= 0f)
            return;

        currentHp -= finalDamage;

        if (currentHp <= 0f)
        {
            currentHp = 0f;

            Die();
        }
    }

    protected virtual void Die()
    {
        if (beliefController != null)
        {
            beliefController.StopBeliefSystem();
        }

        currentState = EnemyState.Dead;

        movementLocked = true;

        StopMovement();

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(
                0f,
                rb.linearVelocity.y
            );
        }

        Destroy(gameObject, destroyDelay);
    }
    private void IgnorePhysicalCollisionWithPlayer(
    Transform playerRoot
)
    {
        if (playerRoot == null)
            return;

        Collider2D[] enemyColliders =
            GetComponentsInChildren<Collider2D>(true);

        Collider2D[] playerColliders =
            playerRoot
                .GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D enemyCollider in enemyColliders)
        {
            /*
             * Trigger는 피해 판정 등에 사용하므로
             * 충돌을 무시하면 안 된다.
             */
            if (enemyCollider.isTrigger)
                continue;

            foreach (Collider2D playerCollider in playerColliders)
            {
                Physics2D.IgnoreCollision(
                    enemyCollider,
                    playerCollider,
                    true
                );
            }
        }
    }
}
