using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// An additive sandbox: keep the real scene and service alive, and write only to a memory copy.
public sealed class FirstAltarStoryPreview : MonoBehaviour
{
    public static FirstAltarStoryPreview Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance.originalService != null;
    public static bool IsTransitioning => Instance != null && Instance.transitioning;
    public static string Message { get; private set; } = "";
    public static bool CanStart => !IsTransitioning && Currency.IsReady && !PlayerDeathCoordinator.IsResultOpen &&
        !(LobbyFlowRuntime.Instance?.IsTransitioning ?? false) && !(Currency.Service?.HasPendingDeath ?? true) &&
        (IsActive || Currency.PendingBalance == 0 && !(EconomyRuntime.Instance?.HasPendingDefeatRewards ?? true) &&
            !(LobbyFlowRuntime.Instance?.HasSaveFailure ?? false)) && Time.timeScale > 0;

    private EconomyService originalService;
    private LobbyFlowRuntime originalRouter;
    private Scene originalScene, previewScene;
    private float originalTimeScale;
    private bool transitioning, quitting;
    private readonly object transitionToken = new object();
    private readonly List<GameObject> activeRoots = new List<GameObject>();
    private readonly List<BodyState> bodies = new List<BodyState>();
    private readonly List<AudioState> audioStates = new List<AudioState>();

    private struct BodyState
    {
        public Rigidbody2D Body;
        public Vector2 Velocity;
        public float AngularVelocity;
    }
    private struct AudioState
    {
        public AudioSource Source;
        public int Samples;
    }

    public static bool TryStart()
    {
        if (!CanStart) { Message = "전환·사망·저장·보상 처리를 마친 뒤 다시 시도해 주세요."; return false; }
        PlayerHealth player = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        if (player != null && (player.IsDead || !IsActive && player.IsInvulnerable))
        { Message = "플레이어가 안정된 상태에서 다시 시도해 주세요."; return false; }
        if (Instance == null) new GameObject("[First altar preview]").AddComponent<FirstAltarStoryPreview>();
        Instance.transitioning = true;
        UiModal.Push(Instance.transitionToken);
        Instance.StartCoroutine(Instance.StartPreview(player != null ? player.MaxHp : 150f));
        return true;
    }

    public static bool TryReturn()
    {
        if (!IsActive || IsTransitioning) return false;
        Instance.transitioning = true;
        UiModal.Push(Instance.transitionToken);
        Instance.StartCoroutine(Instance.ReturnToOriginal());
        return true;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
    }

    public static EconomyService CreateMemoryFixture(EconomyProfileData source, float maxHp)
    {
        EconomyProfileData copy = source.Copy();
        copy.profileId = Guid.NewGuid().ToString("N"); copy.revision = 0;
        copy.balance += EconomyLedger.Pending(copy); copy.pendingGrants.Clear();
        copy.processedTransactions.Clear(); copy.hasDeath = false; copy.death = null;
        copy.narrative = new NarrativeProgressData { tutorial = TutorialProgress.Completed };
        copy.hasRun = true; copy.run = LobbyRules.NewRun(Guid.NewGuid().ToString("N"), maxHp, 1711);
        var store = new MemoryEconomyStore();
        if (!store.TrySave(copy)) return null;
        var service = new EconomyService(store);
        if (!service.Initialize()) return null;
        BranchCandidate combat = service.RunSnapshot().candidates.Find(candidate => candidate.role == BranchRole.Combat);
        if (!service.TryDepartBranch(combat.id, maxHp).Success) return null;
        string stage = service.RunSnapshot().stage.id;
        if (!service.TryCompleteTemporaryStage(stage).Success || !service.TryExitCompletedStage(stage, maxHp).Success) return null;
        return service;
    }

