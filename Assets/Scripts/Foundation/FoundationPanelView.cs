using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 재단 패널.
//
// 레이아웃:
//   ┌ 왼쪽 ──────────┬ 오른쪽 ─────────────────────┐
//   │ [주어][조건][결과] │ 교리1 [주어][조건][결과]        │
//   │ ┌ 교리 카드 ┐     │       [발동 효과 설명]          │
//   │ ├ 교리 카드 ┤     │ 교리2 [주어][조건][결과]        │
//   │ └ 교리 카드 ┘     │       ... (칸 수만큼 스크롤)    │
//   └───────────────┴──────────────────────────────┘
//
// 왼쪽: 탭(주어/조건/결과)으로 인벤토리 조각을 골라 보고,
//   카드를 교리 슬롯으로 드래그 앤 드롭해서 배치한다. 장착은 드래그로만 된다.
// 오른쪽: 교리 슬롯들은 언제든 끼고 뺄 수 있고, 3칸이 차면 자동 장착된다.
// 칸 수는 상점 구매에 따라 늘어나고, 많아지면 스크롤된다.
// 표시만 담당하고, 어떤 카드를 보일지는 FoundationScreen이 정한다.
public class FoundationPanelView : MonoBehaviour
{
    // 왼쪽 목록 한 칸에 표시할 데이터.
    public struct ItemData
    {
        public int EntryIndex;
        public string Label;
        public string Detail;
        public int Count;
    }

    private static readonly Color PanelBackground = new Color(0.93f, 0.88f, 0.80f, 1f);
    private static readonly Color PanelBorder = new Color(0.36f, 0.23f, 0.13f, 1f);
    private static readonly Color LeftBackground = new Color(0.78f, 0.64f, 0.47f, 1f);
    private static readonly Color ListBackground = new Color(0.93f, 0.88f, 0.80f, 1f);
    private static readonly Color TabNormal = new Color(0.55f, 0.42f, 0.30f, 1f);
    private static readonly Color TabActive = new Color(0.95f, 0.90f, 0.81f, 1f);
    private static readonly Color TabTextActive = new Color(0.24f, 0.15f, 0.08f, 1f);
    private static readonly Color CardNormal = new Color(0.95f, 0.90f, 0.81f, 1f);
    private static readonly Color CardHover = new Color(0.98f, 0.94f, 0.87f, 1f);
    private static readonly Color CardSelected = new Color(0.90f, 0.78f, 0.60f, 1f);
    private static readonly Color GroupBackground = new Color(0.78f, 0.64f, 0.47f, 1f);
    private static readonly Color SlotBackground = new Color(0.95f, 0.90f, 0.81f, 1f);
    private static readonly Color SlotBorder = new Color(0.55f, 0.42f, 0.30f, 1f);
    private static readonly Color DescriptionBackground = new Color(0.95f, 0.90f, 0.81f, 1f);

    // 잠긴 교리 칸: 안쪽 칸은 살짝 어둡게, 자물쇠 없이 비활성화만 한다.
    private static readonly Color LockedSlotBackground = new Color(0.83f, 0.76f, 0.64f, 1f);

    private const int InitialDoctrineGroups = 3; // 처음에 보여주는 교리 수 (열린 1 + 잠긴 2)

    private Canvas canvas;
    private GameObject content;
    private RectTransform listRoot;
    private Text emptyText;
    private Text countText;
    private bool isOpen;

    // 실제 열린 교리 수 (기본 1개). 표시는 초기 3칸을 유지하고, 열린 칸 뒤는
    // 같은 그룹 UI로 보이되 입력을 받지 않는다. (상점 구매 시 열린 칸이 늘어난다.)
    private Transform rightContent;
    private readonly List<GameObject> doctrineGroups = new List<GameObject>();
    private readonly List<CanvasGroup> doctrineGroupFades = new List<CanvasGroup>();

    // 실제 열린 교리 수. 이 뒤의 칸은 표시만 되고 입력을 받지 않는다.
    private int unlockedCount = 1;

    private readonly Dictionary<ShopColumn, Image> tabImages = new Dictionary<ShopColumn, Image>();
    private readonly Dictionary<ShopColumn, Button> tabButtons = new Dictionary<ShopColumn, Button>();
    private readonly List<GameObject> itemObjects = new List<GameObject>();

    // 오른쪽 교리 슬롯 (드래그 앤 드롭 전용). 교리 번호(0-based)별로 보관한다.
    private readonly List<Dictionary<ShopColumn, Text>> slotTexts = new List<Dictionary<ShopColumn, Text>>();
    private readonly List<Dictionary<ShopColumn, Image>> slotImages = new List<Dictionary<ShopColumn, Image>>();
    private readonly List<Dictionary<ShopColumn, GameObject>> slotObjects = new List<Dictionary<ShopColumn, GameObject>>();

    // 교리별 설명 줄.
    private readonly List<Text> doctrineDescriptions = new List<Text>();

