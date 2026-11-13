using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ESC 설정 창.
//
// - 1920x1080 기준으로 그려진 Assets/Design/Settings 아트를 런타임 UI로 재구성한다.
// - 화면 모드: 전체 모드(FullScreenWindow) / 창 모드(Windowed) 2종을 실제로 전환한다.
// - 사운드 볼륨: 슬라이더 값이 AudioListener.volume에 즉시 반영된다.
// - 볼륨은 다음 실행에도 유지되며, PC 빌드는 항상 전체 모드로 시작한다.
// - ESC로 열고, 열린 상태에서는 ESC(또는 E)로 닫는다.
public class SettingsScreen : MonoBehaviour
{
    public static SettingsScreen Instance { get; private set; }

    private const string VolumeKey = "settings.volume";
    private const string FullscreenKey = "settings.fullscreen";

    private const int FontSize = 34;
    private static readonly Color TextColor = new Color(0.30f, 0.19f, 0.14f, 1f);
    private static readonly Color BlockerColor = new Color(0f, 0f, 0f, 0.45f);

    // 설정_BG 기준 패널 크기/좌표 (1920x1080, Unity bottom-left)
    private static readonly RectInt BackgroundRect = new RectInt(474, 250, 973, 581);
    private static readonly RectInt GaugeRect = new RectInt(563, 414, 685, 49);
    private static readonly RectInt HandleRect = new RectInt(1116, 406, 107, 67);
    private static readonly RectInt LeftArrowRect = new RectInt(1083, 584, 23, 26);
    private static readonly RectInt RightArrowRect = new RectInt(1349, 584, 23, 26);

    private const float PanelWidth = 973f;
    private const float PanelHeight = 581f;
    // 사운드 바만 살짝 짧게. 볼륨 숫자 위치는 그대로 둔다.
    private const float SliderWidth = 640f;
    private const float GaugeHeight = 49f;
    private const float HandleWidth = 107f;
    private const float HandleHeight = 67f;

    private GameObject canvasObject;
    private Slider volumeSlider;
    private Text volumePercent;
    private Text modeValue;

    private bool isOpen;
    private bool isFullscreen;
    private int openedFrame;
    private float resumedTimeScale = 1f;
    private bool pausedBySettings;

