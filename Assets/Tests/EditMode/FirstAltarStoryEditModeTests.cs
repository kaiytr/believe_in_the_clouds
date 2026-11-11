using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public sealed class FirstAltarStoryEditModeTests
{
    private string directory;
    [SetUp] public void SetUp()
    {
        directory = Path.GetFullPath(Path.Combine("Temp/FirstAltarTests", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
    }
    [TearDown] public void TearDown()
    {
        string allowed = Path.GetFullPath("Temp/FirstAltarTests") + Path.DirectorySeparatorChar;
        Assert.That(directory.StartsWith(allowed, StringComparison.OrdinalIgnoreCase), Is.True);
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    private static EconomyService NewService(out MemoryEconomyStore store, bool tutorial = true)
    {
        store = new MemoryEconomyStore();
        var service = new EconomyService(store, new EconomyRandom(31));
        Assert.That(service.Initialize(), Is.True);
        Assert.That(service.TryInitializeRun(100).Success, Is.True);
        Assert.That(service.TryGrant(100, "story.test").Success, Is.True);
        if (tutorial) Assert.That(service.TryMarkTutorialCompleted().Success, Is.True);
        return service;
    }
    private static string Complete(EconomyService service, BranchRole role = BranchRole.Combat)
    {
        LobbyRunData run = service.RunSnapshot();
        BranchCandidate candidate = run.candidates.FirstOrDefault(item => item.role == role);
        if (candidate == null)
        {
            Assert.That(service.TryConfigurePlayLoopTestCandidates().Success, Is.True);
            candidate = service.RunSnapshot().candidates.First(item => item.role == role);
        }
        Assert.That(service.TryDepartBranch(candidate.id, run.hp).Success, Is.True);
        string stage = service.RunSnapshot().stage.id;
        Assert.That(service.TryCompleteTemporaryStage(stage).Success, Is.True);
        return stage;
    }
    private static string Enter(EconomyService service)
    {
        string stage = Complete(service);
        Assert.That(service.TryReturnFromStage(stage, 73).Success, Is.True);
        Assert.That(service.RunSnapshot().area, Is.EqualTo(RunArea.StoryRoom));
        return service.FirstAltarSnapshot().instanceId;
    }

    [Test] public void NewProfileRequiresTutorialAndShopDoesNotConsumeStory()
    {
        EconomyService service = NewService(out _, false);
        string stage = Complete(service);
        Assert.That(service.TryReturnFromStage(stage, 90).Success, Is.True);
        Assert.That(service.RunSnapshot().area, Is.EqualTo(RunArea.Lobby));
        Assert.That(service.TryMarkTutorialCompleted().Success, Is.True);
        stage = Complete(service, BranchRole.Shop);
        Assert.That(service.TryReturnFromStage(stage, 90).Success, Is.True);
        Assert.That(service.FirstAltarSnapshot().phase, Is.EqualTo(FirstAltarPhase.NotStarted));
        Enter(service);
        Assert.That(service.RunSnapshot().combatClears, Is.EqualTo(2));
    }

    [Test] public void FailedEntryUsesCurrentProgressAndExitOnlyAdvancesVisitOnce()
    {
        EconomyService service = NewService(out MemoryEconomyStore store);
        string stage = Complete(service);
        int visit = service.RunSnapshot().visit;
        Assert.That(service.TryGrantWithEffect(17, Vector3.zero, "pending.story").Success, Is.True);
        store.FailWrites = true;
        Assert.That(service.TryReturnFromStage(stage, 73).Error, Is.EqualTo(EconomyError.SaveFailed));
        Assert.That(service.RunSnapshot().area, Is.EqualTo(RunArea.TemporaryStage));
        store.FailWrites = false;
        Assert.That(service.TryUnlockBossForDevelopment().Success, Is.True);
        Assert.That(service.TryReturnFromStage(stage, 73).Success, Is.True);
        Assert.That(service.TryReturnFromStage(stage, 73).Success, Is.True);
        Assert.That(service.RunSnapshot().combatClears, Is.EqualTo(4));
        Assert.That(service.RunSnapshot().visit, Is.EqualTo(visit));
        Assert.That(service.PendingBalance, Is.Zero);
        string id = service.FirstAltarSnapshot().instanceId;
        Assert.That(service.TryLeaveFirstAltar(id, 73).Success, Is.False);
        Assert.That(service.TryMarkFirstAltarStoryRead(id).Success, Is.False);
        Assert.That(service.TryCommitFirstAltarDeath("wrong").Success, Is.False);
        store.FailWrites = true;
        Assert.That(service.TryCommitFirstAltarDeath(id).Error, Is.EqualTo(EconomyError.SaveFailed));
        Assert.That(service.FirstAltarSnapshot().phase, Is.EqualTo(FirstAltarPhase.AwaitingApproach));
        store.FailWrites = false;
        Assert.That(service.TryCommitFirstAltarDeath(id).Success, Is.True);
        Assert.That(service.TryMarkFirstAltarStoryRead(id).Success, Is.True);
        Assert.That(service.RunSnapshot().visit, Is.EqualTo(visit));
        store.FailWrites = true;
        Assert.That(service.TryLeaveFirstAltar(id, 73).Error, Is.EqualTo(EconomyError.SaveFailed));
        store.FailWrites = false;
        var reopened = service.Reopen(); Assert.That(reopened.Initialize(), Is.True);
        Assert.That(reopened.TryLeaveFirstAltar(id, 73).Success, Is.True);
        string batch = reopened.RunSnapshot().batchId;
        Assert.That(reopened.TryLeaveFirstAltar(id, 73).Success, Is.True);
        Assert.That(reopened.RunSnapshot().batchId, Is.EqualTo(batch));
        Assert.That(reopened.RunSnapshot().visit, Is.EqualTo(visit + 1));
        Assert.That(reopened.RunSnapshot().combatClears, Is.EqualTo(4));
        Assert.That(reopened.RunSnapshot().hp, Is.EqualTo(73));
        Assert.That(EconomyLedger.Validate(reopened.Snapshot()), Is.True);
        stage = Complete(reopened);
        Assert.That(reopened.TryReturnFromStage(stage, 60).Success, Is.True);
        Assert.That(reopened.RunSnapshot().area, Is.EqualTo(RunArea.Lobby));
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void LegacyJsonKeepsAssetsAndAllowsNextCombatWithExistingClears(int version)
    {
        EconomyService source = NewService(out _);
        Assert.That(source.TryUnlockBossForDevelopment().Success, Is.True);
        EconomyProfileData data = source.Snapshot();
        data.schemaVersion = version;
        if (version == 1)
        {
            data.hasRun = false; data.run = null;
            // v1 predates the run domain and cannot contain a successful run-initialize receipt.
            data.processedTransactions.RemoveAll(receipt => receipt.requestKey == "run.initialize" ||
                receipt.id.StartsWith("dev:", StringComparison.Ordinal) || receipt.id.StartsWith("story:", StringComparison.Ordinal));
        }
        string json = JsonUtility.ToJson(data);
        json = Regex.Replace(json, ",\"narrative\":\\{.*\\}\\}$", "}");
        Assert.That(json.Contains("\"narrative\""), Is.False);
        string path = Path.Combine(directory, "legacy.json"); File.WriteAllText(path, json);
        var migrated = new EconomyService(new JsonEconomyStore(path));
        Assert.That(migrated.Initialize(), Is.True);
        Assert.That(migrated.TutorialState, Is.EqualTo(TutorialProgress.LegacyEligible));
        Assert.That(migrated.Balance, Is.EqualTo(data.balance));
        Assert.That(migrated.Snapshot().profileId, Is.EqualTo(data.profileId));
        Assert.That(migrated.TryInitializeRun(100).Success, Is.True);
        int clears = migrated.RunSnapshot().combatClears;
        string id = Enter(migrated);
        Assert.That(migrated.RunSnapshot().combatClears, Is.EqualTo(clears + 1));
        Assert.That(migrated.TryCommitFirstAltarDeath(id).Success, Is.True);
        Assert.That(migrated.TryMarkFirstAltarStoryRead(id).Success, Is.True);
        var resumed = new EconomyService(new JsonEconomyStore(path));
        Assert.That(resumed.Initialize(), Is.True);
        Assert.That(resumed.FirstAltarSnapshot().phase, Is.EqualTo(FirstAltarPhase.StoryRead));
        Assert.That(resumed.TryLeaveFirstAltar(id, 73).Success, Is.True);
        Assert.That(resumed.FirstAltarSnapshot().phase, Is.EqualTo(FirstAltarPhase.Completed));
    }

    [Test] public void MissingV5PhaseCannotUseAnotherObjectsPhaseAndReplayStory()
    {
        EconomyService service = NewService(out _);
        string id = Enter(service);
        Assert.That(service.TryCommitFirstAltarDeath(id).Success, Is.True);
        string json = JsonUtility.ToJson(service.Snapshot());
        json = json.Replace("\"phase\":2,\"instanceId\"", "\"instanceId\"");
        // A top-level unrelated phase must not satisfy the nested required-field check.
        json = "{\"phase\":0," + json.Substring(1);
        string path = Path.Combine(directory, "invalid.json"); File.WriteAllText(path, json);
        var store = new JsonEconomyStore(path);
        Assert.That(store.Load().Status, Is.EqualTo(EconomyLoadStatus.Invalid));
        Assert.That(store.TrySave(new EconomyProfileData()), Is.False);
        Assert.That(File.ReadAllText(path), Is.EqualTo(json));
    }

    [Test] public void UnfinishedProfileSurvivesRunReplacementAndCompletionSurvivesAnotherRun()
    {
        EconomyService service = NewService(out MemoryEconomyStore store);
        string id = Enter(service);
        Assert.That(service.TryCommitFirstAltarDeath(id).Success, Is.True);
        EconomyProfileData data = service.Snapshot();
        data.run = LobbyRules.NewRun("replacement-run", 100, 4);
        Assert.That(store.TrySave(data), Is.True);
        service = service.Reopen(); Assert.That(service.Initialize(), Is.True);
        Assert.That(Enter(service), Is.EqualTo(id));
        Assert.That(service.FirstAltarSnapshot().phase, Is.EqualTo(FirstAltarPhase.BelieverDead));
        Assert.That(service.TryMarkFirstAltarStoryRead(id).Success, Is.True);
        Assert.That(service.TryLeaveFirstAltar(id, 73).Success, Is.True);
        data = service.Snapshot(); data.run = LobbyRules.NewRun("another-run", 100, 7);
        Assert.That(store.TrySave(data), Is.True);
        service = service.Reopen(); Assert.That(service.Initialize(), Is.True);
        string stage = Complete(service);
        Assert.That(service.TryReturnFromStage(stage, 80).Success, Is.True);
        Assert.That(service.RunSnapshot().area, Is.EqualTo(RunArea.Lobby));
    }
}
