using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-500)]
public sealed class CutsceneTutorialFlow : MonoBehaviour
{
    [Header("Sequences")]
    [SerializeField] private CutsceneRunner runner;
    [SerializeField] private CutsceneDefinition introDefinition;
    [SerializeField] private CutsceneDefinition encounterDefinition;
    [SerializeField] private CutsceneDefinition afterDeathDefinition;
    [SerializeField] private bool playIntroOnStart = true;

    [Header("Actors and watcher")]
    [SerializeField] private CutsceneCharacter playerCharacter;
    [SerializeField] private CutsceneCharacter storyBeliever;
    [SerializeField] private CutsceneDeathWatcher deathWatcher;

    private readonly List<(Behaviour component, bool enabled)> heldBehaviours = new List<(Behaviour, bool)>();
    private Rigidbody2D heldBody;
    private bool bodyWasSimulated;
    private bool holdingBeliever;

    public CutsceneRunner Runner => runner;
    public CutsceneDefinition IntroDefinition => introDefinition;
    public CutsceneDefinition EncounterDefinition => encounterDefinition;
    public CutsceneDefinition AfterDeathDefinition => afterDeathDefinition;

    private void Awake()
    {
        if (runner == null)
            runner = GetComponent<CutsceneRunner>();
        if (deathWatcher == null)
            deathWatcher = GetComponent<CutsceneDeathWatcher>();
        HoldBeliever();
    }

    private void OnEnable()
    {
        if (runner != null)
            runner.Completed += HandleCompleted;
    }

    private void OnDisable()
    {
        if (runner != null)
            runner.Completed -= HandleCompleted;
        ReleaseBeliever();
    }

    private IEnumerator Start()
    {
        // 로비 런타임의 씬 초기화/위치 복원을 끝낸 뒤 시작 독백을 재생한다.
        yield return null;
        LobbyFlowRuntime flow = LobbyFlowRuntime.Instance;
        while (flow != null && flow.Scene != null && flow.Scene.gameObject.scene == gameObject.scene && !flow.InputReady)
            yield return null;
        if (playIntroOnStart && runner != null && introDefinition != null)
            runner.Play(introDefinition, GetPlayerHealth(), false);
    }

    public void Configure(CutsceneRunner sequenceRunner, CutsceneDefinition intro,
        CutsceneDefinition encounter, CutsceneDefinition afterDeath,
        CutsceneCharacter playerActor, CutsceneCharacter believer, CutsceneDeathWatcher watcher)
    {
        runner = sequenceRunner;
        introDefinition = intro;
        encounterDefinition = encounter;
        afterDeathDefinition = afterDeath;
        playerCharacter = playerActor;
        storyBeliever = believer;
        deathWatcher = watcher;
        playIntroOnStart = true;

        if (runner != null)
        {
            runner.SetActorBindings(
                new CutsceneActorBinding("player", playerCharacter),
                new CutsceneActorBinding("believer", storyBeliever));
        }
    }

    private void HandleCompleted(CutsceneRunner completedRunner)
    {
        if (completedRunner == null || completedRunner.LastCompletedDefinition != encounterDefinition ||
            deathWatcher == null || storyBeliever == null)
            return;

        ReleaseBeliever();
        deathWatcher.BeginWatching(storyBeliever.Enemy, GetPlayerHealth(), runner, afterDeathDefinition);
    }

    private void HoldBeliever()
    {
        EnemyUnit enemy = storyBeliever != null ? storyBeliever.Enemy : null;
        if (holdingBeliever || enemy == null || enemy.IsDead) return;
        holdingBeliever = true;
        Hold(enemy);
        foreach (EnemyAttack attack in enemy.GetComponentsInChildren<EnemyAttack>(true)) Hold(attack);
        foreach (EnemyContactDamage contact in enemy.GetComponentsInChildren<EnemyContactDamage>(true)) Hold(contact);
        foreach (BeliefView view in enemy.GetComponentsInChildren<BeliefView>(true)) Hold(view);
        heldBody = enemy.GetComponent<Rigidbody2D>();
        if (heldBody != null)
        {
            bodyWasSimulated = heldBody.simulated;
            heldBody.simulated = false;
        }
    }

    private void Hold(Behaviour component)
    {
        heldBehaviours.Add((component, component.enabled));
        component.enabled = false;
    }

    private void ReleaseBeliever()
    {
        if (!holdingBeliever) return;
        holdingBeliever = false;
        if (heldBody != null)
        {
            heldBody.linearVelocity = Vector2.zero;
            heldBody.simulated = bodyWasSimulated;
        }
        heldBody = null;
        for (int i = heldBehaviours.Count - 1; i >= 0; i--)
        {
            var state = heldBehaviours[i];
            if (state.component != null && !state.component.enabled && state.enabled)
                state.component.enabled = true;
        }
        heldBehaviours.Clear();
    }

    private PlayerHealth GetPlayerHealth()
    {
        return playerCharacter != null ? playerCharacter.GetComponent<PlayerHealth>() : null;
    }
}
