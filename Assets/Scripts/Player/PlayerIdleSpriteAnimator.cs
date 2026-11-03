using UnityEngine;

// Sprite-sheet animation for the player's basic states.
// - Dead: plays once and holds the last frame until revived.
// - Jump: picks a frame from vertical velocity (rising / apex / falling).
// - Walk: grounded and moving horizontally.
// - Idle: grounded, not moving, not attacking.
// - Attacking restores the original sprite.
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PlayerIdleSpriteAnimator : MonoBehaviour
{
    [Header("Idle")]
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField, Min(1f)] private float idleFramesPerSecond = 4f;

    [Header("Walk")]
    [SerializeField] private Sprite[] walkFrames;
    [SerializeField, Min(1f)] private float walkFramesPerSecond = 8f;

    [Header("Jump")]
    [Tooltip("순서: 상승 / 정점 / 하강.")]
    [SerializeField] private Sprite[] jumpFrames;
    [Tooltip("이 값보다 빠르게 상승/하강할 때 상승·하강 프레임을 쓴다.")]
    [SerializeField, Min(0f)] private float jumpVelocityThreshold = 1f;

    [Header("Dead")]
    [Tooltip("한 번만 재생하고 마지막 프레임에서 멈춘다.")]
    [SerializeField] private Sprite[] deadFrames;
    [SerializeField, Min(1f)] private float deadFramesPerSecond = 5f;

    [Header("Down Attack")]
    [Tooltip("공중 아래 공격 중 재생. 3프레임을 공격 시간에 맞춰 한 번 재생한다.")]
    [SerializeField] private Sprite[] downAttackFrames;
    [SerializeField, Min(1f)] private float downAttackFramesPerSecond = 12f;

    [Header("Ground Attack (1/2/3타)")]
    [Tooltip("attack-Sheet 0-2: 1타.")]
    [SerializeField] private Sprite[] attack1Frames;
    [Tooltip("attack-Sheet 3-5: 2타.")]
    [SerializeField] private Sprite[] attack2Frames;
    [Tooltip("attack-Sheet 6-8: 3타(피니쉬).")]
    [SerializeField] private Sprite[] attack3Frames;
    [SerializeField, Min(1f)] private float attackFramesPerSecond = 14f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float movementThreshold = 0.08f;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D body;
    private PlayerMove movement;
    private PlayerAttack attack;
    private PlayerHealth health;
    private Sprite originalSprite;
    private Sprite[] activeFrames;
    private float activeFramesPerSecond = 4f;
    private int frameIndex;
    private float frameTimer;
    private bool showingSheetFrame;
    private bool deadPlayed;

    public bool IsDeathAnimationComplete => !HasFrames(deadFrames) || deadPlayed;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMove>();
        attack = GetComponent<PlayerAttack>();
        health = GetComponent<PlayerHealth>();
        originalSprite = spriteRenderer.sprite;
    }

    private void Update()
    {
        if (spriteRenderer == null)
            return;

        bool dead = health != null && health.IsDead;
        if (dead)
        {
            PlayDeadOnce();
            return;
        }

        // 부활하면 사망 연출을 다시 재생할 수 있게 초기화한다.
        deadPlayed = false;

        bool grounded = movement == null || movement.IsGrounded;
        bool moving = body != null && Mathf.Abs(body.linearVelocity.x) > movementThreshold;
        bool attacking = attack != null && attack.IsAttacking;

        // 공중 아래 공격이 가장 우선. 3프레임을 한 번 재생하고 마지막 프레임을 유지한다.
        if (attack != null && attack.IsAirAttacking && HasFrames(downAttackFrames))
        {
            PlayOnce(downAttackFrames, downAttackFramesPerSecond);
            return;
        }

        // 지상 1/2/3타. 각 타수 프레임을 재생하고, 끝나면 마지막 프레임을 유지한다.
        if (attack != null && attack.IsAttacking && !attack.IsAirAttacking)
        {
            Sprite[] comboFrames = ComboFrames(attack.CurrentCombo);
            if (HasFrames(comboFrames))
            {
                PlayOnce(comboFrames, attackFramesPerSecond);
                return;
            }
        }

        if (!grounded && !attacking && HasFrames(jumpFrames))
        {
            ShowJumpFrame();
            return;
        }

        Sprite[] desiredFrames = null;
        float desiredFps = 1f;

        if (grounded && !attacking && moving && HasFrames(walkFrames))
        {
            desiredFrames = walkFrames;
            desiredFps = walkFramesPerSecond;
        }
        else if (grounded && !attacking && !moving && HasFrames(idleFrames))
        {
            desiredFrames = idleFrames;
            desiredFps = idleFramesPerSecond;
        }

        if (desiredFrames == null)
        {
            RestoreOriginal();
            return;
        }

        PlayLoop(desiredFrames, desiredFps);
    }

    private void PlayLoop(Sprite[] frames, float fps)
    {
        if (activeFrames != frames)
        {
            activeFrames = frames;
            activeFramesPerSecond = fps;
            frameIndex = 0;
            frameTimer = 0f;
            ApplyFrame(frames[0]);
            return;
        }

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(1f, activeFramesPerSecond);
        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex = (frameIndex + 1) % frames.Length;
            ApplyFrame(frames[frameIndex]);
        }
    }

    // 상승/정점/하강을 Y 속도로 고른다. 프레임 순서는 jumpFrames 기준.
    private void ShowJumpFrame()
    {
        float verticalSpeed = body != null ? body.linearVelocity.y : 0f;
        int last = jumpFrames.Length - 1;
        int target;

        if (jumpFrames.Length == 1)
            target = 0;
        else if (jumpFrames.Length == 2)
            target = verticalSpeed < -jumpVelocityThreshold ? last : 0;
        else if (verticalSpeed > jumpVelocityThreshold)
            target = 0;
        else if (verticalSpeed < -jumpVelocityThreshold)
            target = last;
        else
            target = last / 2;

        if (activeFrames != jumpFrames)
        {
            activeFrames = jumpFrames;
            frameIndex = target;
            spriteRenderer.sprite = jumpFrames[target];
            showingSheetFrame = true;
            return;
        }

        if (frameIndex != target)
        {
            frameIndex = target;
            ApplyFrame(jumpFrames[target]);
        }
    }

    private void PlayDeadOnce()
    {
        if (!HasFrames(deadFrames))
            return;

        if (deadPlayed)
        {
            // 마지막 프레임 유지.
            ApplyFrame(deadFrames[deadFrames.Length - 1]);
            return;
        }

        OneShotState state = PlayOnce(deadFrames, deadFramesPerSecond, useUnscaledTime: true);
        if (state == OneShotState.Finished)
            deadPlayed = true;
    }

    private enum OneShotState
    {
        Started,
        Playing,
        Finished
    }

    // 한 번만 재생하고 마지막 프레임에서 멈춘다.
    private OneShotState PlayOnce(Sprite[] frames, float fps, bool useUnscaledTime = false)
    {
        if (activeFrames != frames)
        {
            activeFrames = frames;
            activeFramesPerSecond = fps;
            frameIndex = 0;
            frameTimer = 0f;
            ApplyFrame(frames[0]);
            return OneShotState.Started;
        }

        // Death keeps playing while the world is paused, including the final frame's duration.
        frameTimer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(1f, activeFramesPerSecond);
        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            if (frameIndex >= frames.Length - 1)
                return OneShotState.Finished;
            frameIndex++;
            ApplyFrame(frames[frameIndex]);
        }

        return OneShotState.Playing;
    }

    private void ApplyFrame(Sprite sprite)
    {
        spriteRenderer.sprite = sprite;
        showingSheetFrame = true;
    }

    private static bool HasFrames(Sprite[] frames)
    {
        return frames != null && frames.Length > 0;
    }

    private Sprite[] ComboFrames(int comboIndex)
    {
        if (comboIndex <= 0)
            return attack1Frames;
        if (comboIndex == 1)
            return attack2Frames;
        return attack3Frames;
    }

    private void RestoreOriginal()
    {
        if (showingSheetFrame)
        {
            spriteRenderer.sprite = originalSprite;
            showingSheetFrame = false;
        }

        activeFrames = null;
        frameIndex = 0;
        frameTimer = 0f;
    }

    private void OnDisable()
    {
        if (showingSheetFrame && spriteRenderer != null)
            spriteRenderer.sprite = originalSprite;
        showingSheetFrame = false;
        activeFrames = null;
        deadPlayed = false;
    }
}
