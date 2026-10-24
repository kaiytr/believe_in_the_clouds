using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMove : MonoBehaviour, ISlowable
{
    [Header("이동")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("점프")]
    [SerializeField] private float jumpPower = 10f;

    [Header("바닥 체크")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D rb;
    private float moveInput;
    private bool jumpRequested;
    private bool isGrounded;
    private float speedMultiplier = 1f;
    private float slowEndTime;

    /// <summary>
    /// true면 이동 입력/방향 전환을 잠그고 Rigidbody 속도를 덮어쓰지 않는다.
    /// PlayerAttack이 콤보 도중 전진(lunge) 속도를 직접 넣기 위한 용도다.
    /// </summary>
    public bool ControlLocked { get; set; }

    /// <summary>바닥에 닿아 있는지. 공중 공격 가능 여부 판단용.</summary>
    public bool IsGrounded => isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        UpdateSlow();

        // 입력은 렌더 프레임에서만 읽는다.
        if (!ControlLocked)
            moveInput = Input.GetAxisRaw("Horizontal");
        else
            moveInput = 0f;

        // 점프 입력 예약 (실제 물리는 FixedUpdate에서 처리).
        if (!ControlLocked && Input.GetKeyDown(KeyCode.Space))
        {
            jumpRequested = true;
        }

        // 플레이어 방향 전환 (공격 중에는 고정된 타격 방향 유지).
        if (!ControlLocked && moveInput != 0)
        {
            Vector3 scale = transform.localScale;

            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(moveInput);

            transform.localScale = scale;
        }
    }

    private void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        // 물리 쿼리는 물리 틱에서만. Update에서 매 프레임 돌리면 렌더 fps에 비례해 낭비된다.
        isGrounded = groundCheck != null &&
            Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (jumpRequested)
        {
            jumpRequested = false;
            if (isGrounded)
            {
                Jump();
            }
        }

        // 좌우 이동 (공격이 전진 속도를 직접 넣는 동안은 덮어쓰지 않음)
        if (!ControlLocked)
        {
            rb.linearVelocity = new Vector2(
                moveInput * moveSpeed * speedMultiplier,
                rb.linearVelocity.y
            );
        }
    }

    public void ApplySlow(float newSpeedMultiplier, float duration)
    {
        newSpeedMultiplier = Mathf.Clamp01(newSpeedMultiplier);
        speedMultiplier = Mathf.Min(speedMultiplier, newSpeedMultiplier);
        slowEndTime = Mathf.Max(slowEndTime, Time.time + duration);
    }

    private void UpdateSlow()
    {
        if (speedMultiplier >= 1f || Time.time < slowEndTime)
            return;

        speedMultiplier = 1f;
    }

    private void Jump()
    {
        // 현재 Y 속도를 초기화하고 점프
        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x,
            jumpPower
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius
        );
    }
}
