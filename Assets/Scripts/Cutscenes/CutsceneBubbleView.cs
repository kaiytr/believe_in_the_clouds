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
    [SerializeField] private Vector2 panelSize = new Vector2(318f, 120f);
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 18f);
    [SerializeField, Min(12)] private int bodyFontSize = 26;
    [SerializeField, Min(12)] private int choiceFontSize = 22;
    [Header("Auto size (design units)")]
    [SerializeField] private Vector2 minPanelSize = new Vector2(300f, 124f);
    [SerializeField] private float maxPanelWidth = 560f;
    [SerializeField] private float maxPanelHeight = 520f;
    [SerializeField] private Vector4 panelPadding = new Vector4(44f, 58f, 44f, 36f); // left, bottom (tail), right, top
    [SerializeField] private float choiceHeight = 52f;
    [SerializeField] private float choiceSpacing = 8f;
    [SerializeField] private float choiceTextPadding = 20f;

    private Canvas canvas;
    private RectTransform canvasRect;
    private RectTransform panelRect;
    private Text bodyText;
    private RectTransform choicesRoot;
    private CutsceneCharacter speaker;
    private Camera trackingCamera;
    private float displayScale = 1f;
    private readonly List<GameObject> choices = new List<GameObject>(4);
    private Action<int> choiceSelected;
    private string lineFull = string.Empty;
    private string[] choiceLabels = new string[0];
    private bool choiceMode, layoutDirty = true;
    private float laidOutScale = -1f;

    public bool IsVisible => panelRect != null && panelRect.gameObject.activeSelf;
    public RectTransform BubbleRect => panelRect;

    private void Awake() => BuildUi();

    public void ShowLine(CutsceneCharacter currentSpeaker, string text)
    {
        EnsureUi();
        ClearChoices();
        speaker = currentSpeaker;
        choiceSelected = null;
        bodyText.text = text ?? string.Empty;
        lineFull = text ?? string.Empty;
        choiceMode = false;
        layoutDirty = true;
        choicesRoot.gameObject.SetActive(false);
        panelRect.gameObject.SetActive(true);
        ApplyDisplayScale();
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
        bodyText.text = string.Empty;
        choiceSelected = onSelected;
        choicesRoot.gameObject.SetActive(true);
        panelRect.gameObject.SetActive(true);

        int count = options != null ? options.Length : 0;
        choiceLabels = new string[count];
        for (int i = 0; i < count; i++)
        {
            choiceLabels[i] = options[i] ?? string.Empty;
            CreateChoice(i, choiceLabels[i]);
        }
        choiceMode = true;
        layoutDirty = true;
        ApplyDisplayScale();
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
        canvas.pixelPerfect = true;
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
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.sizeDelta = panelSize;

        Image background = panel.GetComponent<Image>();
        background.sprite = GetBubbleSprite();
        background.type = Image.Type.Sliced;
        background.color = Color.white;
        background.raycastTarget = false;

        bodyText = CreateText("Dialogue", panelRect, bodyFontSize, FontStyle.Normal);
        bodyText.alignment = TextAnchor.MiddleCenter;
        bodyText.color = new Color32(40, 40, 48, 255);
        bodyText.lineSpacing = 1f;

        GameObject choicesObject = new GameObject("Choices", typeof(RectTransform));
        choicesObject.transform.SetParent(panelRect, false);
        choicesRoot = choicesObject.GetComponent<RectTransform>();
        choicesRoot.anchorMin = new Vector2(0.1f, 0.15f);
        choicesRoot.anchorMax = new Vector2(0.9f, 0.85f);
        choicesRoot.pivot = new Vector2(0.5f, 0.5f);
        choicesRoot.anchoredPosition = Vector2.zero;
        choicesRoot.sizeDelta = Vector2.zero;
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
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(18, fontSize - 6);
        text.resizeTextMaxSize = fontSize;
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
        float unitScale = displayScale / Mathf.Max(.01f, canvas.scaleFactor);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = Color.white;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color32(225, 226, 232, 255);
        colors.highlightedColor = new Color32(245, 246, 255, 255);
        colors.pressedColor = new Color32(190, 194, 208, 255);
        button.colors = colors;

        Text labelText = CreateText("Label", buttonRect, choiceFontSize, FontStyle.Normal);
        labelText.fontSize = Mathf.Max(1, Mathf.RoundToInt(choiceFontSize * unitScale));
        labelText.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt((choiceFontSize - 2) * unitScale));
        labelText.resizeTextMaxSize = labelText.fontSize;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.color = new Color32(40, 40, 48, 255);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(5f * unitScale, 1f * unitScale);
        labelRect.offsetMax = new Vector2(-5f * unitScale, -1f * unitScale);
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

        ApplyDisplayScale();

        Vector3 screen = trackingCamera.WorldToScreenPoint(speaker.HeadWorldPosition);
        if (screen.z <= 0f)
        {
            panelRect.gameObject.SetActive(false);
            return;
        }

        panelRect.gameObject.SetActive(true);
        Rect safe = Screen.safeArea;
        float scale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
        float halfWidth = panelRect.sizeDelta.x * scale * 0.5f;
        float minX = safe.xMin + halfWidth + 12f;
        float maxX = safe.xMax - halfWidth - 12f;
        float minY = safe.yMin + 12f;
        float maxY = safe.yMax - panelRect.sizeDelta.y * scale - 12f;

        screen.x = minX > maxX ? safe.center.x : Mathf.Clamp(screen.x, minX, maxX);
        screen.y = minY > maxY ? safe.yMin + 12f : Mathf.Clamp(screen.y + screenOffset.y * scale, minY, maxY);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screen, null, out Vector2 local))
            panelRect.anchoredPosition = local + new Vector2(screenOffset.x, 0f);
    }

    private void ApplyDisplayScale()
    {
        float canvasScale = canvas != null ? Mathf.Max(.01f, canvas.scaleFactor) : 1f;
        float screenHeight = trackingCamera != null && trackingCamera.pixelHeight > 0
            ? trackingCamera.pixelHeight
            : Screen.height;
        displayScale = trackingCamera != null && trackingCamera.orthographic
            ? screenHeight / (2f * trackingCamera.orthographicSize) * CombatantHudView.ReferenceCanvasScale
            : 1f;
        float unitScale = displayScale / canvasScale;
        // Size depends on the full text / option list and the display scale only, never on typed-so-far characters.
        if (!layoutDirty && Mathf.Approximately(unitScale, laidOutScale)) return;
        layoutDirty = false;
        laidOutScale = unitScale;
        if (choiceMode) LayoutChoices(unitScale);
        else LayoutDialogue(unitScale);
    }

    // The bubble grows or shrinks around its content: width follows the longest line up to a maximum,
    // then text wraps and the height follows the wrapped text. All numbers are design units scaled by unitScale.
    private void LayoutDialogue(float unitScale)
    {
        int fontSize = Mathf.Max(1, Mathf.RoundToInt(bodyFontSize * unitScale));
        bodyText.fontSize = fontSize;
        bodyText.resizeTextMaxSize = fontSize;
        bodyText.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt((bodyFontSize - 6) * unitScale));
        float horizontal = (panelPadding.x + panelPadding.z) * unitScale;
        float vertical = (panelPadding.y + panelPadding.w) * unitScale;
        float maxTextWidth = maxPanelWidth * unitScale - horizontal;
        bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        float wanted = MeasureWidth(bodyText, lineFull, fontSize) + 44f * unitScale;
        float textWidth = Mathf.Clamp(wanted, Mathf.Max(0f, minPanelSize.x * unitScale - horizontal), maxTextWidth);
        // Measure against a narrower column and add slack: a real wrap that differs slightly must never clip a line.
        float textHeight = MeasureHeight(bodyText, lineFull, textWidth * .85f, fontSize) + 28f * unitScale;
        float panelWidth = textWidth + horizontal;
        float panelHeight = Mathf.Clamp(textHeight + vertical, minPanelSize.y * unitScale, maxPanelHeight * unitScale);
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        Stretch(bodyText.rectTransform, panelPadding, unitScale);
    }

    private void LayoutChoices(float unitScale)
    {
        int fontSize = Mathf.Max(1, Mathf.RoundToInt(choiceFontSize * unitScale));
        float horizontal = (panelPadding.x + panelPadding.z) * unitScale;
        float vertical = (panelPadding.y + panelPadding.w) * unitScale;
        float inset = choiceTextPadding * unitScale;
        float maxButtonWidth = maxPanelWidth * unitScale - horizontal;
        float minButtonWidth = Mathf.Max(0f, minPanelSize.x * unitScale - horizontal);

        // One shared width so the stack reads as a list; it fits the longest option.
        Text sample = choices.Count > 0 && choices[0] != null ? choices[0].GetComponentInChildren<Text>() : bodyText;
        float buttonWidth = minButtonWidth;
        for (int i = 0; i < choiceLabels.Length; i++)
            buttonWidth = Mathf.Max(buttonWidth, MeasureWidth(sample, choiceLabels[i], fontSize) + inset * 2f + 44f * unitScale);
        buttonWidth = Mathf.Min(buttonWidth, maxButtonWidth);

        float y = 0f;
        float spacing = choiceSpacing * unitScale;
        for (int i = 0; i < choices.Count && i < choiceLabels.Length; i++)
        {
            GameObject choice = choices[i];
            if (choice == null) continue;
            Text label = choice.GetComponentInChildren<Text>();
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.fontSize = fontSize;
            label.resizeTextMaxSize = fontSize;
            label.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt((choiceFontSize - 2) * unitScale));
            float textHeight = MeasureHeight(label, choiceLabels[i], (buttonWidth - inset * 2f) * .85f, fontSize) + 22f * unitScale;
            float height = Mathf.Max(choiceHeight * unitScale, textHeight);
            RectTransform buttonRect = choice.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.anchoredPosition = new Vector2(0f, -y);
            buttonRect.sizeDelta = new Vector2(0f, height);
            label.rectTransform.offsetMin = new Vector2(inset, unitScale);
            label.rectTransform.offsetMax = new Vector2(-inset, -unitScale);
            y += height + spacing;
        }
        float stackHeight = Mathf.Max(0f, y - spacing);
        float panelHeight = Mathf.Clamp(stackHeight + vertical, minPanelSize.y * unitScale, float.MaxValue);
        panelRect.sizeDelta = new Vector2(buttonWidth + horizontal, panelHeight);
        Stretch(choicesRoot, panelPadding, unitScale);
    }

    private static void Stretch(RectTransform rect, Vector4 padding, float unitScale)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(padding.x * unitScale, padding.y * unitScale);
        rect.offsetMax = new Vector2(-padding.z * unitScale, -padding.w * unitScale);
    }

    private static float MeasureWidth(Text text, string content, int fontSize)
    {
        if (string.IsNullOrEmpty(content)) return 0f;
        TextGenerationSettings settings = text.GetGenerationSettings(Vector2.zero);
        settings.fontSize = fontSize;
        settings.resizeTextForBestFit = false;
        settings.horizontalOverflow = HorizontalWrapMode.Overflow;
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        return text.cachedTextGeneratorForLayout.GetPreferredWidth(content, settings) / text.pixelsPerUnit;
    }

    private static float MeasureHeight(Text text, string content, float width, int fontSize)
    {
        if (string.IsNullOrEmpty(content)) return 0f;
        TextGenerationSettings settings = text.GetGenerationSettings(new Vector2(Mathf.Max(1f, width), 0f));
        settings.fontSize = fontSize;
        settings.resizeTextForBestFit = false;
        settings.horizontalOverflow = HorizontalWrapMode.Wrap;
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        return text.cachedTextGeneratorForLayout.GetPreferredHeight(content, settings) / text.pixelsPerUnit;
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
        if (bubbleSprite != null) return bubbleSprite;
        HudArtworkSprite artwork = HudArtworkResources.Load("EnemyBeliefSpeechBubble");
        if (!artwork.IsValid) return null;

        // 기존 HUD 원본 텍스처/영역을 그대로 사용하고 테두리·꼬리만 분할 보존한다.
        Sprite source = artwork.Sprite;
        bubbleSprite = Sprite.Create(source.texture, source.rect, new Vector2(.5f, .5f),
            source.pixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(12f, 24f, 12f, 12f));
        bubbleSprite.name = "EnemyBeliefSpeechBubble (Cutscene)";
        bubbleSprite.hideFlags = HideFlags.DontSave;
        return bubbleSprite;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetArtwork()
    {
        if (bubbleSprite != null)
        {
            if (Application.isPlaying) Destroy(bubbleSprite);
            else DestroyImmediate(bubbleSprite);
        }
        bubbleSprite = null;
        koreanFont = null;
    }
    private void OnDestroy()
    {
        Hide();
        if (canvas != null)
            Destroy(canvas.gameObject);
    }
}
