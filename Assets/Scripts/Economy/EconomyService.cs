using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Main-thread service. Work on a copy, save, then publish; notifications remain
// inside the transaction gate so event handlers cannot re-enter a transaction.
public sealed partial class EconomyService
{
    private readonly IEconomyStore store;
    private readonly IEconomyRandom random;
    private EconomyProfileData profile = new EconomyProfileData();
    private bool initialized, busy, rewardsBlocked;
    private DeathWork deathWork;
    private readonly Dictionary<string, int> retryDrops = new Dictionary<string, int>();
    private readonly Dictionary<string, EnemyRewardKind> retryDropKinds = new Dictionary<string, EnemyRewardKind>();

    private sealed class DeathWork
    {
        public EconomyProfileData Data;
        public IEquipmentAdapter Adapter;
        public Action Effects;
        public bool CaptureSaved, EffectsProcessed, Unequipped, Prepared, PreparedSaved;
    }

    public EconomyService(IEconomyStore store, IEconomyRandom random = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.random = random ?? new EconomyRandom();
    }

    public int Balance => profile.balance;
    public long Revision => profile.revision;
    public int PendingBalance => EconomyLedger.Pending(profile);
    public bool IsReady => initialized && deathWork == null;
    public bool HasPendingDeath => deathWork != null;
    public bool AcceptsDefeatRewards => IsReady && !rewardsBlocked;
    public EconomyLoadStatus LoadStatus { get; private set; }
    public event Action<CurrencyChange> Changed;
    public event Action<CoinFlightRequest> FlightRequested;
    public event Action<EconomyDeathRecord> DeathCompleted;
    public EconomyProfileData Snapshot() => profile.Copy();
    public int CountPiece(string id) => EconomyLedger.Count(profile.pieces, id);
    public bool IsPendingToken(string grantId, string tokenId) =>
        profile.pendingGrants.Find(grant => grant.grantId == grantId)?.tokens.Exists(token => token.tokenId == tokenId && !token.settled) ?? false;
    public bool CanAfford(int amount) => amount >= 0 && IsReady && Balance >= amount;

    public bool Initialize()
    {
        if (initialized) return IsReady;
        EconomyLoadResult loaded;
        try { loaded = store.Load(); }
        catch (Exception) { LoadStatus = EconomyLoadStatus.Invalid; return false; }
        LoadStatus = loaded.Status;
        if (loaded.Status == EconomyLoadStatus.Missing)
        {
            var fresh = new EconomyProfileData();
            if (!Save(fresh)) return false;
            profile = fresh;
        }
        else if (loaded.Status == EconomyLoadStatus.Loaded || loaded.Status == EconomyLoadStatus.RecoveredBackup)
        {
            if (!EconomyLedger.Validate(loaded.Data)) return false;
            profile = loaded.Data.Copy();
        }
        else return false;
        initialized = true;
        if (profile.death != null && profile.death.phase != EconomyDeathPhase.Committed)
        {
            // A process restart completes an already captured death using the same journal.
            // This prevents quitting the result screen from restoring pre-death assets.
            profile.death.awaitingChoice = false;
            rewardsBlocked = true;
            deathWork = new DeathWork
            {
                Data = profile.Copy(), CaptureSaved = true, EffectsProcessed = true, Unequipped = true,
                Prepared = profile.death.phase == EconomyDeathPhase.LossPrepared,
                PreparedSaved = profile.death.phase == EconomyDeathPhase.LossPrepared
            };
            return RetryDeath().Success;
        }
        // Flight is presentation only: persisted reservations survive an exit.
        if (PendingBalance > 0) return SettleAllPending().Success;
        return true;
    }

    public CurrencyResult TryGrant(int amount, string reason, string transactionId = null) =>
        CoinTransaction(amount, reason, transactionId, false, false, default, null);
    public CurrencyResult TrySpend(int amount, string reason, string transactionId = null) =>
        CoinTransaction(amount, reason, transactionId, true, false, default, null);
    public CurrencyResult TryGrantWithEffect(int amount, Vector3 position, string reason, string transactionId = null, Camera sourceCamera = null) =>
        CoinTransaction(amount, reason, transactionId, false, true, position, sourceCamera);

