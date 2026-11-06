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

    private Image fill, chip;
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

    private void Update()
    {
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
    private static void ResetStatics() => whiteSprite = null;
}
