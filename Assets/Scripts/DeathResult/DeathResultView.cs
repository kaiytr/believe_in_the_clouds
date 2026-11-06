using System;
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
        Label(composition, data.cause, 15, Ink, .57f, .675f, .82f, .70f);

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

        view.resume = ArtworkButton(composition, "UI/DeathResult/DeathResultContinueButton", "이어하기",
            .588f, .277f, .80f, .351f, onContinue);
        view.exit = ArtworkButton(composition, "UI/DeathResult/DeathResultSaveExitButton", "저장 및 종료",
            .588f, .187f, .80f, .259f, onExit);
        view.error = Label(composition, "", 16, new Color(.58f, .12f, .08f), .56f, .12f, .83f, .18f);
        UiEventSystemBoot.Ensure().SetSelectedGameObject(view.resume.gameObject);
        return view;
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
