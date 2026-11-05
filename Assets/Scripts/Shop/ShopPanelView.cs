using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 상점 패널 (장바구니형, 지급된 아트를 그대로 사용).
//
// 레이아웃:
//   ┌ 위: 교리 카드 3열 그리드 (세로 스크롤) ──────────────────────┐
//   │  [교리 이름][등급]              ● 가격                       │
//   │  [ 아이템 자리 ]                             [  구매  ]      │
//   └ 아래: 장바구니 바 (가로 스크롤) + 총 코인 + 구매 ───────────┘
//
// 동작 규칙:
// - 교리 카드 클릭 → 구매 칸에 1개 추가 (중복 가능, 개수 제한 없음).
// - 구매 칸 카드 클릭 → 그 1개가 빠진다. 좌우 스크롤로 모두 볼 수 있다.
// - 구매 버튼: 구매 칸 전체를 TryPurchase 1건으로 결제. 실패 시 칸 유지.
//
// 표시만 담당한다. 장바구니/구매 요청은 ShopScreen이 한다.
public class ShopPanelView : MonoBehaviour
{
    // 패널 아트 원본 크기 (Assets/Resources/UI/Shop/Shop_BG.png).
    private const float PanelWidth = 1348f;
    private const float PanelHeight = 804f;

    // 교리 카드 아트 원본 크기. 아트 안 좌표를 그대로 쓰기 위해 1:1로 배치한다.
    private const float CardWidth = 405f;
    private const float CardHeight = 159f;
    private const float GridGapX = 26f;
    private const float GridGapY = 30f;
    private const float CatalogMarginX = 34f;
    private const float CatalogTop = 44f;

    // 장바구니 카드 크기 (아트 원본 300x118).
    private const float CartCardWidth = 300f;
    private const float CartCardHeight = 118f;
    private const float CartCardGap = 16f;

    // 장바구니 바 (패널 안쪽 테두리 안에 들어오도록 아래 여백을 준다).
    private const float CartBarWidth = 1246f;
    private const float CartBarHeight = 148f;
    private const float CartBarBottom = 46f;

    private static readonly Color Ink = new Color(0.20f, 0.14f, 0.10f, 1f);
    private static readonly Color InkSoft = new Color(0.40f, 0.31f, 0.24f, 1f);
    private static readonly Color PillText = new Color(0.97f, 0.95f, 0.92f, 1f);
    private static readonly Color BuyDisabledTint = new Color(0.66f, 0.62f, 0.59f, 1f);
    private static readonly Color CardHoverTint = new Color(1f, 0.97f, 0.92f, 1f);
    private static readonly Color CardPressedTint = new Color(0.95f, 0.90f, 0.85f, 1f);

    // 교리칸 1칸 가격. ShopScreen.SlotPrice와 같은 값으로 유지한다.
    internal const int SlotPrice = 100;

    // 체력 회복 1회 가격 (최대 체력 30% 회복). ShopScreen.HealPrice와 같은 값으로 유지한다.
    internal const int HealPrice = 90;

    // 교리칸/체력 회복의 등급 뱃지. 카탈로그와 장바구니에서 같은 값을 쓴다.
    private const ShopRarity SlotRarity = ShopRarity.Rare;
    private const ShopRarity HealRarity = ShopRarity.Uncommon;

    private Canvas canvas;
    private GameObject content;
    private Text pillText;
    private RectTransform catalogContent;
    private RectTransform cartContent;
    private ScrollRect cartScroller;
    private ScrollDragGuard cartGuard;
    private Button buyButton;
    private Image buyImage;
    private GameObject emptyCartText;
    private bool isOpen;

    // 장바구니 합계 (총 코인 알약에 표시).
    private long lastTotal;
    private long lastBalance;

    // 카탈로그 카드 → 항목 식별자. 교리칸 항목은 "slot"이다.
    private readonly Dictionary<GameObject, string> catalogObjectToId = new Dictionary<GameObject, string>();

