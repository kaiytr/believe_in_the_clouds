using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Tutorial 조우 후 first-stone TIP 3페이지 패널.
//
// - Assets/Design/Tip 아트를 런타임 UI로 재구성한다 (SettingsScreen 방식).
// - 배경(TIP_BG, 973x581)의 "- TIP -" 타이틀과 좌측 갈색 박스는 아트 그대로 쓴다.
//   (박스를 코드로 다시 그리지 않는다 — 아트를 덮어 가리는 사고 방지)
// - ESC 알약 + "나가기"는 TIP_button 아트에 글자까지 baked되어 있으므로
//   텍스트를 다시 그리지 않고 아트 한 장(218x50)을 통째로 쓴다.
//   (전에 ESC/나가기를 코드로 또 그려서 두 겹으로 겹쳐 보이던 문제의 원인)
// - 페이지: 1 공격 / 2 이동·회피 / 3 믿음. 갈색 박스 아래 중앙에 페이지명.
// - 폰트는 ONE Mobile Title (UiTheme.GetFont), uGUI Text, 레거시 입력.
public sealed class TutorialTipPanel : MonoBehaviour
{
    public const string StageId = "tutorial.believer.first-stone.tip";

    public static TutorialTipPanel Instance { get; private set; }

    // ---- 아트 원본 좌표 (1920x1080, Unity bottom-left) ----
    private static readonly RectInt BackgroundRect = new RectInt(474, 250, 973, 581);
    private static readonly RectInt LeftArrowRect = new RectInt(531, 528, 21, 24);
    private static readonly RectInt RightArrowRect = new RectInt(997, 528, 21, 24);
    // 알약(ESC 글자 포함) + "나가기"까지 한 번에 들어있는 영역.
    private static readonly RectInt EscArtRect = new RectInt(1162, 313, 218, 50);

    // ---- 패널 기준 상수 (패널 좌상단 = 0,0 / 우 +, 아래 +) ----
    private const float PanelWidth = 973f;
    private const float PanelHeight = 581f;

    // 아트 속 갈색 박스 (실측): x 107~494, y 180~407. 중심 = (300.5, 293.5)
    private const float BoxCenterX = 300.5f;
    private const float BoxTop = 179f;
    private const float BoxBottom = 406f;
    private const float BoxCenterY = (BoxTop + BoxBottom) * 0.5f;
    // 갈색 박스 안쪽 여백. 0이면 그림이 박스 경계까지 꽉 찬다.
    // (박스 실측: x 107~494, y 179~406)
    private const float IllustrationInset = 0f;
    private const float IllustrationWidth = 494f - 107f - IllustrationInset * 2f;
    private const float IllustrationHeight = 406f - 179f - IllustrationInset * 2f;

    // 페이지명: 박스 아래 여백(407~468)의 정중앙, 박스 중심 x.
    private const float LabelCenterY = 437.5f;
    private const float LabelWidth = 440f;
    private const float LabelHeight = 56f;
    private const int LabelFontSize = 40;

    // ---- 설명 본문 그리드 ----
    // 가로: 오른쪽 화살표 오른쪽 끝(546) ~ 패널 안쪽 오른쪽(950) 열 안에서,
    //       가장 긴 줄(329px) 기준으로 좌우 여백이 같아지도록 센터링한다.
    //       → 546 + (404-329)/2 = 583.5
    // 세로: 갈색 박스 세로 중심(293.5)에 줄 블록 전체를 센터링.
    // 줄마다 Text를 따로 두고 MiddleLeft로 배치한다 — 레거시 Text의 위쪽 정렬은
    // 폰트 ascent에 따라 잉크 위치가 흔들리므로, 줄 중심을 직접 잡아야 정확하다.
    // (실측 검증: left 585 전 페이지 동일, 본문 cy 294 = 박스 중심 293.5,
    //  라벨 cx/cy = 박스 중심/여백 정중앙)
    private const float BodyLeft = 583.5f;
    private const float BodyWidth = 366.5f;
    private const int BodyFontSize = 24;
    private const float BodyPitch = 32f;
    private const int BodyMaxLines = 6;

