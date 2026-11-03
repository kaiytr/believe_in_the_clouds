using System;
using System.Collections.Generic;

public sealed partial class EconomyService
{
    // Actual deaths are registered before the reward transaction. Keep failed-save
    // kills until any subsequent commit (including the player's death capture).
    private readonly Dictionary<string, EnemyRewardKind> observedDefeats = new Dictionary<string, EnemyRewardKind>();
    public PlayStatistics StatisticsSnapshot()
    {
        PlayStatistics copy = profile.statistics.Copy();
        MergeObservedDefeats(copy);
        return copy;
    }
    public void ObserveEnemyDefeat(EnemyRewardKind kind, string id)
    {
        if (!AcceptsDefeatRewards || string.IsNullOrWhiteSpace(id) || !Enum.IsDefined(typeof(EnemyRewardKind), kind)) return;
        if (!observedDefeats.ContainsKey(id)) observedDefeats.Add(id, kind);
    }
    private void MergeObservedDefeats(PlayStatistics target)
    {
        foreach (var entry in observedDefeats) target.Defeat(entry.Value, entry.Key);
    }
    private void RecordAcquisitions(EconomyProfileData data)
    {
        if (rewardsBlocked) return;
        long coins = (long)data.balance + EconomyLedger.Pending(data) - profile.balance - EconomyLedger.Pending(profile);
        data.statistics.coins += Math.Max(0L, coins);
        foreach (PieceCount piece in data.pieces)
            data.statistics.keywords += Math.Max(0, piece.count - EconomyLedger.Count(profile.pieces, piece.id));
        MergeObservedDefeats(data.statistics);
    }

    public CurrencyResult CaptureDeferredDeath(string id, IEquipmentAdapter adapter, Action effects, DeathResultData result)
    {
        if (result == null || !result.IsValid()) return Fail(EconomyError.InvalidItem, id);
        return StartDeath(id, adapter, effects, result);
    }
    public CurrencyResult CompleteDeferredDeath(string id)
    {
        if (busy) return Fail(EconomyError.Busy, id);
        if (deathWork == null)
        {
            EconomyReceipt receipt = profile.processedTransactions.Find(item => item.id == id && item.requestKey == "death");
            return receipt != null ? receipt.result.Copy() : Fail(EconomyError.StateConflict, id);
        }
        if (deathWork.Data.death.deathId != id) return Fail(EconomyError.StateConflict, id);
        deathWork.Data.death.awaitingChoice = false;
        return RetryDeath();
    }
}
