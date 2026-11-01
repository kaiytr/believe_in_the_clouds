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
//   카드를 교리 슬롯으로 드래그 앤 드롭해서 배치한다. (클릭 배치도 그대로 됨)
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

    private static readonly Color PanelBackground = new Color(0.85f, 0.85f, 0.85f, 1f);
    private static readonly Color LeftBackground = new Color(0.71f, 0.71f, 0.71f, 1f);
    private static readonly Color ListBackground = new Color(0.83f, 0.83f, 0.83f, 1f);
    private static readonly Color TabNormal = new Color(0.55f, 0.55f, 0.55f, 1f);
    private static readonly Color TabHover = new Color(0.62f, 0.62f, 0.62f, 1f);
    private static readonly Color TabActive = new Color(0.40f, 0.40f, 0.42f, 1f);
    private static readonly Color CardNormal = new Color(0.65f, 0.65f, 0.65f, 1f);
    private static readonly Color CardHover = new Color(0.72f, 0.72f, 0.72f, 1f);
    private static readonly Color CardSelected = new Color(0.90f, 0.62f, 0.62f, 1f);
    private static readonly Color GroupBackground = new Color(0.77f, 0.77f, 0.77f, 1f);
    private static readonly Color SlotBackground = new Color(0.66f, 0.66f, 0.66f, 1f);
    private static readonly Color DescriptionBackground = new Color(0.66f, 0.66f, 0.66f, 1f);

    private const int InitialDoctrineGroups = 3; // 처음에 보여주는 교리 수

    private Canvas canvas;
    private GameObject content;
    private RectTransform listRoot;
    private Text emptyText;
    private Text countText;
    private bool isOpen;

    // 오른쪽 교리 그룹 컨테이너 (SetDoctrineCount에서 증설).
    private Transform rightContent;
    private readonly List<GameObject> doctrineGroups = new List<GameObject>();

    private readonly Dictionary<ShopColumn, Image> tabImages = new Dictionary<ShopColumn, Image>();
    private readonly Dictionary<ShopColumn, Button> tabButtons = new Dictionary<ShopColumn, Button>();
    private readonly List<GameObject> itemObjects = new List<GameObject>();
    private readonly List<Image> itemImages = new List<Image>();
    private readonly List<int> itemEntryIndices = new List<int>();

    // 오른쪽 교리 슬롯 (드래그 앤 드롭 + 클릭). 교리 번호(0-based)별로 보관한다.
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

    /// <summary>목록 카드를 눌렀을 때. (EntryIndex)</summary>
    public event Action<int> ItemClicked;

    // 왼쪽 카드를 교리 슬롯에 드래그해서 놓았을 때. (EntryIndex, 교리 index, 슬롯 열)
    public event Action<int, int, ShopColumn> ItemDroppedOnSlot;

    // 교리 슬롯을 눌렀을 때. (교리 index, Subject/Condition/Result)
    public event Action<int, ShopColumn> SlotClicked;

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

        // 사진 비율(1084 × 600 ≈ 1.8 : 1).
        GameObject panel = UiTheme.CreateImage("Panel", content.transform, PanelBackground);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(1240f, 700f);

        FoundationPanelView view = root.AddComponent<FoundationPanelView>();
        view.canvas = canvas;
        view.content = content;

        view.BuildLeft(panel.transform);
        view.BuildRight(panel.transform, doctrineCount);

        content.SetActive(false);
        return view;
    }

    /// <summary>오른쪽 교리 칸 개수를 맞춘다. 늘어난 만큼 뒤에 그룹을 붙인다.</summary>
    public void SetDoctrineCount(int count)
    {
        count = Mathf.Max(1, count);

        if (rightContent == null)
        {
            return;
        }

        while (doctrineGroups.Count < count)
        {
            BuildDoctrineGroup(rightContent, doctrineGroups.Count);
        }
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

        // 탭 줄.
        GameObject tabRow = new GameObject("Tabs", typeof(RectTransform));
        tabRow.transform.SetParent(left.transform, false);
        RectTransform tabRect = tabRow.GetComponent<RectTransform>();
        tabRect.anchorMin = new Vector2(0f, 1f);
        tabRect.anchorMax = new Vector2(1f, 1f);
        tabRect.pivot = new Vector2(0.5f, 1f);
        tabRect.anchoredPosition = new Vector2(0f, -14f);
        tabRect.sizeDelta = new Vector2(-28f, 56f);

        HorizontalLayoutGroup tabLayout = tabRow.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 8;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = true;

        BuildTab(tabRow.transform, ShopColumn.Subject, "주어");
        BuildTab(tabRow.transform, ShopColumn.Condition, "조건");
        BuildTab(tabRow.transform, ShopColumn.Result, "결과");

        // 목록 영역.
        GameObject list = UiTheme.CreateImage("List", left.transform, ListBackground);
        RectTransform listRect = list.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 0f);
        listRect.anchorMax = new Vector2(1f, 1f);
        listRect.offsetMin = new Vector2(14f, 14f);
        listRect.offsetMax = new Vector2(-14f, -70f);
        listRoot = listRect;

        VerticalLayoutGroup listLayout = list.AddComponent<VerticalLayoutGroup>();
        listLayout.padding = new RectOffset(26, 26, 22, 22);
        listLayout.spacing = 18;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        listLayout.childAlignment = TextAnchor.UpperCenter;

        // 목록이 비었을 때 안내 문구. 레이아웃에서는 제외한다.
        emptyText = UiTheme.CreateText("Empty", list.transform, string.Empty, 20,
            UiTheme.TextMuted, TextAnchor.MiddleCenter);
        UiTheme.Stretch(emptyText.rectTransform, 20f);
        LayoutElement emptyLayout = emptyText.gameObject.AddComponent<LayoutElement>();
        emptyLayout.ignoreLayout = true;
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

        Button button = tab.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => TabClicked?.Invoke(column));

        tabImages[column] = image;
        tabButtons[column] = button;
    }

    /// <summary>활성 탭 표시를 바꾼다.</summary>
    public void SetActiveTab(ShopColumn active)
    {
        foreach (KeyValuePair<ShopColumn, Image> pair in tabImages)
        {
            pair.Value.color = pair.Key == active ? TabActive : TabNormal;
        }
    }

    /// <summary>왼쪽 목록을 통째로 바꾼다. selectedEntryIndex는 선택 표시할 카드(없으면 -1).</summary>
    public void SetItems(IReadOnlyList<ItemData> items, int selectedEntryIndex, string emptyMessage)
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
        itemImages.Clear();
        itemEntryIndices.Clear();
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

        for (int i = 0; i < items.Count; i++)
        {
            BuildItem(items[i], items[i].EntryIndex == selectedEntryIndex);
        }
    }

    private void BuildItem(ItemData data, bool selected)
    {
        GameObject card = UiTheme.CreateImage("Item_" + data.Label, listRoot,
            selected ? CardSelected : CardNormal);
        Image image = card.GetComponent<Image>();
        image.raycastTarget = true;

        LayoutElement layout = card.AddComponent<LayoutElement>();
        layout.preferredHeight = 104f;
        layout.minHeight = 90f;

        string text = data.Count > 1 ? $"{data.Label}  ×{data.Count}" : data.Label;
        if (!string.IsNullOrEmpty(data.Detail))
        {
            text += $"\n<size=13><color=#3A3C40>{data.Detail}</color></size>";
        }

        Text label = UiTheme.CreateText("Label", card.transform, text, 21,
            UiTheme.TextDark, TextAnchor.MiddleCenter);
        label.font = UiTheme.GetFont();
        label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
        UiTheme.Stretch(label.rectTransform, 8f);

        Button button = card.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = selected ? Color.white : new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.05f;
        button.colors = colors;

        int entryIndex = data.EntryIndex;
        button.onClick.AddListener(() => ItemClicked?.Invoke(entryIndex));

        // 드래그: 카드를 누른 채로 교리1 슬롯까지 끌면 배치된다.
        itemDragHandler = card.AddComponent<ItemDragHandler>();
        itemDragHandler.Initialize(this, card);

        itemObjects.Add(card);
        itemImages.Add(image);
        itemEntryIndices.Add(entryIndex);
        itemObjectToEntry[card] = entryIndex;
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

    private ItemDragHandler itemDragHandler;

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

    /// <summary>교리 하나의 슬롯 3개 표시를 갱신한다. 비어 있으면 기본 라벨(주어/조건/결과).</summary>
    public void SetSlots(int doctrineIndex, string subjectLabel, string conditionLabel, string resultLabel)
    {
        SetSlotLabel(doctrineIndex, ShopColumn.Subject, subjectLabel, "주어");
        SetSlotLabel(doctrineIndex, ShopColumn.Condition, conditionLabel, "조건");
        SetSlotLabel(doctrineIndex, ShopColumn.Result, resultLabel, "결과");
    }

    private void SetSlotLabel(int doctrineIndex, ShopColumn column, string label, string fallback)
    {
        Dictionary<ShopColumn, Text> texts = GetSlotTexts(doctrineIndex);
        if (texts == null || !texts.TryGetValue(column, out Text text) || text == null)
        {
            return;
        }

        bool filled = !string.IsNullOrEmpty(label);
        text.font = UiTheme.GetFont();
        text.text = filled ? label : fallback;

        SetSlotFilled(doctrineIndex, column, filled);

        Dictionary<ShopColumn, Image> images = GetSlotImages(doctrineIndex);
        if (images != null && images.TryGetValue(column, out Image image) && image != null)
        {
            image.color = filled ? CardSelected : SlotBackground;
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

    /// <summary>클릭 배치 대상 교리를 강조 표시한다. (빈 슬롯 색으로 구분)</summary>
    public void SetActiveDoctrine(int doctrineIndex)
    {
        for (int d = 0; d < slotImages.Count; d++)
        {
            Dictionary<ShopColumn, Image> images = GetSlotImages(d);
            if (images == null)
            {
                continue;
            }

            bool active = d == doctrineIndex;
            foreach (KeyValuePair<ShopColumn, Image> pair in images)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                bool filled = GetSlotFilled(d, pair.Key);
                if (filled)
                {
                    pair.Value.color = CardSelected;
                }
                else
                {
                    pair.Value.color = active ? TabActive : SlotBackground;
                }
            }
        }
    }

    // ---------------- 오른쪽: 교리 조합 (자동 장착, 칸 수 가변) ----------------

    private void BuildRight(Transform parent, int doctrineCount)
    {
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
        rightRect.anchoredPosition = Vector2.zero;
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

        for (int i = 0; i < Mathf.Max(1, doctrineCount); i++)
        {
            BuildDoctrineGroup(rightContent, i);
        }
    }

    // 교리 하나 = [주어][조건][결과] 슬롯 3개 + 설명 줄. 모든 교리 동일하게 동작한다.
    private void BuildDoctrineGroup(Transform parent, int doctrineIndex)
    {
        int number = doctrineIndex + 1;
        GameObject group = UiTheme.CreateImage("Doctrine" + number, parent, GroupBackground);
        LayoutElement groupLayout = group.AddComponent<LayoutElement>();
        groupLayout.preferredHeight = 240f;
        groupLayout.minHeight = 200f;

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

        BuildSlot(doctrineIndex, slotRow.transform, ShopColumn.Subject, "주어");
        BuildSlot(doctrineIndex, slotRow.transform, ShopColumn.Condition, "조건");
        BuildSlot(doctrineIndex, slotRow.transform, ShopColumn.Result, "결과");

        // 설명 줄.
        GameObject description = UiTheme.CreateImage("Description", group.transform, DescriptionBackground);
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
        doctrineGroups.Add(group);
    }

    private void BuildSlot(int doctrineIndex, Transform parent, ShopColumn column, string defaultLabel)
    {
        GameObject slot = UiTheme.CreateImage("Slot_" + defaultLabel, parent, SlotBackground);
        Image image = slot.GetComponent<Image>();

        Text text = UiTheme.CreateText("Label", slot.transform, defaultLabel, 20,
            UiTheme.TextDark, TextAnchor.MiddleCenter);
        UiTheme.Stretch(text.rectTransform);

        image.raycastTarget = true;
        slotTexts[doctrineIndex][column] = text;
        slotImages[doctrineIndex][column] = image;
        slotObjects[doctrineIndex][column] = slot;

        Button button = slot.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        int capturedDoctrine = doctrineIndex;
        button.onClick.AddListener(() => SlotClicked?.Invoke(capturedDoctrine, column));
    }

    // ---------------- 열기/닫기 ----------------

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

            // 슬롯 위에 있으면 밝게 표시한다.
            int overDoctrine = -1;
            ShopColumn overColumn = ShopColumn.Subject;
            bool over = view != null
                && view.TryGetSlotAt(eventData.position, out overDoctrine, out overColumn);

            for (int d = 0; d < view.slotObjects.Count; d++)
            {
                Dictionary<ShopColumn, Image> images = view.GetSlotImages(d);
                if (images == null)
                {
                    continue;
                }

                foreach (KeyValuePair<ShopColumn, Image> pair in images)
                {
                    if (pair.Value == null)
                    {
                        continue;
                    }

                    pair.Value.color = view.GetSlotFilled(d, pair.Key)
                        ? CardSelected
                        : (over && d == overDoctrine && pair.Key == overColumn ? CardHover : SlotBackground);
                }
            }
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
    }
}
