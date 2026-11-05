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
        SetDialogueTextArea();
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
        bodyText.text = string.Empty;
        SetChoicesArea();
        choiceSelected = onSelected;
        choicesRoot.gameObject.SetActive(true);
        panelRect.gameObject.SetActive(true);
        ApplyDisplayScale();

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
        buttonRect.anchoredPosition = new Vector2(0f, -index * 42f * unitScale);
        buttonRect.sizeDelta = new Vector2(-4f * unitScale, 40f * unitScale);

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

    private void SetDialogueTextArea()
    {
        RectTransform rect = bodyText.rectTransform;
        rect.anchorMin = new Vector2(0.08f, 0.24f);
        rect.anchorMax = new Vector2(0.92f, 0.84f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void SetChoicesArea()
    {
        RectTransform rect = choicesRoot;
        rect.anchorMin = new Vector2(0.1f, 0.15f);
        rect.anchorMax = new Vector2(0.9f, 0.85f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
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
        panelRect.sizeDelta = panelSize * unitScale;
        bodyText.fontSize = Mathf.Max(1, Mathf.RoundToInt(bodyFontSize * unitScale));
        bodyText.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt((bodyFontSize - 6) * unitScale));
        bodyText.resizeTextMaxSize = bodyText.fontSize;

        for (int i = 0; i < choices.Count; i++)
        {
            GameObject choice = choices[i];
            if (choice == null) continue;
            RectTransform buttonRect = choice.GetComponent<RectTransform>();
            buttonRect.anchoredPosition = new Vector2(0f, -i * 42f * unitScale);
            buttonRect.sizeDelta = new Vector2(-4f * unitScale, 40f * unitScale);
            Text label = choice.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = Mathf.Max(1, Mathf.RoundToInt(choiceFontSize * unitScale));
                label.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt((choiceFontSize - 2) * unitScale));
                label.resizeTextMaxSize = label.fontSize;
                label.rectTransform.offsetMin = new Vector2(5f * unitScale, 1f * unitScale);
                label.rectTransform.offsetMax = new Vector2(-5f * unitScale, -1f * unitScale);
            }
        }
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
