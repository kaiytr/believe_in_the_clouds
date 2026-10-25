using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class BossCloudPlatform : MonoBehaviour
{
    private BossCombatController owner;
    private bool white, reported;
    private int generation, pattern;
    private float previousFeet;
    private BoxCollider2D surface;
    private BossWorldCue cue;
    private float expiresAt;
    private SpriteRenderer visual;
    public bool IsWhite => white;
    public bool Reported => reported;
    public void Initialize(BossCombatController boss, bool whiteCloud, BossAttackGeometry shape)
    {
        owner = boss; white = whiteCloud; generation = boss.Generation; pattern = boss.PatternId;
        transform.position = shape.Center;
        transform.localScale = new Vector3(shape.Size.x, shape.Size.y, 1f);
        gameObject.layer = white ? boss.Settings.PlatformLayer : 0;
        surface = GetComponent<BoxCollider2D>();
        surface.isTrigger = !white;
        Physics2D.SyncTransforms();
        previousFeet = boss.PlayerCollider.bounds.min.y;
        expiresAt = Time.time + boss.Settings.CloudDuration;
        visual = GetComponent<SpriteRenderer>();
        cue = BossWorldCue.Create(transform, boss.PresentationFont, boss);
    }
    private void Update()
    {
        if (cue == null || owner == null) return;
        float remaining = Mathf.Max(0f, expiresAt - Time.time);
        Color color = white ? new Color(.6f, .95f, 1f) : new Color(1f, .64f, .88f);
        cue.Show(new Vector2(transform.position.x, surface.bounds.max.y),
            (white ? "흰 구름 · 착지 발판" : "먹구름 · 통과 시 가시") + $"\n{remaining:0.0}초 후 사라짐", color, remaining / owner.Settings.CloudDuration);
        Color fill = visual.color; fill.a = Mathf.Lerp(.35f, 1f, Mathf.Clamp01(remaining / .5f)); visual.color = fill;
    }
    private void FixedUpdate()
    {
        if (reported || owner == null || !owner.CanContinue || generation != owner.Generation || pattern != owner.PatternId) return;
        Collider2D target = owner.PlayerCollider;
        Bounds playerBounds = target.bounds;
        Bounds cloudBounds = surface.bounds;
        float feet = playerBounds.min.y;
        Rigidbody2D playerBody = owner.PlayerHealth.GetComponent<Rigidbody2D>();
        bool descending = playerBody != null && playerBody.linearVelocity.y <= 0.05f;
        bool inWidth = playerBounds.max.x > cloudBounds.min.x + 0.04f && playerBounds.min.x < cloudBounds.max.x - 0.04f;
        bool crossedTop = previousFeet >= cloudBounds.max.y - 0.04f && feet <= cloudBounds.max.y + 0.04f;
        bool validLanding = !white || Mathf.Abs(feet - cloudBounds.max.y) <= 0.09f;
        if (descending && inWidth && crossedTop && validLanding)
        {
            reported = true;
            owner.OnCloudInteraction(white, new Vector2(playerBounds.center.x, feet), generation, pattern);
        }
        previousFeet = feet;
    }
    private void OnDisable() { if (cue != null) cue.gameObject.SetActive(false); }
    private void OnDestroy() { if (cue != null) Destroy(cue.gameObject); }
}
