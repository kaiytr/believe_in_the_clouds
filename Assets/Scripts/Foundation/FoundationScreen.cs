using System.Collections.Generic;
using UnityEngine;

// 재단 화면 모듈.
//
// - 재단 상호작용이 발생하면 패널을 열고, E/ESC로 닫는다.
// - 왼쪽: 탭(주어/조건/결과)으로 인벤토리에 있는 조각만 골라 본다.
//   장착은 왼쪽 카드를 슬롯에 드래그 앤 드롭으로만 된다. 클릭 배치 없음.
// - 오른쪽: 교리 슬롯들은 언제든 끼고 뺄 수 있다. 기본 3칸, 상점 구매로 1칸씩 증가.
//   한 교리의 3칸이 다 차면 자동으로 장착(Compose → TryActivate)하고,
//   하나라도 빠지면 그 교리만 해제된다. 조각은 참조 방식이라 소모 없음 —
//   같은 조각을 교리1·교리2 등 여러 교리에 동시에 배치할 수 있다.
// - 상점과 마찬가지로 InteractableBehaviour.Interacted 이벤트를 듣고 스스로 연다.
//   공통 상호작용 코드는 재단을 모른다.
public class FoundationScreen : MonoBehaviour
{
    // DoctrineSlotStore.DefaultSlots와 같은 기본값. 저장소가 아직 없을 때의 예비값.
    private const int FallbackDoctrineCount = 1;

    private const int ColumnCount = 3;

    public static FoundationScreen Instance { get; private set; }

    private FoundationPanelView panel;
    private FoundationInteractable openFoundation;

    private ShopColumn currentTab = ShopColumn.Subject;

    // 교리별 슬롯: [교리 index, 열 index]. 탭을 바꿔도 유지된다.
    // 칸이 늘어나면 기존 내용은 유지하고 뒤에 빈 칸을 붙인다.
    private readonly List<ShopEntry[]> slotRows = new List<ShopEntry[]>();

    // 교리별로 현재 장착 중인 조합. 슬롯과 1:1로 동기화한다.
    private readonly List<DoctrineManager.ActiveDoctrine> equipped = new List<DoctrineManager.ActiveDoctrine>();

    // 마지막으로 드롭된 교리 (0 = 교리1). 로그/선택 표기용. 클릭으로 바뀌지 않는다.
    private int activeDoctrine;

    public bool IsOpen => panel != null && panel.IsOpen;
    public ShopColumn CurrentTab => currentTab;
    public int ActiveDoctrine => activeDoctrine;

    /// <summary>현재 교리 칸 수 (재단에 표시되는 교리 개수).</summary>
    public int DoctrineCount => slotRows.Count;

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
        EnsureDoctrineCapacity(DesiredDoctrineCount());
        EnsurePanel();

        DoctrineSlotStore store = DoctrineSlotStore.EnsureExists();
        if (store != null)
        {
            store.Changed -= HandleSlotStoreChanged;
            store.Changed += HandleSlotStoreChanged;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        DoctrineSlotStore store = DoctrineSlotStore.Instance;
        if (store != null)
        {
            store.Changed -= HandleSlotStoreChanged;
        }
    }

    // 칸이 늘어나면 기존 내용은 유지하고 뒤에 빈 칸을 붙인다.
    private void EnsureDoctrineCapacity(int count)
    {
        count = Mathf.Max(1, count);
        while (slotRows.Count < count)
        {
            slotRows.Add(new ShopEntry[ColumnCount]);
            equipped.Add(null);
        }

        if (activeDoctrine >= slotRows.Count)
        {
            activeDoctrine = slotRows.Count - 1;
        }
    }

    private static int DesiredDoctrineCount()
    {
        DoctrineSlotStore store = DoctrineSlotStore.Instance;
        if (store != null)
        {
            return Mathf.Max(1, store.SlotCount);
        }

        return FallbackDoctrineCount;
    }

    private void HandleSlotStoreChanged()
    {
        EnsureDoctrineCapacity(DesiredDoctrineCount());
        if (IsOpen)
        {
            panel.SetDoctrineCount(slotRows.Count);
            SyncAllDoctrines();
            Refresh();
        }
    }

    private void OnEnable()
    {
        InteractableBehaviour.Interacted += HandleInteracted;
        UiModal.CloseRequested += HandleCloseRequested;
        Currency.DeathCompleted += HandleDeathCompleted;
    }

