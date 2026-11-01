using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

// Explicit fixture UI, only present in EconomyTest. Never touches player saves.
public sealed class TestEconomyContext : MonoBehaviour
{
    private sealed class FixtureStore : IEconomyStore
    {
        public readonly IEconomyStore Inner;
        public bool FailWrites;
        public FixtureStore(IEconomyStore inner) { Inner = inner; }
        public EconomyLoadResult Load() => Inner.Load();
        public bool TrySave(EconomyProfileData data) => !FailWrites && Inner.TrySave(data);
    }
    private FixtureStore store;
    private PlayerHealth player;
    private ShopInteractable shop;
    private TestEquipmentAdapter equipment;
    private Text status, message;
    private string sessionId = Guid.NewGuid().ToString("N");
    private int spawnIndex;
    public EconomyService Service => Currency.Service;
    public TestEquipmentAdapter Equipment => equipment;
    public string SavePath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Economy/Fixtures/currency-test-v1.json"));

    private void Awake()
    {
        EconomyRuntime.Ensure();
        bool automated = Array.Exists(Environment.GetCommandLineArgs(), arg => arg.Equals("-runTests", StringComparison.OrdinalIgnoreCase));
        store = new FixtureStore(automated ? (IEconomyStore)new MemoryEconomyStore() : new JsonEconomyStore(SavePath));
        EconomyRuntime.Instance.SetService(new EconomyService(store));
    }
    private void Start()
    {
        player = FindFirstObjectByType<PlayerHealth>();
        if (player.GetComponent<DoctrineSubject>() == null)
            player.gameObject.AddComponent<DoctrineSubject>().Identity = SubjectType.Player;
        if (DoctrineManager.Instance == null) new GameObject("[Test DoctrineManager]").AddComponent<DoctrineManager>();
        equipment = new TestEquipmentAdapter(player.gameObject);
        player.GetComponent<PlayerDeathCoordinator>().SetEquipmentAdapter(equipment);
        shop = new GameObject("Economy Test Shop").AddComponent<ShopInteractable>();
        shop.transform.SetParent(transform);
        BuildPanel();
    }

