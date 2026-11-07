using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BelieverSpriteAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite[] walkFrames;
    [SerializeField] private Sprite[] prepareFrames;
    [SerializeField] private Sprite[] attackFrames;
    [SerializeField] private Sprite deathSprite;

    private BelieverEnemy owner;
    private ThrowStoneAttack attack;
    private Rigidbody2D body;
    private SpriteRenderer visual;
    private EnemyState previousState;
    private float stateTime;
    private float attackAnimationTime;

    private void Awake()
    {
        visual = GetComponent<SpriteRenderer>();
        owner = GetComponentInParent<BelieverEnemy>();
        if (owner == null) return;
        attack = owner.GetComponent<ThrowStoneAttack>();
        body = owner.GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        previousState = EnemyState.Idle;
        stateTime = 0f;
        attackAnimationTime = 0f;
    }

    private void LateUpdate()
    {
        if (owner == null) return;
        EnemyState state = owner.CurrentState;
        stateTime = state == previousState ? stateTime + Time.deltaTime : 0f;

        Sprite frame;
        if (owner.IsDead)
            frame = deathSprite;
        else if (state == EnemyState.AttackWindup)
        {
            attackAnimationTime = 0f;
            float duration = Mathf.Max(.001f, attack.WindupDuration);
            int index = Mathf.FloorToInt(stateTime / duration * prepareFrames.Length);
            frame = prepareFrames[Mathf.Min(index, prepareFrames.Length - 1)];
        }
        else if (state == EnemyState.Attack || state == EnemyState.Recovery)
        {
            bool startingAttack = previousState != EnemyState.Attack && previousState != EnemyState.Recovery;
            attackAnimationTime = startingAttack ? 0f : attackAnimationTime + Time.deltaTime;
            int index = Mathf.FloorToInt(attackAnimationTime * 12f);
            frame = index < attackFrames.Length ? attackFrames[index] : GetLocomotionFrame();
        }
        else
            frame = GetLocomotionFrame();

        previousState = state;
        if (frame != null) visual.sprite = frame;
    }

    private Sprite Loop(Sprite[] frames, float framesPerSecond) =>
        frames[Mathf.FloorToInt(stateTime * framesPerSecond) % frames.Length];

    private Sprite GetLocomotionFrame() => Mathf.Abs(body.linearVelocity.x) > .05f
        ? Loop(walkFrames, 9f)
        : Loop(idleFrames, 5f);
}
