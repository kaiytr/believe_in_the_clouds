using UnityEngine;

public sealed class StoryAltarInteractable : InteractableBehaviour, IWorldInteractionPrompt
{
    public Transform PromptAnchor => transform;
    public Vector3 PromptOffset => GetPromptOffsetAboveArt();
    public override string InteractionPrompt => "E : 기록 읽기";
    public override bool CanInteract(GameObject interactor) => base.CanInteract(interactor) &&
        StoryRoomFlow.Instance != null && StoryRoomFlow.Instance.CanInteract &&
        StoryRoomFlow.Instance.Phase >= FirstAltarPhase.BelieverDead;
    protected override void OnInteract(GameObject interactor) => StoryRoomFlow.Instance.OpenDocument();
}
