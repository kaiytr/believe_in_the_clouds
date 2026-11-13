using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Damage chip" behind a health gauge: the gauge drops at once, the amount just lost shows in orange, holds
/// for a moment and then drains away. Healing moves the chip along with the gauge. Works for any filled Image.
/// </summary>
public sealed class GaugeChip : MonoBehaviour
{
    private const float HoldSeconds = .3f, DrainPerSecond = 1.1f;
    private static readonly Color ChipColor = new Color(1f, .56f, .1f, 1f);
    private static Sprite whiteSprite;
    // White copy of a gauge picture's shape (same size, only the opaque part), so the chip follows rounded ends and
    // transparent rows of the art instead of sticking out as a plain rectangle.
    private static readonly System.Collections.Generic.Dictionary<Sprite, Sprite> silhouettes =
        new System.Collections.Generic.Dictionary<Sprite, Sprite>();

    private Image fill, chip;
    private Sprite silhouetteSource;
    private float target = 1f, shown = 1f, holdUntil;
    private bool initialized;

    /// <summary>Creates the chip as a sibling right behind <paramref name="fill"/>, with the same rectangle.</summary>
    public static GaugeChip Attach(Image fill)
    {
        if (fill == null || fill.transform.parent == null) return null;
        GaugeChip existing = fill.transform.parent.GetComponentInChildren<GaugeChip>(true);
        if (existing != null && existing.fill == fill) return existing;

        var go = new GameObject(fill.name + " Chip", typeof(RectTransform), typeof(Image), typeof(GaugeChip));
        go.transform.SetParent(fill.transform.parent, false);
        go.transform.SetSiblingIndex(fill.transform.GetSiblingIndex());
        RectTransform source = fill.rectTransform, rect = go.GetComponent<RectTransform>();
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.anchoredPosition = source.anchoredPosition;
        rect.sizeDelta = source.sizeDelta;
        rect.offsetMin = source.offsetMin;
        rect.offsetMax = source.offsetMax;
        rect.localScale = source.localScale;

        var chip = go.GetComponent<Image>();
        chip.sprite = WhiteSprite();
        chip.type = Image.Type.Filled;
        chip.fillMethod = Image.FillMethod.Horizontal;
        chip.fillOrigin = fill.fillOrigin;
        chip.color = ChipColor;
        chip.raycastTarget = false;

        GaugeChip result = go.GetComponent<GaugeChip>();
        result.fill = fill;
        result.chip = chip;
        result.MatchShape();
        return result;
    }

    /// <summary>Sets the gauge (0..1). A drop leaves the lost part orange; anything else snaps the chip.</summary>
    public void Set(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);
        if (fill != null) fill.fillAmount = normalized;
        if (!initialized)
        {
            initialized = true;
            target = shown = normalized;
        }
        else if (normalized < target - .0001f)
        {
            // Hit: the chip starts from wherever it still was, so quick successive hits stack up.
            shown = Mathf.Max(shown, target);
            holdUntil = Time.time + HoldSeconds;
            target = normalized;
        }
        else if (normalized > target + .0001f)
        {
            target = normalized;
            if (shown < normalized) shown = normalized;
        }
        Apply();
    }

    // The chip takes the silhouette of whatever picture the gauge currently shows.
    private void MatchShape()
    {
        if (chip == null || fill == null || fill.sprite == silhouetteSource) return;
        silhouetteSource = fill.sprite;
        Sprite shape = SilhouetteOf(silhouetteSource);
        chip.sprite = shape != null ? shape : WhiteSprite();
    }

    private static Sprite SilhouetteOf(Sprite source)
    {
        if (source == null || source.texture == null) return null;
        if (silhouettes.TryGetValue(source, out Sprite cached) && cached != null) return cached;

        // The art texture is not readable by the CPU, so copy the sprite's region through a render texture.
        Rect rect = source.textureRect;
        int width = Mathf.Max(1, Mathf.RoundToInt(rect.width)), height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
        Texture2D texture = source.texture;
        RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        RenderTexture previous = RenderTexture.active;
        Texture2D copy;
        try
        {
            Graphics.Blit(texture, target,
                new Vector2(rect.width / texture.width, rect.height / texture.height),
                new Vector2(rect.x / texture.width, rect.y / texture.height));
            RenderTexture.active = target;
            copy = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.DontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            copy.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
        Color32[] pixels = copy.GetPixels32();
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, pixels[i].a);
        copy.SetPixels32(pixels);
        copy.Apply(false, true);

        Sprite shape = Sprite.Create(copy, new Rect(0f, 0f, width, height), new Vector2(.5f, .5f),
            source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        shape.hideFlags = HideFlags.DontSave;
        silhouettes[source] = shape;
        return shape;
    }

    private void Update()
    {
        MatchShape();
        if (shown > target && Time.time >= holdUntil)
            shown = Mathf.MoveTowards(shown, target, DrainPerSecond * Time.deltaTime);
        Apply();
    }

    private void Apply()
    {
        if (chip == null) return;
        chip.fillAmount = shown;
        chip.enabled = shown > target + .0001f;
    }

    private static Sprite WhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Point };
        texture.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255),
            new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
        texture.Apply(false, true);
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(.5f, .5f), 100f);
        whiteSprite.hideFlags = HideFlags.DontSave;
        return whiteSprite;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { whiteSprite = null; silhouettes.Clear(); }
}
