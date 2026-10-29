using System.Collections.Generic;
using UnityEngine;

// 재단 화면 모듈.
//
// - 재단 상호작용이 발생하면 패널을 열고, E/ESC로 닫는다.
// - 왼쪽: 탭(주어/조건/결과)으로 인벤토리에 있는 조각만 골라 본다. 카드를 누르면
//   교리1의 해당 슬롯에 배치된다 (같은 카드를 다시 누르면 해제).
// - 오른쪽: 교리1 슬롯 3개 + 합성 버튼이 동작한다. 교리2·3은 아직 틀만 있다.
//   3칸이 다 차면 합성 → ShopCatalog.Compose → DoctrineManager.TryActivate →
//   조각 3장 소모 → HUD 교리1에 반영.
// - 상점과 마찬가지로 InteractableBehaviour.Interacted 이벤트를 듣고 스스로 연다.
//   공통 상호작용 코드는 재단을 모른다.
public class FoundationScreen : MonoBehaviour
{
    public static FoundationScreen Instance { get; private set; }

    private FoundationPanelView panel;
    private FoundationInteractable openFoundation;

    private ShopColumn currentTab = ShopColumn.Subject;

    // 교리1 슬롯: 열마다 하나씩 기억한다. 탭을 바꿔도 유지된다.
    private ShopEntry slotSubject;
    private ShopEntry slotCondition;
    private ShopEntry slotResult;

    public bool IsOpen => panel != null && panel.IsOpen;
    public ShopColumn CurrentTab => currentTab;

    /// <summary>현재 탭의 슬롯에 들어 있는 조각. 없으면 null.</summary>
    public ShopEntry SelectedEntry
    {
        get
        {
            switch (currentTab)
            {
                case ShopColumn.Subject: return slotSubject;
                case ShopColumn.Condition: return slotCondition;
                default: return slotResult;
            }
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
        panel.SlotClicked += HandleSlotClicked;
        panel.CombineClicked += HandleCombineClicked;
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
        ClearSlots();

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
        Debug.Log($"[재단] 탭 전환: {TabName(column)}");
        Refresh();
    }

    // 왼쪽 카드를 누르면 현재 탭의 슬롯에 배치한다.
    // 이미 같은 카드가 들어 있으면 해제한다.
    private void HandleItemClicked(int entryIndex)
    {
        IReadOnlyList<ShopEntry> entries = ShopCatalog.GetColumn(currentTab);
        if (entryIndex < 0 || entryIndex >= entries.Count)
        {
            return;
        }

        ShopEntry picked = entries[entryIndex];
        ShopEntry current = GetSlot(currentTab);

        if (current == picked)
        {
            SetSlot(currentTab, null);
            Debug.Log($"[재단] 슬롯 해제: {TabName(currentTab)}");
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

            SetSlot(currentTab, picked);
            Debug.Log($"[재단] 슬롯 배치: {TabName(currentTab)} → {picked.Label}");
        }

        Refresh();
    }

    // 오른쪽 슬롯을 직접 누르면 해당 슬롯을 비운다.
    private void HandleSlotClicked(ShopColumn column)
    {
        if (GetSlot(column) == null)
        {
            return;
        }

        SetSlot(column, null);
        Debug.Log($"[재단] 슬롯 해제: {TabName(column)}");
        Refresh();
    }

    // 합성: 3칸이 다 차면 Compose → TryActivate → 조각 3장 소모.
    private void HandleCombineClicked()
    {
        if (slotSubject == null || slotCondition == null || slotResult == null)
        {
            Debug.Log("[재단] 합성 불가: 슬롯 3개를 모두 채우세요.");
            return;
        }

        Doctrine doctrine = ShopCatalog.Compose(slotSubject, slotCondition, slotResult);
        if (doctrine == null)
        {
            Debug.LogWarning("[재단] 합성 실패: 교리를 만들 수 없습니다.");
            return;
        }

        DoctrineManager manager = DoctrineManager.Instance;
        if (manager == null)
        {
            manager = DoctrineHudBoot.EnsureManagerExists();
        }

        if (manager == null)
        {
            Debug.LogWarning("[재단] 합성 실패: 교리 엔진을 찾을 수 없습니다.");
            return;
        }

        // 이미 같은 교리를 갖고 있으면 중복 장착하지 않는다.
        foreach (DoctrineManager.ActiveDoctrine active in manager.ActiveDoctrines)
        {
            if (active?.Doctrine != null && active.Doctrine.HasSameRule(doctrine))
            {
                panel.SetDoctrine1Description($"{DoctrineText.Describe(doctrine)}  ·  이미 보유한 교리입니다.");
                Debug.Log($"[재단] 합성 생략: 이미 보유 → {DoctrineText.ShortName(doctrine)}");
                return;
            }
        }

        // 씬을 옮겨도 유지되도록 owner 없이 전역 등록한다.
        if (!manager.TryActivate(doctrine, out string reason))
        {
            panel.SetDoctrine1Description($"합성 실패: {reason}");
            Debug.LogWarning($"[재단] 합성 실패: {reason}");
            return;
        }

        // 장착 성공 → 조각 3장 소모.
        DoctrineInventory inventory = DoctrineInventory.Instance;
        if (inventory != null)
        {
            inventory.ConsumeEntry(slotSubject);
            inventory.ConsumeEntry(slotCondition);
            inventory.ConsumeEntry(slotResult);
        }

        Debug.Log($"[재단] 합성 완료: {DoctrineText.Describe(doctrine)} → 교리1 ({manager.ActiveCount}개 보유)");
        ClearSlots();
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

        // 왼쪽에서 고른(현재 탭 슬롯) 조각의 EntryIndex. 없으면 -1.
        ShopEntry current = GetSlot(currentTab);
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

        panel.SetDoctrine1Slots(
            slotSubject?.Label,
            slotCondition?.Label,
            slotResult?.Label);

        UpdateCombineButton();
    }

    private void UpdateCombineButton()
    {
        if (slotSubject != null && slotCondition != null && slotResult != null)
        {
            Doctrine preview = ShopCatalog.Compose(slotSubject, slotCondition, slotResult);
            panel.SetDoctrine1Description(preview != null
                ? $"교리1  ·  {DoctrineText.Describe(preview)}"
                : "교리1  ·  합성할 수 없습니다.");
            panel.SetCombineEnabled(true, "합성");
        }
        else
        {
            int filled = (slotSubject != null ? 1 : 0)
                + (slotCondition != null ? 1 : 0)
                + (slotResult != null ? 1 : 0);
            panel.SetDoctrine1Description($"교리1  ·  슬롯 {filled}/3 — 왼쪽에서 조각을 고르세요.");
            panel.SetCombineEnabled(false, $"합성 ({filled}/3)");
        }
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

    private ShopEntry GetSlot(ShopColumn column)
    {
        switch (column)
        {
            case ShopColumn.Subject: return slotSubject;
            case ShopColumn.Condition: return slotCondition;
            default: return slotResult;
        }
    }

    private void SetSlot(ShopColumn column, ShopEntry entry)
    {
        switch (column)
        {
            case ShopColumn.Subject: slotSubject = entry; break;
            case ShopColumn.Condition: slotCondition = entry; break;
            default: slotResult = entry; break;
        }
    }

    private void ClearSlots()
    {
        slotSubject = null;
        slotCondition = null;
        slotResult = null;
    }
}
