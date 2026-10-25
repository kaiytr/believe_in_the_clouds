using System.Collections;
using UnityEngine;

// 플레이어 대시(회피).
// 입력: LeftShift (인스펙터에서 변경 가능). 이동 중이면 그 방향,
// 가만히 있으면 바라보는 방향으로 짧게 돌진한다.
//
// 교리: 시작 시 DoctrineDodgeEmitter가 있으면 OnDodge를 Raise해서
//       "회피하면 ..." 계열 교리가 별도 분기 없이 발동한다.
// 공격 중에 대시를 입력하면 공격을 끊고 대시가 나간다.
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerDash : MonoBehaviour
{
    [Header("입력")]
    [SerializeField] private KeyCode dashKey = KeyCode.LeftShift;

    [Header("대시")]
    [Tooltip("돌진 속도.")]
    [SerializeField, Min(0f)] private float dashSpeed = 14f;
    [Tooltip("돌진 지속 시간(초).")]
    [SerializeField, Min(0f)] private float dashTime = 0.18f;
    [Tooltip("재사용 대기 시간(초).")]
    [SerializeField, Min(0f)] private float cooldown = 0.6f;

    private Rigidbody2D rb;
    private PlayerMove playerMove;
    private PlayerAttack attack;
    private PlayerHealth health;
    private DoctrineDodgeEmitter dodgeEmitter;

    private float cooldownRemaining;
    private bool isDashing;

    public bool IsDashing => isDashing;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMove = GetComponent<PlayerMove>();
        attack = GetComponent<PlayerAttack>();
        health = GetComponent<PlayerHealth>();
        dodgeEmitter = GetComponent<DoctrineDodgeEmitter>();
    }

    private void Update()
    {
        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);

        if (Input.GetKeyDown(dashKey))
            TryDash();
    }

    public bool TryDash()
    {
        if (isDashing || cooldownRemaining > 0f)
            return false;
        if (health != null && health.IsDead)
            return false;

        float input = Input.GetAxisRaw("Horizontal");
        float dir = Mathf.Abs(input) > 0.01f
            ? Mathf.Sign(input)
            : (transform.localScale.x >= 0f ? 1f : -1f);

        // 바라보는 방향도 대시 방향으로 고정한다.
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * dir;
        transform.localScale = s;

        if (attack != null && attack.IsAttacking)
            attack.CancelAttack();

        StartCoroutine(DashRoutine(dir));
        return true;
    }

    private IEnumerator DashRoutine(float dir)
    {
        isDashing = true;
        cooldownRemaining = cooldown;
        playerMove.ControlLocked = true;
        if (dodgeEmitter != null)
            dodgeEmitter.PerformDodge();

        float elapsed = 0f;
        while (elapsed < dashTime)
        {
            rb.linearVelocity = new Vector2(dir * dashSpeed, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 살짝 미끄러지듯 멈춘다 (y는 중력에 맡긴다).
        rb.linearVelocity = new Vector2(dir * dashSpeed * 0.25f, rb.linearVelocity.y);
        playerMove.ControlLocked = false;
        isDashing = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isDashing = false;
        cooldownRemaining = 0f;
    }
}
