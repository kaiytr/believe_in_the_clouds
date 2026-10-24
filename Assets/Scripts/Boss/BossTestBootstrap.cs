using UnityEngine;

/// <summary>
/// Sets up player test hooks and controls for the boss placed in BossTest.unity.
/// The scene itself is cloned from PlayerTEST.unity so the PlayerMove serialized
/// values stay in sync.
/// </summary>
public sealed class BossTestBootstrap : MonoBehaviour
{
    private BossCombatController boss;
    private PlayerHealth playerHealth;
    private BossPlayerCombatEvents playerCombatEvents;
    private int pendingAttackId;
    private bool setupComplete;
    private string latestSignal = "No test signal sent";

    private void Awake()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("BossTest requires a GameObject tagged Player.", this);
            enabled = false;
            return;
        }

        playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = player.AddComponent<PlayerHealth>();

        playerCombatEvents = player.GetComponent<BossPlayerCombatEvents>();
        if (playerCombatEvents == null)
            playerCombatEvents = player.AddComponent<BossPlayerCombatEvents>();

        boss = FindFirstObjectByType<BossCombatController>();
        if (boss == null)
        {
            Debug.LogError("BossTest requires a BossCombatController placed in the scene.", this);
            enabled = false;
            return;
        }

        boss.BeginEncounter();
        setupComplete = true;
    }

    private void Update()
    {
        if (!setupComplete || boss == null || playerCombatEvents == null)
            return;

        if (Input.GetKeyDown(KeyCode.J))
        {
            pendingAttackId = playerCombatEvents.PublishAttackAttempt(boss.gameObject);
            latestSignal = $"Attack attempt #{pendingAttackId}";
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            pendingAttackId = playerCombatEvents.PublishAttackAttempt(boss.gameObject);
            playerCombatEvents.PublishHitConfirmed(pendingAttackId, boss.gameObject, 10f);
            latestSignal = $"Attack hit #{pendingAttackId} (10 damage)";
        }

        if (Input.GetKeyDown(KeyCode.L) && boss.ActiveBossAttackId > 0)
        {
            playerCombatEvents.PublishBossAttackEvaded(boss.ActiveBossAttackId, boss.gameObject);
            latestSignal = $"Evaded boss attack #{boss.ActiveBossAttackId}";
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            boss.ReduceBeliefForTest(20f);
            latestSignal = "Reduced shared belief by 20";
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            boss.ForcePhaseTwoForTest();
            latestSignal = "Forced phase 2";
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            boss.ResetEncounterForTest();
            playerHealth.ResetHealth();
            latestSignal = "Encounter reset";
        }

        HandlePatternKey(KeyCode.Alpha1, BossPatternKind.MeleeCombo);
        HandlePatternKey(KeyCode.Alpha2, BossPatternKind.Dash);
        HandlePatternKey(KeyCode.Alpha3, BossPatternKind.Projectile);
        HandlePatternKey(KeyCode.Alpha4, BossPatternKind.AttackIntentBelief);
        HandlePatternKey(KeyCode.Alpha5, BossPatternKind.CloudBelief);
        HandlePatternKey(KeyCode.Alpha6, BossPatternKind.BurnBelief);
        HandlePatternKey(KeyCode.Alpha7, BossPatternKind.AttackAndDodgeBelief);
        HandlePatternKey(KeyCode.Alpha8, BossPatternKind.NoAttackBelief);
        HandlePatternKey(KeyCode.Alpha9, BossPatternKind.ShockwaveBelief);
    }

    private void HandlePatternKey(KeyCode key, BossPatternKind pattern)
    {
        if (!Input.GetKeyDown(key))
            return;

        boss.ForcePatternForTest(pattern);
        latestSignal = $"Forced pattern: {pattern}";
    }

    private void OnGUI()
    {
        if (!setupComplete || boss == null)
            return;

        GUILayout.BeginArea(new Rect(12f, 12f, 420f, 280f), GUI.skin.box);
        GUILayout.Label("BOSS TEST  |  Placeholder visuals");
        GUILayout.Label($"Phase: {boss.Phase}    HP: {boss.CurrentHp:0}/{boss.MaxHp:0}");
        GUILayout.Label($"Belief: {boss.CurrentBelief:0}/{boss.MaxBelief:0}    State: {(boss.IsVulnerable ? "Vulnerable" : "Maintained")}");
        GUILayout.HorizontalSlider(boss.NormalizedBelief, 0f, 1f);
        GUILayout.Label($"Belief: {boss.ActiveBeliefPhrase}");
        GUILayout.Label($"Pattern: {boss.ActivePatternName}    {boss.StatusMessage}");
        GUILayout.Space(6f);
        GUILayout.Label("Move: A/D or arrows   Jump: Space");
        GUILayout.Label("J: attack attempt   K: attack + 10 damage   L: evade current boss attack");
        GUILayout.Label("B: reduce belief   P: phase 2   R: reset");
        GUILayout.Label("1-9: force melee / dash / projectile / P1 belief patterns / P2 belief patterns");
        GUILayout.Label($"Last signal: {latestSignal}");
        GUILayout.EndArea();
    }
}
