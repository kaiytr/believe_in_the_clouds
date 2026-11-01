using UnityEngine;

// Optional placement contract: targets without it retain the docked HUD prompt.
public interface IWorldInteractionPrompt
{
    Transform PromptAnchor { get; }
    Vector3 PromptOffset { get; }
}

public interface IHoldInteractable
{
    float HoldDuration { get; }
    string HoldLabel { get; }
    void BeginHold(GameObject interactor, int sessionId);
    void CancelHold(GameObject interactor, int sessionId);
    HoldInteractionResult CompleteHold(GameObject interactor, int sessionId);
}

public readonly struct HoldInteractionResult
{
    public readonly bool Success;
    public readonly string Message;
    public HoldInteractionResult(bool success, string message = null) { Success = success; Message = message; }
}

// A captured frame also lets integration tests exercise the actual controller
// without synthesizing OS input or duplicating the hold state machine.
public readonly struct InteractionFrame
{
    public readonly bool Pressed, Held, SecondaryPressed, ClosePressed, ActionPressed;
    public InteractionFrame(bool pressed = false, bool held = false, bool secondaryPressed = false,
        bool closePressed = false, bool actionPressed = false)
    {
        Pressed = pressed; Held = held; SecondaryPressed = secondaryPressed;
        ClosePressed = closePressed; ActionPressed = actionPressed;
    }
}
