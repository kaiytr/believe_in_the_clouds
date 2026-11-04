using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class DeathResultEditModeTests
{
    private MemoryEconomyStore store;
    private EconomyService service;
    [SetUp] public void SetUp()
    {
        store = new MemoryEconomyStore(); service = new EconomyService(store, new EconomyRandom(17));
        Assert.That(service.Initialize(), Is.True);
        Assert.That(service.TryInitializeRun(100).Success, Is.True);
    }
    private DeathResultData Result() => new DeathResultData
    {
        cause = "개 · 돌진", occurredAtUtc = DateTime.UtcNow.ToString("O"), sceneName = "DogCombatTest",
        statistics = service.StatisticsSnapshot(), screenshotPngBase64 = "cG5n"
    };

    [Test] public void EarnedCoinsIgnoreSpendingFlightArrivalAndDuplicateReceipts()
    {
        service.TryGrant(100, "fixture", "a");
        service.TrySpend(30, "fixture");
        service.TryGrantWithEffect(20, Vector3.zero, "fixture", "b");
        service.TryGrantWithEffect(20, Vector3.zero, "fixture", "b");
        service.SettleAllPending();
        Assert.That(service.Balance, Is.EqualTo(90));
        Assert.That(service.StatisticsSnapshot().coins, Is.EqualTo(120));
        var restored = service.Reopen(); Assert.That(restored.Initialize(), Is.True);
        Assert.That(restored.StatisticsSnapshot().coins, Is.EqualTo(120));
    }
    [Test] public void KeywordsIncludeDuplicateOwnedPiecesAndPurchasesExactlyOnce()
    {
        service.TryGrant(1000, "fixture");
        service.TryGrantPiece("subject.player", 3, "fixture", "piece");
        service.TryGrantPiece("subject.player", 3, "fixture", "piece");
        var request = new PurchaseRequest(new[] { "subject.player" }, "purchase");
        Assert.That(service.TryPurchase(request).Success, Is.True);
        service.TryPurchase(request);
        Assert.That(service.StatisticsSnapshot().keywords, Is.EqualTo(4));
    }
    [Test] public void ActualKillsSurviveRewardSaveFailureAndAreFrozenAtDeath()
    {
        store.FailWrites = true;
        service.ObserveEnemyDefeat(EnemyRewardKind.Dog, "dog-1");
        service.ObserveEnemyDefeat(EnemyRewardKind.Dog, "dog-1");
        Assert.That(service.TryRewardDefeat(EnemyRewardKind.Dog, "dog-1", Vector3.zero).Error, Is.EqualTo(EconomyError.SaveFailed));
        store.FailWrites = false;
        service.BlockDefeatRewards();
        var result = Result();
        Assert.That(service.CaptureDeferredDeath("death", null, null, result).Success, Is.True);
        service.ObserveEnemyDefeat(EnemyRewardKind.Executive, "late");
        Assert.That(service.Snapshot().death.result.statistics.Count(EnemyRewardKind.Dog), Is.EqualTo(1));
        Assert.That(service.Snapshot().death.result.statistics.Count(EnemyRewardKind.Executive), Is.Zero);
    }
    [Test] public void DeferredDeathRetriesNeverApplyPenaltyBeforeChoiceAndEffectsOnlyRunOnce()
    {
        service.TryGrant(100, "fixture"); service.TryGrantPiece("subject.player", 13, "fixture");
        int effects = 0; var result = Result();
        store.FailWrites = true;
        Assert.That(service.CaptureDeferredDeath("death", null, () => effects++, result).Error, Is.EqualTo(EconomyError.SaveFailed));
        Assert.That(effects, Is.Zero);
        store.FailWrites = false;
        for (int i = 0; i < 3; i++) Assert.That(service.RetryDeath().Success, Is.True);
        Assert.That(effects, Is.EqualTo(1)); Assert.That(service.Balance, Is.EqualTo(100));
        Assert.That(service.CountPiece("subject.player"), Is.EqualTo(13));
        Assert.That(service.RunSnapshot().area, Is.EqualTo(RunArea.Dying));
        Assert.That(service.CompleteDeferredDeath("death").Success, Is.True);
        Assert.That(service.Balance, Is.EqualTo(70)); Assert.That(service.CountPiece("subject.player"), Is.EqualTo(9));
        service.CompleteDeferredDeath("death");
        Assert.That(service.Balance, Is.EqualTo(70)); Assert.That(effects, Is.EqualTo(1));
        Assert.That(service.RunSnapshot().area, Is.EqualTo(RunArea.Lobby));
        Assert.That(service.StatisticsSnapshot().coins, Is.Zero);
        Assert.That(service.Snapshot().death.result.statistics.coins, Is.EqualTo(100));
    }
    [Test] public void FailedChoiceCanRetryWithoutLosingScreenshotOrRerollingPenalty()
    {
        service.TryGrant(100, "fixture");
        service.CaptureDeferredDeath("death", null, null, Result());
        store.FailWrites = true;
        Assert.That(service.CompleteDeferredDeath("death").Error, Is.EqualTo(EconomyError.SaveFailed));
        Assert.That(service.Balance, Is.EqualTo(100));
        store.FailWrites = false;
        Assert.That(service.CompleteDeferredDeath("death").Success, Is.True);
        var saved = store.Load().Data;
        Assert.That(saved.balance, Is.EqualTo(70));
        Assert.That(saved.death.result.screenshotPngBase64, Is.EqualTo("cG5n"));
        Assert.That(saved.death.result.cause, Is.EqualTo("개 · 돌진"));
    }
    [Test] public void RestartFromUnchosenDeathCompletesJournalWithoutRestoringPreDeathAssets()
    {
        service.TryGrant(100, "fixture");
        service.CaptureDeferredDeath("death", null, null, Result());
        var reopened = service.Reopen(); Assert.That(reopened.Initialize(), Is.True);
        Assert.That(reopened.Balance, Is.EqualTo(70));
        Assert.That(reopened.Snapshot().death.phase, Is.EqualTo(EconomyDeathPhase.Committed));
        Assert.That(reopened.Snapshot().death.result.statistics.coins, Is.EqualTo(100));
        Assert.That(reopened.RunSnapshot().area, Is.EqualTo(RunArea.Lobby));
    }
    [Test] public void VersionThreeDiskSaveMigratesWithoutResettingExistingAssets()
    {
        string directory = Path.Combine(Path.GetTempPath(), "death-migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            service.TryGrant(125, "fixture");
            EconomyProfileData old = service.Snapshot(); old.schemaVersion = 3;
            string json = JsonUtility.ToJson(old).Replace("\"statistics\":" + JsonUtility.ToJson(old.statistics) + ",", "");
            string path = Path.Combine(directory, "economy.json"); File.WriteAllText(path, json);
            var disk = new EconomyService(new JsonEconomyStore(path));
            Assert.That(disk.Initialize(), Is.True); Assert.That(disk.Balance, Is.EqualTo(125));
            Assert.That(disk.StatisticsSnapshot().coins, Is.Zero);
            Assert.That(disk.TryGrant(5, "new").Success, Is.True);
            Assert.That(new JsonEconomyStore(path).Load().Data.schemaVersion, Is.EqualTo(4));
        }
        finally { Directory.Delete(directory, true); }
    }
}
