using UnityEngine;

[DisallowMultipleComponent]
public sealed class CutsceneDeathWatcher : MonoBehaviour
{
    private EnemyUnit target;
    private PlayerHealth player;
    private CutsceneRunner runner;
    private CutsceneDefinition afterDeathDefinition;
    private bool watching;
    private bool played;

    public bool IsWatching => watching;
    public bool HasPlayed => played;

    public void BeginWatching(EnemyUnit storyTarget, PlayerHealth playerActor,
        CutsceneRunner sequenceRunner, CutsceneDefinition sequence)
    {
        if (watching || played || storyTarget == null || playerActor == null ||
            sequenceRunner == null || sequence == null)
            return;

        target = storyTarget;
        player = playerActor;
        runner = sequenceRunner;
        afterDeathDefinition = sequence;
        watching = true;
    }

    private void Update()
    {
        if (!watching || played || target == null || runner == null)
            return;

        // A disabled or unloaded enemy is not a death. Only EnemyUnit's real death state qualifies.
        if (!target.IsDead || runner.IsRunning)
            return;

        if (runner.Play(afterDeathDefinition, player, false))
        {
            played = true;
            watching = false;
        }
    }

    private void OnDisable()
    {
        watching = false;
        target = null;
        player = null;
        runner = null;
        afterDeathDefinition = null;
    }
}
