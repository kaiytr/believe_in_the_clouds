using System;
using System.Collections.Generic;
using UnityEngine;

// 상점 화면 모듈 (장바구니형).
//
// - 위: 교리 나열 칸 (교리 12종 + 교리칸 1종 + 체력 회복 1종 = 14줄, 3열 그리드, 세로 스크롤).
//   카드 클릭 → 구매 칸에 1개 추가. 같은 카드를 여러 번 눌러 중복 구매 가능.
// - 아래: 구매 칸 (담은 순서대로 가로 스크롤). 카드 클릭 → 그 1개가 빠진다.
// - 구매 버튼: 구매 칸 전체를 TryPurchase 1건으로 결제 (조각 + 교리칸 + 체력 회복).
//   교리칸은 코인 결제 후 DoctrineSlotStore.PurchaseSlot()으로 +1.
//   체력 회복은 코인 결제 후 즉시 최대 체력 30% 회복 (교리가 아니라 일회성 구매).
// - 등급은 지금 신경 쓰지 않는다 (표시만 유지).
// - 실제 교리 조합/장착은 재단에서 한다.
public class ShopScreen : MonoBehaviour
{
    public static ShopScreen Instance { get; private set; }
    private ShopPanelView panel;
    private ShopInteractable openShop;
    private ShopInteractable pendingShop;
    private GameObject pendingInteractor;
    private object shopTipModalOwner;

    // 구매 칸: 담은 순서대로 항목 Id. 교리칸은 "slot", 체력 회복은 "heal".
    private readonly List<string> cart = new List<string>();
    private string purchaseId;
    public static event Action<ShopInteractable> ShopClosed;
    public bool IsOpen => panel != null && panel.IsOpen;
    public ShopInteractable OpenShop => openShop;

    // 교리 칸 1칸 가격 (코인). Puri 조각가(30/55/95) 대비 고정가.
    private const int SlotPrice = 100;

    // 체력 회복 1회 가격 (코인). 최대 체력 30% 회복.
    private const int HealPrice = 90;
    private const float HealMaxHpRatio = 0.3f;

    // 카탈로그 항목 Id → 표시 이름/가격. 교리칸("slot")/체력 회복("heal") 포함.
    private static string DisplayName(string id)
    {
        if (id == "slot")
        {
            return "교리칸";
        }

        if (id == "heal")
        {
            return "체력 회복";
        }

        if (ShopCatalog.TryGet(id, out ShopEntry entry))
        {
            return entry.Label;
        }

        return id;
    }

