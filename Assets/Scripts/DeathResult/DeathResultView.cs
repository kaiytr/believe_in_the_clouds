using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed class DeathResultView : MonoBehaviour
{
    internal static readonly Color Ink = new Color(.20f, .15f, .10f);
    internal static readonly Color Wood = new Color(.49f, .35f, .23f);
    internal static readonly Color Paper = new Color(.87f, .83f, .76f);
    internal static readonly Color Inset = new Color(.77f, .71f, .63f);
    private Button resume, exit;
    private Text error;
    private Coroutine doctrineReveal;
    private Action closeScroll;
    // Seconds the description stays open before it closes by itself (the in-game tooltip uses 3).
    private const float doctrineScrollSeconds = 3f;

    public static DeathResultView Create(DeathResultData data, Texture2D capture, Action onContinue, Action onExit)
    {
        Canvas canvas = CreateCanvas("Death Result", 30000);
        var view = canvas.gameObject.AddComponent<DeathResultView>();
        var dim = Box("Dim background", canvas.transform, new Color(0, 0, 0, .64f), 0, 0, 1, 1);
        dim.GetComponent<Image>().raycastTarget = true;
        var compositionObject = new GameObject("Result composition", typeof(RectTransform), typeof(AspectRatioFitter));
        var composition = compositionObject.GetComponent<RectTransform>();
        composition.SetParent(dim, false);
        composition.anchorMin = composition.anchorMax = new Vector2(.5f, .5f);
        composition.sizeDelta = Vector2.zero;
        var aspect = compositionObject.GetComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = 16f / 9f;

        Sprite panelSprite = Resources.Load<Sprite>("UI/DeathResult/result_BG");
        // Bottom-left section of the artwork (277..994 x 715..910 of 1920x1080), or the placeholder frame's area.
        float[] doctrineArea = panelSprite != null ? new[] { .1443f, .1574f, .5177f, .3380f } : new[] { .101f, .145f, .513f, .343f };
        if (panelSprite != null)
        {
            AddFullCanvasArtwork(composition, "Result panel artwork", panelSprite);
        }
        else
        {
            var border = Box("Result frame", composition, Ink, .08f, .10f, .92f, .91f);
            var wood = Box("Wood trim", border, Wood, .006f, .01f, .994f, .99f);
            var panel = Box("Result panel", wood, Paper, .012f, .02f, .988f, .98f);
            var heading = Box("Title board", panel, Ink, .29f, .90f, .71f, 1.10f);
            var titleFill = Box("Title wood", heading, Wood, .012f, .04f, .988f, .96f);
            Label(titleFill, "− RESULT −", 34, Paper, 0, 0, 1, 1);
            Framed("Play statistics", panel, .025f, .35f, .515f, .84f);
            Framed("Reserved area", panel, .025f, .055f, .515f, .30f);
            Framed("Death information", panel, .53f, .055f, .975f, .84f);
        }

        Label(composition, "획득한 코인 개수", 23, Ink, .178f, .63f, .405f, .68f, TextAnchor.MiddleLeft);
        Label(composition, data.statistics.coins + " 개", 23, Ink, .425f, .63f, .49f, .68f, TextAnchor.MiddleRight);
        Label(composition, "획득한 키워드 개수", 23, Ink, .178f, .57f, .405f, .62f, TextAnchor.MiddleLeft);
        Label(composition, data.statistics.keywords + " 개", 23, Ink, .425f, .57f, .49f, .62f, TextAnchor.MiddleRight);
        long total = 0;
        foreach (EnemyKillCount count in data.statistics.kills) total += count.count;
        Label(composition, "처치한 개체 수", 23, Ink, .178f, .51f, .405f, .56f, TextAnchor.MiddleLeft);
        Label(composition, total + " 개체", 23, Ink, .425f, .51f, .49f, .56f, TextAnchor.MiddleRight);
        var kills = new StringBuilder();
        foreach (EnemyRewardKind kind in Enum.GetValues(typeof(EnemyRewardKind)))
        {
            if (kind == EnemyRewardKind.Boss && data.statistics.Count(kind) == 0) continue;
            if (kills.Length > 0) kills.Append(" / ");
            kills.Append(DeathCause.EnemyLabel(kind)).Append(": ").Append(data.statistics.Count(kind))
                .Append(kind == EnemyRewardKind.Dog ? " 마리" : " 명");
        }
        Label(composition, kills.ToString(), 18, Ink, .178f, .455f, .49f, .50f);
        // The cause replaces the old "사망원인" heading, centered in the gap
        // between the right panel's inner top edge (y=254) and the capture (y=355).
        Label(composition, data.cause, 22, Ink, .56f, .688f, .83f, .748f);

        var imageArea = Box("Capture frame", composition, new Color(0, 0, 0, 0), .568f, .374f, .82f, .671f);
        imageArea.GetComponent<Image>().raycastTarget = false;
        imageArea.gameObject.AddComponent<RectMask2D>();
        if (capture != null)
        {
            var image = new GameObject("Gameplay capture", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            image.transform.SetParent(imageArea, false);
            Place(image.GetComponent<RectTransform>(), 0, 0, 1, 1);
            image.GetComponent<RawImage>().texture = capture;
            image.GetComponent<RawImage>().raycastTarget = false;
            image.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            image.GetComponent<AspectRatioFitter>().aspectRatio = (float)capture.width / capture.height;
        }
        else Label(imageArea, "화면을 캡처할 수 없습니다", 19, Paper, .03f, .03f, .97f, .97f);

        view.BuildDoctrineSection(composition, data.doctrines, doctrineArea);

        view.resume = ArtworkButton(composition, "UI/DeathResult/DeathResultContinueButton", "이어하기",
            .588f, .277f, .80f, .351f, onContinue);
        view.exit = ArtworkButton(composition, "UI/DeathResult/DeathResultSaveExitButton", "저장 및 종료",
            .588f, .187f, .80f, .259f, onExit);
        view.error = Label(composition, "", 16, new Color(.58f, .12f, .08f), .56f, .12f, .83f, .18f);
        UiEventSystemBoot.Ensure().SetSelectedGameObject(view.resume.gameObject);
        return view;
    }

    // Doctrines held at death, inside the bottom-left section: a row of scroll tiles. Clicking one hides the tiles and
    // unrolls the description scroll from the section's left edge (like the in-game doctrine tooltip); clicking the
    // scroll brings the tiles back.
    private void BuildDoctrineSection(Transform composition, System.Collections.Generic.List<Doctrine> held, float[] area)
    {
        RectTransform section = Box("Doctrine section", composition, new Color(0, 0, 0, 0), area[0], area[1], area[2], area[3]);
        section.GetComponent<Image>().raycastTarget = false;
        int count = held != null ? held.Count : 0;

        // Section is about 3.67 times wider than tall; a tile keeps the HUD scroll's 118:130 shape.
        const float sectionAspect = 3.667f, tileAspect = 118f / 130f, tileHeight = .7f, gap = .02f;
        float tileWidth = tileHeight * tileAspect / sectionAspect;
        float step = count > 1 ? Mathf.Min(tileWidth + gap, (.96f - tileWidth) / (count - 1)) : 0f;
        float rowWidth = tileWidth + step * Mathf.Max(0, count - 1);
        float left = (1f - rowWidth) * .5f;

        // Tiles, centered in the section.
        RectTransform tiles = Box("Held doctrines", section, new Color(0, 0, 0, 0), 0, 0, 1, 1);
        tiles.GetComponent<Image>().raycastTarget = false;
        if (count == 0) Label(tiles, "보유했던 교리가 없습니다", 20, Ink, .04f, .3f, .96f, .7f);

        // Description scroll: the art is revealed left to right by its fill; text fades in once it is mostly open.
        Sprite art = DoctrineHud.LoadTooltipSprite();
        float artAspect = art != null ? art.rect.width / art.rect.height : 913f / 186f;
        float height = Mathf.Min(1f, sectionAspect / artAspect), bottom = (1f - height) * .5f;
        RectTransform scroll = Box("Doctrine description", section, art != null ? Color.white : new Color(.95f, .86f, .73f),
            0f, bottom, 1f, bottom + height);
        Image scrollImage = scroll.GetComponent<Image>();
        scrollImage.sprite = art != null ? art : UiTheme.WhiteSprite;   // a filled image needs a sprite
        scrollImage.type = Image.Type.Filled;
        scrollImage.fillMethod = Image.FillMethod.Horizontal;
        scrollImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        scrollImage.fillAmount = 0f;
        scrollImage.raycastTarget = true;
        CanvasGroup scrollGroup = scroll.gameObject.AddComponent<CanvasGroup>();
        Button close = scroll.gameObject.AddComponent<Button>();
        close.targetGraphic = scrollImage;
        close.transition = Selectable.Transition.None;
        close.navigation = new Navigation { mode = Navigation.Mode.None };

        // Same inner margins the HUD tooltip uses on this art (52/56 px sides, 40/34 px top and bottom of 913x186).
        RectTransform texts = Box("Texts", scroll, new Color(0, 0, 0, 0), .057f, .215f, .939f, .817f);
        texts.GetComponent<Image>().raycastTarget = false;
        CanvasGroup textGroup = texts.gameObject.AddComponent<CanvasGroup>();
        textGroup.alpha = 0f;
        Text title = Label(texts, "", 24, Ink, 0f, .6f, 1f, 1f, TextAnchor.MiddleLeft);
        Text body = Label(texts, "", 20, Ink, 0f, 0f, 1f, .6f, TextAnchor.UpperLeft);
        scroll.gameObject.SetActive(false);

        // Closing (by click or by the timer) brings the tiles back.
        closeScroll = () =>
        {
            if (doctrineReveal != null) StopCoroutine(doctrineReveal);
            doctrineReveal = null;
            scroll.gameObject.SetActive(false);
            tiles.gameObject.SetActive(true);
        };
        close.onClick.AddListener(() => closeScroll());

        for (int i = 0; i < count; i++)
        {
            DoctrineSlotView slot = DoctrineSlotView.BuildRuntime(tiles);
            slot.name = "Held doctrine " + (i + 1);
            slot.Bind(held[i], DoctrineText.ShortName(held[i]), true);
            Place(slot.GetComponent<RectTransform>(), left + step * i, .5f - tileHeight * .5f,
                left + step * i + tileWidth, .5f + tileHeight * .5f);
            int number = i + 1;
            slot.Clicked += clicked =>
            {
                title.text = "교리" + number;
                body.text = DoctrineText.Describe(clicked.Doctrine);
                tiles.gameObject.SetActive(false);
                scroll.gameObject.SetActive(true);
                if (doctrineReveal != null) StopCoroutine(doctrineReveal);
                doctrineReveal = StartCoroutine(ShowScroll(scrollGroup, scrollImage, textGroup));
            };
        }
    }

    // Unrolls the scroll, holds it for DoctrineScrollSeconds, fades it out and closes it (like the in-game tooltip).
    private IEnumerator ShowScroll(CanvasGroup whole, Image scroll, CanvasGroup texts)
    {
        const float seconds = .4f, fadeSeconds = .5f;
        whole.alpha = 1f;
        scroll.fillAmount = 0f;
        texts.alpha = 0f;
        // The result screen runs with timeScale 0.
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            scroll.fillAmount = k;
            texts.alpha = Mathf.Clamp01((k - .5f) * 2f);
            yield return null;
        }
        scroll.fillAmount = 1f;
        texts.alpha = 1f;

        yield return new WaitForSecondsRealtime(Mathf.Max(.5f, doctrineScrollSeconds));
        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            whole.alpha = 1f - Mathf.Clamp01(t / fadeSeconds);
            yield return null;
        }
        whole.alpha = 1f;
        closeScroll();
    }

    private static void AddFullCanvasArtwork(Transform parent, string name, Sprite sprite)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Place(imageObject.GetComponent<RectTransform>(), 0, 0, 1, 1);
        var image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = false;
        image.raycastTarget = false;
    }

    private static Button ArtworkButton(Transform parent, string resourcePath, string label,
        float x0, float y0, float x1, float y1, Action action)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null) AddFullCanvasArtwork(parent, label + " artwork", sprite);
        var hitArea = Box(label + " hit area", parent, new Color(1, 1, 1, 0), x0, y0, x1, y1);
        Image hitImage = hitArea.GetComponent<Image>();
        hitImage.raycastTarget = true;
        Button button = hitArea.gameObject.AddComponent<Button>();
        button.targetGraphic = hitImage;
        if (sprite == null) Label(hitArea, label, 22, Paper, .03f, .03f, .97f, .97f);
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1, 1, 1, 0);
        colors.highlightedColor = new Color(1, 1, 1, .18f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(.8f, .8f, .8f, .15f);
        button.colors = colors;
        button.onClick.AddListener(() => action());
        return button;
    }
    public void SetBusy(bool value) { resume.interactable = exit.interactable = !value; if (value) error.text = "저장 중…"; }
    public void SetError(string message) { error.text = message; }

    internal static Canvas CreateCanvas(string name, int order)
    {
        UiEventSystemBoot.Ensure();
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order; canvas.pixelPerfect = true;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1100, 660);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        return canvas;
    }
    internal static RectTransform Box(string name, Transform parent, Color color, float x0, float y0, float x1, float y1)
    {
        var go = UiTheme.CreateImage(name, parent, color);
        var rect = go.GetComponent<RectTransform>(); Place(rect, x0, y0, x1, y1); return rect;
    }
    private static RectTransform Framed(string name, Transform parent, float x0, float y0, float x1, float y1)
    {
        var frame = Box(name, parent, new Color(.65f, .57f, .48f), x0, y0, x1, y1);
        return Box("Inset", frame, Inset, .006f, .012f, .994f, .988f);
    }
    internal static Text Label(Transform parent, string value, int size, Color color, float x0, float y0, float x1, float y1,
        TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        Text text = UiTheme.CreateText("Label", parent, value, size, color, alignment);
        text.font = CombatUiResources.KoreanFont() ?? UiTheme.GetFont();
        text.resizeTextForBestFit = true; text.resizeTextMinSize = 10; text.resizeTextMaxSize = size;
        Place(text.rectTransform, x0, y0, x1, y1); return text;
    }
    internal static Button MenuButton(Transform parent, string value, float x0, float y0, float x1, float y1, Action action)
    {
        var frame = Box(value, parent, Ink, x0, y0, x1, y1);
        var fill = Box("Button fill", frame, Wood, .012f, .07f, .988f, .93f);
        var image = fill.GetComponent<Image>(); image.raycastTarget = true;
        Button button = frame.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        ColorBlock colors = button.colors; colors.highlightedColor = new Color(1.2f, 1.15f, 1.08f); colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        Label(fill, value, 22, Paper, .03f, .03f, .97f, .97f);
        button.onClick.AddListener(() => action()); return button;
    }
    internal static void Place(RectTransform rect, float x0, float y0, float x1, float y1)
    {
        rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
