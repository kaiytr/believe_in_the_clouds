using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CutsceneBubbleView : MonoBehaviour
{
    private static Sprite bubbleSprite;
    private static Font koreanFont;

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(640f, 300f);
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 18f);
    [SerializeField, Min(12)] private int bodyFontSize = 29;
    [SerializeField, Min(12)] private int speakerFontSize = 21;
    [SerializeField, Min(12)] private int choiceFontSize = 23;

    private Canvas canvas;
    private RectTransform canvasRect;
    private RectTransform panelRect;
    private Text speakerText;
    private Text bodyText;
    private RectTransform choicesRoot;
    private CutsceneCharacter speaker;
    private Camera trackingCamera;
    private readonly List<GameObject> choices = new List<GameObject>(4);
    private Action<int> choiceSelected;

    public bool IsVisible => panelRect != null && panelRect.gameObject.activeSelf;
    public RectTransform BubbleRect => panelRect;

    private void Awake() => BuildUi();

    public void ShowLine(CutsceneCharacter currentSpeaker, string text)
    {
        EnsureUi();
        ClearChoices();
        speaker = currentSpeaker;
        choiceSelected = null;
        speakerText.text = currentSpeaker != null ? currentSpeaker.DisplayName : string.Empty;
        bodyText.text = text ?? string.Empty;
        bodyText.rectTransform.sizeDelta = new Vector2(-70f, 204f);
        choicesRoot.gameObject.SetActive(false);
        panelRect.gameObject.SetActive(true);
        UpdatePosition();
    }

    public void SetVisibleText(string text)
    {
        EnsureUi();
        bodyText.text = text ?? string.Empty;
    }

    public void ShowChoices(CutsceneCharacter currentSpeaker, string[] options, Action<int> onSelected)
    {
        EnsureUi();
        ClearChoices();
        speaker = currentSpeaker;
        speakerText.text = currentSpeaker != null ? currentSpeaker.DisplayName : string.Empty;
        bodyText.text = string.Empty;
        bodyText.rectTransform.sizeDelta = new Vector2(-70f, 55f);
        choiceSelected = onSelected;
        choicesRoot.gameObject.SetActive(true);
        panelRect.gameObject.SetActive(true);

        int count = options != null ? options.Length : 0;
        for (int i = 0; i < count; i++)
            CreateChoice(i, options[i] ?? string.Empty);

        UpdatePosition();
    }

    public void HideChoices()
    {
        ClearChoices();
        if (choicesRoot != null)
            choicesRoot.gameObject.SetActive(false);
    }

    public void Hide()
    {
        choiceSelected = null;
        ClearChoices();
        speaker = null;
        if (panelRect != null)
            panelRect.gameObject.SetActive(false);
    }

    private void EnsureUi()
    {
        if (panelRect == null)
            BuildUi();
    }

    private void BuildUi()
    {
        if (panelRect != null)
            return;

        UiEventSystemBoot.Ensure();
        GameObject canvasObject = new GameObject("[Cutscene Bubble Canvas]",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 200;
        canvasRect = canvasObject.GetComponent<RectTransform>();

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject("Speech Bubble", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;

        Image background = panel.GetComponent<Image>();
        background.sprite = GetBubbleSprite();
        background.type = Image.Type.Sliced;
        background.color = Color.white;
        background.raycastTarget = false;

        speakerText = CreateText("Speaker", panelRect, speakerFontSize, FontStyle.Bold);
        speakerText.alignment = TextAnchor.MiddleLeft;
        RectTransform speakerRect = speakerText.rectTransform;
        speakerRect.anchorMin = new Vector2(0.1f, 1f);
        speakerRect.anchorMax = new Vector2(0.9f, 1f);
        speakerRect.pivot = new Vector2(0.5f, 1f);
        speakerRect.anchoredPosition = new Vector2(0f, -30f);
        speakerRect.sizeDelta = new Vector2(-64f, 32f);
        speakerText.color = new Color32(93, 59, 42, 255);

        bodyText = CreateText("Dialogue", panelRect, bodyFontSize, FontStyle.Normal);
        bodyText.alignment = TextAnchor.UpperLeft;
        RectTransform bodyRect = bodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0.1f, 1f);
        bodyRect.anchorMax = new Vector2(0.9f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.anchoredPosition = new Vector2(0f, -72f);
        bodyRect.sizeDelta = new Vector2(-70f, 204f);
        bodyText.color = new Color32(47, 42, 37, 255);
        bodyText.lineSpacing = 1.1f;

        GameObject choicesObject = new GameObject("Choices", typeof(RectTransform));
        choicesObject.transform.SetParent(panelRect, false);
        choicesRoot = choicesObject.GetComponent<RectTransform>();
        choicesRoot.anchorMin = new Vector2(0.12f, 0f);
        choicesRoot.anchorMax = new Vector2(0.88f, 0f);
        choicesRoot.pivot = new Vector2(0.5f, 0f);
        choicesRoot.anchoredPosition = new Vector2(0f, 30f);
        choicesRoot.sizeDelta = new Vector2(0f, 118f);
        choicesRoot.gameObject.SetActive(false);

        panelRect.gameObject.SetActive(false);
        trackingCamera = Camera.main;
    }

    private Text CreateText(string childName, Transform parent, int fontSize, FontStyle style)
    {
        GameObject child = new GameObject(childName, typeof(RectTransform), typeof(Text));
        child.transform.SetParent(parent, false);
        Text text = child.GetComponent<Text>();
        text.font = GetKoreanFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;
        text.raycastTarget = false;
        return text;
    }

    private void CreateChoice(int index, string label)
    {
        GameObject buttonObject = new GameObject("Choice " + (index + 1),
            typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(choicesRoot, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(0.5f, 1f);
        buttonRect.anchoredPosition = new Vector2(0f, -index * 48f);
        buttonRect.sizeDelta = new Vector2(-8f, 42f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color32(239, 226, 204, 255);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color32(239, 226, 204, 255);
        colors.highlightedColor = new Color32(255, 243, 218, 255);
        colors.pressedColor = new Color32(222, 200, 168, 255);
        button.colors = colors;

        Text labelText = CreateText("Label", buttonRect, choiceFontSize, FontStyle.Normal);
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.color = new Color32(47, 42, 37, 255);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(15f, 2f);
        labelRect.offsetMax = new Vector2(-10f, -2f);
        labelText.text = label;

        int capturedIndex = index;
        button.onClick.AddListener(() => choiceSelected?.Invoke(capturedIndex));
        choices.Add(buttonObject);
    }

    private void ClearChoices()
    {
        choiceSelected = null;
        for (int i = 0; i < choices.Count; i++)
        {
            if (choices[i] != null)
                Destroy(choices[i]);
        }
        choices.Clear();
    }

    private void LateUpdate()
    {
        if (panelRect != null && panelRect.gameObject.activeSelf)
            UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (panelRect == null || speaker == null)
            return;

        if (trackingCamera == null)
            trackingCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();

        if (trackingCamera == null)
            return;

        Vector3 screen = trackingCamera.WorldToScreenPoint(speaker.HeadWorldPosition);
        if (screen.z <= 0f)
        {
            panelRect.gameObject.SetActive(false);
            return;
        }

        panelRect.gameObject.SetActive(true);
        Rect safe = Screen.safeArea;
        float scale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
        float halfWidth = panelSize.x * scale * 0.5f;
        float halfHeight = panelSize.y * scale * 0.5f;
        float minX = safe.xMin + halfWidth + 12f;
        float maxX = safe.xMax - halfWidth - 12f;
        float minY = safe.yMin + halfHeight + 12f;
        float maxY = safe.yMax - halfHeight - 12f;

        screen.x = minX > maxX ? safe.center.x : Mathf.Clamp(screen.x, minX, maxX);
        screen.y = minY > maxY ? safe.center.y : Mathf.Clamp(screen.y + screenOffset.y, minY, maxY);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screen, null, out Vector2 local))
            panelRect.anchoredPosition = local + new Vector2(screenOffset.x, 0f);
    }

    private static Font GetKoreanFont()
    {
        if (koreanFont != null)
            return koreanFont;

        koreanFont = CombatUiResources.KoreanFont();
        if (koreanFont == null)
            koreanFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return koreanFont;
    }

    private static Sprite GetBubbleSprite()
    {
        if (bubbleSprite != null)
            return bubbleSprite;

        const int width = 256;
        const int height = 192;
        Color32[] pixels = new Color32[width * height];
        Color32 outline = new Color32(91, 65, 49, 255);
        Color32 fill = new Color32(255, 249, 233, 255);

        Rect outer = Rect.MinMaxRect(3f, 35f, 253f, 190f);
        Rect inner = Rect.MinMaxRect(7f, 39f, 249f, 186f);
        Vector2 oa = new Vector2(96f, 38f);
        Vector2 ob = new Vector2(160f, 38f);
        Vector2 oc = new Vector2(128f, 4f);
        Vector2 ia = new Vector2(103f, 40f);
        Vector2 ib = new Vector2(153f, 40f);
        Vector2 ic = new Vector2(128f, 12f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                bool isOuter = InRoundedRect(p, outer, 28f) || InTriangle(p, oa, ob, oc);
                if (!isOuter)
                    continue;

                bool isInner = InRoundedRect(p, inner, 24f) || InTriangle(p, ia, ib, ic);
                pixels[y * width + x] = isInner ? fill : outline;
            }
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            name = "Cutscene Bubble",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        bubbleSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(28f, 28f, 42f, 42f));
        bubbleSprite.name = "Cutscene Bubble";
        bubbleSprite.hideFlags = HideFlags.DontSave;
        return bubbleSprite;
    }

    private static bool InRoundedRect(Vector2 point, Rect rect, float radius)
    {
        float x = Mathf.Clamp(point.x, rect.xMin + radius, rect.xMax - radius);
        float y = Mathf.Clamp(point.y, rect.yMin + radius, rect.yMax - radius);
        return (point - new Vector2(x, y)).sqrMagnitude <= radius * radius;
    }

    private static bool InTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float denominator = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
        float first = ((b.y - c.y) * (point.x - c.x) + (c.x - b.x) * (point.y - c.y)) / denominator;
        float second = ((c.y - a.y) * (point.x - c.x) + (a.x - c.x) * (point.y - c.y)) / denominator;
        float third = 1f - first - second;
        return first >= 0f && second >= 0f && third >= 0f;
    }

    private void OnDestroy()
    {
        Hide();
        if (canvas != null)
            Destroy(canvas.gameObject);
    }
}
