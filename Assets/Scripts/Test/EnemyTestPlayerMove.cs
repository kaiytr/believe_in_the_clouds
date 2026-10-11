using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyTestPlayerMove :
    MonoBehaviour,
    ISlowable
{
    [Header("Move")]
    [SerializeField]
    private float moveSpeed = 5f;

    [Header("Jump")]
    [SerializeField]
    private float jumpPower = 10f;

    [Header("Ground Check")]
    [SerializeField]
    private Transform groundCheck;

    [SerializeField]
    private float groundCheckRadius = 0.2f;

    [SerializeField]
    private LayerMask groundLayer;

    private Rigidbody2D rb;

    private float moveInput;
    private bool isGrounded;

    private float speedMultiplier = 1f;
    private float slowEndTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        UpdateSlow();

        moveInput =
            Input.GetAxisRaw("Horizontal");

        CheckGround();

        if (Input.GetKeyDown(KeyCode.Space) &&
            isGrounded)
        {
            rb.linearVelocity =
                new Vector2(
                    rb.linearVelocity.x,
                    jumpPower
                );
        }
    }

    private void FixedUpdate()
    {
        rb.linearVelocity =
            new Vector2(
                moveInput *
                moveSpeed *
                speedMultiplier,

                rb.linearVelocity.y
            );
    }

    private void CheckGround()
    {
        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        isGrounded =
            Physics2D.OverlapCircle(
                groundCheck.position,
                groundCheckRadius,
                groundLayer
            );
    }

    public void ApplySlow(
        float newSpeedMultiplier,
        float duration
    )
    {
        newSpeedMultiplier =
            Mathf.Clamp01(
                newSpeedMultiplier
            );

        /*
         * 기존 둔화보다 강한 효과가 들어오면
         * 강한 쪽을 사용.
         */
        speedMultiplier =
            Mathf.Min(
                speedMultiplier,
                newSpeedMultiplier
            );

        slowEndTime =
            Mathf.Max(
                slowEndTime,
                Time.time + duration
            );

        Debug.Log(
            $"[Test Player] Slow 적용: {speedMultiplier * 100f}% / {duration}초"
        );
    }

    private void UpdateSlow()
    {
        if (speedMultiplier >= 1f)
            return;

        if (Time.time < slowEndTime)
            return;

        speedMultiplier = 1f;

        Debug.Log(
            "[Test Player] Slow 종료"
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius
        );
    }
}