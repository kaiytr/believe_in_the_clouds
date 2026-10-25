using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 재단 패널.
//
// 레이아웃(제공된 사진):
//   ┌ 왼쪽 ──────────┬ 오른쪽 ─────────────────────┐
//   │ [주어][조건][결과] │ 교리1 [주어][조건][결과]        │
//   │ ┌ 교리 카드 ┐     │       [발동 효과 설명]          │
//   │ ├ 교리 카드 ┤     │ 교리2 ...                       │
//   │ └ 교리 카드 ┘     │ 교리3 ...                       │
//   └───────────────┴──────────────────────────────┘
//
// 왼쪽: 탭으로 주어/조건/결과를 골라 보고, 카드를 눌러 선택한다. (기능 있음)
// 오른쪽: 교리 조합 슬롯 틀만 있다. (아직 동작 없음)
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

    private const int DoctrineGroupCount = 3;

    private Canvas canvas;
    private GameObject content;
    private RectTransform listRoot;
    private Text emptyText;
    private Text countText;
    private bool isOpen;

    private readonly Dictionary<ShopColumn, Image> tabImages = new Dictionary<ShopColumn, Image>();
    private readonly Dictionary<ShopColumn, Button> tabButtons = new Dictionary<ShopColumn, Button>();
    private readonly List<GameObject> itemObjects = new List<GameObject>();
    private readonly List<Image> itemImages = new List<Image>();
    private readonly List<int> itemEntryIndices = new List<int>();

    public bool IsOpen => isOpen;

    /// <summary>탭을 눌렀을 때.</summary>
    public event Action<ShopColumn> TabClicked;

    /// <summary>목록 카드를 눌렀을 때. (EntryIndex)</summary>
    public event Action<int> ItemClicked;

    public static FoundationPanelView BuildRuntime()
    {
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
        view.BuildRight(panel.transform);

        content.SetActive(false);
        return view;
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

        itemObjects.Add(card);
        itemImages.Add(image);
        itemEntryIndices.Add(entryIndex);
    }

    public void SetCountSummary(string summary)
    {
        if (countText != null)
        {
            countText.font = UiTheme.GetFont();
            countText.text = summary ?? string.Empty;
        }
    }

    // ---------------- 오른쪽: 교리 조합 틀 (아직 동작 없음) ----------------

    private void BuildRight(Transform parent)
    {
        GameObject right = new GameObject("Right", typeof(RectTransform));
        right.transform.SetParent(parent, false);
        RectTransform rightRect = right.GetComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(0f, 0f);
        rightRect.anchorMax = new Vector2(1f, 1f);
        rightRect.offsetMin = new Vector2(390f + 30f, 24f);
        rightRect.offsetMax = new Vector2(-30f, -24f);

        VerticalLayoutGroup layout = right.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        for (int i = 0; i < DoctrineGroupCount; i++)
        {
            BuildDoctrineGroup(right.transform, i + 1);
        }
    }

    // 교리 하나 = [주어][조건][결과] 슬롯 3개 + 설명 줄.
    private void BuildDoctrineGroup(Transform parent, int number)
    {
        GameObject group = UiTheme.CreateImage("Doctrine" + number, parent, GroupBackground);

        VerticalLayoutGroup groupLayout = group.AddComponent<VerticalLayoutGroup>();
        groupLayout.padding = new RectOffset(28, 28, 22, 22);
        groupLayout.spacing = 14;
        groupLayout.childControlWidth = true;
        groupLayout.childControlHeight = true;
        groupLayout.childForceExpandWidth = true;
        groupLayout.childForceExpandHeight = true;

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

        BuildSlot(slotRow.transform, "주어");
        BuildSlot(slotRow.transform, "조건");
        BuildSlot(slotRow.transform, "결과");

        // 설명 줄.
        GameObject description = UiTheme.CreateImage("Description", group.transform, DescriptionBackground);
        LayoutElement descriptionLayout = description.AddComponent<LayoutElement>();
        descriptionLayout.flexibleHeight = 0.7f;
        descriptionLayout.minHeight = 36f;

        Text text = UiTheme.CreateText("Text", description.transform,
            $"교리{number}  ·  발동되면 무슨 일이 벌어지는지", 17,
            UiTheme.TextDark, TextAnchor.MiddleCenter);
        UiTheme.Stretch(text.rectTransform, 6f);
    }

    private static void BuildSlot(Transform parent, string label)
    {
        GameObject slot = UiTheme.CreateImage("Slot_" + label, parent, SlotBackground);
        Text text = UiTheme.CreateText("Label", slot.transform, label, 20,
            UiTheme.TextDark, TextAnchor.MiddleCenter);
        UiTheme.Stretch(text.rectTransform);
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
}
