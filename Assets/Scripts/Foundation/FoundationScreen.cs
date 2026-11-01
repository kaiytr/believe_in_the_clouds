using System.Collections.Generic;
using UnityEngine;

// 재단 화면 모듈.
//
// - 재단 상호작용이 발생하면 패널을 열고, E/ESC로 닫는다.
// - 왼쪽: 탭(주어/조건/결과)으로 인벤토리에 있는 조각만 골라 본다.
//   카드를 교리1 슬롯에 드래그 앤 드롭하거나 클릭해서 꽂는다.
// - 오른쪽: 교리1·2·3 슬롯 모두 언제든 끼고 뺄 수 있다.
//   한 교리의 3칸이 다 차면 자동으로 장착(Compose → TryActivate)하고,
//   하나라도 빠지면 그 교리만 해제된다. 조각 소모 없음.
// - 상점과 마찬가지로 InteractableBehaviour.Interacted 이벤트를 듣고 스스로 연다.
//   공통 상호작용 코드는 재단을 모른다.
public class FoundationScreen : MonoBehaviour
{
    // 교리 슬롯 수 (교리1·2·3).
    public const int DoctrineCount = 3;

    private const int ColumnCount = 3;

    public static FoundationScreen Instance { get; private set; }

    private FoundationPanelView panel;
    private FoundationInteractable openFoundation;

    private ShopColumn currentTab = ShopColumn.Subject;

    // 교리별 슬롯: [교리 index, 열 index]. 탭을 바꿔도 유지된다.
    private readonly ShopEntry[,] slots = new ShopEntry[DoctrineCount, ColumnCount];

    // 교리별로 현재 장착 중인 조합. 슬롯과 1:1로 동기화한다.
    private readonly Doctrine[] equipped = new Doctrine[DoctrineCount];

    // 클릭 배치 대상이 되는 교리 (0 = 교리1). 슬롯을 누르면 그 교리로 바뀐다.
    private int activeDoctrine;

    public bool IsOpen => panel != null && panel.IsOpen;
    public ShopColumn CurrentTab => currentTab;
    public int ActiveDoctrine => activeDoctrine;

    /// <summary>현재 탭의 활성 교리 슬롯에 들어 있는 조각. 없으면 null.</summary>
    public ShopEntry SelectedEntry => GetSlot(activeDoctrine, currentTab);

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
        panel.ItemDroppedOnSlot += HandleItemDroppedOnSlot;
        panel.SlotClicked += HandleSlotClicked;
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

