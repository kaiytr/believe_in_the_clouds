using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossAttackVisual : MonoBehaviour
{
    [SerializeField] private LineRenderer outline;
    [SerializeField] private Color baseColor = Color.white;
    [SerializeField] private bool warning;
    private SpriteRenderer fill;
    private float fadeEndsAt, fadeDuration;
    private BossAttackGeometry geometry;
    private BossWorldCue cue;
    private LineRenderer directionMarker;
    private Color warningColor = Color.red;
    public SpriteRenderer Renderer => fill != null ? fill : GetComponent<SpriteRenderer>();

    private void Awake() { fill = GetComponent<SpriteRenderer>(); }

    public void Show(BossAttackGeometry geometry, int order, bool burning = false)
    {
        this.geometry = geometry;
        DangerIndicator indicator = GetComponent<DangerIndicator>();
        if (indicator != null) indicator.Present(geometry.Center, geometry.Angle, geometry.Size);
        else
        {
            transform.SetPositionAndRotation(geometry.Center, Quaternion.Euler(0f, 0f, geometry.Angle));
            transform.localScale = new Vector3(geometry.Size.x, geometry.Size.y, 1f);
        }
        Renderer.sortingOrder = order;
        Renderer.color = burning && !warning ? new Color(1f, 0.43f, 0.12f, 0.9f) : baseColor;
        if (outline != null)
        {
            outline.sortingOrder = order + 1;
            outline.startColor = outline.endColor = warning ? warningColor : new Color(1f, 0.94f, 0.6f, 1f);
        }
    }

    public void SetProgress(float value)
    {
        value = Mathf.Clamp01(value);
        Color color = Renderer.color;
        color.a = warning ? Mathf.Lerp(0.12f, baseColor.a, value) : baseColor.a;
        Renderer.color = color;
        if (warning && outline != null)
            outline.startWidth = outline.endWidth = Mathf.Lerp(.025f, .065f, value);
    }

    public void SetTint(Color color)
    {
        // Gameplay warning fills stay red like ordinary enemy indicators; type tint only colors debug cues.
        warningColor = color;
        if (!warning)
        {
            Color fillColor = color; fillColor.a = .18f; Renderer.color = fillColor;
            if (outline != null) outline.startColor = outline.endColor = color;
        }
    }

    public void SetCue(string text, float remaining, float progress, BossCombatController owner)
    {
        if (cue == null) cue = BossWorldCue.Create(transform, owner.PresentationFont, owner);
        float radians = geometry.Angle * Mathf.Deg2Rad;
        float height = Mathf.Abs(Mathf.Sin(radians)) * geometry.Size.x + Mathf.Abs(Mathf.Cos(radians)) * geometry.Size.y;
        cue.Show(geometry.Center + Vector2.up * (height * .5f),
            remaining > 0f ? text + $" · {remaining:0.0}초" : text, warningColor, 1f - progress);
    }

    public void SetDirection(float sign = 1f)
    {
        if (directionMarker == null)
        {
            directionMarker = new GameObject("Travel direction").AddComponent<LineRenderer>();
            directionMarker.transform.SetParent(transform, false); directionMarker.useWorldSpace = true;
            directionMarker.sharedMaterial = Renderer.sharedMaterial;
            directionMarker.startColor = directionMarker.endColor = new Color(1f, .88f, .65f);
            directionMarker.startWidth = directionMarker.endWidth = .055f;
            directionMarker.sortingOrder = Renderer.sortingOrder + 2;
        }
        float angle = geometry.Angle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Sign(sign);
        Vector2 side = new Vector2(-direction.y, direction.x);
        Vector2 tip = geometry.Center + direction * Mathf.Min(.35f, geometry.Size.x * .25f);
        float head = Mathf.Min(.18f, geometry.Size.y * .35f);
        directionMarker.positionCount = 5;
        directionMarker.SetPositions(new[] { (Vector3)(geometry.Center - direction * Mathf.Min(.3f, geometry.Size.x * .25f)), (Vector3)tip,
            (Vector3)(tip - direction * head + side * head), (Vector3)tip,
            (Vector3)(tip - direction * head - side * head) });
    }

    public void Fade(float duration)
    {
        if (cue != null) cue.gameObject.SetActive(false);
        fadeDuration = Mathf.Max(0.01f, duration);
        fadeEndsAt = Time.time + fadeDuration;
    }

    private void Update()
    {
        if (fadeEndsAt <= 0f) return;
        float remaining = Mathf.Clamp01((fadeEndsAt - Time.time) / fadeDuration);
        Color color = Renderer.color;
        color.a = remaining * baseColor.a;
        Renderer.color = color;
        if (outline != null)
        {
            Color edge = outline.startColor;
            edge.a = remaining;
            outline.startColor = outline.endColor = edge;
        }
        if (remaining <= 0f) gameObject.SetActive(false);
    }
    private void OnDisable() { if (cue != null) cue.gameObject.SetActive(false); }
    private void OnDestroy() { if (cue != null) Destroy(cue.gameObject); }
}