    private CurrencyResult CoinTransaction(int amount, string reason, string id, bool spend, bool effect, Vector3 position, Camera camera)
    {
        if (amount <= 0) return Fail(EconomyError.InvalidAmount, id);
        if (string.IsNullOrWhiteSpace(reason)) return Fail(EconomyError.InvalidReason, id);
        string key = RequestKey(spend ? "spend" : effect ? "flight" : "grant", reason, amount.ToString(CultureInfo.InvariantCulture));
        return Execute(id, key, reason, data =>
        {
            if (spend && data.balance < amount) return Fail(EconomyError.InsufficientFunds, id);
            int applied = spend ? amount : Math.Min(amount, EconomySettings.MaxCoins - data.balance - EconomyLedger.Pending(data));
            if (spend) data.balance -= applied;
            else if (effect && applied > 0) data.pendingGrants.Add(CreateGrant(id: activeTransactionId, amount: applied));
            else data.balance += applied;
            return Success(amount, applied, data.balance, activeTransactionId);
        }, effect ? (Action)(() => RequestFlight(activeTransactionId, position, camera)) : null);
    }

    // Only used while Execute holds the main-thread transaction gate.
    private string activeTransactionId;

    public CurrencyResult TryGrantPiece(string cardId, int amount, string reason, string transactionId = null)
    {
        if (!ShopCatalog.TryGet(cardId, out _)) return Fail(EconomyError.InvalidItem, transactionId);
        if (amount <= 0) return Fail(EconomyError.InvalidAmount, transactionId);
        if (string.IsNullOrWhiteSpace(reason)) return Fail(EconomyError.InvalidReason, transactionId);
        return Execute(transactionId, RequestKey("piece", reason, cardId, amount.ToString(CultureInfo.InvariantCulture)), reason, data =>
        {
            if ((long)EconomyLedger.Count(data.pieces, cardId) + amount > EconomySettings.MaxPiecesPerKind)
                return Fail(EconomyError.ItemLimit, transactionId);
            EconomyLedger.Add(data.pieces, cardId, amount);
            return Success(amount, amount, data.balance, activeTransactionId);
        });
    }

    public CurrencyResult TryPurchase(PurchaseRequest request)
    {
        if (request?.CardIds == null || request.CardIds.Length == 0) return Fail(EconomyError.InvalidItem, request?.TransactionId);
        string[] ids = (string[])request.CardIds.Clone();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        long price = 0;
        foreach (string id in ids)
        {
            if (!ShopCatalog.TryGet(id, out ShopEntry entry)) return Fail(EconomyError.InvalidItem, request.TransactionId);
            if (!unique.Add(id)) return Fail(EconomyError.InvalidTransaction, request.TransactionId);
            price += entry.Price;
        }
        Array.Sort(ids, StringComparer.Ordinal);
        string key = RequestKey("purchase", ids);
        return Execute(request.TransactionId, key, "shop.purchase", data =>
        {
            foreach (string id in ids)
                if (EconomyLedger.Count(data.pieces, id) >= EconomySettings.MaxPiecesPerKind) return Fail(EconomyError.ItemLimit, request.TransactionId);
            if (price > data.balance) return Fail(EconomyError.InsufficientFunds, request.TransactionId);
            data.balance -= (int)price;
            foreach (string id in ids) EconomyLedger.Add(data.pieces, id, 1);
            return Success((int)price, (int)price, data.balance, activeTransactionId);
        });
    }