    private readonly List<Sprite> generatedSprites = new List<Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBoot()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject go = new GameObject("[SettingsScreen]");
        DontDestroyOnLoad(go);
        go.AddComponent<SettingsScreen>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildUi();
        LoadPreferences();
    }

    private void OnEnable()
    {
        UiModal.CloseRequested += HandleCloseRequested;
    }

    private void OnDisable()
    {
        UiModal.CloseRequested -= HandleCloseRequested;
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        Instance = null;
        if (isOpen)
        {
            isOpen = false;
            UiModal.Pop(this);
        }
        for (int i = 0; i < generatedSprites.Count; i++)
        {
            if (generatedSprites[i] != null)
            {
                Destroy(generatedSprites[i]);
            }
        }
        generatedSprites.Clear();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

        if (isOpen)
        {
            Close();
        }
        else if (!UiModal.IsOpen)
        {
            Open();
        }
    }

    /// <summary>
    /// ESC 설정이 게임을 멈췄는지. 다른 UI(상점/TIP 등)가 열려 있거나
    /// 컷씬·사망 연출 중에는 ESC를 눌러도 설정이 열리지 않는다.
    /// </summary>
    public bool IsPausedBySettings => pausedBySettings;

    public void Open()
    {
        if (isOpen)
        {
            return;
        }

        if (canvasObject == null)
        {
            BuildUi();
        }

        EnsureVolumeSlider();
        RefreshModeState();

        isOpen = true;
        openedFrame = Time.frameCount;
        canvasObject.SetActive(true);
        UiEventSystemBoot.Ensure();
        UiModal.Push(this);

        // ESC 설정이 열리면 게임을 멈춘다. 이미 멈춰 있으면(사망 연출 등)
        // 그 상태를 건드리지 않고, 닫을 때도 복원하지 않는다.
        pausedBySettings = false;
        if (Time.timeScale > 0f)
        {
            // 짧은 히트스톱 배율을 일시정지 해제 후의 게임 속도로 저장하지 않는다.
            FindFirstObjectByType<PlayerAttack>()?.EndHitStop();
            resumedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            pausedBySettings = true;
        }
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        if (canvasObject != null)
        {
            canvasObject.SetActive(false);
        }
        UiModal.Pop(this);
        if (pausedBySettings)
        {
            pausedBySettings = false;
            // 닫는 동안 다른 연출이 멈췄으면(사망 등) 덮어쓰지 않는다.
            if (Time.timeScale <= 0f)
                Time.timeScale = Mathf.Max(0.01f, resumedTimeScale);
        }
        PlayerPrefs.Save();
    }

    private void HandleCloseRequested()
    {
        if (!isOpen)
        {
            return;
        }

        // 설정을 연 ESC 입력이 같은 프레임의 상호작용 컨트롤러에 의해
        // 바로 닫기 요청으로 처리되지 않도록 한 프레임 무시한다.
        if (Time.frameCount == openedFrame)
        {
            return;
        }

        Close();
    }

    private void LoadPreferences()
    {
        float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        EnsureVolumeSlider();
        volumeSlider.SetValueWithoutNotify(volume);
        ApplyVolume(volume, false);

#if UNITY_STANDALONE && !UNITY_EDITOR
        // 이전 실행에서 창 모드를 선택했어도 시작 화면은 전체 모드로 표시한다.
        ApplyScreenMode(true);
        PlayerPrefs.SetInt(FullscreenKey, 1);
#else
        int savedFullscreen = PlayerPrefs.GetInt(FullscreenKey, -1);
        if (savedFullscreen >= 0)
        {
            ApplyScreenMode(savedFullscreen == 1);
        }
        else
        {
            isFullscreen = Screen.fullScreenMode != FullScreenMode.Windowed;
        }

#endif

        RefreshModeLabel();
    }

    private void RefreshModeState()
    {
        // 설정 창을 다시 열 때 외부(알트탭 등)에서 바뀐 상태도 반영한다.
        if (PlayerPrefs.HasKey(FullscreenKey))
        {
            isFullscreen = PlayerPrefs.GetInt(FullscreenKey, isFullscreen ? 1 : 0) == 1;
        }
        RefreshModeLabel();
    }

    private void RefreshModeLabel()
    {
        if (modeValue != null)
        {
            modeValue.text = isFullscreen ? "전체 모드" : "창 모드";
        }
    }

    private void ToggleScreenMode()
    {
        ApplyScreenMode(!isFullscreen);
        RefreshModeLabel();
        PlayerPrefs.SetInt(FullscreenKey, isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplyScreenMode(bool fullscreen)
    {
        isFullscreen = fullscreen;

        int displayWidth = Mathf.Max(640, Display.main.systemWidth);
        int displayHeight = Mathf.Max(480, Display.main.systemHeight);

        if (fullscreen)
        {
            Screen.SetResolution(displayWidth, displayHeight, FullScreenMode.FullScreenWindow);
        }
        else
        {
            // 창 모드에서는 화면(제목 표시줄 포함)에 들어오는 가장 큰 16:9 창으로 띄운다. 해상도 비율이 다른 화면(16:10 등)에서
            // 창이 화면 밖으로 잘리지 않도록 가로 폭과 세로 높이 양쪽 한도에 맞춘다.
            int width = Mathf.Min(1600, Mathf.FloorToInt(displayWidth * .92f));
            int height = Mathf.RoundToInt(width * 9f / 16f);
            int maxHeight = Mathf.FloorToInt(displayHeight * .88f);
            if (height > maxHeight)
            {
                height = maxHeight;
                width = Mathf.RoundToInt(height * 16f / 9f);
            }
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
        }
    }

    private void EnsureVolumeSlider()
    {
        if (volumeSlider == null)
        {
            BuildUi();
        }
    }

    private void HandleVolumeChanged(float value)
    {
        ApplyVolume(value, true);
    }

    private void ApplyVolume(float value, bool save)
    {
        value = Mathf.Clamp01(value);
        AudioListener.volume = value;

        if (volumePercent != null)
        {
            volumePercent.text = Mathf.RoundToInt(value * 100f) + "%";
        }

        if (save)
        {
            // 드래그 중 매 프레임 디스크에 쓰지 않도록 값만 기록하고,
            // 실제 저장은 창을 닫을 때 PlayerPrefs.Save()로 처리한다.
            PlayerPrefs.SetFloat(VolumeKey, value);
        }
    }

    private void BuildUi()
    {
        if (canvasObject != null)
        {
            return;
        }

        canvasObject = new GameObject("[SettingsCanvas]");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        // 뒤쪽 월드/UI 클릭 차단
        Image blocker = UiTheme.CreateImage("Blocker", canvasObject.transform, BlockerColor)
            .GetComponent<Image>();
        UiTheme.Stretch(blocker.rectTransform);
        blocker.raycastTarget = true;

        // 설정 패널
        GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panel.anchoredPosition = Vector2.zero;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.sprite = LoadArt("setting_BG", BackgroundRect);
        panelImage.type = Image.Type.Simple;
        panelImage.color = Color.white;
        panelImage.raycastTarget = true;

        RectTransform content = new GameObject("Content", typeof(RectTransform))
            .GetComponent<RectTransform>();
        content.SetParent(panel, false);
        UiTheme.Stretch(content);

        // 화면 모드
        CreateText(content, "ModeLabel", "화면 모드", new Vector2(89f, 232f),
            new Vector2(300f, 60f), new Vector2(0f, 0.5f), TextAnchor.MiddleLeft);

        modeValue = CreateText(content, "ModeValue", "전체 모드", new Vector2(752f, 233f),
            new Vector2(360f, 60f), new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter);

        BuildArrowButton(content, "ModePrev", new Vector2(620.5f, 234f), LeftArrowRect,
            ToggleScreenMode);
        BuildArrowButton(content, "ModeNext", new Vector2(886.5f, 234f), RightArrowRect,
            ToggleScreenMode);

        // 사운드 볼륨
        CreateText(content, "VolumeLabel", "사운드 볼륨", new Vector2(89f, 316f),
            new Vector2(300f, 60f), new Vector2(0f, 0.5f), TextAnchor.MiddleLeft);

        BuildVolumeSlider(content);

        volumePercent = CreateText(content, "VolumePercent", "100%", new Vector2(819f, 392f),
            new Vector2(120f, 60f), new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter);

        // 시작 시에는 닫힌 상태로 둔다.
        canvasObject.SetActive(false);
    }

    private Text CreateText(Transform parent, string name, string content,
        Vector2 anchorPoint, Vector2 size, Vector2 pivot, TextAnchor alignment)
    {
        Text text = UiTheme.CreateText(name, parent, content, FontSize, TextColor, alignment);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = pivot;
        rect.anchoredPosition = new Vector2(anchorPoint.x, -anchorPoint.y);
        rect.sizeDelta = size;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private void BuildArrowButton(Transform parent, string name, Vector2 center,
        RectInt iconRect, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(56f, 56f);
        rect.anchoredPosition = new Vector2(center.x, -center.y);

        Image hitArea = buttonObject.GetComponent<Image>();
        hitArea.sprite = UiTheme.WhiteSprite;
        hitArea.color = new Color(1f, 1f, 1f, 0f);
        hitArea.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = hitArea;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(onClick);

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(buttonObject.transform, false);
        RectTransform icon = iconObject.GetComponent<RectTransform>();
        icon.anchorMin = new Vector2(0.5f, 0.5f);
        icon.anchorMax = new Vector2(0.5f, 0.5f);
        icon.pivot = new Vector2(0.5f, 0.5f);
        icon.anchoredPosition = Vector2.zero;
        icon.sizeDelta = new Vector2(iconRect.width, iconRect.height);

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = LoadArt("setting_mode_button", iconRect);
        iconImage.raycastTarget = false;
    }

    private void BuildVolumeSlider(Transform parent)
    {
        GameObject sliderObject = new GameObject("VolumeSlider", typeof(RectTransform));
        sliderObject.transform.SetParent(parent, false);

        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 1f);
        sliderRect.anchorMax = new Vector2(0f, 1f);
        sliderRect.pivot = new Vector2(0f, 1f);
        sliderRect.anchoredPosition = new Vector2(90f, -368f);
        sliderRect.sizeDelta = new Vector2(SliderWidth, GaugeHeight);

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.transition = Selectable.Transition.None;

        // 트랙 배경
        Image background = CreateImageChild("Background", sliderRect, GaugeRect);
        background.raycastTarget = true;

        // 채움 (Slider가 fillAmount를 직접 갱신한다)
        RectTransform fillArea = new GameObject("Fill Area", typeof(RectTransform))
            .GetComponent<RectTransform>();
        fillArea.SetParent(sliderRect, false);
        UiTheme.Stretch(fillArea);

        Image fill = CreateImageChild("Fill", fillArea, GaugeRect);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        fill.raycastTarget = false;

        // 테두리
        Image outline = CreateImageChild("Outline", sliderRect, GaugeRect);
        outline.raycastTarget = false;

        // 핸들
        RectTransform handleArea = new GameObject("Handle Slide Area", typeof(RectTransform))
            .GetComponent<RectTransform>();
        handleArea.SetParent(sliderRect, false);
        UiTheme.Stretch(handleArea);
        handleArea.offsetMin = new Vector2(HandleWidth * 0.5f, 0f);
        handleArea.offsetMax = new Vector2(-HandleWidth * 0.5f, 0f);

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(handleArea, false);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        // Slider가 y 앵커를 0~1로 늘리므로, 높이가 HandleHeight가 되도록
        // sizeDelta.y = 67 - 49 로 잡는다.
        handleRect.sizeDelta = new Vector2(HandleWidth, HandleHeight - GaugeHeight);
        handleRect.anchoredPosition = Vector2.zero;

        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.sprite = LoadArt("setting_n", HandleRect);
        handleImage.raycastTarget = true;

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.onValueChanged.AddListener(HandleVolumeChanged);

        volumeSlider = slider;
    }

    private Image CreateImageChild(string name, Transform parent, RectInt spriteRect)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        UiTheme.Stretch(rect);

        Image image = go.GetComponent<Image>();
        image.sprite = LoadArt(name == "Outline" ? "gauge_st"
            : name == "Fill" ? "gauge"
            : "gauge_BG", spriteRect);
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        return image;
    }

    private Sprite LoadArt(string assetName, RectInt pixelRect)
    {
        string resourcePath = "Settings/" + assetName;
        Sprite source = Resources.Load<Sprite>(resourcePath);
        Texture2D texture = source != null ? source.texture : null;
        float pixelsPerUnit = source != null ? source.pixelsPerUnit : 100f;

        // 임포트 설정이 Texture로 바뀌어도 동작하도록 폴백한다.
        if (texture == null)
        {
            texture = Resources.Load<Texture2D>(resourcePath);
        }

        if (texture == null)
        {
            Debug.LogError($"Missing settings artwork at Resources/{resourcePath}.png.");
            return null;
        }

        Sprite trimmed = Sprite.Create(
            texture,
            new Rect(pixelRect.x, pixelRect.y, pixelRect.width, pixelRect.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit,
            0,
            SpriteMeshType.FullRect);
        trimmed.name = assetName + " (Settings)";
        generatedSprites.Add(trimmed);
        return trimmed;
    }
}
