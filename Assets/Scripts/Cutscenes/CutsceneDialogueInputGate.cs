public enum CutsceneDialogueClickResult
{
    Ignored,
    TypingCompleted,
    Advanced,
    BlockedByChoice
}

/// <summary>Separates the click that fills a line from the click that advances it.</summary>
public sealed class CutsceneDialogueInputGate
{
    public bool IsTyping { get; private set; }
    public bool WaitingForAdvance { get; private set; }
    public bool ChoicePending { get; private set; }

    public void BeginTyping()
    {
        IsTyping = true;
        WaitingForAdvance = false;
        ChoicePending = false;
    }

    public void BeginChoice()
    {
        IsTyping = false;
        WaitingForAdvance = false;
        ChoicePending = true;
    }

    public void ChoiceSelected()
    {
        ChoicePending = false;
    }

    public void MarkFullyTyped()
    {
        IsTyping = false;
        WaitingForAdvance = true;
    }

    public CutsceneDialogueClickResult HandleClick()
    {
        if (ChoicePending)
            return CutsceneDialogueClickResult.BlockedByChoice;

        if (IsTyping)
        {
            IsTyping = false;
            WaitingForAdvance = true;
            return CutsceneDialogueClickResult.TypingCompleted;
        }

        if (WaitingForAdvance)
        {
            WaitingForAdvance = false;
            return CutsceneDialogueClickResult.Advanced;
        }

        return CutsceneDialogueClickResult.Ignored;
    }

    public void Reset()
    {
        IsTyping = false;
        WaitingForAdvance = false;
        ChoicePending = false;
    }
}
