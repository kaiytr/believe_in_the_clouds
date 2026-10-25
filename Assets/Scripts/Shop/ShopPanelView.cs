using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 상점 패널.
//
// 레이아웃(제공된 사진):
//   ┌ 3열 × 4행 카드 그리드 ┐
//   1열 = 주어 / 2열 = 조건 / 3열 = 결과
//   각 카드 = [왼쪽 회색 아이콘 블록][오른쪽 글 영역]
//
// 동작 규칙:
// - 열마다 하나씩이 아니라 여러 장을 고를 수 있다 (장바구니).
// - 한 장만 골라도 구매 버튼이 켜진다.
// - 구매 시 교리로 활성화하지 않는다. 인벤토리에 쌓기만 한다.
//
// 표시만 담당한다. 장바구니/구매 요청은 ShopScreen이 한다.
public class ShopPanelView : MonoBehaviour
{
    private static readonly Color ColumnBackground = new Color(0.78f, 0.78f, 0.78f, 1f);
    private static readonly Color PanelBackground = new Color(0.86f, 0.86f, 0.86f, 1f);
    private static readonly Color FooterBackground = new Color(0.20f, 0.21f, 0.23f, 0.92f);
    private static readonly Color BuyEnabled = new Color(0.78f, 0.36f, 0.36f, 1f);
    private static readonly Color BuyDisabled = new Color(0.42f, 0.43f, 0.45f, 1f);

    private Canvas canvas;
    private GameObject content;
    private Text titleText;
    private Text footerText;
    private Text selectionText;
    private Button buyButton;
    private Text buyLabel;
    private bool isOpen;

    private readonly List<ShopCardView> subjectCards = new List<ShopCardView>();
    private readonly List<ShopCardView> conditionCards = new List<ShopCardView>();
    private readonly List<ShopCardView> resultCards = new List<ShopCardView>();

    public bool IsOpen => isOpen;

    /// <summary>카드를 눌렀을 때. (열, 해당 열 안의 인덱스)</summary>
    public event Action<ShopColumn, int> CardClicked;

    /// <summary>구매 버튼을 눌렀을 때.</summary>
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

        // 사진의 비율(1088 × 624 ≈ 1.74 : 1)에 맞춘 패널.
        GameObject panel = UiTheme.CreateImage("Panel", content.transform, PanelBackground);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(1090f, 700f);

