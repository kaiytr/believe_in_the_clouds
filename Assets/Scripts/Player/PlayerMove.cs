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

    public float BaseMoveSpeed => moveSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        UpdateSlow();

        // 입력은 렌더 프레임에서만 읽는다.
        moveInput = Input.GetAxisRaw("Horizontal");

        // 점프 입력 예약 (실제 물리는 FixedUpdate에서 처리).
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpRequested = true;
        }

        // 플레이어 방향 전환
        if (moveInput != 0)
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

        // 좌우 이동
        rb.linearVelocity = new Vector2(
            moveInput * moveSpeed * speedMultiplier,
            rb.linearVelocity.y
        );
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