    private void OnDisable()
    {
        InteractableBehaviour.Interacted -= HandleInteracted;
        UiModal.CloseRequested -= HandleCloseRequested;
        Currency.DeathCompleted -= HandleDeathCompleted;
    }

    private void HandleDeathCompleted(EconomyDeathRecord death)
    {
        DoctrineManager manager = DoctrineManager.Instance;
        for (int i = 0; i < slotRows.Count; i++)
        {
            if (equipped[i] != null) manager?.DeactivateEntry(equipped[i]);
            equipped[i] = null;
            System.Array.Clear(slotRows[i], 0, ColumnCount);
        }
        activeDoctrine = 0;
        if (IsOpen) Refresh();
    }

    private void EnsurePanel()
    {
        if (panel != null)
        {
            panel.SetDoctrineCount(slotRows.Count);
            return;
        }

        panel = FoundationPanelView.BuildRuntime(slotRows.Count);
        panel.TabClicked += HandleTabClicked;
        panel.ItemDroppedOnSlot += HandleItemDroppedOnSlot;
        panel.SlotDroppedOnSlot += HandleSlotDroppedOnSlot;
        panel.SlotDroppedOutside += HandleSlotDroppedOutside;
        DontDestroyOnLoad(panel.gameObject);
        // uGUI 드래그는 EventSystem이 없으면 동작하지 않는다.
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

        EnsureDoctrineCapacity(DesiredDoctrineCount());
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

    // 드래그 앤 드롭: 왼쪽 카드를 교리 슬롯에 놓으면 그 교리의 해당 슬롯에 배치한다.
    // 드래그한 카드의 열과 슬롯 열이 다르면 무시한다.
    // 같은 조각을 여러 교리에 동시에 배치 가능. 단, 보유 개수를 초과할 수는 없다.
    // (가용 = 보유 - 슬롯 사용중)
    private void HandleItemDroppedOnSlot(int entryIndex, int doctrineIndex, ShopColumn targetColumn)
    {
        if (doctrineIndex < 0 || doctrineIndex >= slotRows.Count
            || panel == null || !panel.IsUnlocked(doctrineIndex))
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

        ShopEntry existing = GetSlot(doctrineIndex, targetColumn);
        if (existing == picked)
        {
            return;
        }

        // 참조 방식: 같은 조각을 여러 교리에 동시에 배치 가능.
        // 단, 보유 개수를 초과할 수는 없다(가용 = 보유 - 슬롯 사용중).
        int owned = CountOf(DoctrineInventory.Instance, picked);
        int used = CountUsed(picked);
        if (owned - used <= 0)
        {
            Debug.Log($"[재단] 조각 부족: {picked.Label} (보유 {owned} · 사용중 {used})");
            return;
        }

        activeDoctrine = doctrineIndex;
        SetSlot(doctrineIndex, targetColumn, picked);
        Debug.Log($"[재단] 교리{doctrineIndex + 1} 드롭 배치: {TabName(targetColumn)} → {picked.Label}");
        SyncAllDoctrines();
        Refresh();
    }

    private void HandleSlotDroppedOnSlot(int fromDoctrine, ShopColumn fromColumn, int toDoctrine, ShopColumn toColumn)
    {
        if (fromDoctrine < 0 || fromDoctrine >= slotRows.Count) return;
        if (toDoctrine < 0 || toDoctrine >= slotRows.Count) return;
        if (panel == null || !panel.IsUnlocked(fromDoctrine) || !panel.IsUnlocked(toDoctrine)) return;
        if (fromColumn != toColumn) return;
        if (fromDoctrine == toDoctrine && fromColumn == toColumn) return;
        ShopEntry moving = GetSlot(fromDoctrine, fromColumn);
        if (moving == null) return;
        ShopEntry target = GetSlot(toDoctrine, toColumn);
        SetSlot(toDoctrine, toColumn, moving);
        SetSlot(fromDoctrine, fromColumn, target);
        activeDoctrine = toDoctrine;
        SyncAllDoctrines();
        Refresh();
    }

    private void HandleSlotDroppedOutside(int fromDoctrine, ShopColumn fromColumn)
    {
        if (fromDoctrine < 0 || fromDoctrine >= slotRows.Count) return;
        if (GetSlot(fromDoctrine, fromColumn) == null) return;
        SetSlot(fromDoctrine, fromColumn, null);
        activeDoctrine = fromDoctrine;
        SyncAllDoctrines();
        Refresh();
    }

    // 현재 탭에서 가용 개수(보유 - 슬롯 사용중)가 1개 이상인 조각만 목록으로 만든다.
    // 같은 조각은 가용 개수만큼 한 줄씩 나열한다("×N" 표기 없음).
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

        for (int i = 0; i < entries.Count; i++)
        {
            // 가용 = 보유 - 슬롯 사용중. 꽉 찬 조각은 목록에서 숨긴다.
            int available = CountOf(inventory, entries[i]) - CountUsed(entries[i]);

            // 같은 조각을 합쳐서 "×N"으로 보여주지 않고, 가용 개수만큼 한 줄씩 나열한다.
            for (int copy = 0; copy < available; copy++)
            {
                items.Add(new FoundationPanelView.ItemData
                {
                    EntryIndex = i,
                    Label = entries[i].Label,
                    Detail = entries[i].Detail,
                    Count = 1
                });
            }
        }

        panel.SetItems(items,
            $"보유한 {TabName(currentTab)} 교리가 없습니다.\n상점에서 구매해 보세요.");
        panel.SetCountSummary(inventory != null ? inventory.Describe() : "인벤토리 없음");

        // 교리 칸 수만큼 슬롯 라벨 + 설명 줄을 모두 갱신한다.
        for (int d = 0; d < slotRows.Count; d++)
        {
            panel.SetSlots(d, GetLabel(d, ShopColumn.Subject), GetLabel(d, ShopColumn.Condition),
                GetLabel(d, ShopColumn.Result));
            panel.SetDescription(d, DescribeDoctrine(d));
        }

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
                && equipped[doctrineIndex].Doctrine.HasSameRule(composed);
            return $"{name}  ·  {DoctrineText.Describe(composed)}{(isActive ? "  (장착 중)" : string.Empty)}";
        }