    // 구매 칸 카드 오브젝트 (담은 순서대로).
    private readonly List<GameObject> cartObjects = new List<GameObject>();

    // 스킨 로드 (Assets/Resources/UI/Shop). Import 설정과 무관하게 Sprite/Texture2D 둘 다 시도한다.
    private static readonly Dictionary<string, Sprite> skinCache = new Dictionary<string, Sprite>();

    private static Sprite Skin(string name)
    {
        if (skinCache.TryGetValue(name, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Sprite sprite = Resources.Load<Sprite>("UI/Shop/" + name);
        if (sprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>("UI/Shop/" + name);
            if (texture != null)
            {
                sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                sprite.name = name;
            }
        }

        if (sprite != null)
        {
            skinCache[name] = sprite;
        }

        return sprite;
    }

    private static void ApplySkin(Image image, string name)
    {
        Sprite sprite = Skin(name);
        if (sprite == null)
        {
            return;
        }

        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
    }

    /// <summary>부모 좌상단 기준 픽셀 좌표로 고정 배치한다.</summary>
    private static RectTransform Place(Component target, float x, float y, float width, float height)
    {
        RectTransform rect = target.transform as RectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    public bool IsOpen => isOpen;

    /// <summary>교리 나열 칸의 카드를 눌렀을 때. (항목 Id, 교리칸이면 "slot")</summary>
    public event Action<string> CatalogClicked;

    /// <summary>구매 칸의 카드를 눌렀을 때. (구매 칸 안의 순서 index)</summary>
    public event Action<int> CartClicked;

    /// <summary>구매 버튼을 눌렀을 때. (구매 칸 전체 결제)</summary>
    public event Action BuyClicked;

    public static ShopPanelView BuildRuntime()
    {
        GameObject root = new GameObject("[ShopPanel]");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // GraphicRaycaster가 없으면 EventSystem이 있어도 클릭이 UI에 닿지 않는다.
        root.AddComponent<GraphicRaycaster>();

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(root.transform, false);
        UiTheme.Stretch(content.GetComponent<RectTransform>());

        GameObject blocker = UiTheme.CreateImage("Blocker", content.transform,
            new Color(0f, 0f, 0f, 0.55f));
        UiTheme.Stretch(blocker.GetComponent<RectTransform>());
        blocker.GetComponent<Image>().raycastTarget = true;

        // 패널은 아트 원본 크기 그대로 둔다.
        GameObject panel = UiTheme.CreateImage("Panel", content.transform, Color.white);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        ApplySkin(panel.GetComponent<Image>(), "Shop_BG");

        ShopPanelView view = root.AddComponent<ShopPanelView>();
        view.canvas = canvas;
        view.content = content;

        // 위: 교리 카드 3열 그리드 (세로 스크롤).
        view.catalogContent = view.BuildCatalogGrid(panel.transform);
        // 아래: 장바구니 바 (가로 스크롤) + 총 코인 + 구매 버튼.
        view.BuildCartBar(panel.transform);

        view.ClearCart();
        view.SetBuyEnabled(false);
        content.SetActive(false);
        return view;
    }

    public void Open(string shopName)
    {
        isOpen = true;

        if (pillText != null)
        {
            pillText.font = UiTheme.GetFont();
        }

        if (content != null)
        {
            content.SetActive(true);
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Close()
    {
        isOpen = false;
        if (content != null)
        {
            content.SetActive(false);
        }
    }

    // ---------------- 위: 교리 카드 3열 그리드 ----------------

    private RectTransform BuildCatalogGrid(Transform parent)
    {
        // 한 화면에 3줄(159 * 3 + 26 * 2 = 529)이 딱 보이게 고정한다.
        GameObject area = new GameObject("CatalogArea", typeof(RectTransform));
        area.transform.SetParent(parent, false);
        RectTransform areaRect = area.GetComponent<RectTransform>();
        areaRect.anchorMin = new Vector2(0f, 0f);
        areaRect.anchorMax = new Vector2(1f, 1f);
        areaRect.pivot = new Vector2(0.5f, 0.5f);
        float gridHeight = CardHeight * 3f + GridGapY * 2f;
        areaRect.offsetMin = new Vector2(CatalogMarginX, PanelHeight - CatalogTop - gridHeight);
        areaRect.offsetMax = new Vector2(-CatalogMarginX, -CatalogTop);

        GameObject scroll = new GameObject("CatalogScroll", typeof(RectTransform));
        scroll.transform.SetParent(area.transform, false);
        UiTheme.Stretch(scroll.GetComponent<RectTransform>());

        ScrollRect scroller = scroll.AddComponent<ScrollRect>();
        scroller.horizontal = false;
        scroller.vertical = true;
        scroller.movementType = ScrollRect.MovementType.Clamped;
        scroller.scrollSensitivity = 40f;
        // 카드 위에서 드래그로 스크롤할 때 카드가 클릭(담기)되지 않게 한다.
        ScrollDragGuard guard = ScrollDragGuard.Attach(scroller);

        // 뷰포트가 클릭(레이캐스트)을 받아야 휠/드래그가 스크롤로 전달된다.
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scroll.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        UiTheme.Stretch(viewportRect);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.004f);
        viewportImage.raycastTarget = true;
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        scroller.viewport = viewportRect;

        GameObject list = new GameObject("List", typeof(RectTransform));
        list.transform.SetParent(viewport.transform, false);
        RectTransform listRect = list.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 1f);
        listRect.anchorMax = new Vector2(1f, 1f);
        listRect.pivot = new Vector2(0.5f, 1f);
        listRect.anchoredPosition = Vector2.zero;
        listRect.sizeDelta = Vector2.zero;
        scroller.content = listRect;

        GridLayoutGroup grid = list.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.cellSize = new Vector2(CardWidth, CardHeight);
        grid.spacing = new Vector2(GridGapX, GridGapY);
        grid.padding = new RectOffset(0, 0, 0, 0);
        grid.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter fitter = list.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // 한 행에 [주어, 조건, 결과] 순으로 나열한다.
        IReadOnlyList<ShopEntry> subjects = ShopCatalog.GetColumn(ShopColumn.Subject);
        IReadOnlyList<ShopEntry> conditions = ShopCatalog.GetColumn(ShopColumn.Condition);
        IReadOnlyList<ShopEntry> results = ShopCatalog.GetColumn(ShopColumn.Result);
        int rows = Math.Max(subjects.Count, Math.Max(conditions.Count, results.Count));

        catalogObjectToId.Clear();
        for (int row = 0; row < rows; row++)
        {
            if (row < subjects.Count)
            {
                BuildCatalogCard(list.transform, subjects[row], guard);
            }

            if (row < conditions.Count)
            {
                BuildCatalogCard(list.transform, conditions[row], guard);
            }

            if (row < results.Count)
            {
                BuildCatalogCard(list.transform, results[row], guard);
            }
        }

        // 교리칸 + 체력 회복 구매 항목 (마지막 자리 유지).
        BuildSlotCard(list.transform, guard);
        BuildHealCard(list.transform, guard);

        return listRect;
    }

    private static string BadgeSkin(ShopRarity rarity)
    {
        switch (rarity)
        {
            case ShopRarity.Rare: return "Shop_Badge_Rare";
            case ShopRarity.Uncommon: return "Shop_Badge_Epic";
            default: return "Shop_Badge_Common";
        }
    }

    // 카탈로그와 장바구니가 같은 등급 뱃지를 쓰도록 한 곳에서 정한다.
    // "slot"/"heal"은 ShopCatalog에 없으므로 여기서 명시한다.
    private static ShopRarity RarityOf(string id)
    {
        if (id == "slot") return SlotRarity;
        if (id == "heal") return HealRarity;
        if (!string.IsNullOrEmpty(id) && ShopCatalog.TryGet(id, out ShopEntry entry))
        {
            return entry.Rarity;
        }

        return ShopRarity.Common;
    }

    // 카드 한 장 = 지급된 카드 아트 그대로 + 이름/등급/가격/구매 버튼만 얹는다.
    private void BuildCardFrame(Transform parent, string objectName, string id,
        string label, int price, ShopRarity rarity, ScrollDragGuard guard)
    {
        GameObject card = UiTheme.CreateImage(objectName, parent, Color.white);
        Image background = card.GetComponent<Image>();
        ApplySkin(background, "Shop_CardBlank");
        background.raycastTarget = true;

        // 교리 이름 + 등급 뱃지 (목업처럼 이름 바로 뒤에 뱃지가 붙는다).
        GameObject titleRow = new GameObject("TitleRow", typeof(RectTransform));
        titleRow.transform.SetParent(card.transform, false);
        Place(titleRow.transform, 28f, 24f, 300f, 32f);
        HorizontalLayoutGroup titleLayout = titleRow.AddComponent<HorizontalLayoutGroup>();
        titleLayout.spacing = 10f;
        titleLayout.childAlignment = TextAnchor.MiddleLeft;
        titleLayout.childControlWidth = true;
        titleLayout.childControlHeight = true;
        titleLayout.childForceExpandWidth = false;
        titleLayout.childForceExpandHeight = false;

        Text nameText = UiTheme.CreateText("Name", titleRow.transform, label, 24,
            Ink, TextAnchor.MiddleLeft);
        nameText.fontStyle = FontStyle.Bold;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        LayoutElement nameLayout = nameText.gameObject.AddComponent<LayoutElement>();
        nameLayout.preferredWidth = Mathf.Clamp(nameText.preferredWidth + 2f, 40f, 210f);
        nameLayout.preferredHeight = 32f;

        GameObject badge = UiTheme.CreateImage("Badge", titleRow.transform, Color.white);
        Image badgeImage = badge.GetComponent<Image>();
        ApplySkin(badgeImage, BadgeSkin(rarity));
        badgeImage.raycastTarget = false;
        LayoutElement badgeLayout = badge.AddComponent<LayoutElement>();
        badgeLayout.preferredWidth = 66f;
        badgeLayout.preferredHeight = 21f;

        // 가격 (아트에 박힌 코인 바로 오른쪽).
        Text priceText = UiTheme.CreateText("Price", card.transform, price.ToString("N0"), 27,
            Ink, TextAnchor.MiddleLeft);
        priceText.fontStyle = FontStyle.Bold;
        Place(priceText, 322f, 27f, 64f, 30f);

        // 구매 버튼 (아트에 "구매" 글자가 박혀 있음). 그대로 사용한다.
        GameObject buy = UiTheme.CreateImage("Buy", card.transform, Color.white);
        Image buyLocal = buy.GetComponent<Image>();
        ApplySkin(buyLocal, "Shop_Button");
        buyLocal.raycastTarget = false;
        Place(buyLocal, 259f, 75f, 124f, 50f);

        Button button = card.AddComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = CardHoverTint;
        colors.pressedColor = CardPressedTint;
        button.colors = colors;

        catalogObjectToId[card] = id;
        button.onClick.AddListener(() =>
        {
            if (guard != null && guard.ShouldIgnoreClick) return;
            CatalogClicked?.Invoke(id);
        });
    }

    private void BuildCatalogCard(Transform parent, ShopEntry entry, ScrollDragGuard guard)
    {
        BuildCardFrame(parent, "Catalog_" + entry.Id, entry.Id,
            entry.Label, entry.Price, entry.Rarity, guard);
    }

    private void BuildSlotCard(Transform parent, ScrollDragGuard guard)
    {
        BuildCardFrame(parent, "Catalog_slot", "slot",
            "교리칸", SlotPrice, SlotRarity, guard);
    }

    private void BuildHealCard(Transform parent, ScrollDragGuard guard)
    {
        BuildCardFrame(parent, "Catalog_heal", "heal",
            "체력 회복", HealPrice, HealRarity, guard);
    }

    // ---------------- 아래: 장바구니 바 ----------------

    private void BuildCartBar(Transform parent)
    {
        GameObject bar = UiTheme.CreateImage("CartBar", parent, Color.white);
        Image barImage = bar.GetComponent<Image>();
        ApplySkin(barImage, "Shop_CartBar");
        barImage.raycastTarget = false;
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 0f);
        barRect.anchorMax = new Vector2(0.5f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0f, CartBarBottom);
        barRect.sizeDelta = new Vector2(CartBarWidth, CartBarHeight);

        // 왼쪽: 가로 스크롤 장바구니 목록.
        GameObject scroll = new GameObject("CartScroll", typeof(RectTransform));
        scroll.transform.SetParent(bar.transform, false);
        RectTransform scrollRect = scroll.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0.5f);
        scrollRect.anchorMax = new Vector2(0f, 0.5f);
        scrollRect.pivot = new Vector2(0f, 0.5f);
        scrollRect.anchoredPosition = new Vector2(18f, 0f);
        // 카드 3장(300*3) + 간격 2개(16*2) = 932 만 보이게 한다. 4번째부터는 스크롤로 본다.
        scrollRect.sizeDelta = new Vector2(932f, CartCardHeight);

        ScrollRect scroller = scroll.AddComponent<ScrollRect>();
        scroller.horizontal = true;
        scroller.vertical = false;
        scroller.movementType = ScrollRect.MovementType.Clamped;
        scroller.scrollSensitivity = 40f;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scroll.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        UiTheme.Stretch(viewportRect);
        // 레이캐스트를 받아야 휠/드래그가 스크롤로 전달된다.
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.004f);
        viewportImage.raycastTarget = true;
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        scroller.viewport = viewportRect;
        cartScroller = scroller;
        cartGuard = ScrollDragGuard.Attach(scroller);

        GameObject list = new GameObject("CartList", typeof(RectTransform));
        list.transform.SetParent(viewport.transform, false);
        RectTransform listRect = list.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 0.5f);
        listRect.anchorMax = new Vector2(0f, 0.5f);
        listRect.pivot = new Vector2(0f, 0.5f);
        listRect.anchoredPosition = Vector2.zero;
        listRect.sizeDelta = new Vector2(0f, CartCardHeight);
        scroller.content = listRect;
        cartContent = listRect;

