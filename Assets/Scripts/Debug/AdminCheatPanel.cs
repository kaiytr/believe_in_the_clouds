using System;
using UnityEngine;
using UnityEngine.UI;

// 확인용 관리자 창 (디버그 전용).
//
// - 어떤 씬에서든 F9로 열고 닫는다. 씬에 배치할 필요 없다(자동 생성).
// - 코인 지급/차감과 조각 지급을 버튼 한 번으로 처리한다.
// - 실제 재화 API(Currency)를 그대로 쓰므로 저장·원장·HUD 갱신이 정상 동작한다.
// - 게임 규칙에는 관여하지 않는다. 빌드에서도 살아 있지만 F9를 눌러야만 뜬다.
public sealed class AdminCheatPanel : MonoBehaviour
{
    private const KeyCode ToggleKey = KeyCode.F9;
    private const int QuickGrantAmount = 100;
    private const int BigGrantAmount = 1000;

    private static AdminCheatPanel instance;

    private GameObject root;
    private Text balanceText;
    private Text messageText;
    private float messageUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBoot()
    {
        if (instance != null) return;
        GameObject go = new GameObject("[AdminCheatPanel]");
        DontDestroyOnLoad(go);
        go.AddComponent<AdminCheatPanel>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUi();
        Currency.Changed += HandleCurrencyChanged;
    }

    private void OnDestroy()
    {
        Currency.Changed -= HandleCurrencyChanged;
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
        {
            SetOpen(root != null && !root.activeSelf);
        }

        if (root == null || !root.activeSelf) return;

        UpdateBalance();
        if (messageText != null && messageUntil > 0f && Time.unscaledTime >= messageUntil)
        {
            messageText.text = string.Empty;
            messageUntil = 0f;
        }
    }

    private void HandleCurrencyChanged(CurrencyChange change)
    {
        UpdateBalance();
    }

    private void SetOpen(bool open)
    {
        if (root == null) return;
        root.SetActive(open);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (open)
        {
            UpdateBalance();
            ShowMessage($"관리자 창 · {ToggleKey}로 닫기");
        }
    }

    private void UpdateBalance()
    {
        if (balanceText == null) return;
        balanceText.text = Currency.IsReady
            ? $"보유 코인 {Currency.Balance:N0}   ·   비행 중 {Currency.PendingBalance:N0}"
            : "재화 확인 중";
    }

    // ---------------- 동작 ----------------

    private void GrantCoins(int amount)
    {
        var result = Currency.TryGrant(amount, "admin.cheat");
        ShowMessage(result.Success
            ? $"코인 +{result.AppliedAmount:N0} → 보유 {result.BalanceAfter:N0}"
            : $"지급 실패: {result.Error}");
    }

    private void SpendCoins(int amount)
    {
        var result = Currency.TrySpend(amount, "admin.cheat");
        ShowMessage(result.Success
            ? $"코인 -{result.AppliedAmount:N0} → 보유 {result.BalanceAfter:N0}"
            : $"차감 실패: {result.Error}");
    }

    private void ResetCoins()
    {
        if (Currency.Balance <= 0)
        {
            ShowMessage("이미 코인이 0입니다.");
            return;
        }

        SpendCoins(Currency.Balance);
    }

    private void GrantAllPieces()
    {
        if (Currency.Service == null || !Currency.IsReady)
        {
            ShowMessage("재화 시스템이 준비되지 않았습니다.");
            return;
        }

        int granted = 0;
        EconomyError lastError = EconomyError.None;
        foreach (ShopColumn column in Enum.GetValues(typeof(ShopColumn)))
        {
            foreach (ShopEntry entry in ShopCatalog.GetColumn(column))
            {
                var result = Currency.Service.TryGrantPiece(entry.Id, 1, "admin.cheat.piece");
                if (result.Success) granted++;
                else lastError = result.Error;
            }
        }

        ShowMessage(granted > 0
            ? $"조각 {granted}종 지급 · 재단에서 확인"
            : $"조각 지급 실패: {lastError}");
    }

    private void GrantSlots(int count)
    {
        DoctrineSlotStore store = DoctrineSlotStore.EnsureExists();
        if (store == null)
        {
            ShowMessage("교리 칸 저장소를 찾을 수 없습니다.");
            return;
        }

        for (int i = 0; i < count; i++) store.PurchaseSlot();
        ShowMessage($"교리 칸 +{count} → 총 {store.SlotCount}칸 (코인 소모 없음)");
    }

    private void ShowMessage(string message)
    {
        if (messageText == null) return;
        messageText.text = message;
        messageUntil = Time.unscaledTime + 4f;
    }

    // ---------------- UI ----------------

    private void BuildUi()
    {
        root = new GameObject("[AdminCheatCanvas]", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900; // 상점/재단(200)보다 위.

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        UiEventSystemBoot.Ensure();

        GameObject panel = UiTheme.CreateImage("Panel", root.transform,
            new Color(0.06f, 0.08f, 0.12f, 0.94f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-24f, -24f);
        panelRect.sizeDelta = new Vector2(430f, 430f);

        Text title = UiTheme.CreateText("Title", panel.transform,
            $"관리자 창 · {ToggleKey} 닫기", 18, Color.white, TextAnchor.MiddleLeft);
        Place(title.rectTransform, new Vector2(18f, -12f), new Vector2(394f, 28f));

        balanceText = UiTheme.CreateText("Balance", panel.transform, "보유 코인 —", 20,
            new Color(1f, 0.86f, 0.42f, 1f), TextAnchor.MiddleLeft);
        Place(balanceText.rectTransform, new Vector2(18f, -44f), new Vector2(394f, 30f));

        AddButton(panel.transform, 0, $"+{QuickGrantAmount} 코인", () => GrantCoins(QuickGrantAmount));
        AddButton(panel.transform, 1, $"+{BigGrantAmount} 코인", () => GrantCoins(BigGrantAmount));
        AddButton(panel.transform, 2, $"-{QuickGrantAmount} 코인", () => SpendCoins(QuickGrantAmount));
        AddButton(panel.transform, 3, "코인 0으로", ResetCoins);
        AddButton(panel.transform, 4, "조각 12종 모두 +1", GrantAllPieces);
        AddButton(panel.transform, 5, "교리 칸 +1", () => GrantSlots(1));
        AddButton(panel.transform, 6, "교리 칸 +3", () => GrantSlots(3));
        AddButton(panel.transform, 7, "닫기", () => SetOpen(false));

        messageText = UiTheme.CreateText("Message", panel.transform, string.Empty, 15,
            new Color(1f, 0.85f, 0.5f, 1f), TextAnchor.UpperLeft);
        Place(messageText.rectTransform, new Vector2(18f, -386f), new Vector2(394f, 34f));

        root.SetActive(false);
    }

    private void AddButton(Transform parent, int index, string label, Action action)
    {
        GameObject buttonObject = UiTheme.CreateImage("Admin_" + label, parent,
            new Color(0.22f, 0.32f, 0.45f, 1f));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(18f + index % 2 * 200f, -84f - index / 2 * 42f);
        rect.sizeDelta = new Vector2(192f, 34f);

        Image image = buttonObject.GetComponent<Image>();
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => action());

        Text text = UiTheme.CreateText("Label", buttonObject.transform, label, 15,
            Color.white, TextAnchor.MiddleCenter);
        UiTheme.Stretch(text.rectTransform, 2f);
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
