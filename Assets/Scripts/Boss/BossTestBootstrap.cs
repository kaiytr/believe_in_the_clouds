using UnityEngine;

/// <summary>Development inputs; the separate Canvas panel owns debug layout.</summary>
public sealed class BossTestBootstrap : MonoBehaviour
{
    [SerializeField] private BossCombatController boss;
    [SerializeField] private bool startAutomatically = true;
    [SerializeField] private bool showDebugPanel;
    private PlayerCombatEvents playerEvents;
    private BossDebugPanel debugPanel;
    private bool ready;
    private bool activeRunStage;
    public string LatestSignal { get; private set; } = "테스트 신호 없음";
    public bool ShowDebugPanel
    {
        get => showDebugPanel;
        set { showDebugPanel = value; if (debugPanel != null) debugPanel.SetVisible(value); }
    }
    public BossCombatController Boss => boss;
    public BossDebugPanel DebugPanel => debugPanel;

    private void Awake()
    {
        BossEndingSequence ending = GetComponent<BossEndingSequence>();
        if (ending == null) ending = gameObject.AddComponent<BossEndingSequence>();
        ending.Initialize(boss);
    }

    private void Start()
    {
        LobbyFlowRuntime flow = LobbyFlowRuntime.Instance;
        activeRunStage = flow != null && flow.IsActiveRunStage(BranchRole.Boss);
        if (activeRunStage) showDebugPanel = false;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerEvents = player != null ? player.GetComponent<PlayerCombatEvents>() : null;
        if (boss == null || playerEvents == null)
        {
            Debug.LogError("BossTest requires a serialized Boss prefab and a player with PlayerHealth/PlayerCombatEvents.", this);
            enabled = false;
            return;
        }
        if (startAutomatically) boss.BeginEncounter();
        ready = true;
        debugPanel = gameObject.AddComponent<BossDebugPanel>();
        debugPanel.Initialize(this, boss);
        debugPanel.SetVisible(showDebugPanel);
    }

    private void Update()
    {
        if (!ready) return;
        if (Input.GetKeyDown(KeyCode.F1)) ShowDebugPanel = !ShowDebugPanel;
        // In a real run the shortcut keys only work while the test panel is open, so normal play never triggers them.
        if (activeRunStage && !ShowDebugPanel) return;
        if (boss.WaitingForArenaEntry) return;
        if (Input.GetKeyDown(KeyCode.B)) boss.ReduceBeliefForTest(20f);
        if (Input.GetKeyDown(KeyCode.P)) boss.ForcePhaseTwoForTest();
        if (Input.GetKeyDown(KeyCode.R)) RestartForTest();
        if (Input.GetKeyDown(KeyCode.T)) ToggleAutomatic();
        for (int i = 0; i < 9; i++)
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) boss.ForcePatternForTest((BossPatternKind)i);
    }

    public void ToggleAutomatic()
    {
        if (boss.AutomaticPatterns) boss.PauseAndCancelForTest();
        else boss.AutomaticPatterns = true;
    }
    public void RestartForTest()
    {
        if (!ready || boss.WaitingForArenaEntry) return;
        boss.ResetEncounterForTest();
        LatestSignal = "전투 초기화";
    }
}