    private void BuildPanel()
    {
        var root = new GameObject("Economy Test Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 110;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        GameObject panel = UiTheme.CreateImage("Panel", root.transform, new Color(.08f, .12f, .19f, .96f));
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(28, -28); rect.sizeDelta = new Vector2(560, 590);
        Label(panel.transform, "Title", "재화 시스템 테스트", 27, new Vector2(24, -16), new Vector2(512, 48));
        Label(panel.transform, "Subtitle", "격리된 테스트 저장 · 초기 지급 없음", 16, new Vector2(24, -66), new Vector2(512, 28));
        status = Label(panel.transform, "Status", "", 20, new Vector2(24, -104), new Vector2(512, 110));
        string[] labels = { "+100 코인", "상점 열기", "개 처치 5~7", "신도 처치 3~5", "간부 처치 7~8", "보스 처치 0", "13조각 준비·2개 장착", "사망 (30% 손실)", "부활", "저장 실패 ON/OFF", "저장 다시 불러오기", "새 빈 테스트 프로필" };
        Action[] actions = {
            () => Show(Service.TryGrant(100, "test.manual")),
            () => ShopScreen.Instance.Open(shop, player.gameObject),
            () => Reward(EnemyRewardKind.Dog), () => Reward(EnemyRewardKind.Believer),
            () => Reward(EnemyRewardKind.Executive), () => Reward(EnemyRewardKind.Boss),
            PrepareEquipmentFixture, () => player.TakeDamage(100000f),
            () => player.ResetHealth(), ToggleSaveFailure, Reload, ResetFixture
        };
        for (int i = 0; i < labels.Length; i++)
        {
            var button = UiTheme.CreateImage("Action" + i, panel.transform, new Color(.21f, .31f, .43f));
            var r = button.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(24 + i % 2 * 262, -226 - i / 2 * 46); r.sizeDelta = new Vector2(250, 38);
            var image = button.GetComponent<Image>(); image.raycastTarget = true;
            Button control = button.AddComponent<Button>(); control.targetGraphic = image;
            Action action = actions[i]; control.onClick.AddListener(() => action());
            Text text = UiTheme.CreateText("Label", button.transform, labels[i], 16, Color.white); UiTheme.Stretch(text.rectTransform, 4);
        }
        message = Label(panel.transform, "Message", "처치 버튼은 동일한 보상 API를 호출합니다.", 15, new Vector2(24, -518), new Vector2(512, 58));
        message.color = new Color(.7f, .84f, .95f);
        UiEventSystemBoot.Ensure();
    }

    private static Text Label(Transform parent, string name, string text, int fontSize, Vector2 position, Vector2 size)
    {
        Text label = UiTheme.CreateText(name, parent, text, fontSize, Color.white, TextAnchor.UpperLeft);
        RectTransform rect = label.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return label;
    }

    private void Update()
    {
        if (status == null) return;
        var data = Service.Snapshot();
        status.text = $"보유 {Service.Balance:N0}  ·  비행 중 {Service.PendingBalance:N0}\n" +
            $"조각 {EconomyLedger.Total(data.pieces)}  ·  장착 {equipment.EquippedCount}\n" +
            (Service.HasPendingDeath ? "사망 처리 재시도 중" : player.IsDead ? "사망 완료 · 부활 버튼으로 새 생명" : "생존") +
            (store.FailWrites ? " · 저장 실패 주입 중" : " · 저장 정상");
    }

    public CurrencyResult Reward(EnemyRewardKind kind)
    {
        Vector3 position = player.transform.position + new Vector3(3, 1);
        var result = Service.TryRewardDefeat(kind, $"test:{sessionId}/enemy:{spawnIndex++}/defeat", position);
        Show(result); return result;
    }
    private void Show(CurrencyResult result)
    {
        if (message != null) message.text = result.Success ? $"거래 완료 · 처리량 {result.AppliedAmount}" : $"거래 실패 · {result.Error}";
    }
    public void PrepareEquipmentFixture()
    {
        if (!Service.IsReady || player.IsDead) return;
        // Reset fixture data explicitly, then prepare 7 unused + 6 referenced pieces.
        ResetFixture();
        Service.TryGrantPiece("subject.player", 2, "test.fixture");
        Service.TryGrantPiece("condition.death", 2, "test.fixture");
        Service.TryGrantPiece("result.speed", 1, "test.fixture");
        Service.TryGrantPiece("result.explode", 1, "test.fixture");
        Service.TryGrantPiece("subject.enemy", 7, "test.fixture");
        equipment.TryEquip(new Doctrine(SubjectType.Player, ConditionType.OnDeath, ResultType.IncreaseSpeed), Service);
        equipment.TryEquip(new Doctrine(SubjectType.Player, ConditionType.OnDeath, ResultType.Explode), Service);
        if (message != null) message.text = "테스트 자료: 전체 13조각(장착 사용 6 포함) · 사망 후 10조각";
    }
    private void ToggleSaveFailure()
    {
        store.FailWrites = !store.FailWrites;
        if (message != null) message.text = store.FailWrites ? "테스트 저장 실패를 주입합니다." : "저장 복구 · 대기 거래를 재시도합니다.";
    }
    public void Reload()
    {
        if (Service.HasPendingDeath) return;
        equipment.UnequipAllAndGetOwnedPieces(Service.Snapshot().pieces);
        EconomyRuntime.Instance.SetService(new EconomyService(store));
        player.ResetHealth();
    }
    public void ResetFixture()
    {
        if (Service.HasPendingDeath) return;
        equipment?.UnequipAllAndGetOwnedPieces(Service.Snapshot().pieces);
        store = new FixtureStore(new MemoryEconomyStore());
        EconomyRuntime.Instance.SetService(new EconomyService(store));
        player.ResetHealth();
        sessionId = Guid.NewGuid().ToString("N"); spawnIndex = 0;
        if (message != null) message.text = "빈 테스트 프로필 · 0코인 / 0조각 / 장착 없음 (메모리 저장)";
    }
}
