using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class LobbyEditModeTests
{
    private MemoryEconomyStore store;
    private EconomyService service;

    [SetUp] public void SetUp()
    {
        store = new MemoryEconomyStore(); service = new EconomyService(store, new EconomyRandom(43));
        Assert.That(service.Initialize(), Is.True);
        Assert.That(service.TryInitializeRun(100).Success, Is.True);
    }

    [TestCase(0)] [TestCase(2)] [TestCase(3)] [TestCase(50)]
    public void GenerationGuaranteesCombatLimitsSpecialsAndShufflesWithoutLeakingRoles(int clears)
    {
        int twos = 0, bosses = 0; var combatSlots = new int[3];
        for (int seed = 0; seed < 4000; seed++)
        {
            var run = LobbyRules.NewRun("fixture", 100, seed); run.combatClears = clears;
            LobbyRules.GenerateCandidates(run, seed);
            Assert.That(LobbyRules.Validate(run, false), Is.True);
            Assert.That(run.candidates.All(c => c.role == BranchRole.Combat || c.role == BranchRole.Shop || c.role == BranchRole.Boss), Is.True);
            Assert.That(run.candidates.All(c => !c.revealed), Is.True);
            if (run.candidates.Count == 2) twos++;
            if (run.candidates.Any(c => c.role == BranchRole.Boss)) bosses++;
            combatSlots[run.candidates.First(c => c.id.EndsWith(":0")).slot]++;
        }
        Assert.That(twos, Is.InRange(1800, 2200));
        Assert.That(combatSlots.All(count => count > 500), Is.True);
        if (clears < 3) Assert.That(bosses, Is.Zero);
        else Assert.That(bosses, Is.GreaterThan(800));
    }

    [Test]
    public void NewRunDoesNotGrantAssetsAndInitializeReloadNeverRerolls()
    {
        Assert.That(service.Balance, Is.Zero); Assert.That(service.Snapshot().pieces, Is.Empty);
        string expected = JsonUtility.ToJson(service.RunSnapshot());
        service.TryGrant(23, "fixture"); service.TryInitializeRun(200);
        var reopened = service.Reopen(); Assert.That(reopened.Initialize(), Is.True);
        Assert.That(JsonUtility.ToJson(reopened.RunSnapshot()), Is.EqualTo(expected));
        Assert.That(reopened.Balance, Is.EqualTo(23));
        var external = reopened.RunSnapshot(); external.candidates.Clear();
        Assert.That(reopened.RunSnapshot().candidates.Count, Is.GreaterThanOrEqualTo(2));
    }

    [TestCase(0)] [TestCase(3)]
    public void PlayLoopFixtureOffersOnlyCombatShopAndUnlockedBoss(int clears)
    {
        var data = service.Snapshot(); data.run.combatClears = clears;
        Assert.That(store.TrySave(data), Is.True);
        service = service.Reopen(); Assert.That(service.Initialize(), Is.True);
        Assert.That(service.TryConfigurePlayLoopTestCandidates().Success, Is.True);
        var roles = service.RunSnapshot().candidates.Select(candidate => candidate.role).ToArray();
        CollectionAssert.AreEqual(clears >= 3
            ? new[] { BranchRole.Combat, BranchRole.Shop, BranchRole.Boss }
            : new[] { BranchRole.Combat, BranchRole.Shop }, roles);
    }

    [Test]
    public void LegacyEventCandidatePreservesAssetsIdentityAndPaidRevealAsCombat()
    {
        service.TryGrant(91, "fixture"); service.TryGrantPiece("result.speed", 4, "fixture");
        var data = service.Snapshot();
        var candidate = data.run.candidates[0];
        candidate.role = (BranchRole)2; candidate.revealed = true;
        string id = candidate.id, batch = data.run.batchId;
        WithSavedProfile(data, migrated =>
        {
            var run = migrated.RunSnapshot();
            Assert.That(run.batchId, Is.EqualTo(batch));
            Assert.That(run.candidates[0].id, Is.EqualTo(id));
            Assert.That(run.candidates[0].role, Is.EqualTo(BranchRole.Combat));
            Assert.That(run.candidates[0].revealed, Is.True);
            Assert.That(migrated.Balance, Is.EqualTo(91));
            Assert.That(migrated.CountPiece("result.speed"), Is.EqualTo(4));
            Assert.That(migrated.TryRevealBranch(id).Success, Is.True);
            Assert.That(migrated.Balance, Is.EqualTo(91), "A previously paid reveal must remain free.");
        });
    }

    [TestCase(false)] [TestCase(true)]
    public void LegacyEventStageReturnsToLobbyWithoutClearAndNewDepartureDoesNotReplayOldReceipt(bool completed)
    {
        service.TryGrant(91, "fixture");
        var chosen = service.RunSnapshot().candidates.First(candidate => candidate.role == BranchRole.Combat);
        Assert.That(service.TryDepartBranch(chosen.id, 71).Success, Is.True);
        if (completed) Assert.That(service.TryCompleteTemporaryStage(service.RunSnapshot().stage.id).Success, Is.True);
        var data = service.Snapshot();
        data.run.candidates.First(candidate => candidate.id == chosen.id).role = (BranchRole)2;
        data.run.stage.role = (BranchRole)2;
        data.run.hasPosition = true; data.run.playerX = 8; data.run.playerY = -2;
        string oldStage = data.run.stage.id, oldBatch = data.run.batchId;
        WithSavedProfile(data, migrated =>
        {
            var run = migrated.RunSnapshot();
            Assert.That(run.area, Is.EqualTo(RunArea.Lobby));
            Assert.That(run.hasStage, Is.False); Assert.That(run.stage, Is.Null);
            Assert.That(run.hasPosition, Is.False);
            Assert.That(run.hp, Is.EqualTo(71)); Assert.That(run.combatClears, Is.Zero);
            Assert.That(run.batchId, Is.Not.EqualTo(oldBatch));
            Assert.That(migrated.Balance, Is.EqualTo(91));
            var combat = run.candidates.First(candidate => candidate.role == BranchRole.Combat);
            Assert.That(migrated.TryDepartBranch(combat.id, run.hp).Success, Is.True);
            Assert.That(migrated.RunSnapshot().area, Is.EqualTo(RunArea.TemporaryStage));
            string stage = migrated.RunSnapshot().stage.id;
            Assert.That(stage, Is.Not.EqualTo(oldStage));
            Assert.That(migrated.TryCompleteTemporaryStage(stage).Success, Is.True);
            Assert.That(migrated.TryReturnFromStage(stage, run.hp).Success, Is.True);
            Assert.That(migrated.RunSnapshot().combatClears, Is.EqualTo(1));
            var reopened = migrated.Reopen(); Assert.That(reopened.Initialize(), Is.True);
            Assert.That(JsonUtility.ToJson(reopened.RunSnapshot()), Is.EqualTo(JsonUtility.ToJson(migrated.RunSnapshot())));
        });
    }

    [Test]
    public void ExistingBossStageKeepsItsSerializedRoleAndProgress()
    {
        SeedRole(BranchRole.Boss, 3);
        var boss = service.RunSnapshot().candidates.First(candidate => candidate.role == BranchRole.Boss);
        Assert.That(service.TryDepartBranch(boss.id, 82).Success, Is.True);
        var data = service.Snapshot();
        WithSavedProfile(data, migrated =>
        {
            var run = migrated.RunSnapshot();
            Assert.That((int)run.stage.role, Is.EqualTo(3));
            Assert.That(run.stage.role, Is.EqualTo(BranchRole.Boss));
            Assert.That(run.stage.id, Is.EqualTo(data.run.stage.id));
            Assert.That(run.area, Is.EqualTo(RunArea.TemporaryStage));
            Assert.That(run.combatClears, Is.EqualTo(3)); Assert.That(run.hp, Is.EqualTo(82));
        });
    }

    [Test]
    public void LegacyEventDeathRemainsDyingUntilTheDeathJournalCompletes()
    {
        var run = service.RunSnapshot();
        run.candidates[0].role = (BranchRole)2;
        run.hasStage = true;
        run.stage = new TemporaryStageData { id = "legacy-event", branchId = run.candidates[0].id, role = (BranchRole)2 };
        run.area = RunArea.Dying; run.hp = 0;
        run.Normalize();
        Assert.That(run.area, Is.EqualTo(RunArea.Dying)); Assert.That(run.hp, Is.Zero);
        Assert.That(run.hasStage, Is.False);
        Assert.That(LobbyRules.Validate(run, true), Is.True);
    }

    private static void WithSavedProfile(EconomyProfileData data, Action<EconomyService> verify)
    {
        string path = Path.Combine(Path.GetTempPath(), "clouds-event-removal-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, JsonUtility.ToJson(data));
            var migrated = new EconomyService(new JsonEconomyStore(path));
            Assert.That(migrated.Initialize(), Is.True);
            verify(migrated);
        }
        finally
        {
            foreach (string suffix in new[] { "", ".bak", ".tmp", ".recover" }) File.Delete(path + suffix);
        }
    }

    [TestCase(0)] [TestCase(4)] [TestCase(5)] [TestCase(100)]
    public void RevealCostsExactlyFiveOnlyForTheChosenCandidateAndNeverChargesAgain(int coins)
    {
        if (coins > 0) service.TryGrant(coins, "fixture");
        string id = service.RunSnapshot().candidates[0].id;
        var result = service.TryRevealBranch(id);
        Assert.That(result.Success, Is.EqualTo(coins >= 5));
        Assert.That(service.Balance, Is.EqualTo(coins >= 5 ? coins - 5 : coins));
        Assert.That(service.RunSnapshot().candidates.Count(c => c.revealed), Is.EqualTo(coins >= 5 ? 1 : 0));
        if (coins >= 5)
        {
            var reopened = service.Reopen(); reopened.Initialize(); reopened.TryRevealBranch(id);
            Assert.That(reopened.Balance, Is.EqualTo(coins - 5));
        }
    }

    [Test]
    public void FailedRevealIsAtomicAndRetryUsesTheSameCandidates()
    {
        service.TryGrant(5, "fixture"); string before = JsonUtility.ToJson(service.RunSnapshot());
        string id = service.RunSnapshot().candidates[0].id; store.FailWrites = true;
        Assert.That(service.TryRevealBranch(id).Error, Is.EqualTo(EconomyError.SaveFailed));
        Assert.That(service.Balance, Is.EqualTo(5)); Assert.That(JsonUtility.ToJson(service.RunSnapshot()), Is.EqualTo(before));
        store.FailWrites = false; Assert.That(service.TryRevealBranch(id).Success, Is.True);
        Assert.That(service.Balance, Is.Zero);
    }

    [Test]
    public void UnrevealedDepartureIsFreeAndUnfinishedStageCannotReturn()
    {
        var chosen = service.RunSnapshot().candidates[0];
        Assert.That(service.TryDepartBranch(chosen.id, 71).Success, Is.True);
        var run = service.RunSnapshot();
        Assert.That(run.area, Is.EqualTo(RunArea.TemporaryStage)); Assert.That(run.hp, Is.EqualTo(71));
        Assert.That(run.stage.role, Is.EqualTo(chosen.role)); Assert.That(service.Balance, Is.Zero);
        Assert.That(service.TryReturnFromStage(run.stage.id, 71).Error, Is.EqualTo(EconomyError.StateConflict));
        var reopened = service.Reopen(); reopened.Initialize();
        Assert.That(JsonUtility.ToJson(reopened.RunSnapshot()), Is.EqualTo(JsonUtility.ToJson(run)));
    }

    [TestCase(BranchRole.Combat)] [TestCase(BranchRole.Shop)]
    public void OnlyCombatReturnCountsAndRepeatedCompletionOrReturnCannotApplyTwice(BranchRole role)
    {
        SeedRole(role);
        var before = service.RunSnapshot(); var chosen = before.candidates.First(c => c.role == role);
        Assert.That(service.TryDepartBranch(chosen.id, 62).Success, Is.True);
        string stage = service.RunSnapshot().stage.id;
        Assert.That(service.TryCompleteTemporaryStage(stage).Success, Is.True);
        var reopened = service.Reopen(); reopened.Initialize(); service = reopened;
        Assert.That(service.RunSnapshot().stage.completed, Is.True);
        Assert.That(service.TryReturnFromStage(stage, 62).Success, Is.True);
        string after = JsonUtility.ToJson(service.RunSnapshot());
        service.TryCompleteTemporaryStage(stage); service.TryReturnFromStage(stage, 100);
        Assert.That(JsonUtility.ToJson(service.RunSnapshot()), Is.EqualTo(after));
        Assert.That(service.RunSnapshot().combatClears, Is.EqualTo(role == BranchRole.Combat ? 1 : 0));
        Assert.That(service.RunSnapshot().hp, Is.EqualTo(62));
        Assert.That(service.RunSnapshot().batchId, Is.Not.EqualTo(before.batchId));
        Assert.That(service.RunSnapshot().lifeId, Is.EqualTo(before.lifeId));
    }

    [Test]
    public void FailedDepartureDoesNotFlushPendingOrMoveAndSuccessfulRetrySettlesOnlyOnce()
    {
        service.TryGrantWithEffect(17, Vector3.zero, "fixture", "before-departure");
        string branch = service.RunSnapshot().candidates[0].id;
        store.FailWrites = true;
        Assert.That(service.TryDepartBranch(branch, 91).Error, Is.EqualTo(EconomyError.SaveFailed));
        Assert.That(service.RunSnapshot().area, Is.EqualTo(RunArea.Lobby));
        Assert.That(service.Balance, Is.Zero); Assert.That(service.PendingBalance, Is.EqualTo(17));
        store.FailWrites = false;
        Assert.That(service.TryDepartBranch(branch, 91).Success, Is.True);
        Assert.That(service.Balance, Is.EqualTo(17)); Assert.That(service.PendingBalance, Is.Zero);
        service.SettleAllPending(); service.TryDepartBranch(branch, 91);
        Assert.That(service.Balance, Is.EqualTo(17));
    }

    [Test]
    public void FailedCompletionLeavesExitLockedUntilTheCompletionIsSaved()
    {
        EnterCombat(); string stage = service.RunSnapshot().stage.id;
        store.FailWrites = true;
        Assert.That(service.TryCompleteTemporaryStage(stage).Error, Is.EqualTo(EconomyError.SaveFailed));
        Assert.That(service.RunSnapshot().stage.completed, Is.False);
        Assert.That(service.TryReturnFromStage(stage, 100).Error, Is.EqualTo(EconomyError.StateConflict));
        store.FailWrites = false;
        Assert.That(service.TryCompleteTemporaryStage(stage).Success, Is.True);
        Assert.That(service.TryReturnFromStage(stage, 100).Success, Is.True);
        Assert.That(service.RunSnapshot().combatClears, Is.EqualTo(1));
    }

    [Test]
    public void FailedReturnRetainsStageAndExactNextDrawEvenAcrossRestart()
    {
        EnterCombat(); string stage = service.RunSnapshot().stage.id;
        service.TryCompleteTemporaryStage(stage); string before = JsonUtility.ToJson(service.RunSnapshot());
        var independent = service.Reopen(); independent.Initialize();
        store.FailWrites = true;
        Assert.That(service.TryReturnFromStage(stage, 80).Error, Is.EqualTo(EconomyError.SaveFailed));
        Assert.That(JsonUtility.ToJson(service.RunSnapshot()), Is.EqualTo(before));
        store.FailWrites = false;
        Assert.That(independent.TryReturnFromStage(stage, 80).Success, Is.True);
        string expected = JsonUtility.ToJson(independent.RunSnapshot());
        Assert.That(service.TryReturnFromStage(stage, 80).Success, Is.True);
        Assert.That(JsonUtility.ToJson(service.RunSnapshot()), Is.EqualTo(expected));
    }

    [Test]
    public void ThirdCombatUnlocksBossWithoutCountingEntryOrCompletionAlone()
    {
        for (int i = 0; i < 3; i++)
        {
            EnterCombat(); string stage = service.RunSnapshot().stage.id;
            service.TryCompleteTemporaryStage(stage);
            Assert.That(service.RunSnapshot().combatClears, Is.EqualTo(i));
            service.TryReturnFromStage(stage, 100);
            Assert.That(service.RunSnapshot().combatClears, Is.EqualTo(i + 1));
            if (i < 2) Assert.That(service.RunSnapshot().candidates.Any(c => c.role == BranchRole.Boss), Is.False);
        }
    }

    [Test]
    public void CheckpointFailurePublishesNothingAndCheckpointsDoNotGrowReceipts()
    {
        int receipts = service.Snapshot().processedTransactions.Count;
        store.FailWrites = true;
        Assert.That(service.TryCheckpointRun(63, new Vector2(12, -2)).Error, Is.EqualTo(EconomyError.SaveFailed));
        Assert.That(service.RunSnapshot().hp, Is.EqualTo(100));
        store.FailWrites = false;
        Assert.That(service.TryCheckpointRun(63, new Vector2(12, -2)).Success, Is.True);
        var reopened = service.Reopen(); reopened.Initialize();
        Assert.That(reopened.RunSnapshot().hp, Is.EqualTo(63)); Assert.That(reopened.RunSnapshot().playerX, Is.EqualTo(12));
        Assert.That(service.Snapshot().processedTransactions.Count, Is.EqualTo(receipts));
        Assert.That(service.TryCheckpointRun(0, Vector2.zero).Success, Is.False);
        Assert.That(service.TryCheckpointRun(float.NaN, Vector2.zero).Success, Is.False);
        Assert.That(service.TryCheckpointRun(50, new Vector2(float.PositiveInfinity, 0)).Success, Is.False);
    }

    private sealed class FailingStore : IEconomyStore
    {
        public readonly MemoryEconomyStore Memory = new MemoryEconomyStore();
        public int Writes, FailAt;
        public EconomyLoadResult Load() => Memory.Load();
        public bool TrySave(EconomyProfileData data) => ++Writes != FailAt && Memory.TrySave(data);
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void DeathLossAndFreshRunAreOneRecoverableTransaction(int failWrite)
    {
        var disk = new FailingStore(); service = new EconomyService(disk, new EconomyRandom(18)); service.Initialize(); service.TryInitializeRun(100);
        service.TryGrant(100, "fixture"); service.TryGrantPiece("subject.player", 13, "fixture");
        var old = service.RunSnapshot(); EnterCombat();
        disk.FailAt = disk.Writes + failWrite;
        int effects = 0;
        Assert.That(service.TryHandleDeath("life:" + old.lifeId + "/death", null, () => effects++).Error, Is.EqualTo(EconomyError.SaveFailed));
        Assert.That(service.Balance, Is.EqualTo(100)); Assert.That(service.CountPiece("subject.player"), Is.EqualTo(13));
        Assert.That(service.TryReturnFromStage(service.RunSnapshot().stage.id, 50).Success, Is.False);
        if (failWrite > 1)
        {
            var reopened = service.Reopen(); Assert.That(reopened.Initialize(), Is.True);
            Assert.That(service.RetryDeath().Success, Is.True);
            Assert.That(JsonUtility.ToJson(reopened.RunSnapshot()), Is.EqualTo(JsonUtility.ToJson(service.RunSnapshot())));
        }
        else Assert.That(service.RetryDeath().Success, Is.True);
        Assert.That(effects, Is.EqualTo(1)); Assert.That(service.Balance, Is.EqualTo(70));
        Assert.That(service.CountPiece("subject.player"), Is.EqualTo(9));
        var next = service.RunSnapshot();
        Assert.That(next.id, Is.Not.EqualTo(old.id)); Assert.That(next.combatClears, Is.Zero);
        Assert.That(next.area, Is.EqualTo(RunArea.Lobby)); Assert.That(next.hp, Is.EqualTo(100));
        Assert.That(next.candidates.Any(c => c.role == BranchRole.Boss), Is.False);
        service.TryHandleDeath("life:" + old.lifeId + "/death", null, () => effects++);
        Assert.That(effects, Is.EqualTo(1)); Assert.That(service.Balance, Is.EqualTo(70));
    }

    [Test]
    public void BossUsesTheSameRevealPriceAndCompletionStopsAtThePlaceholder()
    {
        SeedRole(BranchRole.Boss, 3); service.TryGrant(5, "fixture");
        var boss = service.RunSnapshot().candidates.First(c => c.role == BranchRole.Boss);
        Assert.That(boss.revealed, Is.False); Assert.That(service.TryRevealBranch(boss.id).Success, Is.True);
        Assert.That(service.Balance, Is.Zero); service.TryDepartBranch(boss.id, 77);
        string stage = service.RunSnapshot().stage.id;
        Assert.That(service.TryCompleteTemporaryStage(stage).Success, Is.True);
        Assert.That(service.RunSnapshot().area, Is.EqualTo(RunArea.Complete));
        Assert.That(service.TryReturnFromStage(stage, 77).Success, Is.False);
        var reopened = service.Reopen(); Assert.That(reopened.Initialize(), Is.True);
        Assert.That(reopened.RunSnapshot().area, Is.EqualTo(RunArea.Complete));
    }

    [Test]
    public void SchemaOneMigratesWithoutResettingAssetsAndSchemaTwoRestoresTheWholeRun()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LobbyTest-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "economy.json"); Directory.CreateDirectory(directory);
        try
        {
            service.TryGrant(91, "fixture"); service.TryGrantPiece("result.speed", 4, "fixture");
            var legacy = service.Snapshot(); legacy.schemaVersion = 1; legacy.hasRun = false; legacy.run = null;
            legacy.processedTransactions.RemoveAll(receipt => receipt.id.StartsWith("run:init:")); // v1 never had a run-init transaction.
            File.WriteAllText(path, JsonUtility.ToJson(legacy));
            var migrated = new EconomyService(new JsonEconomyStore(path)); Assert.That(migrated.Initialize(), Is.True);
            Assert.That(migrated.Balance, Is.EqualTo(91)); Assert.That(migrated.CountPiece("result.speed"), Is.EqualTo(4));
            Assert.That(migrated.RunSnapshot(), Is.Null); Assert.That(migrated.TryInitializeRun(100).Success, Is.True);
            migrated.TryRevealBranch(migrated.RunSnapshot().candidates[0].id);
            var reopened = new EconomyService(new JsonEconomyStore(path)); Assert.That(reopened.Initialize(), Is.True);
            Assert.That(JsonUtility.ToJson(reopened.RunSnapshot()), Is.EqualTo(JsonUtility.ToJson(migrated.RunSnapshot())));
            Assert.That(reopened.Balance, Is.EqualTo(86));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public void InvalidRunCandidatesAndStageDiscriminatorsAreRejected()
    {
        var invalid = service.Snapshot(); invalid.run.candidates.Clear();
        Assert.That(EconomyLedger.Validate(invalid), Is.False);
        invalid = service.Snapshot(); invalid.run.candidates[0].role = BranchRole.Boss;
        Assert.That(EconomyLedger.Validate(invalid), Is.False);
        invalid = service.Snapshot(); invalid.run.hasStage = true;
        Assert.That(EconomyLedger.Validate(invalid), Is.False);
    }

    private void EnterCombat() => Assert.That(service.TryDepartBranch(service.RunSnapshot().candidates.First(c => c.role == BranchRole.Combat).id, 100).Success, Is.True);
    private void SeedRole(BranchRole role, int clears = 0)
    {
        var data = service.Snapshot(); data.run.combatClears = clears;
        for (int seed = 0; seed < 1000; seed++)
        {
            LobbyRules.GenerateCandidates(data.run, seed);
            if (data.run.candidates.Any(c => c.role == role)) break;
        }
        Assert.That(store.TrySave(data), Is.True); service = service.Reopen(); Assert.That(service.Initialize(), Is.True);
    }
}
