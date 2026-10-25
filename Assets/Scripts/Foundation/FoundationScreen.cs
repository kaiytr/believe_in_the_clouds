using System.Collections.Generic;
using UnityEngine;

// 재단 화면 모듈.
//
// - 재단 상호작용이 발생하면 패널을 열고, E/ESC로 닫는다.
// - 왼쪽: 탭(주어/조건/결과)으로 인벤토리에 있는 조각만 골라 본다. 카드를 눌러 선택한다.
// - 오른쪽 조합 슬롯은 아직 틀만 있다.
//
// 상점과 마찬가지로 InteractableBehaviour.Interacted 이벤트를 듣고 스스로 연다.
// 공통 상호작용 코드는 재단을 모른다.
public class FoundationScreen : MonoBehaviour
{
    public static FoundationScreen Instance { get; private set; }

    private FoundationPanelView panel;
    private FoundationInteractable openFoundation;

    private ShopColumn currentTab = ShopColumn.Subject;
    private int selectedEntryIndex = -1;

    public bool IsOpen => panel != null && panel.IsOpen;
    public ShopColumn CurrentTab => currentTab;

    /// <summary>왼쪽 목록에서 선택된 조각. 없으면 null. (오른쪽 조합 단계에서 쓴다)</summary>
    public ShopEntry SelectedEntry
    {
        get
        {
            if (selectedEntryIndex < 0)
            {
                return null;
            }

            IReadOnlyList<ShopEntry> entries = ShopCatalog.GetColumn(currentTab);
            return selectedEntryIndex < entries.Count ? entries[selectedEntryIndex] : null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBoot()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject go = new GameObject("[FoundationScreen]");
        DontDestroyOnLoad(go);
        go.AddComponent<FoundationScreen>();
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
        EnsurePanel();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        InteractableBehaviour.Interacted += HandleInteracted;
        UiModal.CloseRequested += HandleCloseRequested;
    }

    private void OnDisable()
    {
        InteractableBehaviour.Interacted -= HandleInteracted;
        UiModal.CloseRequested -= HandleCloseRequested;
    }

    private void EnsurePanel()
    {
        if (panel != null)
        {
            return;
        }

        panel = FoundationPanelView.BuildRuntime();
        panel.TabClicked += HandleTabClicked;
        panel.ItemClicked += HandleItemClicked;
        DontDestroyOnLoad(panel.gameObject);

        // uGUI 버튼은 EventSystem이 없으면 클릭을 못 받는다.
        UiEventSystemBoot.Ensure();
    }

    private void HandleInteracted(InteractableBehaviour target, GameObject interactor)
    {
        if (target is not FoundationInteractable foundation)
        {
            return;
        }

        if (openFoundation == foundation && IsOpen)
        {
            Close();
            return;
        }

        Open(foundation, interactor);
    }

    private void HandleCloseRequested()
    {
        if (IsOpen)
        {
            Close();
        }
    }

    public void Open(FoundationInteractable foundation, GameObject interactor)
    {
        if (foundation == null)
        {
            return;
        }

        EnsurePanel();
        UiEventSystemBoot.Ensure();

        openFoundation = foundation;
        currentTab = ShopColumn.Subject;
        selectedEntryIndex = -1;

        panel.Open();
        Refresh();

        UiModal.Push(this);

        DoctrineInventory inventory = DoctrineInventory.Instance;
        if (inventory != null)
        {
            inventory.Changed -= Refresh;
            inventory.Changed += Refresh;
        }

        Debug.Log($"[재단] {foundation.FoundationName} 열림 ({interactor?.name})");
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        panel.Close();
        UiModal.Pop(this);

        DoctrineInventory inventory = DoctrineInventory.Instance;
        if (inventory != null)
        {
            inventory.Changed -= Refresh;
        }

        FoundationInteractable closed = openFoundation;
        openFoundation = null;
        Debug.Log($"[재단] {closed?.FoundationName} 닫힘");
    }

    private void HandleTabClicked(ShopColumn column)
    {
        if (currentTab == column)
        {
            return;
        }

        currentTab = column;
        selectedEntryIndex = -1;
        Debug.Log($"[재단] 탭 전환: {TabName(column)}");
        Refresh();
    }

    // 같은 카드를 다시 누르면 선택 해제.
    private void HandleItemClicked(int entryIndex)
    {
        selectedEntryIndex = selectedEntryIndex == entryIndex ? -1 : entryIndex;

        string label = SelectedEntry != null ? SelectedEntry.Label : "없음";
        Debug.Log($"[재단] 교리 선택: {TabName(currentTab)} → {label}");
        Refresh();
    }

    // 현재 탭에서 인벤토리에 1개 이상 있는 조각만 목록으로 만든다.
    private void Refresh()
    {
        if (panel == null)
        {
            return;
        }

        panel.SetActiveTab(currentTab);

        DoctrineInventory inventory = DoctrineInventory.Instance;
        IReadOnlyList<ShopEntry> entries = ShopCatalog.GetColumn(currentTab);
        List<FoundationPanelView.ItemData> items = new List<FoundationPanelView.ItemData>();

        // 선택된 카드가 목록에서 사라졌으면(개수가 0이 되면) 선택을 푼다.
        bool selectionStillValid = false;

        for (int i = 0; i < entries.Count; i++)
        {
            int count = CountOf(inventory, entries[i]);
            if (count <= 0)
            {
                continue;
            }

            items.Add(new FoundationPanelView.ItemData
            {
                EntryIndex = i,
                Label = entries[i].Label,
                Detail = entries[i].Detail,
                Count = count
            });

            if (i == selectedEntryIndex)
            {
                selectionStillValid = true;
            }
        }

        if (!selectionStillValid)
        {
            selectedEntryIndex = -1;
        }

        panel.SetItems(items, selectedEntryIndex,
            $"보유한 {TabName(currentTab)} 교리가 없습니다.\n상점에서 구매해 보세요.");
        panel.SetCountSummary(inventory != null ? inventory.Describe() : "인벤토리 없음");
    }

    private static int CountOf(DoctrineInventory inventory, ShopEntry entry)
    {
        if (inventory == null || entry == null)
        {
            return 0;
        }

        switch (entry.Column)
        {
            case ShopColumn.Subject: return inventory.CountSubject(entry.Subject);
            case ShopColumn.Condition: return inventory.CountCondition(entry.Condition);
            case ShopColumn.Result: return inventory.CountResult(entry.Result);
            default: return 0;
        }
    }

    private static string TabName(ShopColumn column)
    {
        switch (column)
        {
            case ShopColumn.Subject: return "주어";
            case ShopColumn.Condition: return "조건";
            default: return "결과";
        }
    }
}
