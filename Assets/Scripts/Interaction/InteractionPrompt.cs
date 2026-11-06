using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 상호작용 안내 문구.
// 기본 HUD 안내 또는 대상 월드 위치를 따라가는 안내를 표시한다.
// 표시 전용이며 어떤 게임 규칙도 바꾸지 않는다.
[DefaultExecutionOrder(1000)]
public class InteractionPrompt : MonoBehaviour
{
    private GameObject pill;
    private Text label;
    private bool visible;
    private GameObject keycap;
    private Text keyLabel;
    private SquareHoldProgressGraphic holdProgress;
    private RectTransform pillRect, canvasRect;
    private Canvas canvas;
    private Transform followTarget;
    private Vector3 followOffset;
    private Vector2 dockedOffset;
    private bool worldAnchored;
    // Plain (non-hold) prompts: one keycap row per "K : text" line, stacked vertically.
    private readonly List<Row> rows = new List<Row>();
    private string shownMessage;
    private const float KeySize = 40f, RowGap = 8f, Pad = 10f, KeyTextGap = 12f;

    private sealed class Row
    {
        public GameObject Root, Keycap;
        public Text Key, Text;
        public RectTransform Rect, KeyRect, TextRect;
    }

    public bool IsVisible => pill != null && pill.activeSelf;

    public static InteractionPrompt BuildRuntime(Vector2 offset)
    {
        GameObject root = new GameObject("[InteractionPrompt]");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject pill = UiTheme.CreateImage("PromptPill", root.transform,
            new Color(0.10f, 0.11f, 0.13f, 0.82f));
        RectTransform pillRect = pill.GetComponent<RectTransform>();
        pillRect.anchorMin = new Vector2(0.5f, 0f);
        pillRect.anchorMax = new Vector2(0.5f, 0f);
        pillRect.pivot = new Vector2(0.5f, 0f);
        pillRect.anchoredPosition = offset;
        pillRect.sizeDelta = new Vector2(340f, 56f);

        Text label = UiTheme.CreateText("PromptLabel", pill.transform, string.Empty,
            20, Color.white, TextAnchor.MiddleCenter);
        UiTheme.Stretch(label.rectTransform, 6f);

        InteractionPrompt prompt = root.AddComponent<InteractionPrompt>();
        prompt.pill = pill;
        prompt.label = label;
        prompt.pillRect = pillRect;
        prompt.canvasRect = root.GetComponent<RectTransform>();
        prompt.canvas = canvas;
        prompt.dockedOffset = offset;
        GameObject keycap = UiTheme.CreateImage("HoldKey", pill.transform, new Color(.06f, .07f, .09f));
        RectTransform keyRect = keycap.GetComponent<RectTransform>();
        keyRect.anchorMin = keyRect.anchorMax = new Vector2(0, .5f);
        keyRect.anchoredPosition = new Vector2(30, 0); keyRect.sizeDelta = new Vector2(44, 44);
        var baseLine = new GameObject("KeyBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(SquareHoldProgressGraphic));
        baseLine.transform.SetParent(keycap.transform, false); UiTheme.Stretch(baseLine.GetComponent<RectTransform>(), 0);
        var baseGraphic = baseLine.GetComponent<SquareHoldProgressGraphic>();
        baseGraphic.color = new Color(.35f, .38f, .43f); baseGraphic.Thickness = 2; baseGraphic.Progress = 1; baseGraphic.raycastTarget = false;
        var progressRoot = new GameObject("HoldProgress", typeof(RectTransform), typeof(CanvasRenderer), typeof(SquareHoldProgressGraphic));
        progressRoot.transform.SetParent(keycap.transform, false); UiTheme.Stretch(progressRoot.GetComponent<RectTransform>(), 0);
        prompt.holdProgress = progressRoot.GetComponent<SquareHoldProgressGraphic>();
        prompt.holdProgress.color = new Color(1, .9f, .6f); prompt.holdProgress.raycastTarget = false;
        prompt.keyLabel = UiTheme.CreateText("KeyLabel", keycap.transform, "E", 24, Color.white);
        UiTheme.Stretch(prompt.keyLabel.rectTransform, 5);
        prompt.keycap = keycap; keycap.SetActive(false);
        pill.SetActive(false);
        return prompt;
    }

    // Each line is shown as its own row. A line written "E : 진입" gets a keycap square with the key and the
    // text beside it; any other line is plain centred text. Rows are stacked top to bottom, centred on the pill.
    public void SetMessage(string message)
    {
        SetHoldMode(false);
        label.gameObject.SetActive(false);
        if (shownMessage == message) return;
        shownMessage = message;

        string[] lines = string.IsNullOrEmpty(message) ? new string[0] : message.Split('\n');
        while (rows.Count < lines.Length) rows.Add(CreateRow());
        float contentWidth = 0f;
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            row.Root.SetActive(i < lines.Length);
            if (i >= lines.Length) continue;
            SplitKey(lines[i], out string key, out string text);
            row.Key.font = row.Text.font = UiTheme.GetFont();
            row.Keycap.SetActive(key != null);
            row.Key.text = key ?? string.Empty;
            row.Text.text = text;
            row.Text.alignment = key != null ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            float textLeft = key != null ? KeySize + KeyTextGap : 0f;
            row.TextRect.anchorMin = new Vector2(0f, 0f); row.TextRect.anchorMax = new Vector2(1f, 1f);
            row.TextRect.offsetMin = new Vector2(textLeft, 0f); row.TextRect.offsetMax = Vector2.zero;
            contentWidth = Mathf.Max(contentWidth, textLeft + row.Text.preferredWidth + 4f);
        }

        float width = Mathf.Clamp(contentWidth + Pad * 2f, 150f, 640f);
        float height = Pad * 2f + Mathf.Max(1, lines.Length) * KeySize + Mathf.Max(0, lines.Length - 1) * RowGap;
        pillRect.sizeDelta = new Vector2(width, height);
        for (int i = 0; i < lines.Length; i++)
        {
            Row row = rows[i];
            row.Rect.anchorMin = row.Rect.anchorMax = new Vector2(.5f, 1f);
            row.Rect.pivot = new Vector2(.5f, 1f);
            row.Rect.sizeDelta = new Vector2(width - Pad * 2f, KeySize);
            row.Rect.anchoredPosition = new Vector2(0f, -(Pad + i * (KeySize + RowGap)));
        }
    }

