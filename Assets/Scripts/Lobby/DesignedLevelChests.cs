using System.Linq;
using UnityEngine;

public sealed class DesignedLevelChests : MonoBehaviour
{
    [SerializeField] private ChestInteractable[] chests;
    [SerializeField] private bool tutorial;
    private string boundStageId;
    private float nextAttempt;

    private void Update()
    {
        LobbyFlowRuntime flow = LobbyFlowRuntime.Instance;
        if (!tutorial && (flow?.Scene == null || flow.Scene.gameObject.scene != gameObject.scene)) return;
        if (Time.unscaledTime < nextAttempt) return;
        EconomyService service = Currency.Service;
        LobbyRunData run = tutorial ? service?.RunSnapshot() : flow.State;
        if (run?.area != RunArea.TemporaryStage || run.stage == null || run.stage.id == boundStageId) return;

        nextAttempt = Time.unscaledTime + .5f;
        if (!run.stage.hasChestState && !service.TryRegisterStageChests(new ChestManifest {
            stageInstanceId = run.stage.id, spawnIds = chests.Select(chest => chest.SpawnId).ToArray()
        }).Success) return;

        foreach (ChestInteractable chest in chests) chest.Bind(run.stage.id);
        boundStageId = run.stage.id;
    }
}
