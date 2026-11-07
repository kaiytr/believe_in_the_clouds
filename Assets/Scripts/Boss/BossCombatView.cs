using UnityEngine;

public sealed class BossCombatView : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite[] walkFrames;
    [SerializeField] private Sprite[] jumpFrames;
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField] private Sprite dashFrame;
    [SerializeField] private Sprite[] attack1Frames;
    [SerializeField] private Sprite[] attack2Frames;
    [SerializeField] private Sprite[] attack3Frames;
    [SerializeField] private Sprite[] downAttackFrames;

    private BossCombatController owner;
    private BossPresentationState state;
    private float phaseFlashUntil, stateStartedAt, actionStartedAt, actionFrameTime, locomotionStartedAt;
    private Sprite[] actionFrames;
    private Sprite[] locomotionFrames;
    private int actionFirstFrame;
    private bool showingDash;
    private Vector3 previousPosition;

    public bool IsConfigured => visualRoot != null && bodyRenderer != null;
    public BossPresentationState State => state;
    public SpriteRenderer BodyRenderer => bodyRenderer;
    private void Awake()
    {
        owner = GetComponent<BossCombatController>();
        previousPosition = transform.position;
    }

    public void Initialize(BossCombatController boss) { owner = boss; }
    public void SetFacing(int direction) { if (bodyRenderer != null) bodyRenderer.flipX = direction < 0; }
    public void SetState(BossPresentationState value)
    {
        state = value;
        stateStartedAt = Time.time;
        actionFrames = null;
        showingDash = false;
    }

    public void FlashPhaseChange() { phaseFlashUntil = Time.time + 0.35f; }

    public void ShowDash()
    {
        if (dashFrame == null) return;
        showingDash = true;
        // The dash emitter captures this renderer in the same physics tick.
        bodyRenderer.sprite = dashFrame;
    }

    public bool ShowMelee(int index, float impactFrameTime = 0f)
    {
        Sprite[] frames = index <= 0 ? attack1Frames : index == 1 ? attack2Frames : attack3Frames;
        return ShowAttack(frames, impactFrameTime);
    }

    public bool ShowDownAttack(float impactFrameTime = 0f) => ShowAttack(downAttackFrames, impactFrameTime);

    private bool ShowAttack(Sprite[] frames, float impactFrameTime)
    {
        if (!HasFrames(frames)) return false;
        actionFrames = frames;
        actionFrameTime = impactFrameTime;
        actionStartedAt = Time.time;
        actionFirstFrame = impactFrameTime > 0f && frames.Length > 1 ? 1 : 0;
        bodyRenderer.sprite = frames[actionFirstFrame];
        return true;
    }

    private void LateUpdate()
    {
        if (owner == null || !IsConfigured) return;
        float horizontalDistance = Mathf.Abs(transform.position.x - previousPosition.x);
        previousPosition = transform.position;
        UpdateSprite(horizontalDistance);

        // State colors were designed for the placeholder body. Keep the sprite sheet palette.
        if (HasFrames(idleFrames)) return;

        bodyRenderer.color = state == BossPresentationState.Dead
            ? (HasFrames(deathFrames) ? Color.white : new Color(.35f, .37f, .43f)) :
            owner.IsBurning || state == BossPresentationState.Charging ? new Color(1f, .5f, .2f) :
            state == BossPresentationState.Windup || phaseFlashUntil > Time.time ? new Color(1f, .82f, .4f) :
            state == BossPresentationState.Recovery ? new Color(.72f, .75f, .82f) : Color.white;
    }

    private void UpdateSprite(float horizontalDistance)
    {
        if (state == BossPresentationState.Dead)
        {
            if (HasFrames(deathFrames))
                bodyRenderer.sprite = deathFrames[Mathf.Min(deathFrames.Length - 1, Mathf.FloorToInt((Time.time - stateStartedAt) * 5f))];
            return;
        }

        if (showingDash) return;
        if (HasFrames(actionFrames))
        {
            int elapsedFrames = actionFrameTime > 0f ? Mathf.FloorToInt((Time.time - actionStartedAt) / actionFrameTime) : 0;
            bodyRenderer.sprite = actionFrames[Mathf.Min(actionFrames.Length - 1, actionFirstFrame + elapsedFrames)];
            return;
        }

        Rigidbody2D body = owner.Body;
        Vector2 velocity = body != null ? body.linearVelocity : Vector2.zero;
        if (HasFrames(jumpFrames) && body != null && owner.Settings != null &&
            (Mathf.Abs(velocity.y) > 1f || !owner.BodyCollider.IsTouchingLayers(owner.Settings.GroundMask)))
        {
            locomotionFrames = null;
            int last = jumpFrames.Length - 1;
            bodyRenderer.sprite = jumpFrames[velocity.y > 1f ? 0 : velocity.y < -1f ? last : last / 2];
            return;
        }

        bool moving = Mathf.Abs(velocity.x) > .08f || horizontalDistance > .08f * Time.deltaTime;
        Sprite[] frames = moving && HasFrames(walkFrames) ? walkFrames : idleFrames;
        if (!HasFrames(frames)) return;
        if (frames != locomotionFrames)
        {
            locomotionFrames = frames;
            locomotionStartedAt = Time.time;
        }
        float fps = frames == walkFrames ? 8f : 4f;
        bodyRenderer.sprite = frames[Mathf.FloorToInt((Time.time - locomotionStartedAt) * fps) % frames.Length];
    }

    private static bool HasFrames(Sprite[] frames) => frames != null && frames.Length > 0;
}
