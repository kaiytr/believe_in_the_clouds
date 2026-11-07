using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene routing consumes committed run state. Scene load is never the commit.
[DefaultExecutionOrder(-700)]
public sealed class LobbyFlowRuntime : MonoBehaviour
{
    public const string LobbyScene = "Lobby";
    public const string ShopScene = "Shop";
    public const string BossScene = "Boss";
    public static LobbyFlowRuntime Instance { get; private set; }
    public LobbyRunData State { get; private set; }
    public string Message { get; private set; } = "";
    public bool IsTransitioning { get; private set; }
    public bool HasSaveFailure { get; private set; }
    public MemoryEconomyStore FixtureStore { get; private set; }
    public LobbySceneController Scene { get; private set; }
    public EconomyService Service => Currency.Service;
    private EconomyService boundService;
    private PlayerHealth player;
    private long seenRevision = -1;
    private float nextCheckpoint, nextRetry;
    private bool healthPending, restoring;
    private bool playLoopTestMode;
    private int inputAfterFrame;
    private string developerRerollId, developerRerollRun, developerRerollBatch;
    private int developerRerollClears;

    public bool IsPlayLoopTestMode => playLoopTestMode;
    public bool HasPendingDeveloperReroll => developerRerollId != null;
    public bool CanUseDeveloperRunTools => InputReady && !PlayerDeathCoordinator.IsResultOpen;
    public bool CanRerollLobbyForDevelopment => CanUseDeveloperRunTools && Scene.IsLobby &&
        State.area == RunArea.Lobby && !State.hasStage;

    public bool InputReady => Scene != null && player != null && !player.IsDead && !IsTransitioning &&
        !healthPending && Service != null && Service.IsReady && State != null && !NeedsMaxHpSync && MatchesScene() &&
        State.area != RunArea.Complete && State.area != RunArea.Dying && Time.frameCount >= inputAfterFrame;

    private bool NeedsMaxHpSync => State != null && player != null &&
        !Mathf.Approximately(State.maxHp, player.MaxHp);

