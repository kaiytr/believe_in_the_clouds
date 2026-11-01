using System;
using System.Collections.Generic;
using UnityEngine;

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
        panel.BuyClicked += HandleBuyClicked;
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

    private void HandleBuyClicked()
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
        panel.SetFooter($"{count}장 구매 · {result.AppliedAmount:N0}코인 결제 · 보유 {Currency.Balance:N0}");
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
            panel.SetFooter("재화 확인 중 · 저장 또는 사망 처리를 완료한 뒤 구매할 수 있습니다.");
        else
            panel.SetFooter($"보유 {Currency.Balance:N0} · 선택 {selected.Count}장 · 합계 {price:N0} · 부족 {Math.Max(0, price - Currency.Balance):N0}");
        panel.SetBuyEnabled(Currency.IsReady && selected.Count > 0 && price <= Currency.Balance);
    }
}
