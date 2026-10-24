using System.Collections;
using UnityEngine;

// 플레이어 대시(회피).
// 입력: LeftShift (인스펙터에서 변경 가능). 이동 중이면 그 방향,
// 가만히 있으면 바라보는 방향으로 짧게 돌진한다.
//
// 연출: 돌진 동안 몸을 숙이고(PlayerBody pose), 등 뒤에 잔상(고스트 스프라이트)을 남긴다.
// 교리: 시작 시 DoctrineDodgeEmitter가 있으면 OnDodge를 Raise해서
//       "회피하면 ..." 계열 교리가 별도 분기 없이 발동한다.
// 공격 중에 대시를 입력하면 공격을 끊고 대시가 나간다.
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerBody))]
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

    [Header("몸 숙이기")]
    [Tooltip("대시 중 몸 높이 배율(1 = 평소).")]
    [SerializeField, Range(0.2f, 1f)] private float crouchScaleY = 0.7f;

    [Header("잔상")]
    [Tooltip("고스트를 남기는 간격(초).")]
    [SerializeField, Min(0.01f)] private float ghostInterval = 0.03f;
    [Tooltip("고스트가 사라지기까지 시간(초).")]
    [SerializeField, Min(0f)] private float ghostLife = 0.3f;
    [SerializeField] private Color ghostColor = new Color(0.6f, 0.8f, 1f, 0.5f);

    private const string DashPoseKey = "dash";

    private Rigidbody2D rb;
    private PlayerMove playerMove;
    private PlayerBody body;
    private PlayerAttack attack;
    private PlayerHealth health;
    private DoctrineDodgeEmitter dodgeEmitter;
    private SpriteRenderer spriteRenderer;

    private float cooldownRemaining;
    private bool isDashing;

    public bool IsDashing => isDashing;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMove = GetComponent<PlayerMove>();
        body = GetComponent<PlayerBody>();
        attack = GetComponent<PlayerAttack>();
        health = GetComponent<PlayerHealth>();
        dodgeEmitter = GetComponent<DoctrineDodgeEmitter>();
        spriteRenderer = GetComponent<SpriteRenderer>();
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
        body.PushPose(DashPoseKey, crouchScaleY);
        if (dodgeEmitter != null)
            dodgeEmitter.PerformDodge();

        float elapsed = 0f;
        float ghostTimer = 0f;
        while (elapsed < dashTime)
        {
            rb.linearVelocity = new Vector2(dir * dashSpeed, 0f);

            ghostTimer -= Time.deltaTime;
            if (ghostTimer <= 0f)
            {
                ghostTimer = ghostInterval;
                SpawnGhost();
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        body.PopPose(DashPoseKey);
        // 살짝 미끄러지듯 멈춘다 (y는 중력에 맡긴다).
        rb.linearVelocity = new Vector2(dir * dashSpeed * 0.25f, rb.linearVelocity.y);
        playerMove.ControlLocked = false;
        isDashing = false;
    }

    private void SpawnGhost()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return;

        GameObject ghost = new GameObject("DashGhost");
        ghost.transform.position = transform.position;
        ghost.transform.rotation = transform.rotation;
        ghost.transform.localScale = transform.localScale;

        SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>();
        sr.sprite = spriteRenderer.sprite;
        sr.flipX = spriteRenderer.flipX;
        sr.flipY = spriteRenderer.flipY;
        sr.sortingLayerID = spriteRenderer.sortingLayerID;
        sr.sortingOrder = spriteRenderer.sortingOrder - 1;
        sr.color = ghostColor;

        Destroy(ghost, ghostLife + 0.05f);
        StartCoroutine(FadeGhost(sr, ghostLife));
    }

    private IEnumerator FadeGhost(SpriteRenderer sr, float life)
    {
        float t = 0f;
        Color c = ghostColor;
        while (t < life && sr != null)
        {
            t += Time.deltaTime;
            c.a = ghostColor.a * Mathf.Clamp01(1f - t / Mathf.Max(0.01f, life));
            sr.color = c;
            yield return null;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isDashing = false;
        cooldownRemaining = 0f;
        if (body != null)
            body.PopPose(DashPoseKey);
    }
}
