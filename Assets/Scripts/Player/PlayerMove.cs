using UnityEngine;

public class PlayerMove : MonoBehaviour
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
    private bool isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // 좌우 입력
        moveInput = Input.GetAxisRaw("Horizontal");

        // 바닥에 닿아있는지 확인
        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        );

        // 점프
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            Jump();
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
        // 좌우 이동
        rb.linearVelocity = new Vector2(
            moveInput * moveSpeed,
            rb.linearVelocity.y
        );
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