    // 화살표 아이콘: 아트 원본 21x24를 1.25배 (그대로 쓰면 너무 작다).
    private const float ArrowScale = 1.25f;

    // ESC 아트 위치 (패널 기준): x 688, y 468, 218x50.
    // 1162-474 = 688, 831-(313+50) = 468.
    private static readonly Vector2 EscArtTopLeft = new Vector2(688f, 468f);

    private static readonly Color BlockerColor = new Color(0f, 0f, 0f, 0.45f);
    // TIP 아트의 baked 글자색(55, 36, 22)과 동일하게 맞춘다.
    private static readonly Color TipTextColor = new Color(55f / 255f, 36f / 255f, 22f / 255f, 1f);

    private static readonly string[] PageLabels = { "공격", "이동 · 회피", "믿음" };

    // 페이지별 그림. Resources/Tip/Tutorial 아래 파일 이름.
    private static readonly string[] PageIllustrations =
    {
        "Tutorial/TutorialAttack",
        "Tutorial/TutorialMove",
        "Tutorial/TutorialBelief",
    };

    private static readonly string[] PageBodies =
    {
        "마우스 좌클릭으로 공격합니다.\n지상에서는 3연타가 나가고,\n공중에서는 내리찍습니다.\n\n신도를 쓰러뜨려 보세요.",
        "A / D 또는 ← → 로 이동합니다.\nSpace 점프, Shift 회피.\n\n돌이 날아오면 피하세요.",
        "적들은 각자의 고유한 믿음을\n갖고 있습니다.\n반대되는 행동으로 믿음을\n무너뜨리면 적이 약해집니다.",
    };

    // 재단 TIP (런당 최초 1회): 재단 사용 전에 재단과 교리를 알려준다.
    private static readonly string[] FoundationLabels = { "재단", "교리", "획득", "조합", "사용 시기" };

    private static readonly string[] FoundationBodies =
    {
        "재단은 나만의 교리를\n조합 할 수 있는 장소입니다.",
        "교리는 [주어] [조건] [결과]의\n세 가지 키워드를 조합해\n게임 내 규칙을 직접 생성하고\n변경하는 시스템입니다.",
        "상자 개방과 상점 구매로\n새로운 교리를 획득할 수 있습니다.",
        "자신만의 교리를 조합해\n강해지세요",
        "매 분기마다 재단을\n사용할 수 있습니다.",
    };

    // 재단 TIP 페이지별 그림. Resources/Tip/Foundation 아래 파일 이름 (1→5 순서).
    private static readonly string[] FoundationIllustrations =
    {
        "Foundation/FoundationStep1",
        "Foundation/FoundationStep2",
        "Foundation/FoundationStep3",
        "Foundation/FoundationStep4",
        "Foundation/FoundationStep5",
    };

    // 상점 TIP (런당 최초 1회): 상점 사용 전에 상점이 뭔지 알려준다.
    private static readonly string[] ShopLabels = { "상점", "교리 활용" };

    private static readonly string[] ShopBodies =
    {
        "상점에서 원하는 아이템을\n구매할 수 있습니다.",
        "상점에서 구매한 교리들을 재단에서\n조합하여 사용 할 수 있습니다.",
    };

    // 상점 TIP 페이지별 그림. Resources/Tip/Shop 아래 파일 이름 (시간순).
    private static readonly string[] ShopIllustrations =
    {
        "Shop/ShopStep1",
        "Shop/ShopStep2",
    };

    private GameObject canvasObject;
    private Text pageLabel;
    private GameObject prevArrow;
    private GameObject nextArrow;
    private Image pageIllustration;
    private Sprite roundedMaskSprite;
    private string[] activeIllustrations = PageIllustrations;
    private readonly List<Text> bodyLines = new List<Text>();
    private string[] activeLabels = PageLabels;
    private string[] activeBodies = PageBodies;
    private int pageIndex;
    private int openedFrame;

