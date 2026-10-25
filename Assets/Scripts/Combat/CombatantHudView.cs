using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared prefab, styling and world positioning for ordinary enemies and bosses.</summary>
public sealed class CombatantHudView : MonoBehaviour
{
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
    { phrase.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
    public void UpdateHealth(float current, float maximum) => healthFill.fillAmount =
        maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
    public void UpdateBelief(float normalized, bool vulnerable, string text)
    {
        beliefFill.fillAmount = vulnerable ? 0f : Mathf.Clamp01(normalized);
        beliefFill.color = vulnerable ? new Color(.94f, .31f, .25f) : new Color(.4f, .4f, .4f);
        beliefBackground.color = vulnerable ? new Color(.78f, .64f, .62f) : new Color(.73f, .73f, .73f);
        phrase.text = text;
        speechBubble.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }
    public void SetVisible(bool visible) => beliefCanvas.enabled = healthCanvas.enabled = visible;
    public void Position(Bounds body)
    {
        Camera camera = Camera.main;
        float scale = .01f;
        if (camera != null && camera.orthographic)
            scale = Mathf.Max(scale, 12f / 19f * camera.orthographicSize * 2f / Mathf.Max(1, camera.pixelHeight));
        Place(beliefCanvas, body.center + Vector3.up * (body.extents.y + .48f * scale / .01f), scale, camera);
        Place(healthCanvas, new Vector3(body.center.x, body.min.y - .18f * scale / .01f, body.center.z), scale, camera);
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