    // 왼쪽 카드 오브젝트 → EntryIndex. 드래그 시작 시 어느 조각인지 찾는다.
    private readonly Dictionary<GameObject, int> itemObjectToEntry = new Dictionary<GameObject, int>();

    public bool IsOpen => isOpen;

    /// <summary>탭을 눌렀을 때.</summary>
    public event Action<ShopColumn> TabClicked;


    // 왼쪽 카드를 교리 슬롯에 드래그해서 놓았을 때. (EntryIndex, 교리 index, 슬롯 열)
    public event Action<int, int, ShopColumn> ItemDroppedOnSlot;

    public event Action<int, ShopColumn, int, ShopColumn> SlotDroppedOnSlot;

    public event Action<int, ShopColumn> SlotDroppedOutside;

    public static FoundationPanelView BuildRuntime(int doctrineCount)
    {
        doctrineCount = Mathf.Max(1, doctrineCount);

        GameObject root = new GameObject("[FoundationPanel]");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(root.transform, false);
        UiTheme.Stretch(content.GetComponent<RectTransform>());

        GameObject blocker = UiTheme.CreateImage("Blocker", content.transform,
            new Color(0f, 0f, 0f, 0.55f));
        UiTheme.Stretch(blocker.GetComponent<RectTransform>());
        blocker.GetComponent<Image>().raycastTarget = true;

        // 상점과 같은 패널 크기(1240 × 732) + 진한 브라운 테두리 4px.
        GameObject border = UiTheme.CreateImage("PanelBorder", content.transform, PanelBorder);
        RectTransform borderRect = border.GetComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0.5f, 0.5f);
        borderRect.anchorMax = new Vector2(0.5f, 0.5f);
        borderRect.pivot = new Vector2(0.5f, 0.5f);
        borderRect.anchoredPosition = Vector2.zero;
        borderRect.sizeDelta = new Vector2(1248f, 740f);

        GameObject panel = UiTheme.CreateImage("Panel", border.transform, PanelBackground);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        UiTheme.Stretch(panelRect, 4f);

        FoundationPanelView view = root.AddComponent<FoundationPanelView>();
        view.canvas = canvas;
        view.content = content;

        view.BuildLeft(panel.transform);
        view.BuildRight(panel.transform, doctrineCount);

