using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-800)]
public sealed class ChestTestContext : MonoBehaviour
{
    private EconomyService previous, activeService;
    private MemoryEconomyStore store;
    private PlayerHealth player;
    private ChestInteractable[] chests;
    private Text status, message;
    private GameObject modal;
    public EconomyService Service => activeService;
    public MemoryEconomyStore Store => store;
    public PlayerHealth Player => player;
    public string StageId => activeService?.RunSnapshot()?.stage?.id;

    private void Awake()
    {
        EconomyRuntime.Ensure(); previous = Currency.Service;
        store = new MemoryEconomyStore();
        SetService(new EconomyService(store, new EconomyRandom(71)));
    }
    private void Start()
    {
        player = FindFirstObjectByType<PlayerHealth>();
        chests = FindObjectsByType<ChestInteractable>(FindObjectsSortMode.None).OrderBy(c => c.SpawnId, StringComparer.Ordinal).ToArray();
        PrepareStage(); BuildControls();
    }
    private void SetService(EconomyService service)
    {
        activeService = service; EconomyRuntime.Instance.SetService(service);
    }
    private void PrepareStage()
    {
        if (!Service.IsReady) return;
        Service.TryInitializeRun(player.MaxHp);
        LobbyRunData run = Service.RunSnapshot();
        if (run.area == RunArea.Lobby)
            Service.TryDepartBranch(run.candidates.First(c => c.role == BranchRole.Combat).id, player.MaxHp);
        string stageId = StageId;
        if (stageId == null) return;
        CurrencyResult result = Service.TryRegisterStageChests(new ChestManifest {
            stageInstanceId = stageId, spawnIds = chests.Select(c => c.SpawnId).ToArray() });
        foreach (ChestInteractable chest in chests) chest.Bind(stageId);
        if (!result.Success) Show("상자 준비 실패 · " + result.Error);
    }
    public void Reload()
    {
        if (store.FailWrites || Service.HasPendingDeath) { Show("저장 실패를 해제한 뒤 재로드하세요."); return; }
        InteractionController.Instance?.CancelHold();
        SetService(new EconomyService(store));
        player.ResetHealth(); PrepareStage(); Show("테스트 저장 재로드 · 열린 상태와 보상 유지");
    }
    public void ResetFixture()
    {
        InteractionController.Instance?.CancelHold();
        store = new MemoryEconomyStore(); SetService(new EconomyService(store));
        player.ResetHealth(); PrepareStage(); Show("새 테스트 프로필 · 0코인 / 0조각 / 모든 상자 닫힘");
    }
    public void DamagePlayer() => player.TakeDamage(10f);
    public void ToggleSaveFailure() { store.FailWrites = !store.FailWrites; Show(store.FailWrites ? "저장 실패 ON" : "저장 실패 OFF · 새 E 입력으로 재시도"); }
    public void ShowModal() { InteractionController.Instance?.CancelHold(); modal.SetActive(true); UiModal.Push(this); }
    public void CloseModal() { if (modal != null) modal.SetActive(false); UiModal.Pop(this); }
    private void OnEnable() => UiModal.CloseRequested += CloseModal;
    private void OnDisable() { UiModal.CloseRequested -= CloseModal; CloseModal(); }
    private void OnDestroy()
    {
        if (EconomyRuntime.Instance != null && Currency.Service == activeService && previous != null)
        { activeService.SettleAllPending(); EconomyRuntime.Instance.SetService(previous); }
    }
    private void Show(string text) { if (message != null) message.text = text; }
    private void Update()
    {
        if (status == null) return;
        status.text = $"코인 {Service.Balance:N0}  ·  비행 중 {Service.PendingBalance:N0}\n" +
            $"교리 조각 {EconomyLedger.Total(Service.Snapshot().pieces)}개  ·  열린 상자 {chests.Count(c => c.IsOpened)}/{chests.Length}\n" +
            (store.FailWrites ? "저장 실패 ON" : "메모리 테스트 저장 · 실제 프로필 분리");
    }
    private void BuildControls()
    {
        var root = new GameObject("Chest Test Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; root.GetComponent<Canvas>().sortingOrder = 110;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        GameObject panel = UiTheme.CreateImage("Panel", root.transform, new Color(.06f, .09f, .14f, .96f));
        Place(panel.GetComponent<RectTransform>(), new Vector2(24, -24), new Vector2(540, 360));
        Label(panel.transform, "Title", "상자 시스템 테스트", 26, new Vector2(20, -14), new Vector2(500, 40));
        Label(panel.transform, "Instructions", "A/D 이동 · E 1.5초 유지 · 중단하면 0%\n조각 1개 + 코인 7~17 · 열린 상자는 재사용 불가", 17, new Vector2(20, -60), new Vector2(500, 54));
        status = Label(panel.transform, "Status", "", 18, new Vector2(20, -121), new Vector2(500, 80));
        string[] names = { "피해 10", "저장 실패 ON/OFF", "저장 재로드", "새 빈 테스트 프로필", "모달 열기", "체력 회복" };
        Action[] actions = { DamagePlayer, ToggleSaveFailure, Reload, ResetFixture, ShowModal, () => player.Heal(100) };
        for (int i = 0; i < names.Length; i++)
            Button(panel.transform, names[i], new Vector2(20 + i % 2 * 254, -208 - i / 2 * 40), actions[i]);
        message = Label(panel.transform, "Message", "E를 미리 누른 채 접근하면 시작하지 않습니다.", 15, new Vector2(20, -331), new Vector2(500, 26));
        modal = UiTheme.CreateImage("Chest Test Modal", root.transform, new Color(.06f, .08f, .13f, .98f));
        RectTransform rect = modal.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(600, 220);
        Label(modal.transform, "Text", "안내 창 · 월드 상호작용 중단\nE / ESC로 닫은 뒤 E를 놓고 다시 눌러주세요.", 22, new Vector2(26, -24), new Vector2(550, 100));
        Button(modal.transform, "닫기", new Vector2(180, -152), CloseModal);
        modal.SetActive(false); UiEventSystemBoot.Ensure();
    }
    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size; }
    private static Text Label(Transform parent, string name, string content, int size, Vector2 position, Vector2 bounds)
    { Text label = UiTheme.CreateText(name, parent, content, size, Color.white, TextAnchor.UpperLeft); Place(label.rectTransform, position, bounds); return label; }
    private static void Button(Transform parent, string name, Vector2 position, Action action)
    {
        GameObject root = UiTheme.CreateImage(name, parent, new Color(.19f, .28f, .4f));
        Place(root.GetComponent<RectTransform>(), position, new Vector2(244, 34));
        Image image = root.GetComponent<Image>(); image.raycastTarget = true;
        var button = root.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => action());
        Text text = UiTheme.CreateText("Label", root.transform, name, 16, Color.white); UiTheme.Stretch(text.rectTransform, 3);
    }
}