    public CurrencyResult TryRewardDefeat(EnemyRewardKind kind, string defeatId, Vector3 position, Camera sourceCamera = null,
        float presentationDelay = 0f, CoinDropOrigin dropOrigin = null)
    {
        if (busy) return Fail(EconomyError.Busy, defeatId);
        if (!Enum.IsDefined(typeof(EnemyRewardKind), kind)) return Fail(EconomyError.InvalidItem, defeatId);
        if (string.IsNullOrWhiteSpace(defeatId)) return Fail(EconomyError.InvalidTransaction, defeatId);
        string key = RequestKey("defeat", kind.ToString());
        // Duplicate requests return the original receipt, including after death.
        EconomyReceipt receipt = profile.processedTransactions.Find(item => item.id == defeatId);
        if (receipt != null) return receipt.requestKey == key ? receipt.result.Copy() : Fail(EconomyError.InvalidTransaction, defeatId);
        if (!AcceptsDefeatRewards) return Fail(initialized ? EconomyError.PlayerDead : EconomyError.NotReady, defeatId);
        if (retryDropKinds.TryGetValue(defeatId, out EnemyRewardKind previousKind) && previousKind != kind)
            return Fail(EconomyError.InvalidTransaction, defeatId);
        if (!retryDrops.TryGetValue(defeatId, out int rolled))
        { rolled = EconomySettings.RollDrop(kind, random); retryDrops.Add(defeatId, rolled); retryDropKinds.Add(defeatId, kind); }
        return Execute(defeatId, key, "enemy.defeat", data =>
        {
            int applied = Math.Min(rolled, EconomySettings.MaxCoins - data.balance - EconomyLedger.Pending(data));
            if (applied > 0) data.pendingGrants.Add(CreateGrant(activeTransactionId, applied));
            return Success(rolled, applied, data.balance, activeTransactionId);
        }, () => { retryDrops.Remove(defeatId); retryDropKinds.Remove(defeatId); RequestFlight(defeatId, position, sourceCamera, presentationDelay, dropOrigin); });
    }

    private CurrencyResult Execute(string id, string key, string reason, Func<EconomyProfileData, CurrencyResult> mutate,
        Action afterCommit = null, Action<EconomyProfileData, EconomyReceipt> decorateReceipt = null)
    {
        if (busy || deathWork != null) return Fail(EconomyError.Busy, id);
        if (!initialized) return Fail(EconomyError.NotReady, id);
        if (id != null && string.IsNullOrWhiteSpace(id)) return Fail(EconomyError.InvalidTransaction, id);
        id = id ?? Guid.NewGuid().ToString("N");
        EconomyReceipt previous = profile.processedTransactions.Find(item => item.id == id);
        if (previous != null) return previous.requestKey == key ? previous.result.Copy() : Fail(EconomyError.InvalidTransaction, id);
        busy = true; activeTransactionId = id;
        try
        {
            var work = profile.Copy();
            CurrencyResult result = mutate(work);
            if (!result.Success) return result;
            RecordAcquisitions(work);
            work.revision++;
            var receipt = new EconomyReceipt { id = id, requestKey = key, result = result.Copy() };
            decorateReceipt?.Invoke(work, receipt);
            work.processedTransactions.Add(receipt);
            if (!Save(work)) return Fail(EconomyError.SaveFailed, id);
            Publish(work, reason, id);
            if (afterCommit != null) SafeInvoke(afterCommit);
            return result.Copy();
        }
        finally { activeTransactionId = null; busy = false; }
    }

    private static string RequestKey(string operation, params string[] parts)
    {
        var key = new System.Text.StringBuilder(operation);
        foreach (string part in parts) key.Append('|').Append(part?.Length ?? -1).Append(':').Append(part);
        return key.ToString();
    }

    private static PendingGrant CreateGrant(string id, int amount)
    {
        var grant = new PendingGrant { grantId = id, transactionId = id, remainingAmount = amount };
        int count = Math.Min(8, amount);
        for (int i = 0; i < count; i++)
            grant.tokens.Add(new PendingToken { tokenId = i.ToString(CultureInfo.InvariantCulture), amount = amount / count + (i < amount % count ? 1 : 0) });
        return grant;
    }

    private void RequestFlight(string grantId, Vector3 position, Camera camera, float presentationDelay = 0f,
        CoinDropOrigin dropOrigin = null)
    {
        PendingGrant grant = profile.Copy().pendingGrants.Find(item => item.grantId == grantId);
        if (grant == null) return;
        if (FlightRequested == null) return; // Runtime update/reload safely settles when there is no view.
        foreach (Action<CoinFlightRequest> handler in FlightRequested.GetInvocationList())
            SafeInvoke(() => handler(new CoinFlightRequest(grant, position, camera, presentationDelay, dropOrigin)));
    }

