using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class DangerIndicator : MonoBehaviour
{
    [SerializeField, Min(0f)] private float initialWarningAlpha = 0.12f;

    private Transform followTarget;
    private Vector2 followOffset;
    private float followAngle;
    private Vector2 indicatorSize;
    private Vector2 indicatorCenter;
    private bool followTargetEnabled;
    private SpriteRenderer fill;
    private SpriteRenderer sortingReference;
    private float fullAlpha = 0.3f;

    // Projectile "laser" warning: a soft, translucent red line that thins out and vanishes as the shot leaves.
    private const float MinLineFactor = 0.04f;
    private static readonly Color LineColor = new Color(1f, .31f, .33f, .55f);
    private static Sprite softLineSprite;
    private bool thinningLine;
    private float lineFactor = 1f;

    private void Awake()
    {
        fill = GetComponent<SpriteRenderer>();
        if (fill != null)
            fullAlpha = fill.color.a;
    }

    public void Setup(Vector2 center, float angle, Vector2 size)
    {
        followTargetEnabled = false;
        followTarget = null;
        indicatorSize = size;
        Present(center, angle, size);
        SetProgress(0f);
    }

    // Boss and ordinary enemy attacks share this shape presentation.
    public void Present(Vector2 center, float angle, Vector2 size)
    {
        followTargetEnabled = false;
        followTarget = null;
        indicatorCenter = center;
        indicatorSize = size;
        transform.position = center;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        ApplyScale();
    }

    /// <summary>Switch to the thinning laser look for projectile warnings. Hit areas are unaffected: this is presentation only.</summary>
    public void UseThinningLine()
    {
        if (fill == null) fill = GetComponent<SpriteRenderer>();
        if (fill == null) return;
        thinningLine = true;
        fill.sprite = SoftLineSprite();
        ApplyScale();
        SetProgress(0f);
    }

    // 1x1 world unit sprite whose alpha is flat along the line and fades toward both edges across its thickness.
    private static Sprite SoftLineSprite()
    {
        if (softLineSprite != null) return softLineSprite;
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            name = "DangerLineSoft"
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            float distance = Mathf.Abs((y + .5f) / size * 2f - 1f); // 0 at the center, 1 at the edge
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.2f, 1f, distance));
            byte alpha = (byte)Mathf.RoundToInt(255f * fade);
            for (int x = 0; x < size; x++) pixels[y * size + x] = new Color32(255, 255, 255, alpha);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        softLineSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
        softLineSprite.hideFlags = HideFlags.DontSave;
        return softLineSprite;
    }

    private void ApplyScale() =>
        transform.localScale = new Vector3(indicatorSize.x, indicatorSize.y * (thinningLine ? lineFactor : 1f), 1f);

    // Follows an already-computed warning shape while keeping its locked offset and angle.
    public void SetupFollowing(Transform followTarget, Vector2 centerOffset, float angle, Vector2 size)
    {
        this.followTarget = followTarget;
        followOffset = centerOffset;
        followAngle = angle;
        indicatorSize = size;
        followTargetEnabled = true;
        UpdateFollowPosition();
        SetProgress(0f);
    }

    public void SetProgress(float progress)
    {
        if (this == null)
            return;

        if (fill == null)
            fill = GetComponent<SpriteRenderer>();
        if (fill == null)
            return;

        progress = Mathf.Clamp01(progress);
        Color color = fill.color;
        if (thinningLine)
        {
            // One translucent band with soft edges; it narrows to nothing at the moment of firing.
            lineFactor = Mathf.Lerp(1f, MinLineFactor, progress);
            fill.color = LineColor;
            ApplyScale();
            return;
        }
        color.a = Mathf.Lerp(Mathf.Min(initialWarningAlpha, fullAlpha), fullAlpha, progress);
        fill.color = color;
    }

    public void SetSortingBehind(SpriteRenderer reference)
    {
        if (reference == null)
            return;

        sortingReference = reference;
        ApplySortingBehindReference();
    }

    public void Cancel()
    {
        followTargetEnabled = false;
        if (gameObject != null)
            Destroy(gameObject);
    }

    private void LateUpdate()
    {
        if (!followTargetEnabled)
            return;

        if (followTarget == null)
        {
            Destroy(gameObject);
            return;
        }

        UpdateFollowPosition();
    }

    private void UpdateFollowPosition()
    {
        if (followTarget == null)
            return;

        indicatorCenter = (Vector2)followTarget.position + followOffset;
        transform.position = indicatorCenter;
        transform.rotation = Quaternion.Euler(0f, 0f, followAngle);
        ApplyScale();
    }

    private void ApplySortingBehindReference()
    {
        if (sortingReference == null)
            return;

        if (fill == null)
            fill = GetComponent<SpriteRenderer>();
        if (fill != null)
        {
            fill.sortingLayerID = sortingReference.sortingLayerID;
            fill.sortingOrder = sortingReference.sortingOrder - 2;
        }
    }
}
