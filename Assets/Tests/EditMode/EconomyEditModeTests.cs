using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class EconomyEditModeTests
{
    private MemoryEconomyStore store;
    private EconomyService service;

    [SetUp]
    public void SetUp()
    {
        store = new MemoryEconomyStore(); service = new EconomyService(store, new EconomyRandom(17));
        Assert.That(service.Initialize(), Is.True);
    }

    [Test]
    public void NewProfileIsEmptyAndReloadDoesNotApplyInitialValuesAgain()
    {
        Assert.That(service.Balance, Is.Zero); Assert.That(service.PendingBalance, Is.Zero);
        Assert.That(service.Snapshot().pieces, Is.Empty);
        service.TryGrant(75, "fixture", "initial"); service.TryGrantPiece("result.speed", 2, "fixture");
        var restored = new EconomyService(store); Assert.That(restored.Initialize(), Is.True);
        Assert.That(restored.Balance, Is.EqualTo(75)); Assert.That(restored.CountPiece("result.speed"), Is.EqualTo(2));
    }

    [TestCase(0)] [TestCase(-1)] [TestCase(int.MinValue)]
    public void TransactionsRejectNonPositiveAmounts(int amount)
    {
        Assert.That(service.TryGrant(amount, "test").Error, Is.EqualTo(EconomyError.InvalidAmount));
        Assert.That(service.TrySpend(amount, "test").Error, Is.EqualTo(EconomyError.InvalidAmount));
        Assert.That(service.Balance, Is.Zero);
    }

    [Test]
    public void InvalidReasonInsufficientFundsAndAffordabilityAreExplicit()
    {
        Assert.That(service.CanAfford(0), Is.True); Assert.That(service.CanAfford(-1), Is.False);
        Assert.That(service.TryGrant(1, " ").Error, Is.EqualTo(EconomyError.InvalidReason));
        Assert.That(service.TrySpend(1, "purchase").Error, Is.EqualTo(EconomyError.InsufficientFunds));
        Assert.That(service.TryGrant(1, "test", " ").Error, Is.EqualTo(EconomyError.InvalidTransaction));
    }

    [Test]
    public void CapIncludesPendingAndDoesNotOverflowOnIntMaxValue()
    {
        service.TryGrant(EconomySettings.MaxCoins - 5, "fixture");
        var grant = service.TryGrantWithEffect(int.MaxValue, Vector3.zero, "fixture", "pending");
        Assert.That(grant.AppliedAmount, Is.EqualTo(5)); Assert.That(service.PendingBalance, Is.EqualTo(5));
        Assert.That(service.TryGrant(int.MaxValue, "fixture").AppliedAmount, Is.Zero);
        service.SettleAllPending(); Assert.That(service.Balance, Is.EqualTo(EconomySettings.MaxCoins));
    }

    [Test]
    public void ReceiptsSurviveReloadRejectOtherInputsAndDoNotExposeMutableState()
    {
        int changes = 0; service.Changed += _ => changes++;
        var first = service.TryGrant(100, "quest.one", "quest:1");
        first.AppliedAmount = 999;
        Assert.That(service.TryGrant(100, "quest.one", "quest:1").AppliedAmount, Is.EqualTo(100));
        Assert.That(service.TryGrant(101, "quest.one", "quest:1").Error, Is.EqualTo(EconomyError.InvalidTransaction));
        Assert.That(service.TrySpend(100, "quest.one", "quest:1").Error, Is.EqualTo(EconomyError.InvalidTransaction));
        var snapshot = service.Snapshot(); snapshot.balance = 900;
        snapshot.processedTransactions.Clear();
        Assert.That(service.Balance, Is.EqualTo(100)); Assert.That(changes, Is.EqualTo(1));
        var restored = new EconomyService(store); restored.Initialize();
        restored.TryGrant(100, "quest.one", "quest:1"); Assert.That(restored.Balance, Is.EqualTo(100));
    }

    [Test]
    public void SaveFailureDoesNotPublishAndEventReentryIsBusy()
    {
        int changes = 0; CurrencyResult nested = null;
        service.Changed += _ => { changes++; nested = service.TryGrant(10, "reentry"); };
        store.FailWrites = true;
        Assert.That(service.TryGrant(100, "fixture", "retry").Error, Is.EqualTo(EconomyError.SaveFailed));
        Assert.That(service.Balance, Is.Zero); Assert.That(changes, Is.Zero);
        store.FailWrites = false; Assert.That(service.TryGrant(100, "fixture", "retry").Success, Is.True);
        Assert.That(nested.Error, Is.EqualTo(EconomyError.Busy)); Assert.That(service.Balance, Is.EqualTo(100));
    }

    [TestCase("subject.player", 30, ShopRarity.Common)]
    [TestCase("subject.enemy", 30, ShopRarity.Common)]
    [TestCase("subject.boss", 55, ShopRarity.Uncommon)]
    [TestCase("subject.all", 95, ShopRarity.Rare)]
    [TestCase("condition.hit", 55, ShopRarity.Uncommon)]
    [TestCase("condition.attack", 55, ShopRarity.Uncommon)]
    [TestCase("condition.death", 30, ShopRarity.Common)]
    [TestCase("condition.low-health", 30, ShopRarity.Common)]
    [TestCase("result.explode", 95, ShopRarity.Rare)]
    [TestCase("result.speed", 30, ShopRarity.Common)]
    [TestCase("result.attack", 55, ShopRarity.Uncommon)]
    [TestCase("result.heal", 55, ShopRarity.Uncommon)]
    public void AllCatalogPricesAndRaritiesMatchTheDesign(string id, int price, ShopRarity rarity)
    {
        Assert.That(ShopCatalog.TryGet(id, out ShopEntry entry), Is.True);
        Assert.That(entry.Price, Is.EqualTo(price)); Assert.That(entry.Rarity, Is.EqualTo(rarity));
    }

    [Test]
    public void PurchasesAreAtomicUnlimitedAndIdempotent()
    {
        service.TryGrant(300, "fixture");
        var request = new PurchaseRequest(new[] { "subject.player", "result.speed" }, "purchase:1");
        store.FailWrites = true;
        Assert.That(service.TryPurchase(request).Error, Is.EqualTo(EconomyError.SaveFailed));
        Assert.That(service.Balance, Is.EqualTo(300)); Assert.That(service.Snapshot().pieces, Is.Empty);
        store.FailWrites = false; Assert.That(service.TryPurchase(request).Success, Is.True);
        Assert.That(service.Balance, Is.EqualTo(240)); Assert.That(service.CountPiece("result.speed"), Is.EqualTo(1));
        service.TryPurchase(new PurchaseRequest(new[] { "result.speed", "subject.player" }, "purchase:1"));
        Assert.That(service.Balance, Is.EqualTo(240));
        service.TryPurchase(new PurchaseRequest(new[] { "result.speed" }, "purchase:2"));
        Assert.That(service.CountPiece("result.speed"), Is.EqualTo(2));
    }

    [Test]
    public void InvalidCartPieceLimitAndInsufficientFundsNeverPartiallyPurchase()
    {
        Assert.That(service.TryPurchase(new PurchaseRequest(new[] { "result.heal" })).Error, Is.EqualTo(EconomyError.InsufficientFunds));
        service.TryGrant(100, "fixture");
        Assert.That(service.TryPurchase(new PurchaseRequest(new[] { "subject.player", "subject.player" })).Error, Is.EqualTo(EconomyError.InvalidTransaction));
        Assert.That(service.TryPurchase(new PurchaseRequest(new[] { "missing" })).Error, Is.EqualTo(EconomyError.InvalidItem));
        service.TryGrantPiece("subject.player", 9999, "fixture");
        Assert.That(service.TryPurchase(new PurchaseRequest(new[] { "subject.player", "result.speed" })).Error, Is.EqualTo(EconomyError.ItemLimit));
        Assert.That(service.Balance, Is.EqualTo(100)); Assert.That(service.CountPiece("result.speed"), Is.Zero);
        Assert.That(service.TryGrantPiece("subject.player", int.MaxValue, "fixture").Error, Is.EqualTo(EconomyError.ItemLimit));
        Assert.That(ShopCatalog.IdFor((ResultType)3), Is.Null);
    }

    private sealed class EndpointRandom : IEconomyRandom
    {
        public int Calls;
        public readonly bool Upper;
        public EndpointRandom(bool upper) { Upper = upper; }
        public int Next(int min, int max) { Calls++; return Upper ? max - 1 : min; }
    }

    [TestCase(EnemyRewardKind.Dog, 5, 7)]
    [TestCase(EnemyRewardKind.Believer, 3, 5)]
    [TestCase(EnemyRewardKind.Executive, 7, 8)]
    [TestCase(EnemyRewardKind.Boss, 0, 0)]
    public void DropEndpointsAreInclusiveAndRetriesNeverReroll(EnemyRewardKind kind, int min, int max)
    {
        foreach (bool upper in new[] { false, true })
        {
            var rng = new EndpointRandom(upper); var memory = new MemoryEconomyStore();
            var economy = new EconomyService(memory, rng); economy.Initialize();
            memory.FailWrites = true;
            economy.TryRewardDefeat(kind, "enemy:1", Vector3.zero);
            memory.FailWrites = false;
            var result = economy.TryRewardDefeat(kind, "enemy:1", Vector3.zero);
            Assert.That(result.RequestedAmount, Is.EqualTo(upper ? max : min));
            economy.TryRewardDefeat(kind, "enemy:1", Vector3.one);
            Assert.That(rng.Calls, Is.EqualTo(kind == EnemyRewardKind.Boss ? 0 : 1));
            Assert.That(economy.PendingBalance, Is.EqualTo(upper ? max : min));
        }
    }

    [Test]
    public void TokenSettlementRetriesSurvivesExitAndIgnoresStaleCallbacks()
    {
        service.TryGrantWithEffect(12, Vector3.zero, "fixture", "flight:1");
        PendingGrant grant = service.Snapshot().pendingGrants[0];
        var token = grant.tokens[0]; store.FailWrites = true;
        Assert.That(service.TrySettleToken(grant.grantId, token.tokenId).Error, Is.EqualTo(EconomyError.SaveFailed));
        Assert.That(service.PendingBalance, Is.EqualTo(12)); Assert.That(service.Balance, Is.Zero);
        store.FailWrites = false;
        service.TrySettleToken(grant.grantId, token.tokenId); service.TrySettleToken(grant.grantId, token.tokenId);
        Assert.That(service.Balance, Is.EqualTo(token.amount));
        var restored = new EconomyService(store); restored.Initialize();
        Assert.That(restored.PendingBalance, Is.Zero); Assert.That(restored.Balance, Is.EqualTo(12));
        restored.TryGrantWithEffect(5, Vector3.one, "fixture", "flight:2");
        restored.TrySettleToken(grant.grantId, token.tokenId);
        Assert.That(restored.PendingBalance, Is.EqualTo(5));
    }

    private sealed class Equipment : IEquipmentAdapter
    {
        public int Calls;
        public Action OnUnequip;
        public IReadOnlyList<PieceCount> UnequipAllAndGetOwnedPieces(IReadOnlyList<PieceCount> pieces)
        { Calls++; OnUnequip?.Invoke(); return EconomyLedger.CopyPieces(pieces); }
    }

    [Test]
    public void DeathEffectsThenUnequipThenOnePopulationLossIncludingPending()
    {
        service.TryGrant(101, "fixture"); service.TryGrantWithEffect(9, Vector3.zero, "fixture", "old-flight");
        service.TryGrantPiece("subject.player", 2, "fixture"); service.TryGrantPiece("condition.death", 2, "fixture");
        service.TryGrantPiece("result.speed", 2, "fixture"); service.TryGrantPiece("subject.enemy", 7, "fixture");
        var order = new List<string>(); int completed = 0;
        var equipment = new Equipment { OnUnequip = () => order.Add("unequip") };
        service.DeathCompleted += _ => { order.Add("committed"); completed++; };
        var result = service.TryHandleDeath("life:1/death", equipment, () =>
        {
            order.Add("effects");
            Assert.That(service.TryRewardDefeat(EnemyRewardKind.Dog, "late", Vector3.zero).Success, Is.False);
        });
        Assert.That(result.Success, Is.True);
        CollectionAssert.AreEqual(new[] { "effects", "unequip", "committed" }, order);
        Assert.That(service.Balance, Is.EqualTo(77)); Assert.That(service.PendingBalance, Is.Zero);
        Assert.That(EconomyLedger.Total(service.Snapshot().pieces), Is.EqualTo(10));
        Assert.That(service.Snapshot().death.lostPieces, Is.EqualTo(3));
        service.TryHandleDeath("life:1/death", equipment, () => order.Add("duplicate"));
        Assert.That(completed, Is.EqualTo(1)); Assert.That(equipment.Calls, Is.EqualTo(1));
        Assert.That(service.TryRewardDefeat(EnemyRewardKind.Dog, "after", Vector3.zero).Error, Is.EqualTo(EconomyError.PlayerDead));
        service.TrySettleToken("old-flight", "0"); Assert.That(service.Balance, Is.EqualTo(77));
        Assert.That(service.BeginLife(), Is.True);
        Assert.That(service.TryRewardDefeat(EnemyRewardKind.Dog, "new-life", Vector3.zero).Success, Is.True);
    }

    [TestCase(0, 0)] [TestCase(1, 0)] [TestCase(2, 0)] [TestCase(3, 0)]
    [TestCase(4, 1)] [TestCase(9, 2)] [TestCase(13, 3)]
    public void DeathUsesFloorOfTheTotalRatherThanEachKind(int total, int lost)
    {
        if (total > 0) service.TryGrantPiece("subject.player", total, "fixture");
        service.TryHandleDeath("death", new Equipment(), null);
        Assert.That(EconomyLedger.Total(service.Snapshot().pieces), Is.EqualTo(total - lost));
    }

    private sealed class FailNthStore : IEconomyStore
    {
        public readonly MemoryEconomyStore Memory = new MemoryEconomyStore();
        public int Writes, FailAt;
        public EconomyLoadResult Load() => Memory.Load();
        public bool TrySave(EconomyProfileData data) => ++Writes != FailAt && Memory.TrySave(data);
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void FailedDeathKeepsAssetsLocksTradingAndRecoversTheSamePlan(int failedDeathWrite)
    {
        var disk = new FailNthStore(); var economy = new EconomyService(disk, new EconomyRandom(77)); economy.Initialize();
        economy.TryGrant(100, "fixture"); economy.TryGrantPiece("subject.player", 13, "fixture");
        disk.FailAt = disk.Writes + failedDeathWrite;
        int effects = 0; var equipment = new Equipment();
        Assert.That(economy.TryHandleDeath("death", equipment, () => effects++).Error, Is.EqualTo(EconomyError.SaveFailed));
        Assert.That(economy.IsReady, Is.False); Assert.That(economy.Balance, Is.EqualTo(100));
        Assert.That(economy.CountPiece("subject.player"), Is.EqualTo(13));
        Assert.That(economy.TrySpend(1, "locked").Error, Is.EqualTo(EconomyError.Busy));
        Assert.That(economy.RetryDeath().Success, Is.True);
        Assert.That(effects, Is.EqualTo(1)); Assert.That(equipment.Calls, Is.EqualTo(1));
        Assert.That(economy.Balance, Is.EqualTo(70)); Assert.That(economy.CountPiece("subject.player"), Is.EqualTo(10));
        var restored = new EconomyService(disk); restored.Initialize();
        restored.TryHandleDeath("death", equipment, () => effects++);
        Assert.That(restored.Balance, Is.EqualTo(70)); Assert.That(effects, Is.EqualTo(1));
    }

    [TestCase(2)] [TestCase(3)]
    public void PersistedDeathJournalRecoversAfterRestartWithoutReemittingEffects(int failedDeathWrite)
    {
        var disk = new FailNthStore(); var economy = new EconomyService(disk, new EconomyRandom(91)); economy.Initialize();
        economy.TryGrant(101, "fixture"); economy.TryGrantPiece("result.speed", 9, "fixture");
        disk.FailAt = disk.Writes + failedDeathWrite;
        economy.TryHandleDeath("death", new Equipment(), null);
        var restored = new EconomyService(disk); Assert.That(restored.Initialize(), Is.True);
        Assert.That(restored.Balance, Is.EqualTo(71)); Assert.That(restored.CountPiece("result.speed"), Is.EqualTo(7));
        restored.TryHandleDeath("death", new Equipment(), () => Assert.Fail("Death effects replayed"));
        Assert.That(restored.Balance, Is.EqualTo(71));
    }

    [Test]
    public void DeathLossUsesOneMixedPopulationAndJournalRetainsTheExactDraw()
    {
        var disk = new FailNthStore(); var economy = new EconomyService(disk, new EconomyRandom(42)); economy.Initialize();
        economy.TryGrantPiece("subject.player", 1, "fixture"); economy.TryGrantPiece("condition.death", 1, "fixture");
        economy.TryGrantPiece("result.speed", 1, "fixture"); economy.TryGrantPiece("result.explode", 1, "fixture");
        disk.FailAt = disk.Writes + 3; // Journal saved, final commit fails.
        economy.TryHandleDeath("mixed-death", new Equipment(), null);
        var plan = economy.Snapshot().death;
        Assert.That(plan.lostPieces, Is.EqualTo(1));
        string expected = JsonUtility.ToJson(plan);
        var restored = new EconomyService(disk); Assert.That(restored.Initialize(), Is.True);
        var committed = restored.Snapshot().death; committed.phase = EconomyDeathPhase.LossPrepared;
        Assert.That(JsonUtility.ToJson(committed), Is.EqualTo(expected));
        Assert.That(EconomyLedger.Total(restored.Snapshot().pieces), Is.EqualTo(3));
    }

    [Test]
    public void JsonStoreRecoversBackupWithoutOverwritingItWithCorruption()
    {
        WithPath(path =>
        {
            var json = new JsonEconomyStore(path); var economy = new EconomyService(json); economy.Initialize();
            economy.TryGrant(20, "fixture"); economy.TryGrant(30, "fixture");
            File.WriteAllText(path, "{corrupt");
            var recovered = new EconomyService(new JsonEconomyStore(path)); Assert.That(recovered.Initialize(), Is.True);
            Assert.That(recovered.LoadStatus, Is.EqualTo(EconomyLoadStatus.RecoveredBackup)); Assert.That(recovered.Balance, Is.EqualTo(20));
            Assert.That(recovered.TryGrant(5, "after-recovery").Success, Is.True);
            File.WriteAllText(path, "{corrupt-again");
            var again = new EconomyService(new JsonEconomyStore(path)); Assert.That(again.Initialize(), Is.True);
            Assert.That(again.Balance, Is.EqualTo(20));
        });
    }

    [Test]
    public void CorruptAndFutureSavesArePreservedAndLocked()
    {
        WithPath(path =>
        {
            foreach (string json in new[] { "{}", "{broken", JsonUtility.ToJson(new EconomyProfileData { schemaVersion = 99 }) })
            {
                File.WriteAllText(path, json);
                var economy = new EconomyService(new JsonEconomyStore(path));
                Assert.That(economy.Initialize(), Is.False);
                Assert.That(economy.TryGrant(1, "test").Error, Is.EqualTo(EconomyError.NotReady));
                Assert.That(File.ReadAllText(path), Is.EqualTo(json));
            }
        });
    }

    [Test]
    public void ProfileValidationRejectsUnknownIdsDuplicatesNegativeCountsAndOverflow()
    {
        var data = new EconomyProfileData(); data.pieces.Add(new PieceCount("result.slow", 1));
        Assert.That(EconomyLedger.Validate(data), Is.False);
        data.pieces.Clear(); data.pieces.Add(new PieceCount("result.speed", -1)); Assert.That(EconomyLedger.Validate(data), Is.False);
        data.pieces[0].count = 10000; Assert.That(EconomyLedger.Validate(data), Is.False);
        data.pieces[0].count = 1; data.pieces.Add(new PieceCount("result.speed", 1)); Assert.That(EconomyLedger.Validate(data), Is.False);
    }

    private static void WithPath(Action<string> action)
    {
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Economy/UnitTests", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        try { action(Path.Combine(directory, "save.json")); }
        finally { Directory.Delete(directory, true); }
    }
}
