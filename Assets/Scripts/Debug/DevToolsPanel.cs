using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Developer tool panel, available in every scene. Hidden by default; F2 shows or hides it.
/// The layout is the prefab Resources/UI/DevTools, so it can be edited in the editor.
/// Preserves the current run while exposing health, boss-unlock and lobby-reroll actions.
/// </summary>
public sealed class DevToolsPanel : MonoBehaviour
{
    private const KeyCode ToggleKey = KeyCode.F2;
    private const string PrefabPath = "UI/DevTools";

    private static DevToolsPanel instance;
    private GameObject panel;
    private DevToolsRefs refs;
    private bool openedOverModal;
    private readonly object modalToken = new object();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBoot()
    {
        if (instance != null) return;
        var go = new GameObject("[DevTools]");
        DontDestroyOnLoad(go);
        go.AddComponent<DevToolsPanel>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("DevTools prefab is missing: Resources/" + PrefabPath, this); enabled = false; return; }
        panel = Instantiate(prefab, transform);
        refs = panel.GetComponent<DevToolsRefs>();
        if (refs == null || !refs.IsComplete) { Debug.LogError("DevTools prefab needs a complete DevToolsRefs.", this); enabled = false; return; }

        foreach (UnityEngine.UI.Text text in panel.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            text.font = UiTheme.GetFont();
        refs.GrantCoins.onClick.AddListener(GrantCoins);
        refs.Damage.onClick.AddListener(Damage);
        refs.Death.onClick.AddListener(Die);
        refs.ResetAndGoToTitle.onClick.AddListener(ResetAndGoToTitle);
        refs.Heal.onClick.AddListener(Heal);
        refs.UnlockBoss.onClick.AddListener(UnlockBoss);
        refs.RerollBranches.onClick.AddListener(RerollBranches);
        refs.StoryPreview.onClick.AddListener(StartStoryPreview);
        refs.EndStoryPreview.onClick.AddListener(EndStoryPreview);
        ShowFeedback("첫 재단 테스트는 기존 진행을 보관합니다. 분기 리롤은 로비에서만 가능합니다.");
        panel.SetActive(false);
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private void Update()
    {
        if (panel == null) return;
        if (Input.GetKeyDown(ToggleKey)) SetOpen(!panel.activeSelf);
        if (!panel.activeSelf) return;

        PlayerHealth player = FindPlayer();
        LobbyFlowRuntime flow = LobbyFlowRuntime.Instance;
        bool canModify = CanModifyPlayer(player);
        refs.Damage.interactable = canModify;
        refs.Death.interactable = canModify;
        refs.Heal.interactable = canModify && player.CurrentHp < player.MaxHp;
        bool safe = !PlayerDeathCoordinator.IsResultOpen && !(Currency.Service?.HasPendingDeath ?? false) &&
            !(flow?.IsTransitioning ?? false) && !StoryRoomFlow.BlocksDeveloperActions && !FirstAltarStoryPreview.IsActive &&
            !FirstAltarStoryPreview.IsTransitioning;
        refs.GrantCoins.interactable = Currency.IsReady && safe;
        refs.ResetAndGoToTitle.interactable = safe;
        refs.UnlockBoss.interactable = safe && flow != null && flow.CanUseDeveloperRunTools && !flow.HasPendingDeveloperReroll;
        refs.RerollBranches.interactable = safe && !openedOverModal && flow != null && flow.CanRerollLobbyForDevelopment;
        refs.StoryPreview.interactable = FirstAltarStoryPreview.CanStart &&
            (!openedOverModal || StoryRoomFlow.Instance != null || FirstAltarStoryPreview.IsActive);
        refs.EndStoryPreview.interactable = FirstAltarStoryPreview.IsActive && !FirstAltarStoryPreview.IsTransitioning;
        refs.StoryPreview.GetComponentInChildren<UnityEngine.UI.Text>().text = FirstAltarStoryPreview.IsActive
            ? "첫 재단 스토리 · 처음부터" : "첫 재단 스토리 테스트";

        LobbyRunData run = flow != null ? flow.State : null;
        string hp = player != null ? $"체력 {player.CurrentHp:0.#} / {player.MaxHp:0.#}" : "플레이어 없음";
        string progress = run != null
            ? $"전투 클리어 {run.combatClears} · 보스 {(run.combatClears >= LobbyRules.BossUnlockClears ? "해금" : "잠김")}"
            : "진행 중인 런 없음";
        refs.Status.text = FirstAltarStoryPreview.IsActive ? $"첫 재단 테스트 중 · 기존 진행 보관\n{hp}" :
            $"{hp} · {SceneManager.GetActiveScene().name}\n{progress}";
        TextRerollLabel(flow != null && flow.HasPendingDeveloperReroll);
    }

    // While open, game input is blocked so clicking a button does not also attack.
    private void SetOpen(bool open)
    {
        if (open)
        {
            openedOverModal = UiModal.IsOpen;
            panel.GetComponent<Canvas>().sortingOrder = StoryRoomFlow.Instance != null || FirstAltarStoryPreview.IsActive ? 32766 : 500;
            if (FirstAltarStoryPreview.IsActive) ShowFeedback("임시 테스트입니다. 처음부터 재생하거나 기존 진행으로 복귀할 수 있습니다.");
            else if (openedOverModal) ShowFeedback("다른 창을 닫은 뒤 F2를 다시 열어 주세요. 스토리 테스트는 컷씬 중에도 가능합니다.");
        }
        panel.SetActive(open);
        if (open) { UiEventSystemBoot.Ensure(); UiModal.Push(modalToken); }
        else UiModal.Pop(modalToken);
    }

    private void OnDisable() { if (panel != null) panel.SetActive(false); UiModal.Pop(modalToken); }

    private static PlayerHealth FindPlayer() => FindFirstObjectByType<PlayerHealth>();

    private static bool CanModifyPlayer(PlayerHealth player) => player != null && !player.IsDead && Currency.IsReady &&
        !PlayerDeathCoordinator.IsResultOpen && !(Currency.Service?.HasPendingDeath ?? false) &&
        !(LobbyFlowRuntime.Instance?.IsTransitioning ?? false) && !StoryRoomFlow.BlocksDeveloperActions &&
        !FirstAltarStoryPreview.IsActive && !FirstAltarStoryPreview.IsTransitioning;

    private void GrantCoins() => ShowResult(Currency.TryGrant(100, "dev.tools.grant"), "100코인을 지급했습니다.");

    private void Heal()
    {
        PlayerHealth player = FindPlayer();
        if (!CanModifyPlayer(player)) return;
        player.Heal(player.MaxHp);
        bool pending = LobbyFlowRuntime.Instance?.HasSaveFailure ?? false;
        ShowFeedback(pending ? "체력 회복 · 체크포인트 저장은 재시도 중입니다." : "체력을 최대치로 회복했습니다.", pending);
    }

    private void UnlockBoss()
    {
        LobbyFlowRuntime flow = LobbyFlowRuntime.Instance;
        if (flow == null) return;
        ShowResult(flow.UnlockBossForDevelopment(), "보스 해금 조건 달성 · 분기 리롤로 후보를 갱신할 수 있습니다.");
    }

    private void RerollBranches()
    {
        if (openedOverModal) return;
        LobbyFlowRuntime flow = LobbyFlowRuntime.Instance;
        if (flow == null) return;
        CurrencyResult result = flow.RerollLobbyForDevelopment();
        ShowResult(result, "새 분기를 저장했습니다. 로비를 다시 불러옵니다.");
        if (result.Success) SetOpen(false);
    }

    private void StartStoryPreview()
    {
        if (openedOverModal && StoryRoomFlow.Instance == null && !FirstAltarStoryPreview.IsActive) return;
        if (FirstAltarStoryPreview.TryStart()) SetOpen(false);
        else ShowFeedback(FirstAltarStoryPreview.Message, true);
    }

    private void EndStoryPreview()
    {
        if (FirstAltarStoryPreview.TryReturn()) SetOpen(false);
    }

    private void ShowResult(CurrencyResult result, string success)
    {
        if (result != null && result.Success) { ShowFeedback(success); return; }
        string message = result?.Error == EconomyError.SaveFailed
            ? "저장 실패 · 같은 버튼으로 재시도하세요."
            : result?.Error == EconomyError.Busy ? "다른 처리가 진행 중입니다. 잠시 후 다시 시도하세요."
            : "지금은 사용할 수 없습니다. 현재 지역과 진행 상태를 확인하세요.";
        ShowFeedback(message, true);
    }

    private void ShowFeedback(string message, bool error = false)
    {
        refs.Feedback.text = message;
        refs.Feedback.color = error ? new Color(1f, .72f, .55f) : Color.white;
    }

    private void TextRerollLabel(bool pending)
    {
        UnityEngine.UI.Text label = refs.RerollBranches.GetComponentInChildren<UnityEngine.UI.Text>();
        if (label != null) label.text = pending ? "분기 리롤 저장 재시도" : "분기 리롤 · 로비에서만";
    }

    private static void Damage()
    {
        PlayerHealth player = FindPlayer();
        if (CanModifyPlayer(player)) player.TakeDamage(10f);
    }

    private static void Die()
    {
        PlayerHealth player = FindPlayer();
        if (CanModifyPlayer(player)) player.KillForTest();
    }

    // Full reset, then back to the title: the tutorial "first launch" flag (as the editor menu
    // "Tools/테스트/튜토리얼 최초 실행 초기화"), the saved profile (coins, pieces, run) and the in-memory leftovers.
    // Afterwards "시작하기" behaves like a brand-new game.
    private void ResetAndGoToTitle()
    {
        if (StoryRoomFlow.BlocksDeveloperActions || FirstAltarStoryPreview.IsActive || FirstAltarStoryPreview.IsTransitioning) return;
        if (EconomyRuntime.Instance != null && !EconomyRuntime.Instance.ResetSavedProfile())
        {
            ShowFeedback("저장 파일을 지우지 못했습니다. 파일이 열려 있지 않은지 확인하세요.", true);
            return;
        }

        PlayerPrefs.DeleteKey(TitleScreen.TutorialStartedKey);
        PlayerPrefs.Save();
        DoctrineSlotStore.Instance?.ResetToDefault();
        DoctrineManager.Instance?.Clear();
        // The lobby router keeps the old run in memory; it is rebuilt from the fresh profile when needed.
        if (LobbyFlowRuntime.Instance != null) Destroy(LobbyFlowRuntime.Instance.gameObject);
        Time.timeScale = 1f;
        SetOpen(false);
        SceneManager.LoadScene(TitleScreen.SceneName);
    }
}
