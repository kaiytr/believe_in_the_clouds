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
    private float throwTime;

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
        throwTime = 0f;
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
            float duration = Mathf.Max(.001f, attack.WindupDuration);
            int index = Mathf.FloorToInt(stateTime / duration * prepareFrames.Length);
            frame = prepareFrames[Mathf.Min(index, prepareFrames.Length - 1)];
        }
        else if (state == EnemyState.Attack || state == EnemyState.Recovery)
        {
            bool continuingThrow = previousState == EnemyState.Attack || previousState == EnemyState.Recovery;
            throwTime = continuingThrow ? throwTime + Time.deltaTime : 0f;
            int index = Mathf.FloorToInt(throwTime * 12f);
            frame = state == EnemyState.Attack || index < attackFrames.Length
                ? attackFrames[Mathf.Min(index, attackFrames.Length - 1)]
                : Loop(idleFrames, 5f);
        }
        else if (Mathf.Abs(body.linearVelocity.x) > .05f)
            frame = Loop(walkFrames, 9f);
        else
            frame = Loop(idleFrames, 5f);

        previousState = state;
        if (frame != null) visual.sprite = frame;
    }

    private Sprite Loop(Sprite[] frames, float framesPerSecond) =>
        frames[Mathf.FloorToInt(stateTime * framesPerSecond) % frames.Length];
}
