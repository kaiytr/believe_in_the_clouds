using System;

public sealed class CutsceneExternalStage
{
    private readonly CutsceneRunner runner;
    private readonly int runId;

    public string Id { get; }
    public bool IsResolved { get; private set; }
    public bool WasCancelled { get; private set; }

    internal CutsceneExternalStage(CutsceneRunner owner, int generation, string stageId)
    {
        runner = owner;
        runId = generation;
        Id = stageId ?? string.Empty;
    }

    /// <summary>Resume the sequence after the external UI or action has completed.</summary>
    public void Complete() => runner?.ResolveExternalStage(this, runId, false);

    /// <summary>Cancel the sequence and release every cutscene-owned lock.</summary>
    public void Cancel() => runner?.ResolveExternalStage(this, runId, true);

    internal void MarkResolved(bool cancelled)
    {
        IsResolved = true;
        WasCancelled = cancelled;
    }
}
