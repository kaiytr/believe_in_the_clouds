using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Red afterglow along the screen edges when the player is hit: it flares up at once, then fades out.
/// A self-made overlay canvas, created on first use; it never blocks input.
/// </summary>
public sealed class PlayerHitVignette : MonoBehaviour
{
    private const float FlareSeconds = .06f, FadeSeconds = .75f, BaseIntensity = .5f, MaxIntensity = .95f;
    private static PlayerHitVignette instance;
    private static Sprite vignetteSprite;

    private CanvasGroup group;
    private float peak, startedAt = -10f;

    /// <summary>Flashes the vignette. <paramref name="damageRatio"/> (damage / max HP) makes bigger hits stronger.</summary>
    public static void Pulse(float damageRatio = 0f)
    {
        if (!Application.isPlaying) return;
        if (instance == null) instance = Create();
        instance.peak = Mathf.Clamp(BaseIntensity + Mathf.Clamp01(damageRatio) * 1.5f, BaseIntensity, MaxIntensity);
        instance.startedAt = Time.unscaledTime;
    }

    private static PlayerHitVignette Create()
    {
        var root = new GameObject("[Player Hit Vignette]", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        DontDestroyOnLoad(root);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 90;

        var imageObject = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(root.transform, false);
        var rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = imageObject.GetComponent<Image>();
        image.sprite = VignetteSprite();
        image.color = new Color(.9f, .04f, .04f, 1f);
        image.raycastTarget = false;

        var component = root.AddComponent<PlayerHitVignette>();
        component.group = root.GetComponent<CanvasGroup>();
        component.group.alpha = 0f;
        component.group.blocksRaycasts = false;
        component.group.interactable = false;
        return component;
    }

    private void Update()
    {
        float elapsed = Time.unscaledTime - startedAt;
        float alpha;
        if (elapsed < FlareSeconds) alpha = peak * (elapsed / FlareSeconds);
        else
        {
            float t = Mathf.Clamp01((elapsed - FlareSeconds) / FadeSeconds);
            alpha = peak * (1f - t) * (1f - t); // eases out and leaves a short lingering glow
        }
        group.alpha = alpha;
    }

    // Transparent in the middle, increasingly red toward the edges (stretched to the screen).
    private static Sprite VignetteSprite()
    {
        if (vignetteSprite != null) return vignetteSprite;
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            name = "PlayerHitVignette"
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs((x + .5f) / size * 2f - 1f), dy = Mathf.Abs((y + .5f) / size * 2f - 1f);
                // Rounded-rectangle distance so the glow hugs all four edges, not just the corners.
                float edge = Mathf.Pow(Mathf.Pow(dx, 3f) + Mathf.Pow(dy, 3f), 1f / 3f);
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, 1.15f, edge));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * alpha));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        vignetteSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
        vignetteSprite.hideFlags = HideFlags.DontSave;
        return vignetteSprite;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; vignetteSprite = null; }
}
