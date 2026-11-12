using UnityEngine;

[DisallowMultipleComponent]
public sealed class CutsceneDeathWatcher : MonoBehaviour
{
    private EnemyUnit target;
    private PlayerHealth player;
    private CutsceneRunner runner;
    private CutsceneDefinition afterDeathDefinition;
    [SerializeField, Min(0f)] private float deathMotionDelaySeconds = 1f;
    private bool watching;
    private bool played;
    private bool deathObserved;
    private float deathObservedAt;

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
        if (!watching || played || target == null || runner == null || player == null || player.IsDead)
            return;

        // A disabled or unloaded enemy is not a death. Only EnemyUnit's real death state qualifies.
        if (!target.IsDead)
            return;

        // Let the death pose and knockback read before replacing the scene with the after-death dialogue.
        if (!deathObserved)
        {
            deathObserved = true;
            deathObservedAt = Time.time;
        }
        if (runner.IsRunning || Time.time - deathObservedAt < deathMotionDelaySeconds)
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
        deathObserved = false;
        deathObservedAt = 0f;
    }
}
