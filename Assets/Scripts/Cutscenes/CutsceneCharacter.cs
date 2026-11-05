using UnityEngine;

[DisallowMultipleComponent]
public sealed class CutsceneCharacter : MonoBehaviour
{
    [SerializeField] private string displayName = "화자";
    [SerializeField] private Transform headAnchor;
    [SerializeField] private Vector3 fallbackHeadOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Transform projectileOrigin;
    [SerializeField] private EnemyUnit enemy;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public Transform HeadAnchor => headAnchor;
    public Vector3 HeadWorldPosition => headAnchor != null
        ? headAnchor.position
        : transform.TransformPoint(fallbackHeadOffset);
    public Vector3 ProjectileWorldPosition => projectileOrigin != null
        ? projectileOrigin.position
        : transform.TransformPoint(fallbackHeadOffset * 0.65f);
    public EnemyUnit Enemy => enemy != null ? enemy : GetComponent<EnemyUnit>();
    public Rigidbody2D Body => GetComponent<Rigidbody2D>();

    private void Awake()
    {
        if (enemy == null)
            enemy = GetComponent<EnemyUnit>();
    }

    public void Configure(string actorName, Transform head, Transform throwOrigin = null)
    {
        if (!string.IsNullOrEmpty(actorName))
            displayName = actorName;
        if (head != null)
            headAnchor = head;
        if (throwOrigin != null)
            projectileOrigin = throwOrigin;
        if (enemy == null)
            enemy = GetComponent<EnemyUnit>();
    }

    public void SetCutscenePose(CutscenePose pose)
    {
        EnemyUnit owner = Enemy;
        if (owner == null || owner.IsDead)
            return;

        switch (pose)
        {
            case CutscenePose.Walking:
                owner.ChangeState(EnemyState.Chase);
                break;
            case CutscenePose.Throwing:
                owner.ChangeState(EnemyState.Attack);
                break;
            default:
                owner.ChangeState(EnemyState.Idle);
                break;
        }
    }

    public void SetWorldPosition(Vector3 position)
    {
        transform.position = position;
        Rigidbody2D body = Body;
        if (body != null)
            body.position = position;
    }

    public void FaceToward(Vector3 position)
    {
        Vector3 scale = transform.localScale;
        float direction = position.x - transform.position.x;
        if (Mathf.Abs(direction) < 0.001f || Mathf.Abs(scale.x) < 0.001f)
            return;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction);
        transform.localScale = scale;
    }
}

public enum CutscenePose
{
    Idle,
    Walking,
    Throwing
}
