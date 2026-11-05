using UnityEngine;

// Kept as the prefab component to preserve existing asset GUIDs and references.
// Storm clouds are moving decorations whose lower edge defines the lightning range.
// White clouds additionally own a thin one-way landing surface and carry whoever stands on it,
// so the shared PlayerMove never has to know about moving platforms.
[RequireComponent(typeof(BoxCollider2D))]
public sealed class BossCloudPlatform : MonoBehaviour, IMovingGround
{
    private BossCombatController owner;
    private BossCloudField field;
    private Rigidbody2D body;
    private BoxCollider2D surface;
    private Vector2 size;
    private Vector2 limitsX;
    private Vector2 speedRange, turnRange;
    private float speed, turnTimer;
    private int direction = 1;
    private bool moving;
    private int platformLayer;
    private float visualLift;

    public bool IsWhite { get; private set; }
    /// <summary>Logical center. White clouds use the physics body so damage and landing read the same position.</summary>
    public Vector2 Position => body != null ? body.position - new Vector2(0f, visualLift) : (Vector2)transform.position;
    /// <summary>Distance the picture's centre sits above the logical centre (extra puff above the standing line).</summary>
    public float VisualLift => visualLift;
    public Vector2 Size => size;
    public float Left => Position.x - size.x * .5f;
    public float Right => Position.x + size.x * .5f;
    public float Top => Position.y + size.y * .5f;
    public float Bottom => Position.y - size.y * .5f;
    public AttackGeometry Shape => new AttackGeometry(Position, size);
    /// <summary>One-way landing collider (white clouds only).</summary>
    public Collider2D Surface => surface;
    /// <summary>Pauses the random walk (the cloud stays in place; riders keep standing on it). Step still moves it explicitly.</summary>
    public bool MotionEnabled { get; set; } = true;
    public bool IsMoving => moving && MotionEnabled;
    public float CurrentSpeed => IsMoving ? speed * direction : 0f;

    public void Initialize(BossCombatController boss, bool whiteCloud, AttackGeometry shape)
    {
        owner = boss;
        IsWhite = whiteCloud;
        size = shape.Size;
        visualLift = whiteCloud ? boss.Settings.WhiteCloudVisualExtra * .5f : 0f;
        transform.position = shape.Center + new Vector2(0f, visualLift);
        transform.localScale = Vector3.one;
        SpriteRenderer visual = GetComponent<SpriteRenderer>();
        visual.drawMode = SpriteDrawMode.Sliced;
        visual.size = shape.Size + new Vector2(0f, visualLift * 2f);
        visual.color = Color.white;
        gameObject.layer = 0;
        // The prefab collider is only a decoration marker; it must not raise triggers on the moving body.
        GetComponent<BoxCollider2D>().enabled = false;
        if (whiteCloud) BuildSurface();
    }

    public void Bind(BossCloudField owningField, Vector2 arenaX, Vector2 speeds, Vector2 turns)
    {
        field = owningField;
        limitsX = arenaX;
        speedRange = speeds;
        turnRange = turns;
        moving = true;
        Retarget(keepDirection: false);
    }

    private void BuildSurface()
    {
        platformLayer = owner.Settings.PlatformLayer;
        body = GetComponent<Rigidbody2D>();
        if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = false;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.position = transform.position;

        // 루트 배율을 유지해 이미지 크기와 무관하게 실제 착지면을 월드 단위로 맞춘다.
        var landing = new GameObject("Landing surface").transform;
        landing.SetParent(transform, false);
        float thickness = Mathf.Min(owner.Settings.WhiteCloudSurfaceThickness, size.y);
        landing.localPosition = new Vector3(0f, size.y * .5f - thickness * .5f - visualLift, 0f);
        landing.gameObject.layer = 0; // switched to the ground layer once a body is above it (UpdateSurfaceLayer)
        surface = landing.gameObject.AddComponent<BoxCollider2D>();
        surface.size = new Vector2(size.x, thickness);
        surface.usedByEffector = true;
        var effector = landing.gameObject.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.surfaceArc = 170f;
        effector.useSideFriction = false;
        effector.useSideBounce = false;
        if (owner.BodyCollider != null) Physics2D.IgnoreCollision(owner.BodyCollider, surface, true);
    }

