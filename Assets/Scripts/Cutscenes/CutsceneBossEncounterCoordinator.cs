using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BossEncounterTrigger))]
[DefaultExecutionOrder(-550)]
public sealed class CutsceneBossEncounterCoordinator : MonoBehaviour
{
    [SerializeField] private BossEncounterTrigger arena;
    [SerializeField] private CutsceneRunner introRunner;
    [SerializeField] private CutsceneDefinition introDefinition;
    [SerializeField] private UnityEngine.Events.UnityEvent onCloudPreparationRequested =
        new UnityEngine.Events.UnityEvent();

    private bool beginEncounterAttempted;
    private PlayerHealth enteredPlayer;

    public bool HasEntered => arena != null && arena.HasEntered;
    public bool BeginEncounterAttempted => beginEncounterAttempted;
    public BossCombatController EncounterBoss => arena != null ? arena.Boss : null;
    public event Action<CutsceneBossEncounterCoordinator> CloudPreparationRequested;

    private void Awake()
    {
        if (arena == null) arena = GetComponent<BossEncounterTrigger>();
        if (introRunner == null) introRunner = GetComponentInChildren<CutsceneRunner>(true);
        if (arena != null) arena.AutoBeginEncounter = false;
    }

    private void OnEnable() => Subscribe();

    public void Configure(BossEncounterTrigger encounterArena, CutsceneRunner sequenceRunner,
        CutsceneDefinition sequence)
    {
        Unsubscribe();
        arena = encounterArena;
        introRunner = sequenceRunner;
        introDefinition = sequence;
        if (arena != null) arena.AutoBeginEncounter = false;
        Subscribe();
    }

    private void Subscribe()
    {
        if (!isActiveAndEnabled) return;
        if (arena != null)
        {
            arena.ArenaEntered -= HandleArenaEntered;
            arena.ArenaEntered += HandleArenaEntered;
        }
        if (introRunner != null)
        {
            introRunner.Completed -= HandleIntroCompleted;
            introRunner.Cancelled -= HandleIntroCancelled;
            introRunner.Completed += HandleIntroCompleted;
            introRunner.Cancelled += HandleIntroCancelled;
        }
    }

    private void Unsubscribe()
    {
        if (arena != null) arena.ArenaEntered -= HandleArenaEntered;
        if (introRunner == null) return;
        introRunner.Completed -= HandleIntroCompleted;
        introRunner.Cancelled -= HandleIntroCancelled;
    }

    private void HandleArenaEntered()
    {
        BossCombatController boss = EncounterBoss;
        if (boss == null || boss.IsDead || boss.EncounterStarted || beginEncounterAttempted) return;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        enteredPlayer = playerObject != null ? playerObject.GetComponent<PlayerHealth>() : null;
        if (enteredPlayer == null || enteredPlayer.IsDead || introRunner == null || introDefinition == null)
        {
            arena.CancelIntro();
            return;
        }

        // 아레나가 봉쇄와 구름 준비를 끝낸 뒤에만 알림과 대사를 시작한다.
        try { onCloudPreparationRequested?.Invoke(); }
        catch (Exception exception) { Debug.LogException(exception, this); }
        Action<CutsceneBossEncounterCoordinator> handlers = CloudPreparationRequested;
        if (handlers != null)
            foreach (Delegate callback in handlers.GetInvocationList())
                try { ((Action<CutsceneBossEncounterCoordinator>)callback)(this); }
                catch (Exception exception) { Debug.LogException(exception, this); }

        if (!introRunner.Play(introDefinition, enteredPlayer, false)) arena.CancelIntro();
    }

    private void HandleIntroCompleted(CutsceneRunner completedRunner)
    {
        BossCombatController boss = EncounterBoss;
        if (!HasEntered || boss == null || completedRunner == null ||
            completedRunner.LastCompletedDefinition != introDefinition || beginEncounterAttempted ||
            enteredPlayer == null || enteredPlayer.IsDead) return;
        beginEncounterAttempted = true;
        if (!boss.IsDead && !boss.EncounterStarted) boss.BeginEncounter();
        if (!boss.EncounterStarted)
        {
            beginEncounterAttempted = false;
            arena.CancelIntro();
        }
    }

    private void HandleIntroCancelled(CutsceneRunner cancelledRunner)
    {
        if (cancelledRunner == null || cancelledRunner.LastCancelledDefinition != introDefinition) return;
        beginEncounterAttempted = false;
        arena?.CancelIntro();
        enteredPlayer = null;
    }

    private void Update()
    {
        if (introRunner != null && introRunner.IsRunning && enteredPlayer != null && enteredPlayer.IsDead)
            introRunner.Cancel();
    }

    private void OnDisable()
    {
        if (introRunner != null && introRunner.IsRunning) introRunner.Cancel();
        Unsubscribe();
        arena?.CancelIntro();
        enteredPlayer = null;
    }

    private void OnDestroy() => Unsubscribe();
}
