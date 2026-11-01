using UnityEngine;

// Adjacent immediate-E target verifies compatibility in the isolated scene.
public sealed class ChestTestNotice : InteractableBehaviour
{
    public override string InteractionPrompt => "E : 안내 보기";
    protected override void OnInteract(GameObject interactor) =>
        FindFirstObjectByType<ChestTestContext>()?.ShowModal();
}
