public enum EconomyLoadStatus { Missing, Loaded, RecoveredBackup, Invalid, FutureVersion }
public readonly struct EconomyLoadResult
{
    public readonly EconomyLoadStatus Status;
    public readonly EconomyProfileData Data;
    public EconomyLoadResult(EconomyLoadStatus status, EconomyProfileData data = null) { Status = status; Data = data; }
}

public interface IEconomyStore
{
    EconomyLoadResult Load();
    bool TrySave(EconomyProfileData data);
}

// Isolated fixtures can exercise failed writes without touching a user profile.
public sealed class MemoryEconomyStore : IEconomyStore
{
    private EconomyProfileData saved;
    public bool FailWrites { get; set; }
    public int SaveCount { get; private set; }
    public EconomyLoadResult Load() => saved == null
        ? new EconomyLoadResult(EconomyLoadStatus.Missing)
        : new EconomyLoadResult(EconomyLoadStatus.Loaded, saved.Copy());
    public bool TrySave(EconomyProfileData data)
    {
        if (FailWrites || !EconomyLedger.Validate(data)) return false;
        saved = data.Copy(); SaveCount++; return true;
    }
}
