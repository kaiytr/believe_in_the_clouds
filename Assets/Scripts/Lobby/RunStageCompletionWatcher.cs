using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-400)]
public sealed class RunStageCompletionWatcher : MonoBehaviour
{
    [SerializeField] private BranchRole role;
    [SerializeField] private EnemyUnit[] authoredEnemies;

    private LobbyFlowRuntime flow;
    private EnemyUnit[] enemies;
    private BossCombatController boss;
    private BossEndingSequence bossEnding;
    private bool armed;
    private bool completionPending;
    private float nextCompletionAttempt;

    private void Start()
    {
        flow = LobbyFlowRuntime.Instance;
        if (flow == null || !flow.IsActiveRunStage(role))
        {
            enabled = false;
            return;
        }

        armed = true;
        if (role == BranchRole.Combat)
            enemies = authoredEnemies != null && authoredEnemies.Length > 0
                ? authoredEnemies : FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None);
        else if (role == BranchRole.Boss)
        {
            boss = FindFirstObjectByType<BossCombatController>();
            bossEnding = FindFirstObjectByType<BossEndingSequence>();
        }
        else if (role == BranchRole.Shop)
            ShopScreen.ShopClosed += HandleShopClosed;
    }

    private void Update()
    {
        if (!armed || flow == null) return;

        if (role == BranchRole.Combat && enemies != null && enemies.Length > 0 &&
            enemies.All(enemy => enemy == null || enemy.IsDead))
            completionPending = true;
        else if (role == BranchRole.Boss && boss != null && boss.IsDead)
        {
            if (bossEnding != null)
            {
                bossEnding.Begin();
                if (bossEnding.HoldsStageCompletion) return;
            }
            completionPending = true;
        }

        if (!completionPending || Time.unscaledTime < nextCompletionAttempt) return;
        if (flow.State?.stage == null || flow.State.stage.completed)
        {
            completionPending = false;
            return;
        }

        CurrencyResult result = flow.CompleteStage();
        if (result.Success) completionPending = false;
        else nextCompletionAttempt = Time.unscaledTime + .5f;
    }

    private void HandleShopClosed(ShopInteractable shop)
    {
        if (armed && shop != null) completionPending = true;
    }

    private void OnDestroy()
    {
        if (role == BranchRole.Shop) ShopScreen.ShopClosed -= HandleShopClosed;
    }
}