    public CurrencyResult TrySettleToken(string grantId, string tokenId)
    {
        if (busy || deathWork != null) return Fail(EconomyError.Busy);
        if (!initialized) return Fail(EconomyError.NotReady);
        PendingGrant grant = profile.pendingGrants.Find(item => item.grantId == grantId);
        PendingToken token = grant?.tokens.Find(item => item.tokenId == tokenId);
        if (token == null || token.settled) return Success(0, 0, Balance, grantId);
        busy = true;
        try
        {
            var work = profile.Copy();
            PendingGrant pending = work.pendingGrants.Find(item => item.grantId == grantId);
            pending.tokens.Find(item => item.tokenId == tokenId).settled = true;
            pending.remainingAmount -= token.amount;
            work.balance += token.amount;
            if (pending.remainingAmount == 0) work.pendingGrants.Remove(pending);
            work.revision++;
            if (!Save(work)) return Fail(EconomyError.SaveFailed, grantId);
            Publish(work, "coin.arrival", grantId);
            return Success(token.amount, token.amount, Balance, grantId);
        }
        finally { busy = false; }
    }

    public CurrencyResult SettleAllPending()
    {
        if (busy || deathWork != null) return Fail(EconomyError.Busy);
        if (!initialized) return Fail(EconomyError.NotReady);
        int amount = PendingBalance;
        if (amount == 0) return Success(0, 0, Balance, null);
        busy = true;
        try
        {
            var work = profile.Copy();
            work.balance += amount; work.pendingGrants.Clear(); work.revision++;
            if (!Save(work)) return Fail(EconomyError.SaveFailed);
            Publish(work, "coin.flush", null);
            return Success(amount, amount, Balance, null);
        }
        finally { busy = false; }
    }

    // Called as soon as HP reaches zero, before any lethal-hit/death effects.
    public void BlockDefeatRewards() { rewardsBlocked = true; }
    public bool BeginLife()
    {
        if (!IsReady || busy) return false;
        rewardsBlocked = false;
        return true;
    }

    public CurrencyResult TryHandleDeath(string deathId, IEquipmentAdapter equipment, Action processDeathEffects) =>
        StartDeath(deathId, equipment, processDeathEffects, null);

    private CurrencyResult StartDeath(string deathId, IEquipmentAdapter equipment, Action processDeathEffects, DeathResultData result)
    {
        if (busy) return Fail(EconomyError.Busy, deathId);
        if (!initialized) return Fail(EconomyError.NotReady, deathId);
        if (string.IsNullOrWhiteSpace(deathId)) return Fail(EconomyError.InvalidTransaction, deathId);
        var previous = profile.processedTransactions.Find(item => item.id == deathId);
        if (previous != null) return previous.requestKey == "death" ? previous.result.Copy() : Fail(EconomyError.InvalidTransaction, deathId);
        if (deathWork != null)
            return deathWork.Data.death.deathId == deathId ? RetryDeath() : Fail(EconomyError.Busy, deathId);
        rewardsBlocked = true;
        var work = profile.Copy();
        work.hasDeath = true;
        work.death = new EconomyDeathRecord
        {
            deathId = deathId, sourceRevision = profile.revision, phase = EconomyDeathPhase.Captured,
            randomSeed = random.Next(0, int.MaxValue),
            awaitingChoice = result != null, hasResult = result != null, result = result?.Copy()
        };
        MergeObservedDefeats(work.statistics);
        if (work.hasRun)
        {
            work.run.area = RunArea.Dying;
            work.run.hp = 0;
        }
        work.revision++;
        deathWork = new DeathWork { Data = work, Adapter = equipment, Effects = processDeathEffects };
        return RetryDeath();
    }

