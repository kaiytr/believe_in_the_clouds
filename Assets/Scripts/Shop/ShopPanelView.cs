using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 상점 패널 (장바구니형).
//
// 레이아웃(제공된 사진):
//   ┌ 위: 교리 나열 칸 (3열 그리드, 세로 스크롤) ──────────┐
//   │ 카드 × 9 보임 · 교리 12종 + 교리칸 1종 = 13줄 표시    │
//   └ 아래: 구매 칸 (가로 스크롤) + 총 코인 + 구매 버튼 ────┘
//
// 동작 규칙:
// - 교리 나열 칸: 카드 클릭 → 구매 칸에 1개 추가 (중복 가능, 개수 제한 없음).
// - 구매 칸: 카드 클릭 → 그 1개가 빠진다. 좌우 스크롤로 모두 볼 수 있다.
// - 구매 버튼: 구매 칸 전체를 TryPurchase 1건으로 결제. 실패 시 칸 유지.
// - 등급은 지금 신경 쓰지 않는다 (표시만 유지).
//
// 표시만 담당한다. 장바구니/구매 요청은 ShopScreen이 한다.
public class ShopPanelView : MonoBehaviour
{
    private static readonly Color PanelBackground = new Color(0.86f, 0.86f, 0.86f, 1f);
    private static readonly Color ListBackground = new Color(0.71f, 0.71f, 0.71f, 1f);
    private static readonly Color CartBackground = new Color(0.78f, 0.76f, 0.72f, 1f);
    private static readonly Color FooterBackground = new Color(0.20f, 0.21f, 0.23f, 0.92f);
    private static readonly Color BuyEnabled = new Color(0.78f, 0.36f, 0.36f, 1f);
    private static readonly Color BuyDisabled = new Color(0.42f, 0.43f, 0.45f, 1f);

    // 교리칸 1칸 가격. ShopScreen.SlotPrice와 같은 값으로 유지한다.
    internal const int SlotPrice = 100;

    private Canvas canvas;
    private GameObject content;
    private Text titleText;
    private Text footerText;
    private Text totalText;
    private RectTransform catalogContent;
    private RectTransform cartContent;
    private ScrollRect cartScroller;
    private ScrollDragGuard cartGuard;
    private Button buyButton;
    private Text buyLabel;
    private GameObject emptyCartText;
    private bool isOpen;

    // 총 코인 줄에 함께 보여줄 교리칸 수.
    private int slotCount;
    private long lastTotal;
    private long lastBalance;

    // 카탈로그 카드 → 항목 식별자. 교리칸 항목은 "slot"이다.
    private readonly Dictionary<GameObject, string> catalogObjectToId = new Dictionary<GameObject, string>();

    // 구매 칸 카드 오브젝트 (담은 순서대로).
    private readonly List<GameObject> cartObjects = new List<GameObject>();

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

        // 사진 비율(1348 × 796 ≈ 1.69 : 1).
        GameObject panel = UiTheme.CreateImage("Panel", content.transform, PanelBackground);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(1240f, 732f);