    private static int DisplayPrice(string id)
    {
        if (id == "slot")
        {
            return SlotPrice;
        }

        if (id == "heal")
        {
            return HealPrice;
        }

        if (ShopCatalog.TryGet(id, out ShopEntry entry))
        {
            return entry.Price;
        }

        return 0;
    }

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
        panel.CatalogClicked += HandleCatalogClicked;
        panel.CartClicked += HandleCartClicked;
        panel.BuyClicked += HandleBuyClicked;
        DontDestroyOnLoad(panel.gameObject);
        UiEventSystemBoot.Ensure();
    }
    private void HandleInteracted(InteractableBehaviour target, GameObject interactor)
    {
        if (target is not ShopInteractable shop) return;
        if (openShop == shop && IsOpen) Close();
        else if (!HasSeenShopTip() && TutorialTipPanel.Instance != null)
        {
            // 런당 최초 1회: 상점을 열기 전에 상점 TIP를 먼저 보여준다.
            ShowShopTip(shop, interactor);
        }
        else Open(shop, interactor);
    }
    private void HandleCloseRequested()
    {
        // 상점 TIP가 열려 있으면 TIP를 닫는다 (상점 패널은 아직 안 열린 상태).
        if (shopTipModalOwner != null && TutorialTipPanel.Instance != null && TutorialTipPanel.Instance.IsOpen)
        {
            TutorialTipPanel.Instance.HandleCloseRequested();
            return;
        }
        if (IsOpen) Close();
    }
    private void HandleCurrencyChanged(CurrencyChange change) { if (IsOpen) RefreshCart(); }

    public void Open(ShopInteractable shop, GameObject interactor)
    {
        if (shop == null) return;
        EnsurePanel();
        openShop = shop;
        UiEventSystemBoot.Ensure();
        cart.Clear(); purchaseId = null;
        panel.Open(shop.ShopName);
        RefreshCart();
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

    // ---- 상점 TIP (런당 최초 1회) ----
    // 상점 패널을 열기 전에 TIP를 먼저 보여주고, 닫히면 상점 패널을 연다.
    private static bool HasSeenShopTip()
    {
        EconomyService service = Currency.Service;
        if (service == null || !service.IsReady)
            return true; // 서비스 없으면 TIP 없이 바로 연다 (테스트 씬 등).
        return service.Snapshot().shopTipSeen;
    }

    private void ShowShopTip(ShopInteractable shop, GameObject interactor)
    {
        TutorialTipPanel tip = TutorialTipPanel.Instance;
        if (tip == null)
        {
            Open(shop, interactor);
            return;
        }

        pendingShop = shop;
        pendingInteractor = interactor;
        shopTipModalOwner = new object();
        UiModal.Push(shopTipModalOwner);
        tip.Closed -= OnShopTipClosed;
        tip.Closed += OnShopTipClosed;
        tip.OpenShopTip();
    }

    private void OnShopTipClosed()
    {
        TutorialTipPanel tip = TutorialTipPanel.Instance;
        if (tip != null)
            tip.Closed -= OnShopTipClosed;
        if (shopTipModalOwner != null)
            UiModal.Pop(shopTipModalOwner);
        shopTipModalOwner = null;

        Currency.Service?.TryMarkShopTipSeen();

        ShopInteractable shop = pendingShop;
        GameObject interactor = pendingInteractor;
        pendingShop = null;
        pendingInteractor = null;
        if (shop != null)
            Open(shop, interactor);
    }

    /// <summary>교리 나열 칸 카드 클릭 → 구매 칸에 1개 추가 (중복 가능).</summary>
    private void HandleCatalogClicked(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (id != "slot" && id != "heal" && !ShopCatalog.TryGet(id, out _)) return;
        cart.Add(id);
        purchaseId = null;
        RefreshCart();
    }

    /// <summary>구매 칸 카드 클릭 → 그 1개가 빠진다.</summary>
    private void HandleCartClicked(int index)
    {
        if (index < 0 || index >= cart.Count) return;
        cart.RemoveAt(index);
        purchaseId = null;
        RefreshCart();
    }

    /// <summary>구매 버튼: 구매 칸 전체를 1건으로 결제 (조각 + 교리칸 + 체력 회복).</summary>
    private void HandleBuyClicked()
    {
        if (cart.Count == 0) return;

        // 교리칸/체력 회복과 조각을 나눈다.
        int slotCount = 0;
        int healCount = 0;
        var pieceIds = new List<string>();
        foreach (string id in cart)
        {
            if (id == "slot") slotCount++;
            else if (id == "heal") healCount++;
            else pieceIds.Add(id);
        }

        DoctrineSlotStore store = DoctrineSlotStore.EnsureExists();
        if (store == null && slotCount > 0)
        {
            panel.SetFooter("교리 칸 저장소를 찾을 수 없습니다.");
            return;
        }

        if (!Currency.IsReady)
        {
            panel.SetFooter("재화 확인 중 · 저장 또는 사망 처리를 완료한 뒤 구매할 수 있습니다.");
            return;
        }

        // 합계 계산.
        long total = 0;
        foreach (string id in cart) total += DisplayPrice(id);
        if (total > Currency.Balance)
        {
            panel.SetFooter($"코인이 부족합니다. 합계 {total:N0} · 보유 {Currency.Balance:N0} · 칸은 유지됩니다.");
            return;
        }

        // 조각 먼저 결제 (원장에 지급). 실패 시 칸 유지.
        if (pieceIds.Count > 0)
        {
            purchaseId = purchaseId ?? "shop:" + Guid.NewGuid().ToString("N");
            var result = Currency.Service?.TryPurchase(new PurchaseRequest(pieceIds, purchaseId));
            if (result == null || !result.Success)
            {
                panel.SetFooter(result?.Error == EconomyError.InsufficientFunds ? "코인이 부족합니다. 칸은 유지됩니다." :
                    $"구매 실패: {result?.Error ?? EconomyError.NotReady} · 칸은 유지됩니다.");
                return;
            }

            purchaseId = null;

            // 지급이 끝난 조각은 칸에서 즉시 뺀다. 뒤에서 교리칸 결제가 실패해도
            // 같은 조각을 다시 결제해 중복 지급되는 일이 없게 한다.
            cart.RemoveAll(id => id != "slot" && id != "heal");
            RefreshCart();
            RefreshSlotSummary();
        }

        // 체력 회복 결제 (개당 고정가). 실패하면 남은 체력 회복만 칸에 유지한다.
        int healedCount = 0;
        for (int i = 0; i < healCount; i++)
        {
            PlayerHealth player = openShop != null
                ? openShop.GetComponentInParent<PlayerHealth>()
                : null;
            if (player == null)
            {
                player = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
            }

            if (player == null)
            {
                cart.Clear();
                for (int left = 0; left < slotCount; left++) cart.Add("slot");
                for (int left = healedCount; left < healCount; left++) cart.Add("heal");
                RefreshCart();
                RefreshSlotSummary();
                panel.SetFooter("플레이어를 찾을 수 없습니다 · 남은 체력 회복은 칸에 유지됩니다.");
                return;
            }

            string tx = "heal:" + Guid.NewGuid().ToString("N");
            var spend = Currency.TrySpend(HealPrice, "shop.heal", tx);
            if (spend == null || !spend.Success)
            {
                cart.Clear();
                for (int left = 0; left < slotCount; left++) cart.Add("slot");
                for (int left = healedCount; left < healCount; left++) cart.Add("heal");
                RefreshCart();
                RefreshSlotSummary();
                panel.SetFooter(spend?.Error == EconomyError.InsufficientFunds
                    ? $"코인이 부족합니다. 체력 회복 {HealPrice}코인 필요 · 보유 {Currency.Balance:N0} · 남은 체력 회복은 칸에 유지됩니다."
                    : $"체력 회복 구매 실패: {spend?.Error ?? EconomyError.NotReady} · 남은 체력 회복은 칸에 유지됩니다.");
                return;
            }

            player.Heal(player.MaxHp * HealMaxHpRatio);
            healedCount++;
        }

        // 교리칸 결제 (개당 고정가). 실패하면 남은 교리칸만 칸에 유지한다.
        int boughtSlots = 0;
        for (int i = 0; i < slotCount; i++)
        {
            string tx = "slot:" + Guid.NewGuid().ToString("N");
            var spend = Currency.TrySpend(SlotPrice, "shop.slot", tx);
            if (spend == null || !spend.Success)
            {
                int remainingSlots = slotCount - boughtSlots;
                cart.Clear();
                for (int left = 0; left < healedCount; left++) cart.Add("heal");
                for (int left = 0; left < remainingSlots; left++) cart.Add("slot");
                RefreshCart();
                RefreshSlotSummary();
                panel.SetFooter(spend?.Error == EconomyError.InsufficientFunds
                    ? $"코인이 부족합니다. 교리칸 {SlotPrice}코인 필요 · 보유 {Currency.Balance:N0} · 남은 교리칸은 칸에 유지됩니다."
                    : $"교리칸 구매 실패: {spend?.Error ?? EconomyError.NotReady} · 남은 교리칸은 칸에 유지됩니다.");
                return;
            }

            store.PurchaseSlot();
            boughtSlots++;
        }

        int pieceCount = pieceIds.Count;
        int boughtHeals = healedCount;
        cart.Clear(); purchaseId = null;
        RefreshCart();
        RefreshSlotSummary();
        panel.SetFooter($"{pieceCount}장 + 교리칸 {boughtSlots}칸 + 체력 회복 {boughtHeals}회 구매 · 합계 {total:N0}코인 결제 · 보유 {Currency.Balance:N0}");
        Debug.Log($"[상점] 장바구니 구매 조각 {pieceCount}장 + 칸 {boughtSlots}칸 + 회복 {boughtHeals}회 → 보유 {Currency.Balance:N0}");
    }

    private void RefreshSlotSummary()
    {
        DoctrineSlotStore store = DoctrineSlotStore.EnsureExists();
        int total = store != null ? store.SlotCount : DoctrineSlotStore.DefaultSlots;
        panel.SetSlotSummary(total);
    }

    /// <summary>구매 칸 다시 그리기 + 합계/버튼 갱신.</summary>
    private void RefreshCart()
    {
        var names = new List<string>(cart.Count);
        var prices = new List<int>(cart.Count);
        long total = 0;
        foreach (string id in cart)
        {
            names.Add(DisplayName(id));
            int price = DisplayPrice(id);
            prices.Add(price);
            total += price;
        }

        panel.SetCart(cart, names, prices);
        panel.SetTotal(total, Currency.IsReady ? Currency.Balance : 0);

        if (cart.Count == 0)
        {
            panel.SetFooter("위에서 교리를 눌러 담으세요.");
        }
        else if (!Currency.IsReady)
        {
            panel.SetFooter("재화 확인 중 · 저장 또는 사망 처리를 완료한 뒤 구매할 수 있습니다.");
        }
        else
        {
            panel.SetFooter($"구매 칸 {cart.Count}개 · 합계 {total:N0} · 보유 {Currency.Balance:N0} · 부족 {Math.Max(0, total - Currency.Balance):N0}");
        }

        bool canBuy = Currency.IsReady && cart.Count > 0 && total <= Currency.Balance;
        panel.SetBuyEnabled(canBuy, "구매");
    }
}