        Text title = UiTheme.CreateText("Title", panel.transform, "상점", 30,
            UiTheme.TextDark, TextAnchor.MiddleCenter);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -22f);
        titleRect.sizeDelta = new Vector2(0f, 48f);

        // 3열 그리드 영역.
        GameObject grid = new GameObject("Grid", typeof(RectTransform));
        grid.transform.SetParent(panel.transform, false);
        RectTransform gridRect = grid.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0f, 0f);
        gridRect.anchorMax = new Vector2(1f, 1f);
        gridRect.offsetMin = new Vector2(18f, 84f);
        gridRect.offsetMax = new Vector2(-18f, -66f);

        HorizontalLayoutGroup gridLayout = grid.AddComponent<HorizontalLayoutGroup>();
        gridLayout.padding = new RectOffset(0, 0, 0, 0);
        gridLayout.spacing = 16;
        gridLayout.childControlWidth = true;
        gridLayout.childControlHeight = true;
        gridLayout.childForceExpandWidth = true;
        gridLayout.childForceExpandHeight = true;

        ShopPanelView view = root.AddComponent<ShopPanelView>();
        view.canvas = canvas;
        view.content = content;
        view.titleText = title;

        view.subjectCards.AddRange(view.BuildColumn(grid.transform, "주어", ShopColumn.Subject));
        view.conditionCards.AddRange(view.BuildColumn(grid.transform, "조건", ShopColumn.Condition));
        view.resultCards.AddRange(view.BuildColumn(grid.transform, "결과", ShopColumn.Result));

        view.BuildFooter(panel.transform);

        view.ClearSelection();
        view.SetBuyEnabled(false);
        content.SetActive(false);
        return view;
    }

    // 한 열 = 회색 배경 박스 + 세로로 4장 카드.
    private List<ShopCardView> BuildColumn(Transform parent, string columnName, ShopColumn column)
    {
        GameObject columnObject = UiTheme.CreateImage("Column_" + columnName, parent, ColumnBackground);
        columnObject.GetComponent<Image>().raycastTarget = false;

        // 사진처럼 열 안쪽에 여백을 두고 카드를 배치한다.
        VerticalLayoutGroup columnLayout = columnObject.AddComponent<VerticalLayoutGroup>();
        columnLayout.padding = new RectOffset(12, 12, 12, 12);
        columnLayout.spacing = 12;
        columnLayout.childControlWidth = true;
        columnLayout.childControlHeight = true;
        columnLayout.childForceExpandWidth = true;
        columnLayout.childForceExpandHeight = true;

        List<ShopCardView> cards = new List<ShopCardView>();
        IReadOnlyList<ShopEntry> entries = ShopCatalog.GetColumn(column);

        for (int i = 0; i < entries.Count; i++)
        {
            ShopCardView card = ShopCardView.BuildRuntime(columnObject.transform, entries[i]);
            int index = i;
            card.Clicked += _ => CardClicked?.Invoke(column, index);
            cards.Add(card);
        }

        return cards;
    }

    private void BuildFooter(Transform parent)
    {
        GameObject footer = UiTheme.CreateImage("Footer", parent, FooterBackground);
        RectTransform footerRect = footer.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = new Vector2(0f, 16f);
        footerRect.sizeDelta = new Vector2(-36f, 48f);

        Text footerText = UiTheme.CreateText("FooterText", footer.transform,
            "각 열에서 하나씩 선택하세요.", 17, Color.white, TextAnchor.MiddleLeft);
        RectTransform textRect = footerText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0.5f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(16f, 0f);
        textRect.offsetMax = new Vector2(-136f, -2f);
        footerText.horizontalOverflow = HorizontalWrapMode.Overflow;
        footerText.verticalOverflow = VerticalWrapMode.Truncate;
        this.footerText = footerText;

        // 선택 현황 줄: 플레이어 + 피격 → 회복
        Text selectionText = UiTheme.CreateText("SelectionText", footer.transform, "—  +  —  →  —",
            16, new Color(1f, 0.78f, 0.78f, 1f), TextAnchor.MiddleLeft);
        RectTransform selectionRect = selectionText.rectTransform;
        selectionRect.anchorMin = new Vector2(0f, 0f);
        selectionRect.anchorMax = new Vector2(1f, 0.5f);
        selectionRect.offsetMin = new Vector2(16f, 2f);
        selectionRect.offsetMax = new Vector2(-136f, 0f);
        selectionText.horizontalOverflow = HorizontalWrapMode.Overflow;
        selectionText.verticalOverflow = VerticalWrapMode.Truncate;
        this.selectionText = selectionText;

        // 구매 버튼 (오른쪽).
        GameObject buyObject = UiTheme.CreateImage("BuyButton", footer.transform, BuyDisabled);
        buyObject.GetComponent<Image>().raycastTarget = true;
        RectTransform buyRect = buyObject.GetComponent<RectTransform>();
        buyRect.anchorMin = new Vector2(1f, 0.5f);
        buyRect.anchorMax = new Vector2(1f, 0.5f);
        buyRect.pivot = new Vector2(1f, 0.5f);
        buyRect.anchoredPosition = new Vector2(-8f, 0f);
        buyRect.sizeDelta = new Vector2(112f, 36f);

        Text buyLabel = UiTheme.CreateText("BuyLabel", buyObject.transform, "구매", 17,
            Color.white, TextAnchor.MiddleCenter);
        UiTheme.Stretch(buyLabel.rectTransform, 2f);

        Button buyButton = buyObject.AddComponent<Button>();
        buyButton.targetGraphic = buyObject.GetComponent<Image>();
        buyButton.onClick.AddListener(() => BuyClicked?.Invoke());

        this.buyButton = buyButton;
        this.buyLabel = buyLabel;
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

        if (buyLabel != null)
        {
            buyLabel.font = UiTheme.GetFont();
        }

        if (selectionText != null)
        {
            selectionText.font = UiTheme.GetFont();
        }

        ClearSelection();

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

    /// <summary>선택 상태를 모두 지운다.</summary>
    public void ClearSelection()
    {
        ClearCards(subjectCards);
        ClearCards(conditionCards);
        ClearCards(resultCards);
        SetFooter("카드를 한 장 이상 고르세요.");
        SetSelectionSummary(null, null, null);
        SetBuyEnabled(false);
    }

    /// <summary>지정한 카드만 선택 표시한다. (여러 장 동시 선택용)</summary>
    public void AddSelection(ShopColumn column, int index)
    {
        List<ShopCardView> cards = GetCards(column);
        if (index >= 0 && index < cards.Count)
        {
            cards[index].SetSelected(true);
        }
    }

    /// <summary>지정한 카드의 선택 표시를 끈다.</summary>
    public void RemoveSelection(ShopColumn column, int index)
    {
        List<ShopCardView> cards = GetCards(column);
        if (index >= 0 && index < cards.Count)
        {
            cards[index].SetSelected(false);
        }
    }

    /// <summary>열 안에서 하나만 선택되도록 표시한다. (단일 선택이 필요한 곳용)</summary>
    public void SetSelection(ShopColumn column, int index)
    {
        List<ShopCardView> cards = GetCards(column);
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].SetSelected(i == index);
        }
    }

    public void SetFooter(string message)
    {
        if (footerText != null)
        {
            footerText.text = message;
        }
    }

    /// <summary>하단에 현재 선택 현황을 요약해 보여준다.</summary>
    public void SetSelectionSummary(string subject, string condition, string result)
    {
        string pick = Format(subject) + "  +  " + Format(condition) + "  →  " + Format(result);
        selectionText.text = pick;
    }

    private static string Format(string value)
    {
        return string.IsNullOrEmpty(value) ? "—" : value;
    }

    public void SetBuyEnabled(bool value)
    {
        if (buyButton != null)
        {
            buyButton.interactable = value;

            if (buyButton.targetGraphic != null)
            {
                buyButton.targetGraphic.color = value ? BuyEnabled : BuyDisabled;
            }
        }
    }

    private List<ShopCardView> GetCards(ShopColumn column)
    {
        switch (column)
        {
            case ShopColumn.Subject: return subjectCards;
            case ShopColumn.Condition: return conditionCards;
            default: return resultCards;
        }
    }

    private void ClearCards(List<ShopCardView> cards)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].SetSelected(false);
        }
    }

    private void OnDestroy()
    {
        if (canvas != null && canvas.gameObject != null)
        {
            Destroy(canvas.gameObject);
        }
    }
}
