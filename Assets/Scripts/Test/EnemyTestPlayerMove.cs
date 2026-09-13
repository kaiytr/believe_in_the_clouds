using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyTestPlayerMove :
    MonoBehaviour
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

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        moveInput =
            Input.GetAxisRaw(
                "Horizontal"
            );

        if (groundCheck != null)
        {
            isGrounded =
                Physics2D.OverlapCircle(
                    groundCheck.position,
                    groundCheckRadius,
                    groundLayer
                );
        }

        if (Input.GetKeyDown(
                KeyCode.Space) &&
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
                moveInput * moveSpeed,
                rb.linearVelocity.y
            );
    }
}