using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ExecutiveSpriteAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite[] walkFrames;
    [SerializeField] private Sprite[] prepareFrames;
    [SerializeField] private Sprite[] attackFrames;
    [SerializeField] private Sprite deathSprite;

    private ExecutiveEnemy owner;
    private MagicCircleAttack attack;
    private Rigidbody2D body;
    private SpriteRenderer visual;
    private EnemyState previousState;
    private float stateTime;
    private float castTime;

    private void Awake()
    {
        visual = GetComponent<SpriteRenderer>();
        owner = GetComponentInParent<ExecutiveEnemy>();
        if (owner == null) return;
        attack = owner.GetComponent<MagicCircleAttack>();
        body = owner.GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        previousState = EnemyState.Idle;
        stateTime = 0f;
        castTime = 0f;
    }

    private void LateUpdate()
    {
        if (owner == null) return;

        EnemyState state = owner.CurrentState;
        stateTime = state == previousState ? stateTime + Time.deltaTime : 0f;
        Sprite frame = idleFrames[Mathf.FloorToInt(stateTime * 5f) % idleFrames.Length];

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
            // The magic circle resolves immediately, so the pose continues into recovery.
            bool continuingCast = previousState == EnemyState.Attack || previousState == EnemyState.Recovery;
            castTime = continuingCast ? castTime + Time.deltaTime : 0f;
            int index = Mathf.FloorToInt(castTime * 12f);
            if (index < attackFrames.Length) frame = attackFrames[index];
        }
        else if (Mathf.Abs(body.linearVelocity.x) > .05f)
            frame = walkFrames[Mathf.FloorToInt(stateTime * 9f) % walkFrames.Length];

        previousState = state;
        visual.sprite = frame;
    }
}
