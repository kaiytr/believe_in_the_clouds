using UnityEngine;

public sealed class TemporaryStageExit : InteractableBehaviour
{
    private void Awake() { displayName = "로비 복귀 출구"; interactRadius = 1.5f; }
    public override string InteractionPrompt => "E : 로비로 복귀 (남은 체력 유지)";
    public override bool CanInteract(GameObject interactor)
    {
        var flow = LobbyFlowRuntime.Instance;
        return base.CanInteract(interactor) && flow != null && flow.InputReady && flow.State.area == RunArea.TemporaryStage &&
            flow.State.hasStage && flow.State.stage.completed && flow.State.stage.role != BranchRole.Boss;
    }
    protected override void OnInteract(GameObject interactor) { LobbyFlowRuntime.Instance.ReturnToLobby(); }
}
