using System;
using System.Collections.Generic;

// Pure calculations. The service owns the only mutable, published profile.
public static class EconomyLedger
{
    public static int Pending(EconomyProfileData data)
    {
        long sum = 0;
        foreach (PendingGrant grant in data.pendingGrants) sum += grant.remainingAmount;
        return checked((int)sum);
    }

    public static int Count(IReadOnlyList<PieceCount> pieces, string id)
    {
        foreach (PieceCount piece in pieces) if (piece.id == id) return piece.count;
        return 0;
    }

    public static int Total(IReadOnlyList<PieceCount> pieces)
    {
        int total = 0;
        foreach (PieceCount piece in pieces) total = checked(total + piece.count);
        return total;
    }

    public static void Add(List<PieceCount> pieces, string id, int amount)
    {
        PieceCount entry = pieces.Find(piece => piece.id == id);
        if (entry == null) pieces.Add(new PieceCount(id, amount));
        else entry.count = checked(entry.count + amount);
    }

    public static List<PieceCount> CopyPieces(IReadOnlyList<PieceCount> pieces)
    {
        var copy = new List<PieceCount>();
        foreach (PieceCount piece in pieces) copy.Add(new PieceCount(piece.id, piece.count));
        return copy;
    }

    public static bool ValidatePieces(IReadOnlyList<PieceCount> pieces)
    {
        if (pieces == null) return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (PieceCount piece in pieces)
            if (piece == null || !ShopCatalog.TryGet(piece.id, out _) || !ids.Add(piece.id) ||
                piece.count < 0 || piece.count > EconomySettings.MaxPiecesPerKind) return false;
        return true;
    }

    public static bool SamePieces(IReadOnlyList<PieceCount> a, IReadOnlyList<PieceCount> b)
    {
        if (!ValidatePieces(a) || !ValidatePieces(b) || Total(a) != Total(b)) return false;
        foreach (PieceCount piece in a) if (Count(b, piece.id) != piece.count) return false;
        return true;
    }

    public static bool Validate(EconomyProfileData data)
    {
        if (data == null || data.schemaVersion != EconomyProfileData.CurrentSchemaVersion ||
            data.revision < 0 || data.revision == long.MaxValue || string.IsNullOrWhiteSpace(data.profileId) ||
            data.balance < 0 || data.balance > EconomySettings.MaxCoins || !ValidatePieces(data.pieces) ||
            data.statistics == null || !data.statistics.IsValid() || data.pendingGrants == null || data.processedTransactions == null) return false;
        long pending = 0;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (EconomyReceipt receipt in data.processedTransactions)
            if (receipt == null || string.IsNullOrWhiteSpace(receipt.id) || !ids.Add(receipt.id) ||
                string.IsNullOrWhiteSpace(receipt.requestKey) || receipt.result == null || !receipt.result.Success ||
                receipt.result.Error != EconomyError.None || receipt.result.TransactionId != receipt.id ||
                receipt.result.RequestedAmount < 0 || receipt.result.AppliedAmount < 0 ||
                receipt.result.AppliedAmount > receipt.result.RequestedAmount ||
                receipt.result.BalanceAfter < 0 || receipt.result.BalanceAfter > EconomySettings.MaxCoins) return false;
        ids.Clear();
        foreach (PendingGrant grant in data.pendingGrants)
        {
            if (grant == null || string.IsNullOrWhiteSpace(grant.grantId) || !ids.Add(grant.grantId) ||
                grant.remainingAmount <= 0 || grant.tokens == null || grant.tokens.Count == 0 || grant.tokens.Count > 8 ||
                !data.processedTransactions.Exists(receipt => receipt.id == grant.transactionId)) return false;
            EconomyReceipt receipt = data.processedTransactions.Find(item => item.id == grant.transactionId);
            long remaining = 0, originalAmount = 0;
            var tokenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (PendingToken token in grant.tokens)
            {
                if (token == null || string.IsNullOrWhiteSpace(token.tokenId) || !tokenIds.Add(token.tokenId) || token.amount <= 0)
                    return false;
                if (!token.settled) remaining += token.amount;
                originalAmount += token.amount;
            }
            if (remaining != grant.remainingAmount || originalAmount != receipt.result.AppliedAmount) return false;
            pending += remaining;
        }
        if (pending + data.balance > EconomySettings.MaxCoins) return false;
        if (data.hasRun != (data.run != null) || (data.hasRun && !LobbyRules.Validate(data.run,
            data.hasDeath && data.death != null && data.death.phase != EconomyDeathPhase.Committed))) return false;
        if (!ChestRules.ValidateProfile(data) || !NarrativeRules.Validate(data)) return false;
        EconomyDeathRecord death = data.death;
        if (data.hasDeath != (death != null)) return false;
        if (death == null) return true;
        if (string.IsNullOrWhiteSpace(death.deathId) || death.sourceRevision < 0 || death.sourceRevision > data.revision ||
            !Enum.IsDefined(typeof(EconomyDeathPhase), death.phase) || !ValidatePieces(death.allPiecesAfterUnequip) ||
            !ValidatePieces(death.removedPieces) || !ValidatePieces(death.remainingPieces)) return false;
        if (death.hasResult != (death.result != null) || (death.hasResult && !death.result.IsValid()) ||
            (death.awaitingChoice && death.phase != EconomyDeathPhase.Captured)) return false;
        if (death.phase == EconomyDeathPhase.Captured) return true;
        int total = Total(death.allPiecesAfterUnequip);
        if (death.lostCoins < 0 || death.lostCoins > EconomySettings.MaxCoins * (long)EconomySettings.DeathLossPercent / 100 ||
            death.lostPieces != PieceLoss(death, total) ||
            Total(death.removedPieces) != death.lostPieces || Total(death.remainingPieces) + death.lostPieces != total) return false;
        foreach (PieceCount piece in death.allPiecesAfterUnequip)
            if (piece.count != Count(death.remainingPieces, piece.id) + Count(death.removedPieces, piece.id)) return false;
        if (death.phase == EconomyDeathPhase.Committed &&
            !data.processedTransactions.Exists(receipt => receipt.id == death.deathId && receipt.requestKey == "death")) return false;
        return true;
    }

    public static void PrepareDeath(EconomyProfileData data)
    {
        EconomyDeathRecord death = data.death;
        death.allPiecesAfterUnequip = CopyPieces(data.pieces);
        death.remainingPieces = CopyPieces(data.pieces);
        death.removedPieces.Clear();
        death.lostCoins = (int)(((long)data.balance + Pending(data)) * EconomySettings.DeathLossPercent / 100);
        int remaining = Total(data.pieces);
        death.lostPieces = PieceLoss(death, remaining);
        var random = new System.Random(death.randomSeed);
        for (int i = 0; i < death.lostPieces; i++)
        {
            int index = random.Next(remaining--);
            foreach (PieceCount piece in death.remainingPieces)
            {
                if (index >= piece.count) { index -= piece.count; continue; }
                piece.count--;
                Add(death.removedPieces, piece.id, 1);
                break;
            }
        }
        death.phase = EconomyDeathPhase.LossPrepared;
    }

    // Deaths captured before the rule changed keep their original loss.
    private static int PieceLoss(EconomyDeathRecord death, int total) =>
        death.usesThirdPieceLoss ? total / 3 : total * 30 / 100;
}