    private readonly List<Sprite> generatedSprites = new List<Sprite>();

    public bool IsOpen => canvasObject != null && canvasObject.activeSelf;

    public event Action Closed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBoot()
    {
        if (Instance != null)
            return;

        GameObject go = new GameObject("[TutorialTipPanel]");
        DontDestroyOnLoad(go);
        go.AddComponent<TutorialTipPanel>();
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
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        Instance = null;
        for (int i = 0; i < generatedSprites.Count; i++)
        {
            if (generatedSprites[i] != null)
                Destroy(generatedSprites[i]);
        }
        generatedSprites.Clear();
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        // E/ESC 닫기는 InteractionController가 UiModal.CloseRequested로 보낸다.
        // 좌우는 여기서 직접 처리한다 (UiModal이 열려 있어도 먹혀야 함).
        if (Input.GetKeyDown(KeyCode.LeftArrow))
            ShowPage(pageIndex - 1);
        else if (Input.GetKeyDown(KeyCode.RightArrow))
            ShowPage(pageIndex + 1);
    }

    public void Open()
    {
        OpenTutorial();
    }

    public void OpenTutorial()
    {
        if (canvasObject == null)
            BuildUi();

        activeLabels = PageLabels;
        activeBodies = PageBodies;
        activeIllustrations = PageIllustrations;
        pageIndex = 0;
        RefreshPage();
        openedFrame = Time.frameCount;
        canvasObject.SetActive(true);
        UiEventSystemBoot.Ensure();
    }

    // 재단 TIP (런당 최초 1회). 같은 패널·같은 조작으로 재단 설명을 보여준다.
    public void OpenFoundationTip()
    {
        if (canvasObject == null)
            BuildUi();

        activeLabels = FoundationLabels;
        activeBodies = FoundationBodies;
        activeIllustrations = FoundationIllustrations;
        pageIndex = 0;
        RefreshPage();
        openedFrame = Time.frameCount;
        canvasObject.SetActive(true);
        UiEventSystemBoot.Ensure();
    }

    // 상점 TIP (런당 최초 1회). 같은 패널·같은 조작으로 상점 설명을 보여준다.
    public void OpenShopTip()
    {
        if (canvasObject == null)
            BuildUi();

        activeLabels = ShopLabels;
        activeBodies = ShopBodies;
        activeIllustrations = ShopIllustrations;
        pageIndex = 0;
        RefreshPage();
        openedFrame = Time.frameCount;
        canvasObject.SetActive(true);
        UiEventSystemBoot.Ensure();
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        canvasObject.SetActive(false);
        Closed?.Invoke();
    }

    // UiModal.CloseRequested에서 같은 프레임에 닫히지 않도록 가드를 둔다.
    // (SettingsScreen과 동일한 패턴: 연 ESC 입력이 바로 닫기로 처리되는 것 방지)
    public void HandleCloseRequested()
    {
        if (!IsOpen)
            return;
        if (Time.frameCount == openedFrame)
            return;

        Close();
    }

    private void ShowPage(int index)
    {
        int clamped = Mathf.Clamp(index, 0, activeLabels.Length - 1);
        if (clamped == pageIndex)
            return;

        pageIndex = clamped;
        RefreshPage();
    }

