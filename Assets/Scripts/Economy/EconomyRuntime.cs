using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class EconomyRuntime : MonoBehaviour
{
    public const string TutorialPreviewKey = "believe_in_the_clouds.tutorial_preview_once";
    public static EconomyRuntime Instance { get; private set; }
    public EconomySettings Settings { get; private set; }
    private CoinFlightView flightView;
    private float retryAt;
    private bool flushRequested;
    private IEconomyStore persistentStore;
    private sealed class DefeatRetry
    {
        public EnemyRewardKind Kind;
        public string Id;
        public Vector3 Position;
        public float PresentationDelay;
        public CoinDropOrigin DropOrigin;
    }
    private readonly System.Collections.Generic.List<DefeatRetry> defeatRetries = new System.Collections.Generic.List<DefeatRetry>();

    public void RewardDefeat(EnemyRewardKind kind, string id, Vector3 position, float presentationDelay = 0f,
        CoinDropOrigin dropOrigin = null)
    {
        CurrencyResult result = Currency.Service?.TryRewardDefeat(kind, id, position, presentationDelay: presentationDelay, dropOrigin: dropOrigin);
        if (result != null && (result.Error == EconomyError.SaveFailed || result.Error == EconomyError.Busy) &&
            !defeatRetries.Exists(item => item.Id == id))
            defeatRetries.Add(new DefeatRetry { Kind = kind, Id = id, Position = position, PresentationDelay = presentationDelay, DropOrigin = dropOrigin });
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
        bool tutorialPreview = !testRun && Application.isEditor && PlayerPrefs.GetInt(TutorialPreviewKey, 0) != 0;
        if (tutorialPreview) { PlayerPrefs.DeleteKey(TutorialPreviewKey); PlayerPrefs.Save(); }

        // Each Editor Play starts clean for scene iteration. Pass
        // -usePersistentEditorProfile only when deliberately testing save/resume in the Editor.
        // Released builds continue using persistentDataPath.
        if (testRun || tutorialPreview || (Application.isEditor && !usePersistentEditorProfile))
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

    /// <summary>
    /// Developer reset: wipes the saved profile (file and backups in a build, the in-memory profile in the Editor) and
    /// starts a brand-new one. The next "start" behaves like the first launch.
    /// </summary>
    public bool ResetSavedProfile()
    {
        IEconomyStore fresh;
        if (persistentStore is JsonEconomyStore json)
        {
            if (!json.DeleteSaveFiles()) return false;
            fresh = new JsonEconomyStore(json.FilePath);
        }
        else fresh = new MemoryEconomyStore();
        persistentStore = fresh;
        SetService(new EconomyService(fresh));
        return true;
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

    private void HandleSceneChanged(Scene previous, Scene next)
    {
        defeatRetries.Clear();
        flightView?.CancelFlights();
        // A successful flush completes this scene transition's obligation.
        // Keeping this flag set could flush a NEW flight on the next retry tick.
        CurrencyResult flush = Currency.Service?.SettleAllPending();
        flushRequested = flush != null && !flush.Success;

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
            CurrencyResult result = service.TryRewardDefeat(retry.Kind, retry.Id, retry.Position, presentationDelay: retry.PresentationDelay, dropOrigin: retry.DropOrigin);
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
        CurrencyResult flush = Currency.Service?.SettleAllPending();
        flushRequested = flush != null && !flush.Success;
    }

    private void OnApplicationQuit() => Currency.Service?.SettleAllPending();
    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.activeSceneChanged -= HandleSceneChanged;
        Instance = null;
    }
}