    public static LobbyFlowRuntime Ensure()
    {
        if (Instance == null) new GameObject("[Lobby Flow]").AddComponent<LobbyFlowRuntime>();
        return Instance;
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject); EconomyRuntime.Ensure();
    }

    public void Attach(LobbySceneController scene)
    {
        UnbindPlayer(); Scene = scene; player = scene.Player;
        inputAfterFrame = Time.frameCount + 2;
        BindService();
        if (Service.IsReady && State == null) Report(Service.TryInitializeRun(player.MaxHp));
        RefreshState();
        if (Service.IsReady && State != null && State.area != RunArea.Dying && NeedsMaxHpSync)
            Report(Service.TrySynchronizeRunMaxHp(player.MaxHp));
        if (State != null && !NeedsMaxHpSync && MatchesScene() && State.area != RunArea.Dying)
        {
            restoring = true;
            player.GetComponent<PlayerDeathCoordinator>().BindRunLife(State.lifeId);
            if (!player.RestoreHealth(State.hp)) Message = "저장된 체력과 플레이어 설정을 확인해 주세요.";
            if (State.hasPosition)
            {
                Vector2 position = scene.ClampPosition(new Vector2(State.playerX, State.playerY));
                player.transform.position = position;
                if (player.TryGetComponent(out Rigidbody2D body)) { body.position = position; body.linearVelocity = Vector2.zero; }
            }
            restoring = false;
        }
        player.HealthChanged += OnHealthChanged;
        nextCheckpoint = Time.unscaledTime + .75f;
        scene.SetPlayable(InputReady);
    }

    public void Detach(LobbySceneController scene)
    {
        if (Scene != scene) return;
        UnbindPlayer(); Scene = null;
    }
    private void UnbindPlayer()
    {
        if (player != null) player.HealthChanged -= OnHealthChanged;
        player = null;
    }
    private void BindService()
    {
        if (boundService == Service) return;
        boundService = Service; seenRevision = -1; healthPending = false; HasSaveFailure = false;
        RefreshState();
    }
    private void RefreshState()
    {
        if (Service == null || (seenRevision == Service.Revision && boundService == Service)) return;
        State = Service.RunSnapshot(); seenRevision = Service.Revision;
        if (HasPendingDeveloperReroll && (State == null || State.area != RunArea.Lobby || State.hasStage || State.id != developerRerollRun ||
            State.batchId != developerRerollBatch || State.combatClears != developerRerollClears))
            ClearDeveloperReroll();
    }
    private bool MatchesScene() => Scene != null && State != null &&
        (Scene.IsLobby ? State.area == RunArea.Lobby : State.area == RunArea.TemporaryStage || State.area == RunArea.Complete);

    public bool IsActiveRunStage(BranchRole role) => State != null &&
        State.hasStage && State.stage.role == role &&
        (State.area == RunArea.TemporaryStage || (role == BranchRole.Boss && State.area == RunArea.Complete));

    private string StageSceneForCurrentRun()
    {
        if (State?.stage == null) return LobbyScene;
        switch (State.stage.role)
        {
            case BranchRole.Combat:
                // Combat stages are always the designed levels. The play-loop test walks through them in order.
                int level = playLoopTestMode
                    ? State.combatClears % ProjectScenes.CombatLevelCount
                    : StableCombatSceneIndex(State.stage.id, ProjectScenes.CombatLevelCount);
                return ProjectScenes.CombatLevelName(level + 1);
            case BranchRole.Shop: return ShopScene;
            case BranchRole.Boss: return BossScene;
            default: throw new System.InvalidOperationException("지원하지 않는 스테이지 역할입니다.");
        }
    }

    private static int StableCombatSceneIndex(string stageId, int sceneCount)
    {
        byte[] stableId = new System.Guid(LobbyRules.StableId("combat-scene:" + stageId)).ToByteArray();
        return stableId[0] % sceneCount;
    }

    private void Update()
    {
        if (Scene == null) return;
        BindService(); RefreshState();
        if (Service.IsReady && State == null && Time.unscaledTime >= nextRetry)
        {
            nextRetry = Time.unscaledTime + .5f;
            Report(Service.TryInitializeRun(Scene.Player.MaxHp)); RefreshState();
        }
        if (PlayerDeathCoordinator.IsResultOpen)
        {
            Scene.SetPlayable(false);
            return; // The result buttons own routing, including after the death commits.
        }
        if (Service.HasPendingDeath)
        {
            Scene.SetPlayable(false);
            Message = "사망 결과 저장 중 · 실패하면 같은 결과로 재시도합니다.";
            return; // EconomyRuntime retries the captured death journal.
        }
        if (!Service.IsReady || State == null)
        {
            Scene.SetPlayable(false);
            if (!HasSaveFailure) Message = "저장을 불러올 수 없습니다: " + Service.LoadStatus;
            return;
        }
        if (NeedsMaxHpSync && State.area != RunArea.Dying)
        {
            Scene.SetPlayable(false);
            if (Time.unscaledTime >= nextRetry)
            {
                nextRetry = Time.unscaledTime + .5f;
                if (Report(Service.TrySynchronizeRunMaxHp(player.MaxHp)).Success)
                    Attach(Scene);
            }
            return;
        }
        // A death in the lobby also needs a fresh player instance, even though
        // the committed destination has the same scene name.
        if (!MatchesScene() || (State.area == RunArea.Lobby && player != null && player.IsDead))
        {
            if (!IsTransitioning) StartCoroutine(Route(State.area == RunArea.Lobby ? LobbyScene : StageSceneForCurrentRun()));
            return;
        }
        if (!IsTransitioning && State.area != RunArea.Complete && player != null && !player.IsDead &&
            Time.unscaledTime >= (healthPending ? nextRetry : nextCheckpoint))
        {
            nextRetry = Time.unscaledTime + .5f; nextCheckpoint = Time.unscaledTime + .75f;
            SavePlayerCheckpoint();
        }
        Scene.SetPlayable(InputReady);
    }

    public void RouteAfterDeath(string sceneName)
    {
        if (IsTransitioning) throw new System.InvalidOperationException("이미 씬을 전환하고 있습니다.");
        if (Service.HasPendingDeath) throw new System.InvalidOperationException("사망 저장을 완료하지 못했습니다.");
        StartCoroutine(Route(sceneName));
    }

    private IEnumerator Route(string sceneName)
    {
        IsTransitioning = true; Scene?.SetPlayable(false);
        yield return null; // Consume the press in the old scene only.
        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        while (load != null && !load.isDone) yield return null;
        IsTransitioning = false;
        inputAfterFrame = Time.frameCount + 2;
    }

    private void OnHealthChanged(PlayerHealth health)
    {
        if (!restoring && !IsTransitioning && health == player && !health.IsDead && MatchesScene()) SavePlayerCheckpoint();
    }
    private void SavePlayerCheckpoint()
    {
        if (IsTransitioning || !MatchesScene() || player == null || player.IsDead || State == null || NeedsMaxHpSync || State.area == RunArea.Complete) return;
        CurrencyResult result = Service.TryCheckpointRun(player.CurrentHp, player.transform.position);
        healthPending = !result.Success;
        if (!result.Success) Report(result);
        else { HasSaveFailure = false; RefreshState(); }
    }

    public CurrencyResult Reveal(string branchId)
    {
        if (!InputReady) return Report(CurrencyResult.Failed(EconomyError.Busy));
        return Report(Service.TryRevealBranch(branchId));
    }
    public CurrencyResult Depart(string branchId)
    {
        if (!InputReady) return Report(CurrencyResult.Failed(EconomyError.Busy));
        CurrencyResult result = Report(Service.TryDepartBranch(branchId, player.CurrentHp));
        if (result.Success && !MatchesScene() && !IsTransitioning) StartCoroutine(Route(StageSceneForCurrentRun()));
        return result;
    }
    public CurrencyResult CompleteStage()
    {
        if (!InputReady || !State.hasStage) return Report(CurrencyResult.Failed(EconomyError.StateConflict));
        return Report(Service.TryCompleteTemporaryStage(State.stage.id));
    }
    public CurrencyResult ReturnToLobby()
    {
        if (!InputReady || !State.hasStage) return Report(CurrencyResult.Failed(EconomyError.StateConflict));
        CurrencyResult result = Report(Service.TryReturnFromStage(State.stage.id, player.CurrentHp));
        if (result.Success && playLoopTestMode)
        {
            CurrencyResult candidates = Service.TryConfigurePlayLoopTestCandidates();
            if (candidates.Success) Report(candidates);
            else Debug.LogError("Could not prepare the next play loop test branches: " + candidates.Error, this);
        }
        if (result.Success && !MatchesScene() && !IsTransitioning) StartCoroutine(Route(LobbyScene));
        return result;
    }

    public CurrencyResult UnlockBossForDevelopment()
    {
        if (!CanUseDeveloperRunTools || HasPendingDeveloperReroll)
            return Report(CurrencyResult.Failed(EconomyError.Busy));
        return Report(Service.TryUnlockBossForDevelopment());
    }

    public CurrencyResult RerollLobbyForDevelopment()
    {
        if (!CanRerollLobbyForDevelopment) return Report(CurrencyResult.Failed(EconomyError.StateConflict));
        if (!HasPendingDeveloperReroll)
        {
            // A failed write followed by a process restart keeps the same draw for this source batch.
            developerRerollId = LobbyRules.StableId("dev:lobby-reroll-request:" + State.id + ":" + State.batchId);
            developerRerollRun = State.id;
            developerRerollBatch = State.batchId;
            developerRerollClears = State.combatClears;
        }
        CurrencyResult result = Report(Service.TryRerollLobbyForDevelopment(developerRerollId,
            developerRerollBatch, developerRerollClears, player.CurrentHp, player.transform.position));
        if (result.Success)
        {
            ClearDeveloperReroll();
            StartCoroutine(Route(LobbyScene));
        }
        else if (result.Error != EconomyError.SaveFailed && result.Error != EconomyError.Busy)
            ClearDeveloperReroll();
        return result;
    }

    private void ClearDeveloperReroll()
    {
        developerRerollId = developerRerollRun = developerRerollBatch = null;
        developerRerollClears = 0;
    }

    public void DamageForTest() { if (InputReady) player.TakeDamage(10f); }
    public void DieForTest() { if (InputReady) player.KillForTest(); }
    public void GrantForTest() { if (InputReady) Report(Currency.TryGrant(100, "lobby.test.grant")); }
    public void StartMemoryFixture()
    {
        if (IsTransitioning || Service.HasPendingDeath) return;
        FixtureStore = new MemoryEconomyStore();
        EconomyRuntime.Instance.SetService(new EconomyService(FixtureStore));
        boundService = null; BindService();
        Report(Service.TryInitializeRun(Scene.Player.MaxHp));
        if (Scene.IsLobby) Attach(Scene);
        Message = "빈 메모리 테스트 프로필 · 기존 저장 파일은 유지됩니다.";
    }

    public bool PrepareStandaloneShopTest(PlayerHealth testPlayer)
    {
        if (testPlayer == null || IsTransitioning || Service == null || !Service.IsReady) return false;

        BindService();
        RefreshState();
        if (State != null && IsActiveRunStage(BranchRole.Shop)) return true;

        // ShopTest를 에디터에서 직접 실행한 경우에만 사용할 격리된 테스트 런이다.
        // 실제 플레이 데이터와 재화 저장소는 변경하지 않는다.
        FixtureStore = new MemoryEconomyStore();
        EconomyRuntime.Instance.SetService(new EconomyService(FixtureStore));
        boundService = null;
        BindService();

        CurrencyResult result = Report(Service.TryInitializeRun(testPlayer.MaxHp));
        if (!result.Success) return false;
        result = Report(Service.TryConfigurePlayLoopTestCandidates());
        if (!result.Success) return false;
        result = Report(Service.TryGrant(100, "shop.test.standalone", "shop-test:coins:" + State.id));
        if (!result.Success) return false;

        LobbyRunData run = Service.RunSnapshot();
        BranchCandidate shop = null;
        for (int i = 0; run != null && i < run.candidates.Count; i++)
        {
            if (run.candidates[i].role == BranchRole.Shop)
            {
                shop = run.candidates[i];
                break;
            }
        }
        if (shop == null) return false;

        result = Report(Service.TryRevealBranch(shop.id));
        if (!result.Success) return false;
        result = Report(Service.TryDepartBranch(shop.id, testPlayer.CurrentHp));
        if (!result.Success) return false;

        Message = "상점 단독 테스트 · 메모리 저장소 사용 중";
        return IsActiveRunStage(BranchRole.Shop);
    }

    public void StartPlayLoopTest()
    {
        playLoopTestMode = true;
        StartMemoryFixture();
        if (Service != null && Service.IsReady)
        {
            CurrencyResult candidates = Service.TryConfigurePlayLoopTestCandidates();
            if (candidates.Success) Report(candidates);
            else Debug.LogError("Could not prepare the play loop test branches: " + candidates.Error, this);

            if (State != null)
            {
                CurrencyResult grant = Service.TryGrant(100, "playloop.test.start", "playloop:coins:" + State.id);
                if (grant.Success) Report(grant);
                else Debug.LogError("Could not seed play loop test currency: " + grant.Error, this);
            }
        }
        Message = "전체 플레이 루프 테스트 · 전투/상점과 3회 전투 후 보스 분기가 준비됩니다.";
    }
    public void ToggleSaveFailure()
    {
        if (FixtureStore == null) { Message = "먼저 ‘새 메모리 테스트’를 시작해 주세요."; return; }
        FixtureStore.FailWrites = !FixtureStore.FailWrites;
        Message = FixtureStore.FailWrites ? "테스트 저장 실패 켬" : "테스트 저장 복구 · 대기 작업 재시도";
    }
    public void ReloadForTest()
    {
        if (IsTransitioning || Service.HasPendingDeath || healthPending) return;
        EconomyRuntime.Instance.SetService(Service.Reopen());
        boundService = null; BindService(); Attach(Scene);
        Message = "저장된 런·후보·체력을 다시 불러왔습니다.";
    }

    private CurrencyResult Report(CurrencyResult result)
    {
        HasSaveFailure = result.Error == EconomyError.SaveFailed;
        if (result.Success) Message = "처리 완료";
        else if (result.Error == EconomyError.InsufficientFunds) Message = "코인이 부족합니다. 역할 공개에는 5코인이 필요합니다.";
        else if (result.Error == EconomyError.SaveFailed) Message = "저장 실패 · 상태를 유지합니다. 복구 후 다시 시도해 주세요.";
        else Message = "처리할 수 없습니다: " + result.Error;
        seenRevision = -1; RefreshState();
        return result;
    }
    private void OnApplicationPause(bool paused) { if (paused && Scene != null) SavePlayerCheckpoint(); }
    private void OnApplicationQuit() { if (Scene != null) SavePlayerCheckpoint(); }
    private void OnDestroy()
    {
        if (Instance != this) return;
        UnbindPlayer(); Instance = null;
    }
}