        HorizontalLayoutGroup listLayout = list.AddComponent<HorizontalLayoutGroup>();
        listLayout.padding = new RectOffset(0, 0, 0, 0);
        listLayout.spacing = CartCardGap;
        // 켜야 LayoutElement의 300x118이 실제 크기로 적용된다.
        // (끄면 카드 RectTransform 기본값 100x100으로 그려져 아트가 뭉개진다.)
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = false;
        listLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = list.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        // 비었을 때 안내 문구.
        GameObject empty = new GameObject("EmptyCart", typeof(RectTransform));
        empty.transform.SetParent(viewport.transform, false);
        RectTransform emptyRect = empty.GetComponent<RectTransform>();
        UiTheme.Stretch(emptyRect, 8f);
        Text emptyText = UiTheme.CreateText("Text", empty.transform, "위에서 교리를 눌러 담으세요.", 16,
            InkSoft, TextAnchor.MiddleCenter);
        UiTheme.Stretch(emptyText.rectTransform);
        emptyCartText = empty;

        // 총 코인 알약 (아트에 "총 코인" 글자와 코인이 박혀 있음).
        GameObject pill = UiTheme.CreateImage("TotalPill", bar.transform, Color.white);
        Image pillImage = pill.GetComponent<Image>();
        ApplySkin(pillImage, "Shop_TotalPill");
        pillImage.raycastTarget = false;
        RectTransform pillRect = pill.GetComponent<RectTransform>();
        pillRect.anchorMin = new Vector2(1f, 1f);
        pillRect.anchorMax = new Vector2(1f, 1f);
        pillRect.pivot = new Vector2(1f, 1f);
        pillRect.anchoredPosition = new Vector2(-18f, -10f);
        pillRect.sizeDelta = new Vector2(200f, 41f);

