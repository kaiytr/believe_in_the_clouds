using UnityEngine;

[DisallowMultipleComponent]
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
    }

    private void Start()
    {
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

        deathWatcher.BeginWatching(storyBeliever.Enemy, GetPlayerHealth(), runner, afterDeathDefinition);
    }

    private PlayerHealth GetPlayerHealth()
    {
        return playerCharacter != null ? playerCharacter.GetComponent<PlayerHealth>() : null;
    }
}