        int filled = 0;
        for (int c = 0; c < ColumnCount; c++)
        {
            if (slotRows[doctrineIndex][c] != null)
            {
                filled++;
            }
        }

        return $"{name}  ·  슬롯 {filled}/3 — 왼쪽 조각을 드래그해서 놓으세요.";
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
        for (int d = 0; d < slotRows.Count; d++)
        {
            SyncDoctrine(d);
        }
    }

    // 한 교리의 슬롯 3칸이 다 차면 자동으로 장착하고, 하나라도 비면 장착 해제한다.
    // 조각 소모 없음(참조 방식) — 보유 1개면 여러 교리에 동시 배치 가능.
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
                manager.DeactivateEntry(equipped[doctrineIndex]);
                Debug.Log($"[재단] 교리{doctrineIndex + 1} 해제: 슬롯이 비었음");
                equipped[doctrineIndex] = null;
            }

            return;
        }

        // 이미 같은 조합이 장착 중이면 그대로 둔다.
        if (equipped[doctrineIndex] != null && equipped[doctrineIndex].Doctrine.HasSameRule(wanted))
        {
            return;
        }

        // 조합이 바뀌면 기존 것을 먼저 해제한다.
        if (equipped[doctrineIndex] != null)
        {
            manager.DeactivateEntry(equipped[doctrineIndex]);
            equipped[doctrineIndex] = null;
        }

        // 같은 조합이 다른 교리에 있어도 각각 장착한다.
        // 같은 조각을 여러 교리에 동시에 배치하는 2번 케이스 허용.

        if (!manager.TryActivate(wanted, null, out string reason, out var entry))
        {
            Debug.LogWarning($"[재단] 교리{doctrineIndex + 1} 장착 실패: {reason}");
            return;
        }

        equipped[doctrineIndex] = entry;
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

    // 슬롯에 이미 들어간 조각 개수(교리 무관 합산). 참조 방식이라 이동/교환 시 그대로 유지.
    private int CountUsed(ShopEntry entry)
    {
        if (entry == null)
        {
            return 0;
        }

        int used = 0;
        for (int d = 0; d < slotRows.Count; d++)
        {
            ShopEntry[] row = slotRows[d];
            if (row == null)
            {
                continue;
            }

            for (int c = 0; c < row.Length; c++)
            {
                if (row[c] == entry)
                {
                    used++;
                }
            }
        }

        return used;
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
        if (doctrineIndex < 0 || doctrineIndex >= slotRows.Count)
        {
            return null;
        }

        return slotRows[doctrineIndex][(int)column];
    }

    private void SetSlot(int doctrineIndex, ShopColumn column, ShopEntry entry)
    {
        if (doctrineIndex < 0 || doctrineIndex >= slotRows.Count)
        {
            return;
        }

        slotRows[doctrineIndex][(int)column] = entry;
    }
}