    private void RefreshPage()
    {
        if (pageLabel != null)
            pageLabel.text = activeLabels[pageIndex];

        // 첫 페이지에서는 왼쪽 화살표, 마지막 페이지에서는 오른쪽 화살표를 숨긴다.
        if (prevArrow != null)
            prevArrow.SetActive(pageIndex > 0);
        if (nextArrow != null)
            nextArrow.SetActive(pageIndex < activeLabels.Length - 1);

        RefreshIllustration();

        string[] lines = activeBodies[pageIndex].Split('\n');

        // 1) 텍스트/활성 먼저 반영.
        for (int i = 0; i < bodyLines.Count; i++)
        {
            Text line = bodyLines[i];
            bool hasText = i < lines.Length && lines[i].Length > 0;
            line.text = hasText ? lines[i] : string.Empty;
            line.gameObject.SetActive(hasText);
        }

        // 2) 줄 블록을 갈색 박스 세로 중심에 센터링 (줄 중심 기준).
        int slots = Mathf.Min(lines.Length, bodyLines.Count);
        float blockHeight = slots > 0 ? (slots - 1) * BodyPitch : 0f;
        float firstCenter = BoxCenterY - blockHeight * 0.5f;

        for (int i = 0; i < bodyLines.Count; i++)
        {
            if (i >= lines.Length)
                continue;

            RectTransform rect = bodyLines[i].rectTransform;
            rect.anchoredPosition = new Vector2(BodyLeft, -(firstCenter + i * BodyPitch));
        }
    }

    // 현재 페이지의 그림을 갈색 박스 안에 비율 유지로 배치한다.
    private void RefreshIllustration()
    {
        if (pageIllustration == null)
            return;

        Sprite sprite = null;
        if (activeIllustrations != null && pageIndex < activeIllustrations.Length)
        {
            string path = activeIllustrations[pageIndex];
            if (!string.IsNullOrEmpty(path))
            {
                string resourcePath = "Tip/" + path;
                sprite = Resources.Load<Sprite>(resourcePath);
                if (sprite == null)
                {
                    // 임포트 설정이 Sprite가 아니어도 그림은 보이도록 텍스처 폴백.
                    Texture2D texture = Resources.Load<Texture2D>(resourcePath);
                    if (texture != null)
                    {
                        sprite = Sprite.Create(texture,
                            new Rect(0f, 0f, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f), 100f, 0,
                            SpriteMeshType.FullRect);
                        sprite.name = path + " (Tip fallback)";
                        generatedSprites.Add(sprite);
                    }
                    else
                    {
                        Debug.LogError($"TIP 그림을 불러오지 못했습니다: Resources/{resourcePath}.png");
                    }
                }
            }
        }

        pageIllustration.sprite = sprite;
        pageIllustration.gameObject.SetActive(sprite != null);
        if (sprite == null)
            return;

        // 박스를 꽉 채운다(cover): 더 넓게 맞추는 축을 기준으로 확대하고,
        // 남는 쪽은 마스크가 잘라낸다. 작은 원본도 확대해서 여백이 생기지 않게 한다.
        Vector2 source = sprite.rect.size;
        float scale = Mathf.Max(IllustrationWidth / source.x, IllustrationHeight / source.y);
        pageIllustration.rectTransform.sizeDelta = source * scale;
        // 상점 팁 2페이지는 오른쪽 빈칸이 보여서, 그림만 조금 왼쪽으로 민다.
        float nudge = activeIllustrations == ShopIllustrations && pageIndex == 1 ? -4f : 0f;
        pageIllustration.rectTransform.anchoredPosition = new Vector2(nudge, 0f);
    }

    // 둥근 모서리 마스크용 스프라이트. 9-slice로 늘려 써서 크기와 무관하게 모서리 반경이 유지된다.
    private Sprite GetRoundedMaskSprite()
    {
        if (roundedMaskSprite != null)
            return roundedMaskSprite;

        const int size = 64;
        const int radius = 20;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "TipRoundedMask"
        };

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 네 모서리 원 안쪽인지 검사 (안티에일리어싱 포함).
                float dx = 0f, dy = 0f;
                bool corner = false;
                if (x < radius) { dx = radius - x - 0.5f; corner = true; }
                else if (x >= size - radius) { dx = x - (size - radius) + 0.5f; corner = true; }
                if (y < radius) { dy = radius - y - 0.5f; corner = true; }
                else if (y >= size - radius) { dy = y - (size - radius) + 0.5f; corner = true; }

