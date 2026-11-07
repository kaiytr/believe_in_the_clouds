using UnityEngine;

public sealed class TemporaryStageExit : InteractableBehaviour, IWorldInteractionPrompt
{
    public Transform PromptAnchor => transform;
    // Like the branch doors: the prompt sits in the middle of the door.
    public Vector3 PromptOffset => GetPromptOffsetAtArtCenter();
    public bool PromptCentered => true;

    private void Awake() { displayName = "로비 복귀 출구"; interactRadius = 1.5f; }
    public override string InteractionPrompt => "E : 진입";
    public override bool CanInteract(GameObject interactor)
    {
        var flow = LobbyFlowRuntime.Instance;
        if (!base.CanInteract(interactor) || flow == null || !flow.InputReady || flow.State.area != RunArea.TemporaryStage || !flow.State.hasStage)
            return false;

        LobbyBranchRevealEffect reveal = GetComponent<LobbyBranchRevealEffect>();
        if (reveal != null && reveal.IsPlaying) return false;

        // Visiting a shop is optional. Allow its return door to complete an
        // otherwise unused shop stage so the player can always leave.
        return flow.State.stage.role == BranchRole.Shop ||
            flow.State.stage.completed && flow.State.stage.role != BranchRole.Boss;
    }
    protected override void OnInteract(GameObject interactor)
    {
        LobbyFlowRuntime flow = LobbyFlowRuntime.Instance;
        if (flow == null || flow.State?.stage == null) return;

        if (flow.State.stage.role == BranchRole.Shop && !flow.State.stage.completed &&
            !flow.CompleteStage().Success)
            return;

        flow.ReturnToLobby();
    }
}
