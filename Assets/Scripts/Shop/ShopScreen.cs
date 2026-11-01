using System;
using System.Collections.Generic;
using UnityEngine;

// 상점 화면 모듈 (Puri 재화 구조 + Leesc 조각/칸 분리 버튼).
//
// - Puri 구조: 선택은 ShopCatalog Id(string) 기반, 구매는 Currency.Service.TryPurchase.
// - Leesc 기능: 조각 구매 / 교리 칸 구매 버튼 2개 분리.
//   - 조각 구매: 선택된 Id들 → TryPurchase (코인 차감 + 원장에 조각 지급).
//   - 교리 칸 구매: 조각 선택과 무관, DoctrineSlotStore +1 (코인 결제, 고정가).
// - 실제 교리 조합/장착은 재단에서 한다.
public class ShopScreen : MonoBehaviour
{
    public static ShopScreen Instance { get; private set; }
    private ShopPanelView panel;
    private ShopInteractable openShop;
    private readonly HashSet<string> selected = new HashSet<string>(StringComparer.Ordinal);
    private string purchaseId;
    public static event Action<ShopInteractable> ShopClosed;
    public bool IsOpen => panel != null && panel.IsOpen;
    public ShopInteractable OpenShop => openShop;

    // 교리 칸 1칸 가격 (코인). Puri 조각가(30/55/95) 대비 고정가.
    private const int SlotPrice = 100;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBoot()
    {
        if (Instance == null) new GameObject("[ShopScreen]").AddComponent<ShopScreen>();
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsurePanel();
    }
    private void OnEnable()
    {
        InteractableBehaviour.Interacted += HandleInteracted;
        UiModal.CloseRequested += HandleCloseRequested;
        Currency.Changed += HandleCurrencyChanged;
    }
    private void OnDisable()
    {
        InteractableBehaviour.Interacted -= HandleInteracted;
        UiModal.CloseRequested -= HandleCloseRequested;
        Currency.Changed -= HandleCurrencyChanged;
        Close();
    }
    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        if (panel != null) Destroy(panel.gameObject);
    }
    private void EnsurePanel()
    {
        if (panel != null) return;
        panel = ShopPanelView.BuildRuntime();
        panel.CardClicked += HandleCardClicked;
        panel.PieceBuyClicked += HandlePieceBuyClicked;
        panel.SlotBuyClicked += HandleSlotBuyClicked;
        DontDestroyOnLoad(panel.gameObject);
        UiEventSystemBoot.Ensure();
    }
    private void HandleInteracted(InteractableBehaviour target, GameObject interactor)
    {
        if (target is not ShopInteractable shop) return;
        if (openShop == shop && IsOpen) Close();
        else Open(shop, interactor);
    }
    private void HandleCloseRequested() { if (IsOpen) Close(); }
    private void HandleCurrencyChanged(CurrencyChange change) { if (IsOpen) RefreshSummary(); }

    public void Open(ShopInteractable shop, GameObject interactor)
    {
        if (shop == null) return;
        EnsurePanel();
        openShop = shop;
        UiEventSystemBoot.Ensure();
        selected.Clear(); purchaseId = null;
        panel.Open(shop.ShopName);
        RefreshSummary();
        RefreshSlotSummary();
        UiModal.Push(this);
    }
    public void Close()
    {
        if (!IsOpen) return;
        ShopInteractable closedShop = openShop;
        panel.Close(); UiModal.Pop(this); openShop = null;
        if (closedShop != null) ShopClosed?.Invoke(closedShop);
    }

    private void HandleCardClicked(ShopColumn column, int index)
    {
        var entries = ShopCatalog.GetColumn(column);
        if (index < 0 || index >= entries.Count) return;
        string id = entries[index].Id;
        if (!selected.Remove(id)) selected.Add(id);
        purchaseId = null;
        RefreshSummary();
    }

    private void RefreshSlotSummary()
    {
        DoctrineSlotStore store = DoctrineSlotStore.EnsureExists();
        int total = store != null ? store.SlotCount : DoctrineSlotStore.DefaultSlots;
        panel.SetSlotSummary(total);
        RefreshSlotButton();
    }

    private void RefreshSlotButton()
    {
        bool affordable = Currency.IsReady && Currency.CanAfford(SlotPrice);
        DoctrineSlotStore store = DoctrineSlotStore.EnsureExists();
        int total = store != null ? store.SlotCount : DoctrineSlotStore.DefaultSlots;
        panel.SetSlotBuyEnabled(affordable, $"교리칸 구매\n{total} → {total + 1}칸 ({SlotPrice}코인)");
    }

    /// <summary>조각 구매: 선택된 Id들을 TryPurchase로 결제 + 원장 지급.</summary>
    private void HandlePieceBuyClicked()
    {
        if (selected.Count == 0) return;
        purchaseId = purchaseId ?? "shop:" + Guid.NewGuid().ToString("N");
        var result = Currency.Service?.TryPurchase(new PurchaseRequest(selected, purchaseId));
        if (result == null || !result.Success)
        {
            panel.SetFooter(result?.Error == EconomyError.InsufficientFunds ? "코인이 부족합니다. 선택은 유지됩니다." :
                $"구매 실패: {result?.Error ?? EconomyError.NotReady} · 선택은 유지됩니다.");
            return;
        }
        int count = selected.Count;
        selected.Clear(); purchaseId = null;
        RefreshSummary();
        RefreshSlotSummary();
        panel.SetFooter($"{count}장 구매 · {result.AppliedAmount:N0}코인 결제 · 보유 {Currency.Balance:N0}");
        Debug.Log($"[상점] 조각 구매 {count}장 → 보유 {Currency.Balance:N0}");
    }

    /// <summary>교리 칸 구매: 조각 선택과 무관, 코인 결제 후 칸 +1.</summary>
    private void HandleSlotBuyClicked()
    {
        DoctrineSlotStore store = DoctrineSlotStore.EnsureExists();
        if (store == null)
        {
            panel.SetFooter("교리 칸 저장소를 찾을 수 없습니다.");
            return;
        }

        if (!Currency.IsReady)
        {
            panel.SetFooter("재화 확인 중 · 저장 또는 사망 처리를 완료한 뒤 구매할 수 있습니다.");
            return;
        }

        string tx = "slot:" + Guid.NewGuid().ToString("N");
        var spend = Currency.TrySpend(SlotPrice, "shop.slot", tx);
        if (spend == null || !spend.Success)
        {
            panel.SetFooter(spend?.Error == EconomyError.InsufficientFunds
                ? $"코인이 부족합니다. 교리칸 {SlotPrice}코인 필요 · 보유 {Currency.Balance:N0}"
                : $"교리칸 구매 실패: {spend?.Error ?? EconomyError.NotReady}");
            RefreshSlotButton();
            return;
        }

        int total = store.PurchaseSlot();
        panel.SetSlotSummary(total);
        RefreshSummary();
        RefreshSlotButton();
        panel.SetFooter($"교리 칸 구매 → 총 {total}칸 · 보유 {Currency.Balance:N0}. 재단에서 확인하세요.");
        Debug.Log($"[상점] 교리 칸 구매 → 총 {total}칸");
    }

    private void RefreshSummary()
    {
        panel.ClearSelection();
        long price = 0;
        var labels = new string[3];
        foreach (ShopColumn column in Enum.GetValues(typeof(ShopColumn)))
        {
            var entries = ShopCatalog.GetColumn(column);
            var names = new List<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (!selected.Contains(entries[i].Id)) continue;
                panel.AddSelection(column, i);
                price += entries[i].Price;
                names.Add(entries[i].Label);
            }
            labels[(int)column] = string.Join(", ", names);
        }
        panel.SetSelectionSummary(labels[0], labels[1], labels[2]);
        if (!Currency.IsReady)
        {
            panel.SetFooter("재화 확인 중 · 저장 또는 사망 처리를 완료한 뒤 구매할 수 있습니다.");
        }
        else
        {
            panel.SetFooter($"보유 {Currency.Balance:N0} · 선택 {selected.Count}장 · 합계 {price:N0} · 부족 {Math.Max(0, price - Currency.Balance):N0}");
        }

        bool canBuy = Currency.IsReady && selected.Count > 0 && price <= Currency.Balance;
        panel.SetPieceBuyEnabled(canBuy, canBuy ? $"조각 구매 ({selected.Count}장 · {price:N0})" : "조각 구매");
        RefreshSlotButton();
    }
}