    private void FixedUpdate()
    {
        if (!IsWhite) return;
        UpdateSurfaceLayer();
        if (MotionEnabled) Step(Time.fixedDeltaTime);
    }

    private void Update()
    {
        if (!IsWhite && MotionEnabled) Step(Time.deltaTime);
    }

    /// <summary>Advance the random horizontal walk. Exposed so tests can drive movement deterministically.</summary>
    public void Step(float deltaTime)
    {
        if (!moving || deltaTime <= 0f) return;
        turnTimer -= deltaTime;
        if (turnTimer <= 0f) Retarget(keepDirection: false);
        float half = size.x * .5f;
        float min = limitsX.x + half, max = limitsX.y - half;
        float x;
        if (min >= max) x = (limitsX.x + limitsX.y) * .5f;
        else
        {
            x = Position.x + direction * speed * deltaTime;
            if (x <= min) { x = min; direction = 1; }
            else if (x >= max) { x = max; direction = -1; }
        }
        MoveToX(x);
    }

    private void Retarget(bool keepDirection)
    {
        if (!keepDirection) direction = Random.value < .5f ? -1 : 1;
        speed = Random.Range(speedRange.x, speedRange.y);
        turnTimer = Random.Range(turnRange.x, turnRange.y);
    }

    private void MoveToX(float x)
    {
        if (!IsWhite)
        {
            transform.position = new Vector3(x, transform.position.y, transform.position.z);
            return;
        }
        Vector2 from = body.position;
        Vector2 delta = new Vector2(x - from.x, 0f);
        UpdateSurfaceLayer();
        if (delta.x == 0f) return;
        CarryRider(delta);
        body.MovePosition(from + delta);
        carryVelocityX = delta.x / Mathf.Max(.0001f, Time.fixedDeltaTime);
        carryStamp = Time.fixedTime;
    }

    // The base ground check overlaps a small circle around the feet. A thin one-way surface that is
    // visible to it from below would grant a mid-air jump, so it is only a "ground" layer for a body above it.
    private void UpdateSurfaceLayer()
    {
        if (surface == null) return;
        bool above = field != null && field.TryGetRider(out Collider2D rider, out _) && rider.bounds.min.y >= SurfaceTop - .05f;
        int layer = above ? platformLayer : 0;
        if (surface.gameObject.layer != layer) surface.gameObject.layer = layer;
    }

    private float carryVelocityX, carryStamp = -1f;
    /// <summary>Horizontal speed of this cloud's last step; 0 once it has stopped moving.</summary>
    public float GroundVelocityX =>
        IsWhite && Time.fixedTime - carryStamp <= Time.fixedDeltaTime * 2.5f ? carryVelocityX : 0f;

    public float SurfaceTop => surface != null ? surface.bounds.max.y : Top;

    private void CarryRider(Vector2 delta)
    {
        if (field == null || !field.TryGetRider(out Collider2D rider, out Rigidbody2D riderBody)) return;
        Bounds feet = rider.bounds;
        float top = SurfaceTop;
        bool overlapsX = feet.max.x > Left + .05f && feet.min.x < Right - .05f;
        bool restingOnTop = feet.min.y >= top - .12f && feet.min.y <= top + .18f && riderBody.linearVelocity.y <= .5f;
        if (overlapsX && restingOnTop) riderBody.position += delta;
    }

    /// <summary>True when the rider's feet rest on this white cloud's landing surface.</summary>
    public bool IsCarrying(Collider2D rider)
    {
        if (!IsWhite || rider == null) return false;
        Bounds feet = rider.bounds;
        float top = SurfaceTop;
        return feet.max.x > Left + .05f && feet.min.x < Right - .05f && feet.min.y >= top - .12f && feet.min.y <= top + .18f;
    }
}