        Text total = UiTheme.CreateText("Total", pill.transform, "0", 20,
            PillText, TextAnchor.MiddleLeft);
        total.fontStyle = FontStyle.Bold;
        RectTransform totalRect = total.rectTransform;
        totalRect.anchorMin = Vector2.zero;
        totalRect.anchorMax = Vector2.one;
        totalRect.offsetMin = new Vector2(110f, 0f);
        totalRect.offsetMax = new Vector2(-10f, 0f);
        pillText = total;

        // 전체 구매 버튼 (아트에 "구매" 글자가 박혀 있음). 그대로 사용한다.
        GameObject buyObject = UiTheme.CreateImage("BuyButton", bar.transform, Color.white);
        buyImage = buyObject.GetComponent<Image>();
        ApplySkin(buyImage, "Shop_Button");
        buyImage.raycastTarget = true;
        RectTransform buyRect = buyObject.GetComponent<RectTransform>();
        buyRect.anchorMin = new Vector2(1f, 0f);
        buyRect.anchorMax = new Vector2(1f, 0f);
        buyRect.pivot = new Vector2(1f, 0f);
        buyRect.anchoredPosition = new Vector2(-18f, 10f);
        buyRect.sizeDelta = new Vector2(186f, 73f);

        Button button = buyObject.AddComponent<Button>();
        button.targetGraphic = buyImage;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock buyColors = button.colors;
        buyColors.normalColor = Color.white;
        buyColors.highlightedColor = new Color(1f, 0.97f, 0.93f, 1f);
        buyColors.pressedColor = new Color(0.93f, 0.89f, 0.84f, 1f);
        buyColors.disabledColor = BuyDisabledTint;
        buyColors.colorMultiplier = 1f;
        button.colors = buyColors;
        button.onClick.AddListener(() => BuyClicked?.Invoke());
        buyButton = button;
    }

    // ---------------- 구매 칸 표시 ----------------

    /// <summary>구매 칸을 통째로 다시 그린다. (Id 목록 순서대로, 중복 허용)</summary>
    public void SetCart(IReadOnlyList<string> ids, IReadOnlyList<string> names,
        IReadOnlyList<int> prices)
    {
        for (int i = 0; i < cartObjects.Count; i++)
        {
            if (cartObjects[i] != null)
            {
                cartObjects[i].SetActive(false);
                Destroy(cartObjects[i]);
            }
        }

        cartObjects.Clear();

        // 카드가 줄어든 뒤 스크롤이 빈 곳에 남지 않도록 왼쪽 끝으로 되돌린다.
        if (cartScroller != null) cartScroller.horizontalNormalizedPosition = 0f;

        bool empty = ids == null || ids.Count == 0;
        if (emptyCartText != null)
        {
            emptyCartText.SetActive(empty);
        }

        if (empty || names == null || prices == null)
        {
            return;
        }

        int count = Math.Min(ids.Count, Math.Min(names.Count, prices.Count));
        for (int i = 0; i < count; i++)
        {
            int index = i;
            GameObject card = UiTheme.CreateImage("Cart_" + i + "_" + ids[i], cartContent, Color.white);
            Image background = card.GetComponent<Image>();
            ApplySkin(background, "Shop_CardBlankSmall");
            background.raycastTarget = true;

            LayoutElement cardSize = card.AddComponent<LayoutElement>();
            cardSize.preferredWidth = CartCardWidth;
            cardSize.preferredHeight = CartCardHeight;
            cardSize.minWidth = CartCardWidth;
            cardSize.minHeight = CartCardHeight;

            ShopRarity rarity = RarityOf(ids[i]);

            // 교리 이름 + 등급 뱃지.
            GameObject titleRow = new GameObject("TitleRow", typeof(RectTransform));
            titleRow.transform.SetParent(card.transform, false);
            Place(titleRow.transform, 26f, 14f, 230f, 26f);
            HorizontalLayoutGroup titleLayout = titleRow.AddComponent<HorizontalLayoutGroup>();
            titleLayout.spacing = 8f;
            titleLayout.childAlignment = TextAnchor.MiddleLeft;
            titleLayout.childControlWidth = true;
            titleLayout.childControlHeight = true;
            titleLayout.childForceExpandWidth = false;
            titleLayout.childForceExpandHeight = false;

            Text nameText = UiTheme.CreateText("Name", titleRow.transform, names[i] ?? ids[i], 23,
                Ink, TextAnchor.MiddleLeft);
            nameText.fontStyle = FontStyle.Bold;
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            LayoutElement nameLayout = nameText.gameObject.AddComponent<LayoutElement>();
            nameLayout.preferredWidth = Mathf.Clamp(nameText.preferredWidth + 2f, 40f, 186f);
            nameLayout.preferredHeight = 26f;

            GameObject badge = UiTheme.CreateImage("Badge", titleRow.transform, Color.white);
            Image badgeImage = badge.GetComponent<Image>();
            ApplySkin(badgeImage, BadgeSkin(rarity));
            badgeImage.raycastTarget = false;
            LayoutElement badgeLayout = badge.AddComponent<LayoutElement>();
            badgeLayout.preferredWidth = 54f;
            badgeLayout.preferredHeight = 17f;

            // 코인 + 가격.
            GameObject coin = UiTheme.CreateImage("Coin", card.transform, Color.white);
            Image coinImage = coin.GetComponent<Image>();
            ApplySkin(coinImage, "Shop_Coin");
            coinImage.raycastTarget = false;
            Place(coinImage, 26f, 62f, 22f, 19f);

            Text priceText = UiTheme.CreateText("Price", card.transform,
                prices[i].ToString("N0"), 22, Ink, TextAnchor.MiddleLeft);
            priceText.fontStyle = FontStyle.Bold;
            Place(priceText, 54f, 58f, 110f, 26f);

            Button button = card.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = CardHoverTint;
            colors.pressedColor = CardPressedTint;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                // 가로 스크롤 드래그로 카드가 빠지지 않게 한다.
                if (cartGuard != null && cartGuard.ShouldIgnoreClick) return;
                CartClicked?.Invoke(index);
            });

            cartObjects.Add(card);
        }
    }

    /// <summary>구매 칸을 비운다.</summary>
    public void ClearCart()
    {
        SetCart(null, null, null);
    }

    /// <summary>총 코인 알약 표시 = 장바구니 합계. balance는 호환용으로만 받는다.</summary>
    public void SetTotal(long total, long balance)
    {
        lastTotal = total;
        lastBalance = balance;

        if (pillText != null)
        {
            pillText.font = UiTheme.GetFont();
            pillText.text = lastTotal.ToString("N0");
        }
    }

    /// <summary>구매 버튼 활성.</summary>
    public void SetBuyEnabled(bool value, string label = null)
    {
        if (buyButton != null)
        {
            buyButton.interactable = value;
        }

        if (buyImage != null)
        {
            buyImage.color = value ? Color.white : BuyDisabledTint;
        }
    }

    // 안내 줄은 삭제됨. ShopScreen의 기존 호출과 호환을 위해 no-op으로 유지한다.
    public void SetFooter(string message)
    {
    }

    /// <summary>교리 칸 수 안내 (별도 표시 없음, 호환용).</summary>
    public void SetSlotSummary(int currentCount)
    {
    }

    public void SetBuyEnabled(bool value)
    {
        SetBuyEnabled(value, "구매");
    }

    private void OnDestroy()
    {
        if (canvas != null && canvas.gameObject != null)
        {
            Destroy(canvas.gameObject);
        }
    }
}
