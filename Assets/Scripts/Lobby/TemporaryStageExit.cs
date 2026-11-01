using UnityEngine;

public sealed class TemporaryStageExit : InteractableBehaviour
{
    private void Awake() { displayName = "로비 복귀 출구"; interactRadius = 1.5f; }
    public override string InteractionPrompt
    {
        get
        {
            var flow = LobbyFlowRuntime.Instance;
            return flow != null && flow.State != null && flow.State.hasStage &&
                flow.State.stage.role == BranchRole.Event && !flow.State.stage.completed
                ? "E : 이벤트 확인 후 로비 복귀"
                : "E : 로비로 복귀 (남은 체력 유지)";
        }
    }
    public override bool CanInteract(GameObject interactor)
    {
        var flow = LobbyFlowRuntime.Instance;
        if (!base.CanInteract(interactor) || flow == null || !flow.InputReady || flow.State.area != RunArea.TemporaryStage || !flow.State.hasStage)
            return false;

        bool eventPlaceholder = flow.State.stage.role == BranchRole.Event && !flow.State.stage.completed;
        bool completedNonBoss = flow.State.stage.completed && flow.State.stage.role != BranchRole.Boss;
        return eventPlaceholder || completedNonBoss;
    }
    protected override void OnInteract(GameObject interactor)
    {
        LobbyFlowRuntime flow = LobbyFlowRuntime.Instance;
        if (flow.State.stage.role == BranchRole.Event && !flow.State.stage.completed && !flow.CompleteStage().Success)
            return;

        flow.ReturnToLobby();
    }
}