    // "E : 진입" -> key "E", text "진입". Anything else has no key.
    private static void SplitKey(string line, out string key, out string text)
    {
        key = null; text = line;
        int split = line.IndexOf(" : ", System.StringComparison.Ordinal);
        if (split < 1 || split > 3) return;
        key = line.Substring(0, split).Trim();
        text = line.Substring(split + 3).Trim();
    }

    private Row CreateRow()
    {
        var row = new Row();
        row.Root = new GameObject("PromptRow", typeof(RectTransform));
        row.Root.transform.SetParent(pill.transform, false);
        row.Rect = row.Root.GetComponent<RectTransform>();

        row.Keycap = UiTheme.CreateImage("Key", row.Root.transform, new Color(.06f, .07f, .09f));
        row.KeyRect = row.Keycap.GetComponent<RectTransform>();
        row.KeyRect.anchorMin = row.KeyRect.anchorMax = new Vector2(0f, .5f);
        row.KeyRect.pivot = new Vector2(0f, .5f);
        row.KeyRect.anchoredPosition = Vector2.zero;
        row.KeyRect.sizeDelta = new Vector2(KeySize, KeySize);
        var border = new GameObject("KeyBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(SquareHoldProgressGraphic));
        border.transform.SetParent(row.Keycap.transform, false);
        UiTheme.Stretch(border.GetComponent<RectTransform>(), 0f);
        var graphic = border.GetComponent<SquareHoldProgressGraphic>();
        graphic.color = new Color(.35f, .38f, .43f); graphic.Thickness = 2f; graphic.Progress = 1f; graphic.raycastTarget = false;
        row.Key = UiTheme.CreateText("KeyLabel", row.Keycap.transform, "E", 24, Color.white);
        UiTheme.Stretch(row.Key.rectTransform, 4f);

        row.Text = UiTheme.CreateText("Text", row.Root.transform, string.Empty, 22, Color.white, TextAnchor.MiddleLeft);
        row.Text.horizontalOverflow = HorizontalWrapMode.Overflow;
        row.TextRect = row.Text.rectTransform;
        return row;
    }

    public void SetHoldMessage(string key, string message, float progress)
    {
        pillRect.sizeDelta = new Vector2(340f, 56f);
        shownMessage = null;
        foreach (Row row in rows) row.Root.SetActive(false);
        label.gameObject.SetActive(true);
        SetHoldMode(true);
        keyLabel.text = key; label.text = message; holdProgress.Progress = progress;
    }

    private void SetHoldMode(bool enabled)
    {
        if (keycap == null) return;
        keycap.SetActive(enabled);
        label.rectTransform.offsetMin = new Vector2(enabled ? 62 : 6, 6);
        label.rectTransform.offsetMax = new Vector2(-6, -6);
        if (!enabled) holdProgress.Progress = 0;
    }

    public void SetVisible(bool value)
    {
        if (!value && holdProgress != null) holdProgress.Progress = 0;
        visible = value;
        RefreshPosition();
    }

    public void Follow(Transform target, Vector3 localOffset, bool centered = false)
    {
        worldAnchored = true; followTarget = target; followOffset = localOffset;
        pillRect.anchorMin = pillRect.anchorMax = new Vector2(.5f, .5f);
        pillRect.pivot = new Vector2(.5f, centered ? .5f : 0f);
        RefreshPosition();
    }

    public void Dock()
    {
        worldAnchored = false; followTarget = null;
        pillRect.pivot = new Vector2(.5f, 0f);
        pillRect.anchorMin = pillRect.anchorMax = new Vector2(.5f, 0);
        pillRect.anchoredPosition = dockedOffset;
        RefreshPosition();
    }

    // Run after camera follow, including when input is paused or manually driven.
    private void LateUpdate() => RefreshPosition();

    private void RefreshPosition()
    {
        if (pill == null) return;
        bool show = visible;
        if (show && worldAnchored)
        {
            Camera camera = Camera.main;
            show = followTarget != null && followTarget.gameObject.activeInHierarchy && camera != null;
            if (show)
            {
                Vector3 position = followTarget.TransformPoint(followOffset);
                Vector3 viewport = camera.WorldToViewportPoint(position);
                show = viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1;
                if (show)
                {
                    Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    show = RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                        camera.WorldToScreenPoint(position), uiCamera, out Vector2 local);
                    if (show) pillRect.anchoredPosition = local;
                }
            }
        }
        if (pill.activeSelf != show) pill.SetActive(show);
    }
}
