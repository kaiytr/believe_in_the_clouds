using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class CutsceneBossEncounterCoordinator : MonoBehaviour
{
    [Header("Arena entry")]
    [SerializeField] private BossCombatController boss;
    [SerializeField] private BoxCollider2D entry;
    [SerializeField] private Collider2D[] gates = Array.Empty<Collider2D>();
    [SerializeField] private BossArenaCamera arenaCamera;

    [Header("Introduction")]
    [SerializeField] private CutsceneRunner introRunner;
    [SerializeField] private CutsceneDefinition introDefinition;
    [SerializeField] private UnityEngine.Events.UnityEvent onCloudPreparationRequested =
        new UnityEngine.Events.UnityEvent();

    private bool hasEntered;
    private bool beginEncounterAttempted;
    private bool gatesClosed;

    public bool HasEntered => hasEntered;
    public bool BeginEncounterAttempted => beginEncounterAttempted;
    public BossCombatController EncounterBoss => boss;
    public BoxCollider2D EntryCollider => entry;
    public Collider2D[] Gates => gates;
    public BossArenaCamera ArenaCamera => arenaCamera;
    public event Action<CutsceneBossEncounterCoordinator> CloudPreparationRequested;

    private void Awake()
    {
        if (entry == null)
            entry = GetComponent<BoxCollider2D>();
        if (entry != null)
            entry.isTrigger = true;
        if (boss == null)
            boss = GetComponentInChildren<BossCombatController>(true);
        if (arenaCamera == null)
            arenaCamera = GetComponent<BossArenaCamera>();
        if (introRunner == null)
            introRunner = GetComponentInChildren<CutsceneRunner>(true);
        SetGates(false);
    }

    private void OnEnable()
    {
        SubscribeRunner();
    }

    public void Configure(BossCombatController encounterBoss, BoxCollider2D entryCollider,
        Collider2D[] arenaGates, BossArenaCamera cameraRig,
        CutsceneRunner sequenceRunner, CutsceneDefinition sequence)
    {
        UnsubscribeRunner();
        boss = encounterBoss;
        entry = entryCollider;
        gates = arenaGates ?? Array.Empty<Collider2D>();
        arenaCamera = cameraRig;
        introRunner = sequenceRunner;
        introDefinition = sequence;
        SubscribeRunner();
    }

    private void SubscribeRunner()
    {
        if (!isActiveAndEnabled || introRunner == null)
            return;
        introRunner.Completed -= HandleIntroCompleted;
        introRunner.Cancelled -= HandleIntroCancelled;
        introRunner.Completed += HandleIntroCompleted;
        introRunner.Cancelled += HandleIntroCancelled;
    }

    private void UnsubscribeRunner()
    {
        if (introRunner == null)
            return;
        introRunner.Completed -= HandleIntroCompleted;
        introRunner.Cancelled -= HandleIntroCancelled;
    }

    private void OnTriggerEnter2D(Collider2D other) => TryEnter(other);
    private void OnTriggerStay2D(Collider2D other) => TryEnter(other);

    private void TryEnter(Collider2D other)
    {
        if (hasEntered || boss == null || boss.IsDead || introRunner == null ||
            introDefinition == null || other == null || entry == null)
            return;

        LobbyFlowRuntime flow = LobbyFlowRuntime.Instance;
        if (flow != null && flow.IsActiveRunStage(BranchRole.Boss) && !flow.InputReady)
            return;

        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player == null || player.IsDead || !player.CompareTag("Player") ||
            other != player.BodyCollider || boss.EncounterStarted)
            return;

        Bounds playerBounds = player.BodyCollider.bounds;
        Bounds entryBounds = entry.bounds;
        if (playerBounds.min.x < entryBounds.min.x + 0.05f ||
            playerBounds.max.x > entryBounds.max.x - 0.05f)
            return;

        hasEntered = true;
        SetGates(true);
        arenaCamera?.Enter(player.transform);
        NotifyCloudPreparation();

        if (!introRunner.Play(introDefinition, player, false))
            RollBackEntry();
    }

    private void NotifyCloudPreparation()
    {
        try { onCloudPreparationRequested?.Invoke(); }
        catch (Exception ex) { Debug.LogException(ex, this); }

        Action<CutsceneBossEncounterCoordinator> handlers = CloudPreparationRequested;
        if (handlers == null)
            return;

        foreach (Delegate callback in handlers.GetInvocationList())
        {
            try { ((Action<CutsceneBossEncounterCoordinator>)callback)(this); }
            catch (Exception ex) { Debug.LogException(ex, this); }
        }
    }

    private void HandleIntroCompleted(CutsceneRunner completedRunner)
    {
        if (!hasEntered || completedRunner == null ||
            completedRunner.LastCompletedDefinition != introDefinition || beginEncounterAttempted)
            return;

        // Cloud preparation happens at entry; the existing encounter begins only after this signal.
        beginEncounterAttempted = true;
        if (!boss.IsDead && !boss.EncounterStarted)
            boss.BeginEncounter();

        if (!boss.EncounterStarted)
            RollBackEntry();
    }

    private void HandleIntroCancelled(CutsceneRunner cancelledRunner)
    {
        if (hasEntered && cancelledRunner != null &&
            cancelledRunner.LastCancelledDefinition == introDefinition)
            RollBackEntry();
    }

    private void SetGates(bool closed)
    {
        gatesClosed = closed;
        foreach (Collider2D gate in gates)
        {
            if (gate == null)
                continue;
            gate.isTrigger = !closed;
            SpriteRenderer visual = gate.GetComponent<SpriteRenderer>();
            if (visual != null)
                visual.enabled = closed;
        }
    }

    private void RollBackEntry()
    {
        hasEntered = false;
        if (!beginEncounterAttempted || boss == null || !boss.EncounterStarted)
            beginEncounterAttempted = false;
        SetGates(false);
        arenaCamera?.Exit();
    }

    private void OnDisable()
    {
        if (introRunner != null)
            introRunner.Cancel();
        UnsubscribeRunner();

        if (gatesClosed)
            SetGates(false);
        arenaCamera?.Exit();
    }

    private void OnDestroy() => UnsubscribeRunner();
}