        panel.Open();
        SyncAllDoctrines();
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
        Debug.Log($"[재단] 탭 전환: {TabName(column)}");
        Refresh();
    }

    // 왼쪽 카드를 누르면 활성 교리(기본 교리1)의 현재 탭 슬롯에 배치한다.
    // 이미 같은 카드가 들어 있으면 해제한다.
    private void HandleItemClicked(int entryIndex)
    {
        IReadOnlyList<ShopEntry> entries = ShopCatalog.GetColumn(currentTab);
        if (entryIndex < 0 || entryIndex >= entries.Count)
        {
            return;
        }

        ShopEntry picked = entries[entryIndex];
        ShopEntry current = GetSlot(activeDoctrine, currentTab);

        if (current == picked)
        {
            SetSlot(activeDoctrine, currentTab, null);
            Debug.Log($"[재단] 교리{activeDoctrine + 1} 슬롯 해제: {TabName(currentTab)}");
        }
        else
        {
            // 인벤토리에 1개라도 있어야 배치할 수 있다.
            int count = CountOf(DoctrineInventory.Instance, picked);
            if (count <= 0)
            {
                Debug.Log($"[재단] 조각 부족: {picked.Label} (보유 0)");
                return;
            }

            SetSlot(activeDoctrine, currentTab, picked);
            Debug.Log($"[재단] 교리{activeDoctrine + 1} 배치: {TabName(currentTab)} → {picked.Label}");
        }

        SyncAllDoctrines();
        Refresh();
    }

    // 드래그 앤 드롭: 왼쪽 카드를 교리 슬롯에 놓으면 그 교리의 해당 슬롯에 배치한다.
    // 드래그한 카드의 열과 슬롯 열이 다르면 무시한다.
    private void HandleItemDroppedOnSlot(int entryIndex, int doctrineIndex, ShopColumn targetColumn)
    {
        if (doctrineIndex < 0 || doctrineIndex >= DoctrineCount)
        {
            return;
        }

        IReadOnlyList<ShopEntry> columnEntries = ShopCatalog.GetColumn(currentTab);
        if (entryIndex < 0 || entryIndex >= columnEntries.Count)
        {
            return;
        }

        ShopEntry picked = columnEntries[entryIndex];

        if (picked.Column != targetColumn)
        {
            Debug.Log($"[재단] 드롭 무시: {picked.Label}({TabName(picked.Column)}) → {TabName(targetColumn)} 슬롯 불가");
            return;
        }

        int count = CountOf(DoctrineInventory.Instance, picked);
        if (count <= 0)
        {
            Debug.Log($"[재단] 조각 부족: {picked.Label} (보유 0)");
            return;
        }

        activeDoctrine = doctrineIndex;
        SetSlot(doctrineIndex, targetColumn, picked);
        Debug.Log($"[재단] 교리{doctrineIndex + 1} 드롭 배치: {TabName(targetColumn)} → {picked.Label}");
        SyncAllDoctrines();
        Refresh();
    }

    // 오른쪽 슬롯을 직접 누르면 그 슬롯을 비우고, 클릭 배치 대상 교리를 그 교리로 바꾼다.
    private void HandleSlotClicked(int doctrineIndex, ShopColumn column)
    {
        if (doctrineIndex < 0 || doctrineIndex >= DoctrineCount)
        {
            return;
        }

        bool hadEntry = GetSlot(doctrineIndex, column) != null;
        activeDoctrine = doctrineIndex;

        if (hadEntry)
        {
            SetSlot(doctrineIndex, column, null);
            Debug.Log($"[재단] 교리{doctrineIndex + 1} 슬롯 해제: {TabName(column)}");
            SyncAllDoctrines();
        }

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

        // 왼쪽에서 고른(활성 교리의 현재 탭 슬롯) 조각의 EntryIndex. 없으면 -1.
        ShopEntry current = GetSlot(activeDoctrine, currentTab);
        int currentIndex = -1;
        if (current != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] == current)
                {
                    currentIndex = i;
                    break;
                }
            }
        }

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
        }

        panel.SetItems(items, currentIndex,
            $"보유한 {TabName(currentTab)} 교리가 없습니다.\n상점에서 구매해 보세요.");
        panel.SetCountSummary(inventory != null ? inventory.Describe() : "인벤토리 없음");

        // 세 교리의 슬롯 라벨 + 설명 줄을 모두 갱신한다.
        for (int d = 0; d < DoctrineCount; d++)
        {
            panel.SetSlots(d, GetLabel(d, ShopColumn.Subject), GetLabel(d, ShopColumn.Condition),
                GetLabel(d, ShopColumn.Result));
            panel.SetDescription(d, DescribeDoctrine(d));
        }

        panel.SetActiveDoctrine(activeDoctrine);
        panel.SetActiveTab(currentTab);
    }

    private string GetLabel(int doctrineIndex, ShopColumn column)
    {
        return GetSlot(doctrineIndex, column)?.Label;
    }

    /// <summary>슬롯 3칸 상태에 맞춘 설명 줄 문구.</summary>
    private string DescribeDoctrine(int doctrineIndex)
    {
        string name = $"교리{doctrineIndex + 1}";
        Doctrine composed = ComposeDoctrine(doctrineIndex);

        if (composed != null)
        {
            bool isActive = equipped[doctrineIndex] != null
                && equipped[doctrineIndex].HasSameRule(composed);
            return $"{name}  ·  {DoctrineText.Describe(composed)}{(isActive ? "  (장착 중)" : string.Empty)}";
        }

        int filled = 0;
        for (int c = 0; c < ColumnCount; c++)
        {
            if (slots[doctrineIndex, c] != null)
            {
                filled++;
            }
        }

        return $"{name}  ·  슬롯 {filled}/3 — 왼쪽에서 조각을 고르세요.";
    }

    // 슬롯 3칸이 다 차면 조합된 교리, 아니면 null.
    private Doctrine ComposeDoctrine(int doctrineIndex)
    {
        return ShopCatalog.Compose(
            GetSlot(doctrineIndex, ShopColumn.Subject),
            GetSlot(doctrineIndex, ShopColumn.Condition),
            GetSlot(doctrineIndex, ShopColumn.Result));
    }

    private void SyncAllDoctrines()
    {
        for (int d = 0; d < DoctrineCount; d++)
        {
            SyncDoctrine(d);
        }
    }

    /// <summary>
    /// 한 교리의 슬롯 3칸이 다 차면 자동으로 장착하고, 하나라도 비면 장착 해제한다.
    /// 조각 소모 없음 — 꽂았다 뺐다 자유.
    /// </summary>
    private void SyncDoctrine(int doctrineIndex)
    {
        DoctrineManager manager = DoctrineManager.Instance;
        if (manager == null)
        {
            manager = DoctrineHudBoot.EnsureManagerExists();
        }

        if (manager == null)
        {
            return;
        }

        Doctrine wanted = ComposeDoctrine(doctrineIndex);

        // 하나라도 비면 그 교리만 장착 해제.
        if (wanted == null)
        {
            if (equipped[doctrineIndex] != null)
            {
                manager.Deactivate(equipped[doctrineIndex]);
                Debug.Log($"[재단] 교리{doctrineIndex + 1} 해제: 슬롯이 비었음");
                equipped[doctrineIndex] = null;
            }

            return;
        }

        // 이미 같은 조합이 장착 중이면 그대로 둔다.
        if (equipped[doctrineIndex] != null && equipped[doctrineIndex].HasSameRule(wanted))
        {
            return;
        }

        // 조합이 바뀌면 기존 것을 먼저 해제한다.
        if (equipped[doctrineIndex] != null)
        {
            manager.Deactivate(equipped[doctrineIndex]);
            equipped[doctrineIndex] = null;
        }

        // 다른 교리 칸에 같은 조합이 이미 장착돼 있으면 중복 등록하지 않는다.
        for (int other = 0; other < DoctrineCount; other++)
        {
            if (other != doctrineIndex && equipped[other] != null && equipped[other].HasSameRule(wanted))
            {
                Debug.Log($"[재단] 교리{doctrineIndex + 1} 생략: 교리{other + 1}와 같은 조합");
                return;
            }
        }

        if (!manager.TryActivate(wanted, out string reason))
        {
            Debug.LogWarning($"[재단] 교리{doctrineIndex + 1} 장착 실패: {reason}");
            return;
        }

        equipped[doctrineIndex] = wanted;
        Debug.Log($"[재단] 교리{doctrineIndex + 1} 장착: {DoctrineText.Describe(wanted)}");
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

    private ShopEntry GetSlot(int doctrineIndex, ShopColumn column)
    {
        if (doctrineIndex < 0 || doctrineIndex >= DoctrineCount)
        {
            return null;
        }

        return slots[doctrineIndex, (int)column];
    }

    private void SetSlot(int doctrineIndex, ShopColumn column, ShopEntry entry)
    {
        if (doctrineIndex < 0 || doctrineIndex >= DoctrineCount)
        {
            return;
        }

        slots[doctrineIndex, (int)column] = entry;
    }
}