        content.SetActive(false);
        return view;
    }

    /// <summary>오른쪽 교리 칸 개수를 맞춘다. 표시용 3칸 중 열린 칸만 활성화한다.</summary>
    public void SetDoctrineCount(int count)
    {
        SetUnlockedCount(count);
    }

    /// <summary>열린 교리 수. 항상 뒤에 잠긴 칸 1개를 붙여 보여주고, 남는 칸은 비활성화한다.</summary>
    public void SetUnlockedCount(int unlocked)
    {
        if (rightContent == null)
        {
            return;
        }

        unlocked = Mathf.Max(1, unlocked);

        // 항상 열린 칸 뒤에 잠긴 칸 1개를 둔다.
        // (열린 1 + 잠긴 2 = 3칸 → 구매 시 열린 2 + 잠긴 1 = 3칸 → 전부 해금되면 열린 3 + 잠긴 1 = 4칸)
        int visible = Mathf.Max(unlocked + 1, InitialDoctrineGroups);
        while (doctrineGroups.Count < visible)
        {
            BuildDoctrineGroup(rightContent, doctrineGroups.Count);
        }

        unlockedCount = unlocked;

        for (int i = 0; i < doctrineGroups.Count; i++)
        {
            bool isUnlocked = i < unlocked;
            bool isVisible = i < visible;
            if (doctrineGroups[i] != null)
            {
                doctrineGroups[i].SetActive(isVisible);
            }

            if (i < doctrineGroupFades.Count && doctrineGroupFades[i] != null)
            {
                doctrineGroupFades[i].alpha = isUnlocked ? 1f : 0.55f;
                doctrineGroupFades[i].interactable = isUnlocked;
                doctrineGroupFades[i].blocksRaycasts = isUnlocked;
            }
        }

        // 잠긴 칸의 설명은 안내 문구를 지워 비활성 느낌만 남긴다.
        for (int i = 0; i < doctrineDescriptions.Count; i++)
        {
            Text description = doctrineDescriptions[i];
            if (description != null && !IsUnlocked(i))
            {
                description.font = UiTheme.GetFont();
                description.text = string.Empty;
            }
        }

        RefreshSlotColors();
    }

    // ---------------- 왼쪽: 탭 + 교리 목록 (기능 있음) ----------------

    private void BuildLeft(Transform parent)
    {
        GameObject left = UiTheme.CreateImage("Left", parent, LeftBackground);
        RectTransform leftRect = left.GetComponent<RectTransform>();
        leftRect.anchorMin = new Vector2(0f, 0f);
        leftRect.anchorMax = new Vector2(0f, 1f);
        leftRect.pivot = new Vector2(0f, 0.5f);
        leftRect.anchoredPosition = Vector2.zero;
        leftRect.sizeDelta = new Vector2(390f, 0f);

        // 사진처럼 탭은 왼쪽 목록 박스 위에 걸치게 둔다.
        GameObject tabRow = new GameObject("Tabs", typeof(RectTransform));
        tabRow.transform.SetParent(left.transform, false);
        RectTransform tabRect = tabRow.GetComponent<RectTransform>();
        tabRect.anchorMin = new Vector2(0f, 1f);
        tabRect.anchorMax = new Vector2(1f, 1f);
        tabRect.pivot = new Vector2(0f, 1f);
        tabRect.anchoredPosition = new Vector2(14f, 6f);
        tabRect.sizeDelta = new Vector2(-28f, 44f);

        HorizontalLayoutGroup tabLayout = tabRow.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 6;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = true;

        BuildTab(tabRow.transform, ShopColumn.Subject, "주어");
        BuildTab(tabRow.transform, ShopColumn.Condition, "조건");
        BuildTab(tabRow.transform, ShopColumn.Result, "결과");

        // 목록 영역. 조각을 개수만큼 한 줄씩 나열하므로 많아지면 스크롤된다.
        GameObject scroll = new GameObject("ListScroll", typeof(RectTransform));
        scroll.transform.SetParent(left.transform, false);
        RectTransform scrollRect = scroll.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(14f, 14f);
        scrollRect.offsetMax = new Vector2(-14f, -70f);

        ScrollRect scroller = scroll.AddComponent<ScrollRect>();
        scroller.horizontal = false;
        scroller.vertical = true;
        scroller.movementType = ScrollRect.MovementType.Clamped;
        scroller.scrollSensitivity = 22f;

        GameObject viewport = UiTheme.CreateImage("Viewport", scroll.transform, ListBackground);
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
        listRoot = listRect;
        scroller.content = listRect;

        VerticalLayoutGroup listLayout = list.AddComponent<VerticalLayoutGroup>();
        listLayout.padding = new RectOffset(26, 26, 22, 22);
        listLayout.spacing = 18;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        listLayout.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter listFitter = list.AddComponent<ContentSizeFitter>();
        listFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        listFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // 목록이 비었을 때 안내 문구. 스크롤 영역 위에 고정으로 띄운다.
        // (목록 컨텐츠는 비면 높이가 0이라 그 안에 두면 보이지 않는다.)
        emptyText = UiTheme.CreateText("Empty", viewport.transform, string.Empty, 20,
            UiTheme.TextMuted, TextAnchor.MiddleCenter);
        UiTheme.Stretch(emptyText.rectTransform, 20f);
        emptyText.gameObject.SetActive(false);

        // 인벤토리 개수 요약 (목록 아래쪽).
        countText = UiTheme.CreateText("Count", left.transform, string.Empty, 15,
            UiTheme.TextMuted, TextAnchor.MiddleCenter);
        RectTransform countRect = countText.rectTransform;
        countRect.anchorMin = new Vector2(0f, 0f);
        countRect.anchorMax = new Vector2(1f, 0f);
        countRect.pivot = new Vector2(0.5f, 0f);
        countRect.anchoredPosition = new Vector2(0f, 20f);
        countRect.sizeDelta = new Vector2(-28f, 26f);
    }

    private void BuildTab(Transform parent, ShopColumn column, string label)
    {
        GameObject tab = UiTheme.CreateImage("Tab_" + label, parent, TabNormal);
        Image image = tab.GetComponent<Image>();
        image.raycastTarget = true;

        Text text = UiTheme.CreateText("Label", tab.transform, label, 20,
            Color.white, TextAnchor.MiddleCenter);
        UiTheme.Stretch(text.rectTransform);
        text.fontStyle = FontStyle.Bold;

        Button button = tab.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => TabClicked?.Invoke(column));

        tabImages[column] = image;
        tabButtons[column] = button;
    }

    /// <summary>활성 탭 표시를 바꾼다. 활성 탭은 밝은 박스 + 진한 글자.</summary>
    public void SetActiveTab(ShopColumn active)
    {
        foreach (KeyValuePair<ShopColumn, Image> pair in tabImages)
        {
            bool isActive = pair.Key == active;
            pair.Value.color = isActive ? TabActive : TabNormal;
        }

        foreach (KeyValuePair<ShopColumn, Button> pair in tabButtons)
        {
            Text label = pair.Value.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = pair.Key == active ? TabTextActive : Color.white;
            }
        }
    }

    /// <summary>왼쪽 목록을 통째로 바꾼다. 드래그 전용이라 선택 표시는 없다.</summary>
    public void SetItems(IReadOnlyList<ItemData> items, string emptyMessage)
    {
        for (int i = 0; i < itemObjects.Count; i++)
        {
            if (itemObjects[i] != null)
            {
                itemObjects[i].SetActive(false);
                Destroy(itemObjects[i]);
            }
        }

        itemObjects.Clear();
        itemObjectToEntry.Clear();

        bool empty = items == null || items.Count == 0;
        if (emptyText != null)
        {
            emptyText.font = UiTheme.GetFont();
            emptyText.text = emptyMessage ?? string.Empty;
            emptyText.gameObject.SetActive(empty);
        }

        if (empty)
        {
            return;
        }

        // 같은 조각을 여러 줄 나열한다. 드래그 전용이라 선택 표시 없이 동일하게 그린다.
        for (int i = 0; i < items.Count; i++)
        {
            BuildItem(items[i]);
        }
    }

    private void BuildItem(ItemData data)
    {
        GameObject card = UiTheme.CreateImage("Item_" + data.Label, listRoot, CardNormal);
        Image image = card.GetComponent<Image>();
        image.raycastTarget = true;
        AddOutline(card.transform, SlotBorder, 5f);

        LayoutElement layout = card.AddComponent<LayoutElement>();
        layout.preferredHeight = 118f;
        layout.minHeight = 96f;

        // 조각은 개수만큼 한 줄씩 따로 표시하므로 "×N"을 붙이지 않는다.
        // 사진처럼 제목은 크게 굵게, 설명은 작게.
        string text = $"<size=26><b>{data.Label}</b></size>";
        if (!string.IsNullOrEmpty(data.Detail))
        {
            text += $"\n<size=14><color=#5A4632>{data.Detail}</color></size>";
        }

        Text label = UiTheme.CreateText("Label", card.transform, text, 21,
            TabTextActive, TextAnchor.MiddleLeft);
        label.font = UiTheme.GetFont();
        label.fontStyle = FontStyle.Normal;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        UiTheme.Stretch(label.rectTransform, 12f);

        card.AddComponent<ItemDragHandler>().Initialize(this, card);

        itemObjects.Add(card);
        itemObjectToEntry[card] = data.EntryIndex;
    }

    /// <summary>슬롯 오브젝트의 교리/열을 찾는다. 모르면 false.</summary>
    public bool FindSlot(GameObject slotObject, out int doctrineIndex, out ShopColumn column)
    {
        for (int d = 0; d < slotObjects.Count; d++)
        {
            Dictionary<ShopColumn, GameObject> slots = slotObjects[d];
            if (slots == null)
            {
                continue;
            }

            foreach (KeyValuePair<ShopColumn, GameObject> pair in slots)
            {
                if (pair.Value == slotObject)
                {
                    doctrineIndex = d;
                    column = pair.Key;
                    return true;
                }
            }
        }

        doctrineIndex = -1;
        column = ShopColumn.Subject;
        return false;
    }

    /// <summary>슬롯 라벨 텍스트. 없으면 null.</summary>
    public Text GetSlotText(int doctrineIndex, ShopColumn column)
    {
        Dictionary<ShopColumn, Text> texts = GetSlotTexts(doctrineIndex);
        if (texts != null && texts.TryGetValue(column, out Text text))
        {
            return text;
        }

        return null;
    }

    /// <summary>드래그 위치의 슬롯만 밝게 표시한다.</summary>
    public void HighlightSlotAt(Vector2 screenPosition)
    {
        bool over = TryGetSlotAt(screenPosition, out int overDoctrine, out ShopColumn overColumn);
        for (int d = 0; d < slotObjects.Count; d++)
        {
            Dictionary<ShopColumn, Image> images = GetSlotImages(d);
            if (images == null)
            {
                continue;
            }

            bool unlocked = IsUnlocked(d);

            foreach (KeyValuePair<ShopColumn, Image> pair in images)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                if (!unlocked)
                {
                    pair.Value.color = LockedSlotBackground;
                    continue;
                }

                pair.Value.color = GetSlotFilled(d, pair.Key)
                    ? CardSelected
                    : (over && d == overDoctrine && pair.Key == overColumn ? CardHover : SlotBackground);
            }
        }
    }

    /// <summary>드래그 하이라이트를 원래 색으로 되돌린다.</summary>
    public void RefreshSlotColors()
    {
        for (int d = 0; d < slotObjects.Count; d++)
        {
            Dictionary<ShopColumn, Image> images = GetSlotImages(d);
            if (images == null)
            {
                continue;
            }

            bool unlocked = IsUnlocked(d);

            foreach (KeyValuePair<ShopColumn, Image> pair in images)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                pair.Value.color = !unlocked
                    ? LockedSlotBackground
                    : (GetSlotFilled(d, pair.Key) ? CardSelected : SlotBackground);
            }
        }
    }

    /// <summary>이 교리 칸이 열려 있는지. 잠긴 칸은 드롭도 받지 않는다.</summary>
    public bool IsUnlocked(int doctrineIndex)
    {
        return doctrineIndex >= 0 && doctrineIndex < unlockedCount;
    }

    /// <summary>드래그 중인 카드의 EntryIndex. 모르면 -1.</summary>
    public int GetEntryIndex(GameObject cardObject)
    {
        if (cardObject == null)
        {
            return -1;
        }

        return itemObjectToEntry.TryGetValue(cardObject, out int entryIndex) ? entryIndex : -1;
    }

    /// <summary>드래그 종료 지점에 있는 교리 슬롯. 슬롯 위가 아니면 false.</summary>
    public bool TryGetSlotAt(Vector2 screenPosition, out int doctrineIndex, out ShopColumn column)
    {
        int count = slotObjects.Count;
        for (int d = 0; d < count; d++)
        {
            // 잠긴 칸은 드롭 대상이 아니다.
            if (!IsUnlocked(d))
            {
                continue;
            }

            Dictionary<ShopColumn, GameObject> slots = slotObjects[d];
            if (slots == null)
            {
                continue;
            }

            foreach (KeyValuePair<ShopColumn, GameObject> pair in slots)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                RectTransform rect = pair.Value.GetComponent<RectTransform>();
                if (rect == null)
                {
                    continue;
                }

                // ScreenSpaceOverlay라 카메라는 null로 넘긴다.
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, null))
                {
                    doctrineIndex = d;
                    column = pair.Key;
                    return true;
                }
            }
        }

        doctrineIndex = -1;
        column = ShopColumn.Subject;
        return false;
    }

    public void SetCountSummary(string summary)
    {
        if (countText != null)
        {
            countText.font = UiTheme.GetFont();
            countText.text = summary ?? string.Empty;
        }
    }

    // 슬롯에 실제 조각이 들어 있는지 (강조색 계산용). 교리 수를 따라 늘어난다.
    private readonly List<bool[]> slotFilledRows = new List<bool[]>();

    private bool GetSlotFilled(int doctrineIndex, ShopColumn column)
    {
        if (doctrineIndex < 0 || doctrineIndex >= slotFilledRows.Count)
        {
            return false;
        }

        return slotFilledRows[doctrineIndex][(int)column];
    }

    private void SetSlotFilled(int doctrineIndex, ShopColumn column, bool filled)
    {
        while (slotFilledRows.Count <= doctrineIndex)
        {
            slotFilledRows.Add(new bool[3]);
        }

        slotFilledRows[doctrineIndex][(int)column] = filled;
    }

    /// <summary>교리 하나의 슬롯 3개 표시를 갱신한다. 비어 있으면 빈 칸으로 둔다.</summary>
    public void SetSlots(int doctrineIndex, string subjectLabel, string conditionLabel, string resultLabel)
    {
        SetSlotLabel(doctrineIndex, ShopColumn.Subject, subjectLabel);
        SetSlotLabel(doctrineIndex, ShopColumn.Condition, conditionLabel);
        SetSlotLabel(doctrineIndex, ShopColumn.Result, resultLabel);
    }

    private void SetSlotLabel(int doctrineIndex, ShopColumn column, string label)
    {
        Dictionary<ShopColumn, Text> texts = GetSlotTexts(doctrineIndex);
        if (texts == null || !texts.TryGetValue(column, out Text text) || text == null)
        {
            return;
        }

        bool filled = !string.IsNullOrEmpty(label);
        text.font = UiTheme.GetFont();
        text.fontStyle = filled ? FontStyle.Bold : FontStyle.Normal;
        text.fontSize = filled ? 20 : 16;
        text.text = filled ? label : string.Empty;

        SetSlotFilled(doctrineIndex, column, filled);

        Dictionary<ShopColumn, Image> images = GetSlotImages(doctrineIndex);
        if (images != null && images.TryGetValue(column, out Image image) && image != null)
        {
            image.color = !IsUnlocked(doctrineIndex)
                ? LockedSlotBackground
                : (filled ? CardSelected : SlotBackground);
        }
    }

    private Dictionary<ShopColumn, Text> GetSlotTexts(int doctrineIndex)
    {
        return doctrineIndex >= 0 && doctrineIndex < slotTexts.Count ? slotTexts[doctrineIndex] : null;
    }

    private Dictionary<ShopColumn, Image> GetSlotImages(int doctrineIndex)
    {
        return doctrineIndex >= 0 && doctrineIndex < slotImages.Count ? slotImages[doctrineIndex] : null;
    }

    /// <summary>교리 하나의 설명 줄을 갱신한다.</summary>
    public void SetDescription(int doctrineIndex, string description)
    {
        if (doctrineIndex < 0 || doctrineIndex >= doctrineDescriptions.Count)
        {
            return;
        }

        Text target = doctrineDescriptions[doctrineIndex];
        if (target != null)
        {
            target.font = UiTheme.GetFont();
            target.text = description ?? string.Empty;
        }
    }

    // ---------------- 오른쪽: 교리 조합 (자동 장착, 칸 수 가변) ----------------

    private void BuildRight(Transform parent, int doctrineCount)
    {
        doctrineCount = Mathf.Max(1, doctrineCount);

        // 교리가 많아지면 오른쪽이 스크롤된다.
        GameObject scroll = new GameObject("RightScroll", typeof(RectTransform));
        scroll.transform.SetParent(parent, false);
        RectTransform scrollRect = scroll.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(390f + 30f, 24f);
        scrollRect.offsetMax = new Vector2(-30f, -24f);

        ScrollRect scroller = scroll.AddComponent<ScrollRect>();
        scroller.horizontal = false;
        scroller.vertical = true;
        scroller.movementType = ScrollRect.MovementType.Clamped;
        scroller.scrollSensitivity = 18f;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scroll.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        UiTheme.Stretch(viewportRect);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        viewport.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        scroller.viewport = viewportRect;

        GameObject right = new GameObject("Right", typeof(RectTransform));
        right.transform.SetParent(viewport.transform, false);
        RectTransform rightRect = right.GetComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(0f, 1f);
        rightRect.anchorMax = new Vector2(1f, 1f);
        rightRect.pivot = new Vector2(0.5f, 1f);
        rightRect.anchoredPosition = new Vector2(0f, -6f);
        rightRect.sizeDelta = new Vector2(0f, 0f);
        rightContent = right.transform;
        scroller.content = rightRect;

        VerticalLayoutGroup layout = right.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = right.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        for (int i = 0; i < doctrineCount; i++)
        {
            BuildDoctrineGroup(rightContent, i);
        }

        // 잠긴 칸: 표시용으로 뒤에 2칸을 더 붙이되, 열린 칸만 활성화한다.
        // (초기는 열린 1 + 잠긴 2 = 3칸 표시. 자물쇠 아이콘은 두지 않는다.)
        SetUnlockedCount(doctrineCount);
    }

    // 교리 하나 = [주어][조건][결과] 슬롯 3개 + 설명 줄. 모든 교리 동일하게 동작한다.
    // 사진처럼 그룹 박스 밑에 교리 번호 스티커를 붙인다.
    private void BuildDoctrineGroup(Transform parent, int doctrineIndex)
    {
        GameObject holder = new GameObject("DoctrineHolder" + (doctrineIndex + 1), typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        LayoutElement holderLayout = holder.AddComponent<LayoutElement>();
        holderLayout.preferredHeight = 212f;
        holderLayout.minHeight = 180f;

        // 잠금/해금 표시용. 잠긴 칸은 이 그룹 전체를 흐리게 하고 입력을 막는다.
        CanvasGroup fade = holder.AddComponent<CanvasGroup>();
        doctrineGroupFades.Add(fade);

        VerticalLayoutGroup holderVertical = holder.AddComponent<VerticalLayoutGroup>();
        holderVertical.padding = new RectOffset(0, 0, 0, 0);
        holderVertical.spacing = 0f;
        holderVertical.childControlWidth = true;
        holderVertical.childControlHeight = true;
        holderVertical.childForceExpandWidth = true;
        holderVertical.childForceExpandHeight = true;

        int number = doctrineIndex + 1;
        GameObject group = UiTheme.CreateImage("Doctrine" + number, holder.transform, GroupBackground);
        LayoutElement groupLayout = group.AddComponent<LayoutElement>();
        groupLayout.preferredHeight = 180f;
        groupLayout.minHeight = 150f;

        VerticalLayoutGroup groupVertical = group.AddComponent<VerticalLayoutGroup>();
        groupVertical.padding = new RectOffset(28, 28, 22, 22);
        groupVertical.spacing = 14;
        groupVertical.childControlWidth = true;
        groupVertical.childControlHeight = true;
        groupVertical.childForceExpandWidth = true;
        groupVertical.childForceExpandHeight = true;

        // 슬롯 3개.
        GameObject slotRow = new GameObject("Slots", typeof(RectTransform));
        slotRow.transform.SetParent(group.transform, false);
        LayoutElement slotRowLayout = slotRow.AddComponent<LayoutElement>();
        slotRowLayout.flexibleHeight = 2f;

        HorizontalLayoutGroup slotLayout = slotRow.AddComponent<HorizontalLayoutGroup>();
        slotLayout.spacing = 22;
        slotLayout.childControlWidth = true;
        slotLayout.childControlHeight = true;
        slotLayout.childForceExpandWidth = true;
        slotLayout.childForceExpandHeight = true;

        slotTexts.Add(new Dictionary<ShopColumn, Text>());
        slotImages.Add(new Dictionary<ShopColumn, Image>());
        slotObjects.Add(new Dictionary<ShopColumn, GameObject>());
        while (slotFilledRows.Count <= doctrineIndex)
        {
            slotFilledRows.Add(new bool[3]);
        }

        BuildSlot(doctrineIndex, slotRow.transform, ShopColumn.Subject);
        BuildSlot(doctrineIndex, slotRow.transform, ShopColumn.Condition);
        BuildSlot(doctrineIndex, slotRow.transform, ShopColumn.Result);

        // 설명 줄.
        GameObject description = UiTheme.CreateImage("Description", group.transform, DescriptionBackground);
        AddOutline(description.transform, SlotBorder, 4f);
        LayoutElement descriptionLayout = description.AddComponent<LayoutElement>();
        descriptionLayout.flexibleHeight = 0.7f;
        descriptionLayout.minHeight = 36f;

        Text text = UiTheme.CreateText("Text", description.transform,
            $"교리{number}  ·  발동되면 무슨 일이 벌어지는지", 17,
            UiTheme.TextDark, TextAnchor.MiddleCenter);
        UiTheme.Stretch(text.rectTransform, 6f);

        while (doctrineDescriptions.Count <= doctrineIndex)
        {
            doctrineDescriptions.Add(null);
        }

        doctrineDescriptions[doctrineIndex] = text;
        doctrineGroups.Add(holder);

        // 교리 번호 스티커: 그룹 박스 왼쪽 아래에 겹쳐 붙는다.
        GameObject badge = UiTheme.CreateImage("DoctrineBadge" + number, holder.transform, GroupBackground);
        AddOutline(badge.transform, SlotBorder, 4f);
        RectTransform badgeRect = badge.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0f, 0f);
        badgeRect.anchorMax = new Vector2(0f, 0f);
        badgeRect.pivot = new Vector2(0f, 1f);
        badgeRect.anchoredPosition = new Vector2(28f, 0f);
        badgeRect.sizeDelta = new Vector2(96f, 32f);
        badge.GetComponent<Image>().raycastTarget = false;

        Text badgeText = UiTheme.CreateText("Text", badge.transform, $"교리{number}", 18,
            TabTextActive, TextAnchor.MiddleCenter);
        badgeText.fontStyle = FontStyle.Bold;
        UiTheme.Stretch(badgeText.rectTransform, 2f);
    }

    private static void FillBorderOutline(GameObject border, float thickness, Color color)
    {
        Image image = border.GetComponent<Image>();
        image.sprite = UiTheme.WhiteSprite;
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;

        AddBorderStrip(border.transform, "Top", color, new Vector2(0f, 1f), new Vector2(1f, 1f), thickness, false);
        AddBorderStrip(border.transform, "Bottom", color, new Vector2(0f, 0f), new Vector2(1f, 0f), thickness, false);
        AddBorderStrip(border.transform, "Left", color, new Vector2(0f, 0f), new Vector2(0f, 1f), thickness, true);
        AddBorderStrip(border.transform, "Right", color, new Vector2(1f, 0f), new Vector2(1f, 1f), thickness, true);
    }

    private static void AddBorderStrip(Transform parent, string name, Color color,
        Vector2 anchorMin, Vector2 anchorMax, float thickness, bool vertical)
    {
        GameObject strip = UiTheme.CreateImage(name, parent, color);
        RectTransform rect = strip.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = vertical
            ? new Vector2(thickness, 0f)
            : new Vector2(0f, thickness);
        strip.GetComponent<Image>().raycastTarget = false;
    }

    /// <summary>사진의 진한 베이지 테두리. 카드/슬롯/설명 박스에 두른다.</summary>
    private static void AddOutline(Transform parent, Color color, float thickness)
    {
        GameObject border = new GameObject("Outline", typeof(RectTransform), typeof(Image));
        border.transform.SetParent(parent, false);
        RectTransform borderRect = border.GetComponent<RectTransform>();
        UiTheme.Stretch(borderRect, 0f);
        FillBorderOutline(border, thickness, color);
    }

    private void BuildSlot(int doctrineIndex, Transform parent, ShopColumn column)
    {
        GameObject slot = UiTheme.CreateImage("Slot_" + column, parent, SlotBackground);
        AddOutline(slot.transform, SlotBorder, 5f);
        Image image = slot.GetComponent<Image>();

        Text text = UiTheme.CreateText("Label", slot.transform, string.Empty, 20,
            UiTheme.TextDark, TextAnchor.MiddleCenter);
        UiTheme.Stretch(text.rectTransform);

        image.raycastTarget = true;
        slotTexts[doctrineIndex][column] = text;
        slotImages[doctrineIndex][column] = image;
        slotObjects[doctrineIndex][column] = slot;

        slot.AddComponent<SlotDragHandler>().Initialize(this, slot);
    }

    public void Open()
    {
        isOpen = true;

        // OS 폰트는 런타임에 만들어지므로 열 때마다 모든 글자에 다시 적용한다.
        Text[] texts = GetComponentsInChildren<Text>(true);
        Font font = UiTheme.GetFont();
        for (int i = 0; i < texts.Length; i++)
        {
            texts[i].font = font;
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

    private void OnDestroy()
    {
        if (canvas != null && canvas.gameObject != null)
        {
            Destroy(canvas.gameObject);
        }
    }

    // 왼쪽 카드 드래그 처리. 캔버스 스케일러를 타지 않도록 최상위로 띄운다.
    private sealed class ItemDragHandler : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private FoundationPanelView view;
        private GameObject cardObject;
        private Transform originalParent;
        private int originalSibling;
        private Vector2 originalAnchoredPos;

        public void Initialize(FoundationPanelView panelView, GameObject card)
        {
            view = panelView;
            cardObject = card;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (view == null || cardObject == null)
            {
                return;
            }

            RectTransform rect = cardObject.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            originalParent = cardObject.transform.parent;
            originalSibling = cardObject.transform.GetSiblingIndex();
            originalAnchoredPos = rect.anchoredPosition;

            // 레이아웃에서 빼서 드래그 중 크기가 안 바뀌게 한다.
            LayoutElement layout = cardObject.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.ignoreLayout = true;
            }

            cardObject.transform.SetParent(view.canvas.transform, true);
            cardObject.transform.SetAsLastSibling();

            Image image = cardObject.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = false;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (cardObject == null)
            {
                return;
            }

            RectTransform rect = cardObject.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            rect.position = eventData.position;
            view.HighlightSlotAt(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (view == null || cardObject == null)
            {
                return;
            }

            bool hasTarget = view.TryGetSlotAt(
                eventData.position, out int targetDoctrine, out ShopColumn targetColumn);
            int entryIndex = view.GetEntryIndex(cardObject);

            // 카드를 원래 자리로 되돌린다. 목록은 Refresh에서 다시 그린다.
            cardObject.transform.SetParent(originalParent, false);
            cardObject.transform.SetSiblingIndex(originalSibling);
            RectTransform rect = cardObject.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = originalAnchoredPos;
            }

            LayoutElement layout = cardObject.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.ignoreLayout = false;
            }

            Image image = cardObject.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
            }

            if (hasTarget && entryIndex >= 0)
            {
                view.ItemDroppedOnSlot?.Invoke(entryIndex, targetDoctrine, targetColumn);
            }
        }

        private void HighlightSlotAt(Vector2 screenPosition)
        {
            view?.HighlightSlotAt(screenPosition);
        }
    }

    // 채워진 슬롯 드래그 처리. 같은 열 다른 슬롯에 드롭하면 이동/교환,
    // 슬롯 밖(빈 곳/목록)에 드롭하면 해제. 빈 슬롯은 드래그 불가.
    private sealed class SlotDragHandler : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private FoundationPanelView view;
        private GameObject slotObject;
        private int fromDoctrine = -1;
        private ShopColumn fromColumn = ShopColumn.Subject;
        private Transform originalParent;
        private int originalSibling;
        private GameObject ghost;
        private bool dragging;

        public void Initialize(FoundationPanelView panelView, GameObject slot)
        {
            view = panelView;
            slotObject = slot;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = false;
            ghost = null;
            if (view == null || slotObject == null)
            {
                return;
            }

            if (!view.FindSlot(slotObject, out fromDoctrine, out fromColumn))
            {
                return;
            }

            if (!view.GetSlotFilled(fromDoctrine, fromColumn))
            {
                return;
            }

            RectTransform slotRect = slotObject.GetComponent<RectTransform>();
            if (slotRect == null)
            {
                return;
            }

            originalParent = slotObject.transform.parent;
            originalSibling = slotObject.transform.GetSiblingIndex();

            // 원본은 자리에 두고, 반투명 고스트를 최상위에서 끈다. 레이아웃이 안 흔들린다.
            ghost = new GameObject("SlotDragGhost", typeof(RectTransform), typeof(Image));
            RectTransform ghostRect = ghost.GetComponent<RectTransform>();
            ghostRect.SetParent(view.canvas.transform, false);
            ghostRect.sizeDelta = slotRect.rect.size;
            ghostRect.position = slotRect.position;
            Image ghostImage = ghost.GetComponent<Image>();
            Image sourceImage = slotObject.GetComponent<Image>();
            ghostImage.sprite = sourceImage != null ? sourceImage.sprite : null;
            ghostImage.color = new Color(0.95f, 0.75f, 0.75f, 0.85f);
            ghostImage.raycastTarget = false;
            Text label = view.GetSlotText(fromDoctrine, fromColumn);
            if (label != null)
            {
                GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelGo.transform.SetParent(ghost.transform, false);
                Text ghostText = labelGo.GetComponent<Text>();
                ghostText.font = label.font;
                ghostText.fontSize = label.fontSize;
                ghostText.text = label.text;
                ghostText.color = label.color;
                ghostText.alignment = label.alignment;
                UiTheme.Stretch(labelGo.GetComponent<RectTransform>(), 4f);
                ghostText.raycastTarget = false;
            }

            ghost.transform.SetAsLastSibling();
            dragging = true;
            view.HighlightSlotAt(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || ghost == null)
            {
                return;
            }

            ghost.GetComponent<RectTransform>().position = eventData.position;
            view.HighlightSlotAt(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            if (ghost != null)
            {
                Destroy(ghost);
                ghost = null;
            }

            if (view == null)
            {
                return;
            }

            if (view.TryGetSlotAt(eventData.position, out int targetDoctrine, out ShopColumn targetColumn))
            {
                view.SlotDroppedOnSlot?.Invoke(fromDoctrine, fromColumn, targetDoctrine, targetColumn);
            }
            else
            {
                view.SlotDroppedOutside?.Invoke(fromDoctrine, fromColumn);
            }

            view.RefreshSlotColors();
        }
    }
}
