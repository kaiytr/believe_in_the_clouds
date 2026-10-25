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
    public string LatestSignal { get; private set; } = "테스트 신호 없음";
    public bool ShowDebugPanel
    {
        get => showDebugPanel;
        set { showDebugPanel = value; if (debugPanel != null) debugPanel.SetVisible(value); }
    }
    public BossCombatController Boss => boss;
    public BossDebugPanel DebugPanel => debugPanel;

    private void Start()
    {
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
        if (Input.GetKeyDown(KeyCode.J)) PublishTestAttack(false);
        if (Input.GetKeyDown(KeyCode.K)) PublishTestAttack(true);
        if (Input.GetKeyDown(KeyCode.L) && boss.AllowManualEvadeInjection && boss.ActiveBossAttackId > 0)
        {
            playerEvents.PublishAttackEvaded(boss.ActiveBossAttackId, boss.gameObject);
            LatestSignal = "수동 회피 신호 (결과에는 미반영)";
        }
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
    public void PublishTestAttack(bool hit)
    {
        if (!ready) return;
        using var attack = playerEvents.PublishAttackAttempt(boss.gameObject);
        if (hit) playerEvents.TryApplyHitAndPublish(attack, boss.gameObject, 10f);
        LatestSignal = hit ? "더미 적중 #" + attack?.Id + " (기본 피해 10)" : "더미 공격 시도 #" + attack?.Id;
    }
    public void RestartForTest()
    {
        if (!ready) return;
        boss.ResetEncounterForTest();
        LatestSignal = "전투 초기화";
    }
}
