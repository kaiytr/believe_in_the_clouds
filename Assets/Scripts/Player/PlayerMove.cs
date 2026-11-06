using UnityEngine;

/// <summary>Ground that moves by itself (e.g. a boss cloud). Standing on it must not read as the player walking.</summary>
public interface IMovingGround
{
    float GroundVelocityX { get; }
}

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMove : MonoBehaviour, ISlowable
{
    [Header("이동")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("점프")]
    // 0.74 유닛 타일 4칸: 중력 배율 4, 0.02초 물리 틱에서 약 2.96 유닛 상승.
    [SerializeField] private float jumpPower = 15.64f;
    [Tooltip("점프가 실제로 발동되는 순간 재생할 클립. 비어 있으면 소리 없이 동작한다.")]
    [SerializeField] private AudioClip jumpSound;
    [Tooltip("점프 사운드 볼륨 (0~1).")]
    [SerializeField, Range(0f, 1f)] private float jumpSoundVolume = 0.8f;

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
    private DoctrineStats doctrineStats;
    private PlayerHealth health;
    private AudioSource jumpAudioSource;

    /// <summary>
    /// true면 이동 입력/방향 전환을 잠그고 Rigidbody 속도를 덮어쓰지 않는다.
    /// PlayerAttack이 콤보 도중 전진(lunge) 속도를 직접 넣기 위한 용도다.
    /// </summary>
    public bool ControlLocked { get; set; }

    /// <summary>
    /// Non-zero makes the player walk in that direction (-1/1) on their own, even while locked or under a modal.
    /// Used by cutscenes so scripted walking uses the normal movement and animation.
    /// </summary>
    public float ScriptedMoveInput { get; set; }

    /// <summary>바닥에 닿아 있는지. 공중 공격 가능 여부 판단용.</summary>
    public bool IsGrounded => isGrounded;
    /// <summary>Horizontal speed of the moving ground under the feet (0 on ordinary ground).</summary>
    public float GroundVelocityX { get; private set; }
    /// <summary>True while the player is holding a horizontal direction.</summary>
    public bool HasMoveInput => moveInput != 0f;
    public LayerMask GroundLayer => groundLayer;

    public float BaseMoveSpeed => moveSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        doctrineStats = GetComponent<DoctrineStats>();
        health = GetComponent<PlayerHealth>();

        // 공격/사망/대시음과 같은 AudioSource를 공유한다 (PlayOneShot은 겹쳐 재생 가능).
        jumpAudioSource = GetComponent<AudioSource>();
        if (jumpAudioSource == null)
            jumpAudioSource = gameObject.AddComponent<AudioSource>();
        jumpAudioSource.playOnAwake = false;
        jumpAudioSource.loop = false;
        jumpAudioSource.spatialBlend = 0f; // 2D
        if (jumpAudioSource.volume <= 0f)
            jumpAudioSource.volume = 1f;
    }

    private void Update()
    {
        UpdateSlow();
        if (health != null && health.IsDead)
        {
            moveInput = 0f;
            jumpRequested = false;
            return;
        }

        // 모달 UI가 열려 있는 동안은 이동 입력을 읽지 않는다.
        if (ScriptedMoveInput != 0f)
            moveInput = Mathf.Sign(ScriptedMoveInput);
        else if (!ControlLocked && !UiModal.IsOpen)
            moveInput = Input.GetAxisRaw("Horizontal");
        else
            moveInput = 0f;

        // 점프 입력 예약 (실제 물리는 FixedUpdate에서 처리).
        if (!ControlLocked && !UiModal.IsOpen && Input.GetKeyDown(KeyCode.Space))
        {
            jumpRequested = true;
        }

        // 플레이어 방향 전환 (공격 중에는 고정된 타격 방향 유지).
        if ((!ControlLocked || ScriptedMoveInput != 0f) && moveInput != 0)
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
        if (health != null && health.IsDead)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // 물리 쿼리는 물리 틱에서만. Update에서 매 프레임 돌리면 렌더 fps에 비례해 낭비된다.
        Collider2D ground = groundCheck != null
            ? Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer)
            : null;
        isGrounded = ground != null;
        IMovingGround moving = ground != null ? ground.GetComponentInParent<IMovingGround>() : null;
        GroundVelocityX = moving != null ? moving.GroundVelocityX : 0f;

        if (jumpRequested)
        {
            jumpRequested = false;
            if (isGrounded)
            {
                Jump();
            }
        }

        // 좌우 이동 (공격이 전진 속도를 직접 넣는 동안은 덮어쓰지 않음)
        // 교리 이속 증가(+40%)는 DoctrineStats.SpeedMultiplier로 합산된다.
        if (!ControlLocked || ScriptedMoveInput != 0f)
        {
            float doctrineSpeed = doctrineStats != null ? doctrineStats.SpeedMultiplier : 1f;
            rb.linearVelocity = new Vector2(
                moveInput * moveSpeed * speedMultiplier * doctrineSpeed,
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

    // Doctrine IncreaseSpeed (+40%): 지속 5초 (교체식). duration<=0이면 만료 없음.
    public void ApplyDoctrineSpeed(float multiplier, float duration = 5f)
    {
        if (doctrineStats == null)
            doctrineStats = GetComponent<DoctrineStats>();
        if (doctrineStats == null || multiplier <= 0f)
            return;

        doctrineStats.ApplySpeedBuff(multiplier, duration);
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

        if (jumpSound != null && jumpAudioSource != null)
            jumpAudioSource.PlayOneShot(jumpSound, jumpSoundVolume);
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
