using UnityEngine;

// Presentation only. The parent DogEnemy and DogChargeAttack own movement and damage.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class DogSpriteAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite[] walkFrames;
    [SerializeField] private Sprite[] attackFrames;
    [SerializeField] private Sprite deathSprite;
    [SerializeField, Min(1f)] private float idleFramesPerSecond = 5f;
    [SerializeField, Min(1f)] private float walkFramesPerSecond = 9f;
    [SerializeField, Min(1f)] private float chargeFramesPerSecond = 12f;
    [SerializeField, Min(0f)] private float recoveryPoseSeconds = .15f;

    private DogEnemy owner;
    private DogChargeAttack attack;
    private Rigidbody2D body;
    private SpriteRenderer visual;
    private EnemyState previousState;
    private float stateTime;
    private bool hasState;

    private void Awake()
    {
        visual = GetComponent<SpriteRenderer>();
        owner = GetComponentInParent<DogEnemy>();
        if (owner == null) return;
        attack = owner.GetComponent<DogChargeAttack>();
        body = owner.GetComponent<Rigidbody2D>();
    }

    private void OnEnable() { hasState = false; stateTime = 0f; }

    private void LateUpdate()
    {
        if (owner == null || visual == null) return;
        EnemyState state = owner.CurrentState;
        if (!hasState || state != previousState)
        {
            hasState = true; previousState = state; stateTime = 0f;
        }
        else stateTime += Time.deltaTime;

        Sprite frame;
        if (owner.IsDead)
            frame = deathSprite;
        else if (state == EnemyState.AttackWindup)
        {
            float duration = attack != null ? Mathf.Max(.001f, attack.WindupDuration) : .5f;
            frame = Frame(attackFrames, Mathf.Min(2, Mathf.FloorToInt(stateTime / duration * 3f)));
        }
        else if (state == EnemyState.Attack)
            frame = Frame(attackFrames, 3 + Mathf.FloorToInt(stateTime * chargeFramesPerSecond) % 2);
        else if (state == EnemyState.Recovery && stateTime < recoveryPoseSeconds)
            frame = Frame(attackFrames, 5);
        else if (body != null && Mathf.Abs(body.linearVelocity.x) > .05f)
            frame = Loop(walkFrames, walkFramesPerSecond);
        else
            frame = Loop(idleFrames, idleFramesPerSecond);

        if (frame != null) visual.sprite = frame;
    }

    private Sprite Loop(Sprite[] frames, float framesPerSecond) =>
        frames != null && frames.Length > 0
            ? frames[Mathf.FloorToInt(stateTime * framesPerSecond) % frames.Length]
            : Frame(idleFrames, 0);

    private static Sprite Frame(Sprite[] frames, int index) =>
        frames != null && frames.Length > 0 ? frames[Mathf.Clamp(index, 0, frames.Length - 1)] : null;
}
