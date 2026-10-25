using System.Collections;
using UnityEngine;

// 플레이어 대시(회피).
// 입력: LeftShift (인스펙터에서 변경 가능). 이동 중이면 그 방향,
// 가만히 있으면 바라보는 방향으로 짧게 돌진한다.
//
// 공격 중에 대시를 입력하면 공격을 끊고 대시가 나간다.
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CombatAfterimageEmitter))]
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

    [Header("대시 잔상")]
    [SerializeField] private CombatAfterimage afterimagePrefab;
    [SerializeField] private Color afterimageTint = new Color(0.6f, 0.8f, 1f, 0.45f);

    private Rigidbody2D rb;
    private PlayerMove playerMove;
    private PlayerAttack attack;
    private PlayerHealth health;
    private SpriteRenderer bodyVisual;
    private CombatAfterimageEmitter afterimages;

    private float cooldownRemaining;
    private bool isDashing;

    public bool IsDashing => isDashing;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMove = GetComponent<PlayerMove>();
        attack = GetComponent<PlayerAttack>();
        health = GetComponent<PlayerHealth>();
        bodyVisual = GetComponent<SpriteRenderer>();
        if (bodyVisual == null) bodyVisual = GetComponentInChildren<SpriteRenderer>();
        afterimages = GetComponent<CombatAfterimageEmitter>();
        if (afterimages == null) afterimages = gameObject.AddComponent<CombatAfterimageEmitter>();
    }

    private void Update()
    {
        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);

        if (!UiModal.IsOpen && Input.GetKeyDown(dashKey))
            TryDash();
    }

    public bool TryDash()
    {
        if (!isActiveAndEnabled || isDashing || cooldownRemaining > 0f)
            return false;
        if (health != null && health.IsDead)
            return false;
        if (UiModal.IsOpen)
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

        float elapsed = 0f;
        while (elapsed < dashTime)
        {
            rb.linearVelocity = new Vector2(dir * dashSpeed, 0f);
            if (bodyVisual != null && bodyVisual.enabled)
                afterimages.Emit(afterimagePrefab, bodyVisual, bodyVisual.sortingOrder - 1, afterimageTint);
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
        if (isDashing)
        {
            if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (playerMove != null) playerMove.ControlLocked = false;
        }
        afterimages?.Clear();
        isDashing = false;
        cooldownRemaining = 0f;
    }
}
