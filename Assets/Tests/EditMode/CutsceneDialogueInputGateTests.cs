using NUnit.Framework;

public sealed class CutsceneDialogueInputGateTests
{
    [Test]
    public void TypingClickCompletesCurrentLineAndNextClickAdvances()
    {
        CutsceneDialogueInputGate gate = new CutsceneDialogueInputGate();
        gate.BeginTyping();

        Assert.That(gate.HandleClick(), Is.EqualTo(CutsceneDialogueClickResult.TypingCompleted));
        Assert.That(gate.WaitingForAdvance, Is.True);
        Assert.That(gate.HandleClick(), Is.EqualTo(CutsceneDialogueClickResult.Advanced));
    }

    [Test]
    public void ChoiceClickIsConsumedBeforeSelectedLineCanAdvance()
    {
        CutsceneDialogueInputGate gate = new CutsceneDialogueInputGate();
        gate.BeginChoice();

        Assert.That(gate.HandleClick(), Is.EqualTo(CutsceneDialogueClickResult.BlockedByChoice));
        gate.ChoiceSelected();
        gate.BeginTyping();

        Assert.That(gate.HandleClick(), Is.EqualTo(CutsceneDialogueClickResult.TypingCompleted));
        Assert.That(gate.HandleClick(), Is.EqualTo(CutsceneDialogueClickResult.Advanced));
    }
}