                float alpha = 1f;
                if (corner)
                {
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    alpha = Mathf.Clamp01(radius - distance + 0.5f);
                }

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        roundedMaskSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        roundedMaskSprite.name = "TipRoundedMask (Tip)";
        generatedSprites.Add(roundedMaskSprite);
        return roundedMaskSprite;
    }

    private void BuildUi()    {
        if (canvasObject != null)
            return;

        canvasObject = new GameObject("[TutorialTipCanvas]");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        Image blocker = UiTheme.CreateImage("Blocker", canvasObject.transform, BlockerColor)
            .GetComponent<Image>();
        UiTheme.Stretch(blocker.rectTransform);
        blocker.raycastTarget = true;

        GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panel.anchoredPosition = Vector2.zero;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.sprite = LoadDesignArt("TIP_BG", BackgroundRect);
        panelImage.type = Image.Type.Simple;
        panelImage.color = Color.white;
        panelImage.raycastTarget = true;

        RectTransform content = new GameObject("Content", typeof(RectTransform))
            .GetComponent<RectTransform>();
        content.SetParent(panel, false);
        UiTheme.Stretch(content);

        // 페이지 그림: 갈색 박스 안쪽을 꽉 채운다. 원본이 박스보다 좁으면 확대하고,
        // 넘치는 부분은 둥근 모서리 마스크로 잘라 박스 밖으로 나가지 않게 한다.
        GameObject illustrationMaskObject = new GameObject("IllustrationMask",
            typeof(RectTransform), typeof(Image), typeof(Mask));
        illustrationMaskObject.transform.SetParent(content, false);

        RectTransform illustrationMask = illustrationMaskObject.GetComponent<RectTransform>();
        illustrationMask.anchorMin = new Vector2(0f, 1f);
        illustrationMask.anchorMax = new Vector2(0f, 1f);
        illustrationMask.pivot = new Vector2(0.5f, 0.5f);
        illustrationMask.sizeDelta = new Vector2(IllustrationWidth, IllustrationHeight);
        illustrationMask.anchoredPosition = new Vector2(BoxCenterX, -BoxCenterY);

        Image maskImage = illustrationMaskObject.GetComponent<Image>();
        maskImage.sprite = GetRoundedMaskSprite();
        maskImage.type = Image.Type.Sliced;
        maskImage.color = Color.white;
        maskImage.raycastTarget = false;

        Mask mask = illustrationMaskObject.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject illustrationObject = new GameObject("Illustration",
            typeof(RectTransform), typeof(Image));
        illustrationObject.transform.SetParent(illustrationMaskObject.transform, false);

        RectTransform illustrationRect = illustrationObject.GetComponent<RectTransform>();
        illustrationRect.anchorMin = new Vector2(0.5f, 0.5f);
        illustrationRect.anchorMax = new Vector2(0.5f, 0.5f);
        illustrationRect.pivot = new Vector2(0.5f, 0.5f);
        illustrationRect.anchoredPosition = Vector2.zero;
        illustrationRect.sizeDelta = new Vector2(IllustrationWidth, IllustrationHeight);

        pageIllustration = illustrationObject.GetComponent<Image>();
        pageIllustration.color = Color.white;
        pageIllustration.preserveAspect = false;
        pageIllustration.raycastTarget = false;
        illustrationObject.SetActive(false);

        // 페이지명: 갈색 박스 아래 여백의 정중앙, 박스 중심 x.
        pageLabel = CreateText(content, "PageLabel", string.Empty,
            new Vector2(BoxCenterX - LabelWidth * 0.5f, LabelCenterY - LabelHeight * 0.5f),
            new Vector2(LabelWidth, LabelHeight), new Vector2(0.5f, 1f),
            LabelFontSize, TextAnchor.MiddleCenter);

        // 설명 본문: 줄마다 Text를 하나씩, MiddleLeft로 두고 중심을 직접 잡는다.
        for (int i = 0; i < BodyMaxLines; i++)
        {
            Text line = CreateText(content, "BodyLine" + i, string.Empty,
                new Vector2(BodyLeft, 0f),
                new Vector2(BodyWidth, BodyPitch), new Vector2(0f, 0.5f),
                BodyFontSize, TextAnchor.MiddleLeft);
            line.horizontalOverflow = HorizontalWrapMode.Overflow;
            line.verticalOverflow = VerticalWrapMode.Overflow;
            line.gameObject.SetActive(false);
            bodyLines.Add(line);
        }

        // 좌/우 화살표 (아트 위치 그대로, 살짝 확대)
        prevArrow = BuildArrowButton(content, "PrevPage", LeftArrowRect, () => ShowPage(pageIndex - 1));
        nextArrow = BuildArrowButton(content, "NextPage", RightArrowRect, () => ShowPage(pageIndex + 1));

        // ESC + 나가기: 아트 한 장 (글자 baked). 텍스트를 다시 그리지 않는다.
        BuildEscArt(content);

        canvasObject.SetActive(false);
    }

    // topLeft: 패널 좌상단 기준 (우로 +, 아래로 +). 앵커는 패널 좌상단(0,1).
    private Text CreateText(Transform parent, string name, string content,
        Vector2 topLeft, Vector2 size, Vector2 pivot, int fontSize, TextAnchor alignment)
    {
        // 폰트: 원모바일 (ONE Mobile Title) — UiTheme.GetFont()가 OS 폴백까지 처리.
        Text text = UiTheme.CreateText(name, parent, content, fontSize, TipTextColor, alignment);
        text.font = UiTheme.GetFont();
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = pivot;
        rect.anchoredPosition = new Vector2(
            topLeft.x + pivot.x * size.x,
            -topLeft.y - (1f - pivot.y) * size.y);
        rect.sizeDelta = size;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private GameObject BuildArrowButton(Transform parent, string name, RectInt iconRect, Action onClick)
    {
        // 아이콘 x는 아트 원본 위치, y는 갈색 박스 세로 중심에 맞춘다.
        float localCenterX = iconRect.x + iconRect.width * 0.5f - BackgroundRect.x;
        float localCenterY = BoxCenterY;

        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(64f, 64f);
        rect.anchoredPosition = new Vector2(localCenterX, -localCenterY);

        Image hitArea = buttonObject.GetComponent<Image>();
        hitArea.sprite = UiTheme.WhiteSprite;
        hitArea.color = new Color(1f, 1f, 1f, 0f);
        hitArea.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = hitArea;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick?.Invoke());

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(buttonObject.transform, false);
        RectTransform icon = iconObject.GetComponent<RectTransform>();
        icon.anchorMin = new Vector2(0.5f, 0.5f);
        icon.anchorMax = new Vector2(0.5f, 0.5f);
        icon.pivot = new Vector2(0.5f, 0.5f);
        icon.anchoredPosition = Vector2.zero;
        icon.sizeDelta = new Vector2(
            Mathf.Round(iconRect.width * ArrowScale),
            Mathf.Round(iconRect.height * ArrowScale));

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = LoadDesignArt("TIP_button-1",
            name == "PrevPage" ? LeftArrowRect : RightArrowRect);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        return buttonObject;
    }

    // TIP_button 아트에는 알약 + "ESC" + "나가기"가 이미 그려져 있다.
    // 여기에 텍스트를 더 얹으면 두 겹으로 보이므로 아트만 쓴다.
    private void BuildEscArt(Transform parent)
    {
        GameObject buttonObject = new GameObject("EscClose", typeof(RectTransform), typeof(Image));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(EscArtRect.width, EscArtRect.height);
        rect.anchoredPosition = new Vector2(EscArtTopLeft.x, -EscArtTopLeft.y);

        Image background = buttonObject.GetComponent<Image>();
        background.sprite = LoadDesignArt("TIP_button", EscArtRect);
        background.type = Image.Type.Simple;
        background.color = Color.white;
        background.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(Close);
    }

    private Sprite LoadDesignArt(string assetName, RectInt pixelRect)
    {
        Sprite trimmed = TipArtworkLoader.Load(assetName, pixelRect);
        if (trimmed != null && !generatedSprites.Contains(trimmed))
            generatedSprites.Add(trimmed);
        return trimmed;
    }
}