    private IEnumerator StartPreview(float maxHp)
    {
        EconomyService source = originalService ?? Currency.Service;
        EconomyService fixture = CreateMemoryFixture(source.Snapshot(), maxHp);
        if (fixture == null)
        {
            Message = "테스트 진행을 준비하지 못했습니다. 기존 진행을 유지합니다.";
            EndTransition(); yield break;
        }
        if (originalService == null)
        {
            originalService = source; originalScene = SceneManager.GetActiveScene(); originalTimeScale = Time.timeScale;
            originalRouter = LobbyFlowRuntime.Instance;
            originalRouter?.SuspendForDeveloperPreview();
            foreach (GameObject root in originalScene.GetRootGameObjects())
            {
                if (!root.activeSelf) continue;
                activeRoots.Add(root);
                foreach (Rigidbody2D body in root.GetComponentsInChildren<Rigidbody2D>())
                    bodies.Add(new BodyState { Body = body, Velocity = body.linearVelocity, AngularVelocity = body.angularVelocity });
                foreach (AudioSource playing in root.GetComponentsInChildren<AudioSource>())
                    if (playing.isPlaying) audioStates.Add(new AudioState { Source = playing, Samples = playing.timeSamples });
                root.SetActive(false);
            }
        }
        else yield return UnloadPreview();

        EconomyRuntime.Instance.SetService(fixture);
        Time.timeScale = 1f;
        AsyncOperation load = null;
        var oldHandles = new HashSet<int>();
        for (int i = 0; i < SceneManager.sceneCount; i++) oldHandles.Add(SceneManager.GetSceneAt(i).handle);
        try { load = SceneManager.LoadSceneAsync(LobbyFlowRuntime.FirstAltarScene, LoadSceneMode.Additive); }
        catch (Exception ex) { Debug.LogWarning("재단 테스트 씬 전환 실패: " + ex.Message); }
        if (load != null) while (!load.isDone) yield return null;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!oldHandles.Contains(scene.handle) && scene.name == LobbyFlowRuntime.FirstAltarScene) previewScene = scene;
        }
        if (!previewScene.IsValid() || !previewScene.isLoaded)
        {
            Message = "테스트 씬을 불러오지 못해 기존 진행으로 복귀했습니다.";
            yield return UnloadPreview(); RestoreOriginal(); EndTransition(); yield break;
        }
        SceneManager.SetActiveScene(previewScene);
        Message = "첫 재단 테스트 중 · 기존 진행 보관. F2에서 처음부터 또는 테스트 종료를 선택할 수 있습니다.";
        yield return ReleasePress(); EndTransition();
    }

    private IEnumerator UnloadPreview()
    {
        LobbyFlowRuntime router = LobbyFlowRuntime.Instance;
        if (router != null && router != originalRouter) router.DiscardDeveloperPreviewRouter();
        if (previewScene.IsValid() && previewScene.isLoaded)
        {
            foreach (GameObject root in previewScene.GetRootGameObjects()) root.SetActive(false);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(previewScene);
            if (unload != null) while (!unload.isDone) yield return null;
        }
        previewScene = default;
    }

    private IEnumerator ReturnToOriginal()
    {
        yield return UnloadPreview();
        RestoreOriginal();
        Message = "재단 테스트를 종료하고 보관한 기존 진행으로 돌아왔습니다.";
        yield return ReleasePress(); EndTransition();
    }

    private void RestoreOriginal()
    {
        if (originalService == null) return;
        EconomyRuntime.Instance.SetService(originalService);
        Time.timeScale = originalTimeScale;
        if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
        originalRouter?.ResumeFromDeveloperPreview();
        foreach (GameObject root in activeRoots) if (root != null) root.SetActive(true);
        foreach (BodyState body in bodies)
            if (body.Body != null) { body.Body.linearVelocity = body.Velocity; body.Body.angularVelocity = body.AngularVelocity; }
        foreach (AudioState playing in audioStates)
        {
            if (playing.Source == null || playing.Source.clip == null || !playing.Source.isActiveAndEnabled) continue;
            playing.Source.Play();
            playing.Source.timeSamples = Mathf.Clamp(playing.Samples, 0, playing.Source.clip.samples - 1);
        }
        originalService = null; originalRouter = null;
        activeRoots.Clear(); bodies.Clear(); audioStates.Clear();
    }

    private static IEnumerator ReleasePress()
    {
        yield return null;
        while (Input.GetMouseButton(0) || Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.F2)) yield return null;
        yield return null;
    }
    private void EndTransition() { transitioning = false; UiModal.Pop(transitionToken); }
    private void OnApplicationQuit() { quitting = true; }
    private void OnDestroy()
    {
        if (Instance != this) return;
        if (!quitting && originalService != null && EconomyRuntime.Instance != null)
        {
            LobbyFlowRuntime router = LobbyFlowRuntime.Instance;
            if (router != null && router != originalRouter) router.DiscardDeveloperPreviewRouter();
            RestoreOriginal();
        }
        UiModal.Pop(transitionToken); Instance = null;
    }
}
