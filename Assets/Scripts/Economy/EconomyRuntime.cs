using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class EconomyRuntime : MonoBehaviour
{
    public static EconomyRuntime Instance { get; private set; }
    public EconomySettings Settings { get; private set; }
    private CoinFlightView flightView;
    private float retryAt;
    private bool flushRequested;
    private const string FoundationTestSceneName = "FoundationTest";
    private IEconomyStore persistentStore;
    private bool foundationTestProfileActive;
    private sealed class DefeatRetry
    {
        public EnemyRewardKind Kind;
        public string Id;
        public Vector3 Position;
    }
    private readonly System.Collections.Generic.List<DefeatRetry> defeatRetries = new System.Collections.Generic.List<DefeatRetry>();

    public void RewardDefeat(EnemyRewardKind kind, string id, Vector3 position)
    {
        CurrencyResult result = Currency.Service?.TryRewardDefeat(kind, id, position);
        if (result != null && (result.Error == EconomyError.SaveFailed || result.Error == EconomyError.Busy) &&
            !defeatRetries.Exists(item => item.Id == id))
            defeatRetries.Add(new DefeatRetry { Kind = kind, Id = id, Position = position });
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Ensure()
    {
        if (Instance == null) new GameObject("[Economy]").AddComponent<EconomyRuntime>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Settings = Resources.Load<EconomySettings>("EconomySettings");
        if (Settings == null) Settings = ScriptableObject.CreateInstance<EconomySettings>();
        IEconomyStore store;
        string[] arguments = Environment.GetCommandLineArgs();
        bool testRun = Array.Exists(arguments, arg => arg.Equals("-runTests", StringComparison.OrdinalIgnoreCase));
        bool usePersistentEditorProfile = Array.Exists(arguments,
            arg => arg.Equals("-usePersistentEditorProfile", StringComparison.OrdinalIgnoreCase));

        // Each Editor Play starts clean for scene iteration. Pass
        // -usePersistentEditorProfile only when deliberately testing save/resume in the Editor.
        // Released builds continue using persistentDataPath.
        if (testRun || (Application.isEditor && !usePersistentEditorProfile))
            store = new MemoryEconomyStore();
        else
        {
            string directory = Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Economy/Editor"))
                : Application.persistentDataPath;
            store = new JsonEconomyStore(Path.Combine(directory, "economy-save-v1.json"));
        }
        persistentStore = store;
        SetService(new EconomyService(store));
        SceneManager.activeSceneChanged += HandleSceneChanged;
    }

    public void SetService(EconomyService service)
    {
        if (flightView != null)
        {
            flightView.Shutdown();
            Destroy(flightView.gameObject);
        }
        Currency.Bind(service);
        defeatRetries.Clear();
        service.Initialize();
        var viewObject = new GameObject("CoinFlights", typeof(RectTransform));
        viewObject.transform.SetParent(transform, false);
        flightView = viewObject.AddComponent<CoinFlightView>();
        flightView.Initialize(service, Settings);
        flushRequested = false;
    }

    public void UseFoundationTestProfile()
    {
        if (foundationTestProfileActive || persistentStore == null)
        {
            return;
        }

        foundationTestProfileActive = true;
        SetService(new EconomyService(new MemoryEconomyStore()));
        Debug.Log("[Economy] FoundationTest에서 격리된 메모리 프로필을 사용합니다.");
    }

    private void RestorePersistentProfile()
    {
        if (!foundationTestProfileActive || persistentStore == null)
        {
            return;
        }

        foundationTestProfileActive = false;
        SetService(new EconomyService(persistentStore));
        Debug.Log("[Economy] 저장된 재화 프로필을 다시 불러왔습니다.");
    }

    private void HandleSceneChanged(Scene previous, Scene next)
    {
        defeatRetries.Clear();
        flightView?.CancelFlights();
        flushRequested = true;
        Currency.Service?.SettleAllPending();

        if (next.name == FoundationTestSceneName)
        {
            UseFoundationTestProfile();
        }
        else
        {
            RestorePersistentProfile();
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < retryAt) return;
        retryAt = Time.unscaledTime + 0.5f;
        EconomyService service = Currency.Service;
        if (service == null) return;
        for (int i = defeatRetries.Count - 1; i >= 0; i--)
        {
            DefeatRetry retry = defeatRetries[i];
            if (!service.AcceptsDefeatRewards) { defeatRetries.RemoveAt(i); continue; }
            CurrencyResult result = service.TryRewardDefeat(retry.Kind, retry.Id, retry.Position);
            if (result.Success || (result.Error != EconomyError.SaveFailed && result.Error != EconomyError.Busy)) defeatRetries.RemoveAt(i);
        }
        if (service.HasPendingDeath) service.RetryDeath();
        else if (flushRequested || flightView == null || !flightView.HasFlights)
            flushRequested = !service.SettleAllPending().Success;
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused) return;
        flightView?.CancelFlights();
        flushRequested = true;
        Currency.Service?.SettleAllPending();
    }

    private void OnApplicationQuit() => Currency.Service?.SettleAllPending();
    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.activeSceneChanged -= HandleSceneChanged;
        Instance = null;
    }
}