        Text title = UiTheme.CreateText("Title", panel.transform, "상점", 22,
            UiTheme.TextDark, TextAnchor.MiddleCenter);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -10f);
        titleRect.sizeDelta = new Vector2(0f, 36f);

        ShopPanelView view = root.AddComponent<ShopPanelView>();
        view.canvas = canvas;
        view.content = content;
        view.titleText = title;

        // 위: 교리 나열 칸 (세로 스크롤).
        view.catalogContent = view.BuildCatalogGrid(panel.transform);
        // 아래: 구매 칸 (가로 스크롤) + 총 코인 + 구매 버튼.
        view.BuildCartBar(panel.transform);
        view.BuildFooter(panel.transform);

        view.ClearCart();
        view.SetBuyEnabled(false);
        content.SetActive(false);
        return view;
    }

    public void Open(string shopName)
    {
        isOpen = true;

        if (titleText != null)
        {
            titleText.font = UiTheme.GetFont();
            titleText.text = string.IsNullOrEmpty(shopName) ? "상점" : shopName;
        }

        if (footerText != null)
        {
            footerText.font = UiTheme.GetFont();
        }

        if (totalText != null)
        {
            totalText.font = UiTheme.GetFont();
        }

        if (buyLabel != null)
        {
            buyLabel.font = UiTheme.GetFont();
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

    // ---------------- 위: 교리 나열 칸 (3열, 세로 스크롤) ----------------

    private RectTransform BuildCatalogGrid(Transform parent)
    {
        GameObject area = new GameObject("CatalogArea", typeof(RectTransform));
        area.transform.SetParent(parent, false);
        RectTransform areaRect = area.GetComponent<RectTransform>();
        areaRect.anchorMin = new Vector2(0f, 0f);
        areaRect.anchorMax = new Vector2(1f, 1f);
        // 사진: 위 62px(테두리) ~ 아래 205px(구매 칸 + 푸터) 사이가 나열 칸. 높이 465.
        areaRect.offsetMin = new Vector2(62f, 205f);
        areaRect.offsetMax = new Vector2(-62f, -62f);

        GameObject scroll = new GameObject("CatalogScroll", typeof(RectTransform));
        scroll.transform.SetParent(area.transform, false);
        RectTransform scrollRect = scroll.GetComponent<RectTransform>();
        UiTheme.Stretch(scrollRect);

        ScrollRect scroller = scroll.AddComponent<ScrollRect>();
        scroller.horizontal = false;
        scroller.vertical = true;
        scroller.movementType = ScrollRect.MovementType.Clamped;
        scroller.scrollSensitivity = 40f;
        // 카드 위에서 드래그로 스크롤할 때 카드가 클릭(담기)되지 않게 한다.
        ScrollDragGuard guard = ScrollDragGuard.Attach(scroller);

        // 뷰포트가 클릭(레이캐스트)을 받아야 휠/드래그가 스크롤로 전달된다.
        GameObject viewport = UiTheme.CreateImage("Viewport", scroll.transform, ListBackground);
        viewport.GetComponent<Image>().raycastTarget = true;
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        UiTheme.Stretch(viewportRect);
        viewport.AddComponent<Mask>().showMaskGraphic = true;
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

        // 한 화면에 3×3 = 9개가 딱 맞게 보인다. (가로 1116, 세로 465, 간격 8)
        GridLayoutGroup grid = list.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.cellSize = new Vector2(366f, 149f);
        grid.spacing = new Vector2(8f, 8f);
        grid.padding = new RectOffset(0, 0, 0, 0);
        grid.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter fitter = list.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // 세로 3줄 = 왼쪽 주어 / 가운데 조건 / 오른쪽 결과 순으로 나열한다.
        // Grid는 가로 방향으로 채우므로, 한 행씩 [주어, 조건, 결과] 묶음으로 넣는다.
        // 교리칸 구매 항목은 마지막 자리 유지.
        IReadOnlyList<ShopEntry> subjects = ShopCatalog.GetColumn(ShopColumn.Subject);
        IReadOnlyList<ShopEntry> conditions = ShopCatalog.GetColumn(ShopColumn.Condition);
        IReadOnlyList<ShopEntry> results = ShopCatalog.GetColumn(ShopColumn.Result);
        int rows = Math.Max(subjects.Count, Math.Max(conditions.Count, results.Count));

        catalogObjectToId.Clear();
        for (int row = 0; row < rows; row++)
        {
            if (row < subjects.Count)
            {
                ShopEntry entry = subjects[row];
                BuildCatalogCard(list.transform, entry.Label, entry.Detail,
                    entry.Price, entry.Id, catalogObjectToId, guard);
            }

            if (row < conditions.Count)
            {
                ShopEntry entry = conditions[row];
                BuildCatalogCard(list.transform, entry.Label, entry.Detail,
                    entry.Price, entry.Id, catalogObjectToId, guard);
            }

            if (row < results.Count)
            {
                ShopEntry entry = results[row];
                BuildCatalogCard(list.transform, entry.Label, entry.Detail,
                    entry.Price, entry.Id, catalogObjectToId, guard);
            }
        }

        // 교리칸 구매 항목 (마지막 자리 유지).
        BuildCatalogCard(list.transform, "교리칸", "재단 교리 +1칸",
            SlotPrice, "slot", catalogObjectToId, guard);

        return listRect;
    }

    private GameObject BuildCatalogCard(Transform parent, string name, string detail,
        int price, string id, Dictionary<GameObject, string> map, ScrollDragGuard guard)
    {
        GameObject card = UiTheme.CreateImage("Catalog_" + id, parent, Color.white);
        Image background = card.GetComponent<Image>();
        background.raycastTarget = true;

        // 사진의 카드 구성: 위 [이름 + 등급] / 아래 [설명 + 구매 버튼 자리].
        // 등급은 지금 신경 쓰지 않으므로 뱃지 없이 이름 + 가격만 둔다.
        VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 6;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        GameObject top = new GameObject("Top", typeof(RectTransform));
        top.transform.SetParent(card.transform, false);
        HorizontalLayoutGroup topLayout = top.AddComponent<HorizontalLayoutGroup>();
        topLayout.spacing = 8;
        topLayout.childControlWidth = true;
        topLayout.childControlHeight = true;
        topLayout.childForceExpandWidth = true;
        topLayout.childForceExpandHeight = true;

        Text nameText = UiTheme.CreateText("Name", top.transform, name, 20,
            UiTheme.TextDark, TextAnchor.MiddleLeft);
        LayoutElement nameLayout = nameText.gameObject.AddComponent<LayoutElement>();
        nameLayout.flexibleWidth = 2f;

        Text priceText = UiTheme.CreateText("Price", top.transform, $"● {price:N0}", 18,
            UiTheme.TextDark, TextAnchor.MiddleRight);
        LayoutElement priceLayout = priceText.gameObject.AddComponent<LayoutElement>();
        priceLayout.flexibleWidth = 1f;

        GameObject bottom = new GameObject("Bottom", typeof(RectTransform));
        bottom.transform.SetParent(card.transform, false);
        HorizontalLayoutGroup bottomLayout = bottom.AddComponent<HorizontalLayoutGroup>();
        bottomLayout.spacing = 8;
        bottomLayout.childControlWidth = true;
        bottomLayout.childControlHeight = true;
        bottomLayout.childForceExpandWidth = true;
        bottomLayout.childForceExpandHeight = true;

        Text detailText = UiTheme.CreateText("Detail", bottom.transform, detail, 14,
            UiTheme.TextMuted, TextAnchor.MiddleLeft);
        LayoutElement detailLayout = detailText.gameObject.AddComponent<LayoutElement>();
        detailLayout.flexibleWidth = 2f;

        Text buyText = UiTheme.CreateText("Buy", bottom.transform, "담기", 16,
            new Color(0.78f, 0.36f, 0.36f, 1f), TextAnchor.MiddleCenter);
        LayoutElement buyLayout = buyText.gameObject.AddComponent<LayoutElement>();
        buyLayout.flexibleWidth = 1f;

        Button button = card.AddComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        map[card] = id;
        button.onClick.AddListener(() =>
        {
            if (guard != null && guard.ShouldIgnoreClick) return;
            CatalogClicked?.Invoke(id);
        });

        return card;
    }

    // ---------------- 아래: 구매 칸 (가로 스크롤) ----------------

    private void BuildCartBar(Transform parent)
    {
        GameObject bar = UiTheme.CreateImage("CartBar", parent, CartBackground);
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0f, 58f);
        barRect.sizeDelta = new Vector2(-124f, 132f);

        // 왼쪽: 가로 스크롤 구매 목록. 카드 3개(270 × 3 + 간격 16 × 2 + 여백 8)가 보인다.
        GameObject scroll = new GameObject("CartScroll", typeof(RectTransform));
        scroll.transform.SetParent(bar.transform, false);
        RectTransform scrollRect = scroll.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(0f, 1f);
        scrollRect.pivot = new Vector2(0f, 0.5f);
        scrollRect.anchoredPosition = new Vector2(12f, 0f);
        scrollRect.sizeDelta = new Vector2(850f, -24f);

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
        // Mask가 Graphic을 요구하므로 Image를 먼저 붙인다.
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImage.raycastTarget = true;
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        scroller.viewport = viewportRect;
        cartScroller = scroller;
        cartGuard = ScrollDragGuard.Attach(scroller);

        GameObject list = new GameObject("CartList", typeof(RectTransform));
        list.transform.SetParent(viewport.transform, false);
        RectTransform listRect = list.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 0f);
        listRect.anchorMax = new Vector2(0f, 1f);
        listRect.pivot = new Vector2(0f, 0.5f);
        listRect.anchoredPosition = Vector2.zero;
        listRect.sizeDelta = new Vector2(0f, 0f);
        scroller.content = listRect;
        cartContent = listRect;

        HorizontalLayoutGroup listLayout = list.AddComponent<HorizontalLayoutGroup>();
        listLayout.padding = new RectOffset(4, 4, 0, 0);
        listLayout.spacing = 16;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = false;
        listLayout.childForceExpandHeight = true;

        ContentSizeFitter fitter = list.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        // 비었을 때 안내 문구.
        GameObject empty = new GameObject("EmptyCart", typeof(RectTransform));
        empty.transform.SetParent(viewport.transform, false);
        RectTransform emptyRect = empty.GetComponent<RectTransform>();
        UiTheme.Stretch(emptyRect, 12f);
        Text emptyText = UiTheme.CreateText("Text", empty.transform, "위에서 교리를 눌러 담으세요.", 17,
            UiTheme.TextMuted, TextAnchor.MiddleCenter);
        UiTheme.Stretch(emptyText.rectTransform);
        emptyCartText = empty;

        // 오른쪽: 총 코인 + 구매 버튼.
        Text total = UiTheme.CreateText("Total", bar.transform, "총 코인 ● 0", 15,
            UiTheme.TextDark, TextAnchor.MiddleRight);
        RectTransform totalRect = total.rectTransform;
        totalRect.anchorMin = new Vector2(1f, 1f);
        totalRect.anchorMax = new Vector2(1f, 1f);
        totalRect.pivot = new Vector2(1f, 1f);
        totalRect.anchoredPosition = new Vector2(-10f, -6f);
        totalRect.sizeDelta = new Vector2(240f, 40f);
        totalText = total;

        GameObject buyObject = UiTheme.CreateImage("BuyButton", bar.transform, BuyDisabled);
        buyObject.GetComponent<Image>().raycastTarget = true;
        RectTransform buyRect = buyObject.GetComponent<RectTransform>();
        buyRect.anchorMin = new Vector2(1f, 0f);
        buyRect.anchorMax = new Vector2(1f, 0f);
        buyRect.pivot = new Vector2(1f, 0f);
        buyRect.anchoredPosition = new Vector2(-12f, 12f);
        buyRect.sizeDelta = new Vector2(230f, 72f);

        Text label = UiTheme.CreateText("Label", buyObject.transform, "구매", 24,
            Color.white, TextAnchor.MiddleCenter);
        UiTheme.Stretch(label.rectTransform, 2f);
        buyLabel = label;

        Button button = buyObject.AddComponent<Button>();
        button.targetGraphic = buyObject.GetComponent<Image>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => BuyClicked?.Invoke());
        buyButton = button;
    }

    private void BuildFooter(Transform parent)
    {
        GameObject footer = UiTheme.CreateImage("Footer", parent, FooterBackground);
        RectTransform footerRect = footer.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = new Vector2(0f, 14f);
        footerRect.sizeDelta = new Vector2(-124f, 36f);

        Text footerText = UiTheme.CreateText("FooterText", footer.transform,
            "교리를 눌러 담으세요.", 17, Color.white, TextAnchor.MiddleLeft);
        RectTransform textRect = footerText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(16f, 0f);
        textRect.offsetMax = new Vector2(-16f, 0f);
        footerText.horizontalOverflow = HorizontalWrapMode.Wrap;
        footerText.verticalOverflow = VerticalWrapMode.Truncate;
        this.footerText = footerText;
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
            background.raycastTarget = true;

            // 구매 칸은 카드 3개가 보이도록 너비 270 고정.
            LayoutElement cardSize = card.AddComponent<LayoutElement>();
            cardSize.preferredWidth = 270f;
            cardSize.minWidth = 270f;

            VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 4;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            Text nameText = UiTheme.CreateText("Name", card.transform, names[i] ?? ids[i], 18,
                UiTheme.TextDark, TextAnchor.MiddleCenter);
            nameText.font = UiTheme.GetFont();

            Text priceText = UiTheme.CreateText("Price", card.transform, $"● {prices[i]:N0}", 16,
                UiTheme.TextDark, TextAnchor.MiddleCenter);
            priceText.font = UiTheme.GetFont();

            Button button = card.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
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

    /// <summary>총 코인(구매 칸 합계 + 보유/부족) 표시.</summary>
    public void SetTotal(long total, long balance)
    {
        lastTotal = total;
        lastBalance = balance;
        RefreshTotalText();
    }

    private void RefreshTotalText()
    {
        if (totalText == null)
        {
            return;
        }

        totalText.font = UiTheme.GetFont();
        string second = slotCount > 0 ? $"합계 ● {lastTotal:N0} · 교리칸 {slotCount}칸" : $"합계 ● {lastTotal:N0}";
        totalText.text = $"총 코인 ● {lastBalance:N0}\n{second}";
    }

    /// <summary>구매 버튼 활성/라벨.</summary>
    public void SetBuyEnabled(bool value, string label = null)
    {
        if (buyButton != null)
        {
            buyButton.interactable = value;

            if (buyButton.targetGraphic != null)
            {
                buyButton.targetGraphic.color = value ? BuyEnabled : BuyDisabled;
            }
        }

        if (buyLabel != null)
        {
            buyLabel.font = UiTheme.GetFont();
            if (label != null)
            {
                buyLabel.text = label;
            }
        }
    }

    public void SetFooter(string message)
    {
        if (footerText != null)
        {
            footerText.text = message;
        }
    }

    /// <summary>교리 칸 수 안내. (구매 칸 안에 교리칸 항목이 포함되므로 별도 버튼 없음)</summary>
    public void SetSlotSummary(int currentCount)
    {
        slotCount = currentCount;
        RefreshTotalText();
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
