using System;
using System.Collections.Generic;
using UnityEngine;

public enum EconomyError
{
    None, NotReady, InvalidAmount, InvalidReason, InsufficientFunds, Busy,
    SaveFailed, InvalidTransaction, StateConflict, InvalidItem, ItemLimit, PlayerDead
}

[Serializable]
public class CurrencyResult
{
    public bool Success;
    public EconomyError Error;
    public int RequestedAmount;
    public int AppliedAmount;
    public int BalanceAfter;
    public string TransactionId;

    public CurrencyResult Copy() => (CurrencyResult)MemberwiseClone();
    public static CurrencyResult Failed(EconomyError error, int balance = 0, string id = null) =>
        new CurrencyResult { Error = error, BalanceAfter = balance, TransactionId = id };
}

public readonly struct CurrencyChange
{
    public readonly int BeforeBalance, AfterBalance, BeforePending, AfterPending, ActualDelta;
    public readonly string Reason, TransactionId;

    public CurrencyChange(int before, int after, int beforePending, int afterPending, string reason, string id)
    {
        BeforeBalance = before; AfterBalance = after;
        BeforePending = beforePending; AfterPending = afterPending;
        ActualDelta = after - before; Reason = reason; TransactionId = id;
    }
}

public sealed class PurchaseRequest
{
    public readonly string[] CardIds;
    public readonly string TransactionId;
    public PurchaseRequest(IEnumerable<string> cardIds, string transactionId = null)
    {
        CardIds = cardIds == null ? null : new List<string>(cardIds).ToArray();
        TransactionId = transactionId;
    }
}

[Serializable]
public sealed class PieceCount
{
    public string id;
    public int count;
    public PieceCount() { }
    public PieceCount(string id, int count) { this.id = id; this.count = count; }
}

[Serializable]
public sealed class PendingToken
{
    public string tokenId;
    public int amount;
    public bool settled;
}

[Serializable]
public sealed class PendingGrant
{
    public string grantId;
    public string transactionId;
    public int remainingAmount;
    public List<PendingToken> tokens = new List<PendingToken>();
}

[Serializable]
public sealed class EconomyReceipt
{
    public string id;
    public string requestKey;
    public CurrencyResult result;
    public bool hasChestReward;
    public ChestRewardReceipt chestReward;
}

public enum EconomyDeathPhase { Captured, LossPrepared, Committed }

[Serializable]
public sealed class EconomyDeathRecord
{
    public string deathId;
    public long sourceRevision;
    public int randomSeed;
    public EconomyDeathPhase phase;
    public bool awaitingChoice;
    public bool hasResult;
    public DeathResultData result;
    public int lostCoins;
    public int lostPieces;
    public List<PieceCount> allPiecesAfterUnequip = new List<PieceCount>();
    public List<PieceCount> removedPieces = new List<PieceCount>();
    public List<PieceCount> remainingPieces = new List<PieceCount>();
}

[Serializable]
public sealed class EconomyProfileData
{
    public const int CurrentSchemaVersion = 4;
    public int schemaVersion = CurrentSchemaVersion;
    public long revision;
    public string profileId = Guid.NewGuid().ToString("N");
    public int balance;
    public PlayStatistics statistics = new PlayStatistics();
    // Includes pieces referenced by equipped doctrines, exactly once.
    public List<PieceCount> pieces = new List<PieceCount>();
    public List<PendingGrant> pendingGrants = new List<PendingGrant>();
    public List<EconomyReceipt> processedTransactions = new List<EconomyReceipt>();
    // JsonUtility serializes inline custom classes by value, including nulls.
    // Keep an explicit discriminator so an empty death object is never a death.
    public bool hasDeath;
    public EconomyDeathRecord death;
    public bool hasRun;
    public LobbyRunData run;

    public EconomyProfileData Copy()
    {
        var copy = JsonUtility.FromJson<EconomyProfileData>(JsonUtility.ToJson(this));
        copy.Normalize();
        return copy;
    }

    public void Normalize()
    {
        statistics = statistics ?? new PlayStatistics();
        if (!hasDeath) death = null;
        else if (death != null && !death.hasResult) death.result = null;
        if (!hasRun) run = null;
        else run?.Normalize();
        if (processedTransactions != null)
            foreach (EconomyReceipt receipt in processedTransactions)
                if (receipt != null && !receipt.hasChestReward) receipt.chestReward = null;
    }
}

// Presentation only: follow the corpse while it moves, then keep its final
// world position after destruction. Shared by every token and save retry.
public sealed class CoinDropOrigin
{
    private Transform source;
    private readonly Vector3 localOffset;
    private Vector3 position;
    private bool ready;

    public CoinDropOrigin(Transform source, Vector3 initialPosition)
    {
        this.source = source;
        position = initialPosition;
        localOffset = source != null ? source.InverseTransformPoint(initialPosition) : Vector3.zero;
        ready = source == null;
    }
    public Vector3 Position
    {
        get
        {
            if (source != null) position = source.TransformPoint(localOffset);
            return position;
        }
    }
    public bool IsReady => ready || source == null;
    public void Freeze()
    {
        position = Position;
        source = null;
        ready = true;
    }
}

public readonly struct CoinFlightRequest
{
    public readonly PendingGrant Grant;
    public readonly Vector3 Position;
    public readonly Camera SourceCamera;
    public readonly float PresentationDelay;
    public readonly CoinDropOrigin DropOrigin;
    public CoinFlightRequest(PendingGrant grant, Vector3 position, Camera camera, float presentationDelay = 0f,
        CoinDropOrigin dropOrigin = null)
    {
        Grant = grant; Position = position; SourceCamera = camera;
        PresentationDelay = Mathf.Max(0f, presentationDelay); DropOrigin = dropOrigin;
    }
}

public enum EnemyRewardKind { Dog, Believer, Executive, Boss }

public interface IEconomyRandom { int Next(int minInclusive, int maxExclusive); }
public sealed class EconomyRandom : IEconomyRandom
{
    private readonly System.Random random;
    public EconomyRandom() { random = new System.Random(); }
    public EconomyRandom(int seed) { random = new System.Random(seed); }
    public int Next(int minInclusive, int maxExclusive) => random.Next(minInclusive, maxExclusive);
}