    public CurrencyResult RetryDeath()
    {
        if (busy) return Fail(EconomyError.Busy);
        if (deathWork == null) return Fail(EconomyError.StateConflict);
        busy = true;
        try
        {
            DeathWork pending = deathWork;
            string id = pending.Data.death.deathId;
            if (!pending.CaptureSaved)
            {
                if (!Save(pending.Data)) return Fail(EconomyError.SaveFailed, id);
                pending.CaptureSaved = true;
                profile = pending.Data.Copy();
            }
            if (!pending.EffectsProcessed)
            {
                pending.EffectsProcessed = true; // Never emit the same death effects again on a retry.
                if (pending.Effects != null) SafeInvoke(pending.Effects);
            }
            if (pending.Data.death.awaitingChoice) return Success(0, 0, Balance, id);
            if (!pending.Unequipped)
            {
                try
                {
                    var all = pending.Adapter == null ? EconomyLedger.CopyPieces(pending.Data.pieces)
                        : pending.Adapter.UnequipAllAndGetOwnedPieces(EconomyLedger.CopyPieces(pending.Data.pieces));
                    if (!EconomyLedger.SamePieces(pending.Data.pieces, all)) return Fail(EconomyError.StateConflict, id);
                    pending.Data.pieces = EconomyLedger.CopyPieces(all);
                    pending.Unequipped = true;
                }
                catch (Exception) { return Fail(EconomyError.StateConflict, id); }
            }
            if (!pending.Prepared)
            {
                EconomyLedger.PrepareDeath(pending.Data);
                pending.Data.revision++;
                pending.Prepared = true;
            }
            if (!pending.PreparedSaved)
            {
                if (!Save(pending.Data)) return Fail(EconomyError.SaveFailed, id);
                pending.PreparedSaved = true;
                profile = pending.Data.Copy();
            }
            var final = pending.Data.Copy();
            int beforeTotal = final.balance + EconomyLedger.Pending(final);
            final.balance = beforeTotal - final.death.lostCoins;
            final.pendingGrants.Clear();
            final.pieces = EconomyLedger.CopyPieces(final.death.remainingPieces);
            final.death.phase = EconomyDeathPhase.Committed;
            if (final.hasRun)
                final.run = LobbyRules.NewRun(LobbyRules.StableId(id + ":next-run"),
                    pending.Data.run.maxHp, pending.Data.death.randomSeed);
            final.statistics = new PlayStatistics();
            final.revision++;
            var result = Success(beforeTotal, final.death.lostCoins, final.balance, id);
            final.processedTransactions.Add(new EconomyReceipt { id = id, requestKey = "death", result = result.Copy() });
            if (!Save(final)) return Fail(EconomyError.SaveFailed, id);
            deathWork = null;
            observedDefeats.Clear();
            Publish(final, "player.death", id);
            if (DeathCompleted != null)
                foreach (Action<EconomyDeathRecord> handler in DeathCompleted.GetInvocationList())
                    SafeInvoke(() => handler(profile.Copy().death));
            return result;
        }
        finally { busy = false; }
    }

    private bool Save(EconomyProfileData data)
    {
        if (!EconomyLedger.Validate(data)) return false;
        try { return store.TrySave(data.Copy()); }
        catch (Exception) { return false; }
    }

    private void Publish(EconomyProfileData data, string reason, string id)
    {
        var change = new CurrencyChange(Balance, data.balance, PendingBalance, EconomyLedger.Pending(data), reason, id);
        profile = data;
        if (Changed == null) return;
        foreach (Action<CurrencyChange> handler in Changed.GetInvocationList()) SafeInvoke(() => handler(change));
    }

    private CurrencyResult Fail(EconomyError error, string id = null) => CurrencyResult.Failed(error, Balance, id);
    private static CurrencyResult Success(int requested, int applied, int balance, string id) =>
        new CurrencyResult { Success = true, RequestedAmount = requested, AppliedAmount = applied, BalanceAfter = balance, TransactionId = id };
    private static void SafeInvoke(Action action)
    {
        try { action(); }
        catch (Exception ex) { Debug.LogException(ex); }
    }
}
