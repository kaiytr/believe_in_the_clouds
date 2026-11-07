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
        if (!run.stage.hasChestState)
        {
            CurrencyResult registration = service.TryRegisterStageChests(new ChestManifest {
                stageInstanceId = run.stage.id, spawnIds = chests.Select(chest => chest.SpawnId).ToArray()
            });
            // 등록 실패는 조용히 넘기면 상자가 영원히 잠긴 채로 남는다. 원인을 콘솔에 남긴다.
            if (!registration.Success)
            {
                Debug.LogWarning($"[상자] 스테이지 상자 등록 실패 (stage={run.stage.id}, error={registration.Error})", this);
                return;
            }
        }

        foreach (ChestInteractable chest in chests) chest.Bind(run.stage.id);
        boundStageId = run.stage.id;
    }
}
