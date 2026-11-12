using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ChestEditModeTests
{
    private MemoryEconomyStore store;
    private EconomyService service;
    private string stageId;
    private ChestManifest Manifest(params string[] ids) => new ChestManifest { stageInstanceId = stageId, spawnIds = ids };
    [SetUp] public void SetUp()
    {
        StartChestStage(ChestRules.TableVersion);
    }
    private void StartChestStage(int tableVersion)
    {
        store = new MemoryEconomyStore(); service = new EconomyService(store, new EconomyRandom(43));
        Assert.That(service.Initialize(), Is.True);
        Assert.That(service.TryInitializeRun(100).Success, Is.True);
        Assert.That(service.TryDepartBranch(service.RunSnapshot().candidates.First(c => c.role == BranchRole.Combat).id, 100).Success, Is.True);
        stageId = service.RunSnapshot().stage.id;
        ChestManifest manifest = Manifest("A", "B"); manifest.rewardTableVersion = tableVersion;
        Assert.That(service.TryRegisterStageChests(manifest).Success, Is.True);
    }

    [TestCase(0, ShopRarity.Common)] [TestCase(64, ShopRarity.Common)]
    [TestCase(65, ShopRarity.Uncommon)] [TestCase(89, ShopRarity.Uncommon)]
    [TestCase(90, ShopRarity.Rare)] [TestCase(99, ShopRarity.Rare)]
    public void RarityBoundaries(int roll, ShopRarity expected) => Assert.That(ChestRules.RarityFor(roll), Is.EqualTo(expected));

    [Test] public void AllHundredInputsProduceExactRequestedWeightsAndCatalogPools()
    {
        var counts = Enumerable.Range(0, 100).GroupBy(ChestRules.RarityFor).ToDictionary(g => g.Key, g => g.Count());
        Assert.That(counts[ShopRarity.Common], Is.EqualTo(65)); Assert.That(counts[ShopRarity.Uncommon], Is.EqualTo(25)); Assert.That(counts[ShopRarity.Rare], Is.EqualTo(10));
        Assert.That(ChestRules.Pool(ShopRarity.Common).Length, Is.EqualTo(5));
        Assert.That(ChestRules.Pool(ShopRarity.Uncommon).Length, Is.EqualTo(5)); Assert.That(ChestRules.Pool(ShopRarity.Rare).Length, Is.EqualTo(2));
    }
    private sealed class FixedRandom : IEconomyRandom
    {
        private readonly int[] values; private int index;
        public FixedRandom(params int[] values) { this.values = values; }
        public int Next(int min, int max)
        { int value = values[index++]; Assert.That(value, Is.InRange(min, max - 1)); return value; }
    }
    [TestCase(0, 7)] [TestCase(65, 17)] [TestCase(99, 7)]
    public void RewardHasOnePieceAndInclusiveIndependentCoins(int rarity, int coins)
    {
        ChestRecord plan = ChestRules.Roll("fixture", new FixedRandom(rarity, 0, coins));
        Assert.That(plan.pieceAmount, Is.EqualTo(1)); Assert.That(plan.rolledCoins, Is.EqualTo(2 * coins));
        Assert.That(plan.rarity, Is.EqualTo(ChestRules.RarityFor(rarity)));
    }
    [Test] public void EveryPoolIndexIsReachableInStableIdOrder()
    {
        foreach (int rarity in new[] { 0, 65, 90 })
        {
            ShopEntry[] pool = ChestRules.Pool(ChestRules.RarityFor(rarity));
            for (int i = 0; i < pool.Length; i++)
                Assert.That(ChestRules.Roll("fixture", new FixedRandom(rarity, i, 12)).pieceId, Is.EqualTo(pool[i].Id));
        }
    }
    [Test] public void RegistrationIsIdempotentAndRejectsDuplicateOrChangedManifests()
    {
        int saves = store.SaveCount;
        Assert.That(service.TryRegisterStageChests(Manifest("B", "A")).Success, Is.True);
        Assert.That(store.SaveCount, Is.EqualTo(saves));
        Assert.That(service.TryRegisterStageChests(Manifest("A", "C")).Error, Is.EqualTo(EconomyError.InvalidTransaction));
        Assert.That(service.TryRegisterStageChests(Manifest("A", "A")).Error, Is.EqualTo(EconomyError.InvalidItem));
        Assert.That(service.TryRegisterStageChests(Manifest()).Success, Is.False);
    }
    [Test] public void FailedRegistrationAndProcessRecreationKeepTheSamePlan()
    {
        service.TryCompleteTemporaryStage(stageId); service.TryReturnFromStage(stageId, 100);
        service.TryDepartBranch(service.RunSnapshot().candidates.First(c => c.role == BranchRole.Combat).id, 100);
        stageId = service.RunSnapshot().stage.id;
        string expected = JsonUtility.ToJson(ChestRules.Plan(stageId, "A"));
        store.FailWrites = true;
        Assert.That(service.TryRegisterStageChests(Manifest("A")).Error, Is.EqualTo(EconomyError.SaveFailed));
        Assert.That(service.GetChestState(stageId, "A"), Is.Null);
        service = service.Reopen(); Assert.That(service.Initialize(), Is.True); store.FailWrites = false;
        Assert.That(service.TryRegisterStageChests(Manifest("A")).Success, Is.True);
        Assert.That(JsonUtility.ToJson(service.GetChestState(stageId, "A")), Is.EqualTo(expected));
    }
    [TestCase(ChestRules.LegacyTableVersion, 13)] [TestCase(ChestRules.TableVersion, 26)]
    public void StablePlanKeepsOriginalDrawAcrossRewardTableVersions(int tableVersion, int coins)
    {
        ChestRecord first = ChestRules.Plan("test-stage", "test-spawn", tableVersion);
        Assert.That(first.pieceId, Is.EqualTo("subject.enemy"));
        Assert.That(first.rolledCoins, Is.EqualTo(coins));
        Assert.That(JsonUtility.ToJson(ChestRules.Plan("test-stage", "test-spawn", tableVersion)), Is.EqualTo(JsonUtility.ToJson(first)));
    }
    [Test] public void OpenCommitsAllRewardsAndOneReceiptInOneSaveAndRejectsReentry()
    {
        int saves = store.SaveCount, effects = 0, changes = 0;
        service.FlightRequested += request => { effects++; Assert.That(service.TryOpenChest(stageId, "A", Vector3.zero).Error, Is.EqualTo(EconomyError.Busy)); };
        service.Changed += change => { if (change.Reason == "chest.open") changes++; };
        ChestOpenResult result = service.TryOpenChest(stageId, "A", Vector3.zero);
        Assert.That(result.Success, Is.True); Assert.That(result.IsReplay, Is.False);
        Assert.That(store.SaveCount, Is.EqualTo(saves + 1)); Assert.That(effects, Is.EqualTo(1)); Assert.That(changes, Is.EqualTo(1));
        Assert.That(service.CountPiece(result.Reward.pieceId), Is.EqualTo(1)); Assert.That(service.Balance, Is.Zero);
        Assert.That(service.PendingBalance, Is.EqualTo(result.Reward.rolledCoins));
        Assert.That(service.GetChestState(stageId, "A").opened, Is.True); Assert.That(EconomyLedger.Validate(service.Snapshot()), Is.True);
        ChestOpenResult replay = service.TryOpenChest(stageId, "A", Vector3.one);
        Assert.That(replay.Success && replay.IsReplay, Is.True); Assert.That(store.SaveCount, Is.EqualTo(saves + 1)); Assert.That(effects, Is.EqualTo(1));
    }
    [TestCase(ChestRules.LegacyTableVersion)] [TestCase(ChestRules.TableVersion)]
    public void FailedOpenChangesNothingAndRetryDoesNotReroll(int tableVersion)
    {
        StartChestStage(tableVersion);
        string before = JsonUtility.ToJson(service.Snapshot()); int effects = 0;
        service.FlightRequested += _ => effects++;
        store.FailWrites = true;
        Assert.That(service.TryOpenChest(stageId, "A", Vector3.zero).Error, Is.EqualTo(EconomyError.SaveFailed));
        Assert.That(JsonUtility.ToJson(service.Snapshot()), Is.EqualTo(before)); Assert.That(effects, Is.Zero);
        service = service.Reopen(); Assert.That(service.Initialize(), Is.True); store.FailWrites = false;
        ChestRecord plan = service.GetChestState(stageId, "A");
        ChestOpenResult result = service.TryOpenChest(stageId, "A", Vector3.zero);
        Assert.That(result.Success, Is.True); Assert.That(result.Reward.pieceId, Is.EqualTo(plan.pieceId)); Assert.That(result.Reward.rolledCoins, Is.EqualTo(plan.rolledCoins));
    }
    [Test] public void PieceLimitAbortsWholeOpenWithoutCoinsOrAlternativePiece()
    {
        ChestRecord plan = service.GetChestState(stageId, "A"); service.TryGrantPiece(plan.pieceId, 9999, "fixture");
        Assert.That(service.TryOpenChest(stageId, "A", Vector3.zero).Error, Is.EqualTo(EconomyError.ItemLimit));
        Assert.That(service.PendingBalance, Is.Zero); Assert.That(service.GetChestState(stageId, "A").opened, Is.False);
        Assert.That(service.GetChestState(stageId, "A").pieceId, Is.EqualTo(plan.pieceId));
    }
    [TestCase(0)] [TestCase(3)]
    public void CoinCapIncludesReservationsButStillGrantsThePiece(int room)
    {
        service.TryGrant(EconomySettings.MaxCoins - room - 2, "fixture"); service.TryGrantWithEffect(2, Vector3.zero, "fixture");
        ChestOpenResult result = service.TryOpenChest(stageId, "A", Vector3.zero);
        Assert.That(result.Success, Is.True); Assert.That(result.Reward.appliedCoins, Is.EqualTo(room));
        Assert.That(service.CountPiece(result.Reward.pieceId), Is.EqualTo(1));
        Assert.That(service.Balance + service.PendingBalance, Is.EqualTo(EconomySettings.MaxCoins));
    }
    [Test] public void ReloadSettlesCoinsOnceAndKeepsOpenStateEvenAfterSpendingAndDeath()
    {
        ChestOpenResult result = service.TryOpenChest(stageId, "A", Vector3.zero);
        int coins = result.Reward.appliedCoins;
        service = service.Reopen(); Assert.That(service.Initialize(), Is.True);
        Assert.That(service.Balance, Is.EqualTo(coins)); Assert.That(service.PendingBalance, Is.Zero);
        service.TrySpend(coins, "fixture"); service.TryGrantPiece("result.speed", 3, "fixture");
        Assert.That(service.TryHandleDeath("death-fixture", null, null).Success, Is.True);
        int pieces = EconomyLedger.Total(service.Snapshot().pieces);
        Assert.That(service.TryOpenChest(stageId, "A", Vector3.zero).IsReplay, Is.True);
        Assert.That(EconomyLedger.Total(service.Snapshot().pieces), Is.EqualTo(pieces));
        Assert.That(service.TryOpenChest(stageId, "B", Vector3.zero).Success, Is.False);
        service.TrySettleToken(result.TransactionId, "0"); Assert.That(service.Balance, Is.Zero);
        Assert.That(EconomyLedger.Validate(service.Snapshot()), Is.True);
    }
    [Test] public void ReturnedSnapshotsCannotMutatePlansAndCorruptOpenReceiptIsRejected()
    {
        ChestRecord plan = service.GetChestState(stageId, "A"); plan.pieceId = "bad";
        Assert.That(service.GetChestState(stageId, "A").pieceId, Is.Not.EqualTo("bad"));
        service.TryOpenChest(stageId, "A", Vector3.zero);
        EconomyProfileData corrupt = service.Snapshot(); corrupt.run.stage.chestState.records[0].appliedCoins++;
        Assert.That(EconomyLedger.Validate(corrupt), Is.False);
        corrupt = service.Snapshot(); ChestRecord record = corrupt.run.stage.chestState.records[0];
        record.opened = false; record.openTransactionId = null; record.appliedCoins = 0;
        Assert.That(EconomyLedger.Validate(corrupt), Is.False, "An existing open receipt cannot be paired with a closed chest.");
    }
    [Test] public void JsonDiskRoundTripPreservesChestReceiptAndSettlesReservationsOnce()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ChestRoundTrip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); string path = Path.Combine(directory, "economy.json");
        try
        {
            var disk = new JsonEconomyStore(path); Assert.That(disk.Load().Status, Is.EqualTo(EconomyLoadStatus.Missing));
            Assert.That(disk.TrySave(service.Snapshot()), Is.True);
            var writer = new EconomyService(disk); Assert.That(writer.Initialize(), Is.True);
            ChestOpenResult opened = writer.TryOpenChest(stageId, "A", Vector3.zero); Assert.That(opened.Success, Is.True);
            var reader = new EconomyService(new JsonEconomyStore(path)); Assert.That(reader.Initialize(), Is.True);
            Assert.That(reader.GetChestState(stageId, "A").opened, Is.True);
            Assert.That(reader.CountPiece(opened.Reward.pieceId), Is.EqualTo(1));
            Assert.That(reader.Balance, Is.EqualTo(opened.Reward.appliedCoins)); Assert.That(reader.PendingBalance, Is.Zero);
            Assert.That(reader.TryOpenChest(stageId, "A", Vector3.zero).IsReplay, Is.True);
            var again = new EconomyService(new JsonEconomyStore(path)); Assert.That(again.Initialize(), Is.True);
            Assert.That(again.Balance, Is.EqualTo(reader.Balance));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test] public void LegacyChestJsonKeepsPlansAndReceiptsWhenNewStagesUseDoubledRewards()
    {
        StartChestStage(ChestRules.LegacyTableVersion);
        Assert.That(service.TryGrant(91, "legacy.fixture").Success, Is.True);
        ChestRecord closedPlan = service.GetChestState(stageId, "B");
        ChestOpenResult opened = service.TryOpenChest(stageId, "A", Vector3.zero);
        Assert.That(opened.Success, Is.True);
        EconomyProfileData legacy = service.Snapshot();
        legacy.processedTransactions.Single(r => r.id == opened.TransactionId).chestReward.rewardTableVersion = 0;
        // 배포 전 V1 JSON에는 영수증의 테이블 버전 필드 자체가 없었다.
        string json = JsonUtility.ToJson(legacy).Replace("\"rewardTableVersion\":0,", "");
        string directory = Path.Combine(Path.GetTempPath(), "ChestV1Compatibility-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); string path = Path.Combine(directory, "economy.json");
        try
        {
            File.WriteAllText(path, json);
            var reader = new EconomyService(new JsonEconomyStore(path));
            Assert.That(reader.Initialize(), Is.True); Assert.That(reader.LoadStatus, Is.EqualTo(EconomyLoadStatus.Loaded));
            Assert.That(reader.Balance, Is.EqualTo(91 + opened.Reward.appliedCoins));
            Assert.That(reader.PendingBalance, Is.Zero);
            Assert.That(JsonUtility.ToJson(reader.GetChestState(stageId, "B")), Is.EqualTo(JsonUtility.ToJson(closedPlan)));
            Assert.That(reader.TryOpenChest(stageId, "A", Vector3.one).IsReplay, Is.True);
            Assert.That(reader.TryOpenChest(stageId, "A", Vector3.zero).Reward.rolledCoins, Is.EqualTo(opened.Reward.rolledCoins));
            Assert.That(reader.CountPiece(opened.Reward.pieceId), Is.EqualTo(1));
            ChestOpenResult second = reader.TryOpenChest(stageId, "B", Vector3.zero);
            Assert.That(second.Reward.rolledCoins, Is.EqualTo(closedPlan.rolledCoins));
            Assert.That(second.Reward.rewardTableVersion, Is.EqualTo(ChestRules.LegacyTableVersion));

            Assert.That(reader.TryCompleteTemporaryStage(stageId).Success, Is.True);
            Assert.That(reader.TryReturnFromStage(stageId, 100).Success, Is.True);
            Assert.That(reader.TryDepartBranch(reader.RunSnapshot().candidates.First(c => c.role == BranchRole.Combat).id, 100).Success, Is.True);
            string nextStageId = reader.RunSnapshot().stage.id;
            Assert.That(reader.TryRegisterStageChests(new ChestManifest { stageInstanceId = nextStageId, spawnIds = new[] { "A" } }).Success, Is.True);
            ChestOpenResult current = reader.TryOpenChest(nextStageId, "A", Vector3.zero);
            Assert.That(current.Success, Is.True); Assert.That(current.Reward.rewardTableVersion, Is.EqualTo(ChestRules.TableVersion));
            Assert.That(current.Reward.rolledCoins, Is.EqualTo(2 * ChestRules.Plan(nextStageId, "A", ChestRules.LegacyTableVersion).rolledCoins));
            Assert.That(EconomyLedger.Validate(reader.Snapshot()), Is.True);

            reader = new EconomyService(new JsonEconomyStore(path)); Assert.That(reader.Initialize(), Is.True);
            int balance = reader.Balance;
            Assert.That(reader.TryOpenChest(stageId, "A", Vector3.zero).IsReplay, Is.True);
            Assert.That(reader.TryOpenChest(nextStageId, "A", Vector3.zero).IsReplay, Is.True);
            reader.TrySettleToken(opened.TransactionId, "0"); reader.TrySettleToken(current.TransactionId, "0");
            Assert.That(reader.Balance, Is.EqualTo(balance)); Assert.That(reader.PendingBalance, Is.Zero);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test] public void CurrentChestValidationRejectsOddCoinsAndMismatchedReceiptVersions()
    {
        Assert.That(service.TryOpenChest(stageId, "A", Vector3.zero).Success, Is.True);
        EconomyProfileData corrupt = service.Snapshot();
        corrupt.run.stage.chestState.records.Single(r => r.spawnId == "B").rolledCoins = 15;
        Assert.That(EconomyLedger.Validate(corrupt), Is.False);
        corrupt = service.Snapshot();
        corrupt.processedTransactions.Single(r => r.id == ChestRules.OpenId(stageId, "A")).chestReward.rewardTableVersion = ChestRules.LegacyTableVersion;
        Assert.That(EconomyLedger.Validate(corrupt), Is.False);
    }

    [TestCase(1)] [TestCase(2)]
    public void LegacyJsonMigratesWithoutResettingAssetsOrRun(int version)
    {
        string directory = Path.Combine(Path.GetTempPath(), "ChestMigration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); string path = Path.Combine(directory, "economy.json");
        try
        {
            service.TryGrant(91, "fixture"); service.TryGrantPiece("result.speed", 4, "fixture");
            service.TryGrantWithEffect(7, Vector3.zero, "fixture");
            EconomyProfileData legacy = service.Snapshot(); legacy.schemaVersion = version;
            legacy.run.stage.hasChestState = false; legacy.run.stage.chestState = null;
            legacy.processedTransactions.RemoveAll(r => r.id.StartsWith("chest."));
            if (version == 1) { legacy.hasRun = false; legacy.run = null; legacy.processedTransactions.RemoveAll(r => r.id.StartsWith("run:") || r.id.StartsWith("depart:")); }
            File.WriteAllText(path, JsonUtility.ToJson(legacy));
            var reopened = new EconomyService(new JsonEconomyStore(path)); Assert.That(reopened.Initialize(), Is.True);
            Assert.That(reopened.Balance, Is.EqualTo(98)); Assert.That(reopened.CountPiece("result.speed"), Is.EqualTo(4));
            Assert.That(reopened.Snapshot().schemaVersion, Is.EqualTo(EconomyProfileData.CurrentSchemaVersion));
            if (version == 2) Assert.That(reopened.RunSnapshot().stage.id, Is.EqualTo(stageId));
            else Assert.That(reopened.RunSnapshot(), Is.Null);
            Assert.That(EconomyLedger.Validate(reopened.Snapshot()), Is.True);
        }
        finally { Directory.Delete(directory, true); }
    }
    [TestCase(0, 0, 20)] [TestCase(.125f, 20, 20)] [TestCase(.25f, 20, 0)]
    [TestCase(.375f, 20, -20)] [TestCase(.5f, 0, -20)] [TestCase(.625f, -20, -20)]
    [TestCase(.75f, -20, 0)] [TestCase(.875f, -20, 20)] [TestCase(1, 0, 20)]
    public void SquareProgressFollowsClockwisePerimeter(float progress, float x, float y) =>
        Assert.That(Vector2.Distance(SquareHoldProgressGraphic.PointAt(progress, 40), new Vector2(x, y)), Is.LessThan(.001f));
}
