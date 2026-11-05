using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared prefab, styling and world positioning for ordinary enemies and bosses.</summary>
public sealed class CombatantHudView : MonoBehaviour
{
    public const float ReferenceCanvasScale = 0.01f;
    private const float ReferenceScreenHeight = 1080f;

    [SerializeField] private Canvas beliefCanvas, healthCanvas;
    [SerializeField] private Image healthFill, beliefFill, beliefBackground, speechBubble;
    [SerializeField] private Text phrase;
    public Canvas BeliefCanvas => beliefCanvas;
    public Canvas HealthCanvas => healthCanvas;
    public Image HealthFill => healthFill;
    public Image BeliefFill => beliefFill;
    public Text Phrase => phrase;
    public bool IsConfigured => beliefCanvas != null && healthCanvas != null && healthFill != null &&
        beliefFill != null && beliefBackground != null && speechBubble != null && phrase != null;

    public static CombatantHudView Create(Transform parent)
    {
        var prefab = CombatUiResources.HudPrefab;
        if (prefab == null) { Debug.LogError("Missing shared Combat/CombatantHud resource.", parent); return null; }
        return Instantiate(prefab, parent, false);
    }

    public void Initialize(Font font)
    {
        phrase.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (beliefCanvas != null)
            beliefCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 180f);
        if (healthCanvas != null)
            healthCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 62f);

        Image healthBackground = healthFill != null && healthFill.transform.parent != null
            ? healthFill.transform.parent.GetComponent<Image>()
            : null;
        ApplyArtwork(healthBackground, "EnemyStrengthBackground");
        ApplyArtwork(healthFill, "EnemyStrengthGauge");
        ApplyArtwork(beliefBackground, "EnemyBeliefBackground");
        ApplyArtwork(beliefFill, "EnemyBeliefGauge");
        if (beliefBackground != null)
        {
            RectTransform beliefBarRect = beliefBackground.rectTransform;
            beliefBarRect.anchorMin = beliefBarRect.anchorMax = new Vector2(0.5f, 0.5f);
            beliefBarRect.pivot = new Vector2(0.5f, 0.5f);
            beliefBarRect.anchoredPosition = new Vector2(0f, 72f);
            beliefBarRect.sizeDelta = new Vector2(318f, 22f);
        }
        if (healthFill != null)
        {
            healthFill.rectTransform.offsetMin = new Vector2(4f, 3f);
            healthFill.rectTransform.offsetMax = new Vector2(-4f, -3f);
        }
        if (beliefFill != null)
        {
            RectTransform beliefFillRect = beliefFill.rectTransform;
            beliefFillRect.anchorMin = Vector2.zero;
            beliefFillRect.anchorMax = Vector2.one;
            beliefFillRect.offsetMin = new Vector2(3f, 3f);
            beliefFillRect.offsetMax = new Vector2(-3f, -3f);
        }
        HudArtworkSprite speechArtwork = HudArtworkResources.Load("EnemyBeliefSpeechBubble");
        if (speechArtwork.IsValid)
        {
            speechBubble.sprite = speechArtwork.Sprite;
            speechBubble.color = Color.white;
            speechBubble.preserveAspect = false;

            RectTransform bubbleRect = speechBubble.rectTransform;
            bubbleRect.anchorMin = bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
            bubbleRect.pivot = new Vector2(0.5f, 0.5f);
            bubbleRect.anchoredPosition = new Vector2(0f, -14f);
            bubbleRect.sizeDelta = new Vector2(
                speechArtwork.PixelBounds.width,
                speechArtwork.PixelBounds.height);

            RectTransform phraseRect = phrase.rectTransform;
            phraseRect.anchorMin = Vector2.zero;
            phraseRect.anchorMax = Vector2.one;
            phraseRect.offsetMin = new Vector2(32f, 34f);
            phraseRect.offsetMax = new Vector2(-32f, -22f);
        }

        Transform oldTail = speechBubble.transform.Find("BeliefSpeechBubbleTail");
        if (oldTail != null)
            oldTail.gameObject.SetActive(false);

        if (healthFill != null)
        {
            healthFill.type = Image.Type.Filled;
            healthFill.fillMethod = Image.FillMethod.Horizontal;
            healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
        if (beliefFill != null)
        {
            beliefFill.type = Image.Type.Filled;
            beliefFill.fillMethod = Image.FillMethod.Horizontal;
            beliefFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
    }

    private static void ApplyArtwork(Image target, string assetName)
    {
        if (target == null)
            return;

        HudArtworkSprite artwork = HudArtworkResources.Load(assetName);
        if (!artwork.IsValid)
            return;

        target.sprite = artwork.Sprite;
        target.color = Color.white;
        target.preserveAspect = false;
        target.raycastTarget = false;
    }
    public void UpdateHealth(float current, float maximum) => healthFill.fillAmount =
        maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
    public void UpdateBelief(float normalized, bool vulnerable, string text)
    {
        beliefFill.fillAmount = vulnerable ? 0f : Mathf.Clamp01(normalized);
        beliefFill.color = Color.white;
        beliefBackground.color = vulnerable ? new Color(.78f, .64f, .62f) : new Color(.73f, .73f, .73f);
        phrase.text = text;
        speechBubble.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }
    public void SetVisible(bool visible) => beliefCanvas.enabled = healthCanvas.enabled = visible;
    public void Position(Bounds body)
    {
        Camera camera = Camera.main;
        float healthScale = ReferenceCanvasScale;
        if (camera != null && camera.orthographic)
            healthScale = Mathf.Max(.0001f, camera.orthographicSize * 2f / ReferenceScreenHeight);

        Place(beliefCanvas, body.center + Vector3.up * (body.extents.y + .95f), ReferenceCanvasScale, camera);
        Place(healthCanvas, new Vector3(body.center.x,
            body.min.y - .52f * healthScale / ReferenceCanvasScale, body.center.z), healthScale, camera);
    }
    private static void Place(Canvas canvas, Vector3 position, float scale, Camera camera)
    {
        canvas.transform.SetPositionAndRotation(position, Quaternion.identity);
        Vector3 parentScale = canvas.transform.parent.lossyScale;
        canvas.transform.localScale = new Vector3(Inverse(parentScale.x) * scale, Inverse(parentScale.y) * scale, 1f);
        canvas.worldCamera = camera;
    }
    private static float Inverse(float value) => Mathf.Abs(value) > .0001f ? 1f / value : 1f;
}
