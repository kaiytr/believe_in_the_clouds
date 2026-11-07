using UnityEngine;
using UnityEngine.UI;

/// <summary>World annotations for debug mode; gameplay retains the attack's shape and direction.</summary>
public sealed class BossWorldCue : MonoBehaviour
{
    private Transform source;
    private BossCombatController owner;
    private Canvas canvas;
    private Text label;
    private Image progress;
    private Vector2 position;

    public static BossWorldCue Create(Transform source, Font font, BossCombatController owner)
    {
        var root = new GameObject("World cue", typeof(RectTransform), typeof(Canvas), typeof(BossWorldCue));
        root.transform.SetParent(source.parent, false);
        var cue = root.GetComponent<BossWorldCue>();
        cue.source = source;
        cue.owner = owner;
        cue.canvas = root.GetComponent<Canvas>();
        cue.canvas.renderMode = RenderMode.WorldSpace;
        cue.canvas.enabled = owner != null && owner.DebugUiVisible;
        cue.canvas.sortingOrder = 60;
        ((RectTransform)root.transform).sizeDelta = new Vector2(176f, 40f);
        var background = root.AddComponent<Image>();
        background.color = new Color(.025f, .035f, .065f, .92f); background.raycastTarget = false;
        var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(root.transform, false);
        cue.label = textObject.GetComponent<Text>();
        cue.label.font = font; cue.label.fontSize = 14; cue.label.alignment = TextAnchor.MiddleCenter;
        cue.label.raycastTarget = false; cue.label.horizontalOverflow = HorizontalWrapMode.Wrap;
        cue.label.verticalOverflow = VerticalWrapMode.Truncate;
        cue.label.rectTransform.anchorMin = Vector2.zero; cue.label.rectTransform.anchorMax = Vector2.one;
        cue.label.rectTransform.offsetMin = new Vector2(5f, 3f); cue.label.rectTransform.offsetMax = new Vector2(-5f, 0f);
        var barObject = new GameObject("Time remaining", typeof(RectTransform), typeof(Image));
        barObject.transform.SetParent(root.transform, false);
        cue.progress = barObject.GetComponent<Image>(); cue.progress.raycastTarget = false;
        cue.progress.rectTransform.anchorMin = Vector2.zero; cue.progress.rectTransform.anchorMax = new Vector2(1f, 0f);
        cue.progress.rectTransform.pivot = Vector2.zero; cue.progress.rectTransform.sizeDelta = new Vector2(0f, 3f);
        return cue;
    }

    public void Show(Vector2 worldPosition, string text, Color color, float remaining = 1f)
    {
        position = worldPosition;
        label.text = text; label.color = color; progress.color = color;
        progress.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(remaining), 0f);
        gameObject.SetActive(true);
        RefreshVisibility();
    }

    private void LateUpdate()
    {
        if (source == null) { Destroy(gameObject); return; }
        if (!source.gameObject.activeInHierarchy) { gameObject.SetActive(false); return; }
        RefreshVisibility();
    }
    private void RefreshVisibility()
    {
        canvas.enabled = owner != null && owner.DebugUiVisible;
        if (canvas.enabled) Place();
    }
    private void Place()
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        // Constant pixel size keeps the annotation readable at both test resolutions.
        float unitsPerPixel = camera.orthographic ? camera.orthographicSize * 2f / Mathf.Max(1, camera.pixelHeight) : .012f;
        transform.SetPositionAndRotation(position + Vector2.up * (28f * unitsPerPixel), Quaternion.identity);
        transform.localScale = Vector3.one * unitsPerPixel;
        canvas.worldCamera = camera;
    }
}
