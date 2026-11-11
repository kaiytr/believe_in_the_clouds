using UnityEngine;

public sealed class StoryRoomExit : InteractableBehaviour, IWorldInteractionPrompt
{
    public Transform PromptAnchor => transform;
    public Vector3 PromptOffset => GetPromptOffsetAtArtCenter();
    public bool PromptCentered => true;
    public override string InteractionPrompt => "E : 로비로";
    public override bool CanInteract(GameObject interactor) => base.CanInteract(interactor) &&
        StoryRoomFlow.Instance != null && StoryRoomFlow.Instance.CanInteract &&
        StoryRoomFlow.Instance.Phase == FirstAltarPhase.StoryRead;
    protected override void OnInteract(GameObject interactor) => StoryRoomFlow.Instance.LeaveRoom();
}
