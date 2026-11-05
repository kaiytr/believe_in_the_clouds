using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Independent regular / belief lanes, prophecy windows, persistent clouds and the intro hold. Few, targeted cases.</summary>
public sealed class BossPatternReworkPlayModeTests
{
    private BossCombatController boss;
    private PlayerHealth player;
    private Rigidbody2D playerBody;
    private BossEncounterTrigger entry;
    private BossCombatSettings originalData, data;
    private bool holdIntro;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        holdIntro = false;
        yield return Load(false);
        PutPlayer(new Vector2(-4.21f, -2.33f));
        yield return Until(() => boss.EncounterStarted);
        originalData = boss.Settings; data = Object.Instantiate(originalData); Set(boss, "settings", data);
        yield return new WaitForSeconds(.3f);
        boss.PauseAndCancelForTest(); player.ResetHealth();
    }

    private IEnumerator Load(bool intro)
    {
        SceneManager.sceneLoaded += PrepareLoadedScene;
        holdIntro = intro;
        Type editor = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("UnityEditor.SceneManagement.EditorSceneManager", false)).First(type => type != null);
        MethodInfo method = editor.GetMethod("LoadSceneAsyncInPlayMode", BindingFlags.Public | BindingFlags.Static, null,
            new[] { typeof(string), typeof(LoadSceneParameters) }, null);
        var load = (AsyncOperation)method.Invoke(null, new object[] { ProjectScenes.BossTest, new LoadSceneParameters(LoadSceneMode.Single) });
        while (!load.isDone) yield return null;
        SceneManager.sceneLoaded -= PrepareLoadedScene;
        yield return null;
        boss = Object.FindFirstObjectByType<BossCombatController>();
        entry = Object.FindFirstObjectByType<BossEncounterTrigger>();
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerHealth>();
        playerBody = player.GetComponent<Rigidbody2D>();
        player.GetComponent<PlayerMove>().enabled = false;
    }

    private void PrepareLoadedScene(Scene scene, LoadSceneMode mode)
    {
        var controller = Object.FindFirstObjectByType<BossCombatController>();
        if (controller != null) controller.AutomaticPatterns = false;
        var intro = Object.FindFirstObjectByType<CutsceneBossEncounterCoordinator>();
        if (intro != null) intro.enabled = false;
        var trigger = Object.FindFirstObjectByType<BossEncounterTrigger>();
        if (trigger != null) trigger.AutoBeginEncounter = !holdIntro;
    }

    private static void Set(object target, string field, object value)
    { target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value); }

    private void PutPlayer(Vector2 position, bool freeze = true)
    {
        playerBody.gravityScale = freeze ? 0f : 4f;
        playerBody.position = position; player.transform.position = position; playerBody.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }

    private void PutBodyCenter(Vector2 center, bool freeze = true)
    {
        Vector2 offset = (Vector2)player.BodyCollider.bounds.center - (Vector2)player.transform.position;
        PutPlayer(center - offset, freeze);
    }

    private IEnumerator Until(Func<bool> predicate, float timeout = 8f)
    {
        float until = Time.time + timeout;
        while (!predicate() && Time.time < until) yield return null;
        Assert.That(predicate(), Is.True, "Timed out: " + boss.StatusMessage);
    }

    private int Count(BossAttackKind kind) => boss.AttackHistory.Count(record => record.Kind == kind);
    private static void Move(BossCloudPlatform cloud, Vector2 position)
    {
        cloud.transform.position = position;
        var body = cloud.GetComponent<Rigidbody2D>();
        if (body != null) body.position = position;
    }

    [UnityTest]
    public IEnumerator BeliefStartsDuringARegularAttackWithoutCancellingItAndBurnIsNotRetroactive()
    {
        PutPlayer((Vector2)boss.transform.position + Vector2.right * 1.2f);
        boss.ForcePatternForTest(BossPatternKind.MeleeCombo);
        yield return Until(() => boss.ActiveBossAttackId > 0);
        int generation = boss.Generation;
        boss.ForcePatternForTest(BossPatternKind.BurnBelief, cancelRunning: false);
        Assert.That(boss.Generation, Is.EqualTo(generation), "A belief must not cancel the running regular attack.");
        Assert.That(boss.IsRegularRunning, Is.True);
        Assert.That(boss.IsBurning, Is.True);
        Assert.That(boss.BurnRemaining, Is.EqualTo(data.BurnWindow).Within(.2f));
        yield return Until(() => !boss.IsRegularRunning);
        Assert.That(Count(BossAttackKind.Melee), Is.EqualTo(3));
        Assert.That(boss.AttackHistory.Where(record => record.Kind == BossAttackKind.Melee).All(record => !record.BurnEnhanced), Is.True,
            "The attack that was already running keeps its start-time snapshot.");

        player.ResetHealth(); boss.PauseAndCancelForTest(); boss.ForcePatternForTest(BossPatternKind.BurnBelief);
        PutPlayer((Vector2)boss.transform.position + Vector2.right * 1.2f);
        boss.ForcePatternForTest(BossPatternKind.MeleeCombo, cancelRunning: false);
        yield return Until(() => boss.ActiveBossAttackId > 0);
        Assert.That(boss.ActiveAttacks.All(record => record.BurnEnhanced), Is.True, "A new attack records the enhancement at its start.");
    }

    [UnityTest]
    public IEnumerator OnlyOneBeliefRemainsActiveAndCooldownStartsAfterItsEffectEnds()
    {
        data.BurnWindow = .25f;
        data.BeliefCooldown = .25f;
        PutPlayer((Vector2)boss.transform.position + Vector2.left * 12f);
        boss.ForcePatternForTest(BossPatternKind.BurnBelief);
        boss.ForcePatternForTest(BossPatternKind.Dash, cancelRunning: false);
        Assert.That(boss.IsRegularRunning, Is.True, "일반 공격은 믿음과 독립적으로 진행한다.");
        boss.ForcePatternForTest(BossPatternKind.CloudBelief, cancelRunning: false);
        boss.ForcePatternForTest(BossPatternKind.AttackIntentBelief, cancelRunning: false);
        Assert.That(boss.PatternUseCount(BossPatternKind.CloudBelief), Is.Zero);
        Assert.That(boss.Prophecies, Is.Empty);
        yield return new WaitForSeconds(.35f);
        Assert.That(boss.IsBurning, Is.False);
        Assert.That(boss.HasActiveBelief, Is.True, "강화가 기록된 공격이 끝나기 전에는 다른 믿음을 겹치지 않는다.");
        Assert.That(boss.IsPatternEligible(BossPatternKind.CloudBelief), Is.False);
        yield return Until(() => !boss.HasActiveBelief);
        data.PhaseOnePool = new[]
        {
            new BossPatternWeight(BossPatternKind.MeleeCombo, 1f, 0f),
            new BossPatternWeight(BossPatternKind.CloudBelief, 1f, 0f)
        };
        boss.AutomaticPatterns = true;
        yield return new WaitForSeconds(.08f);
        Assert.That(boss.PatternUseCount(BossPatternKind.CloudBelief), Is.Zero, "효과 종료 이후 쿨타임을 기다린다.");
        yield return Until(() => boss.PatternUseCount(BossPatternKind.CloudBelief) > 0);
        Assert.That(boss.Clouds.LightningActive, Is.True);
        Assert.That(boss.ActiveBeliefPhrase, Is.EqualTo("너는 번개를 피하지 못할 것이다."));
    }

    [UnityTest]
    public IEnumerator AttackProphecyOnlyClosesAtAnAttackStartedAfterTheReactionTime()
    {
        PutPlayer((Vector2)boss.transform.position + Vector2.left * 6f);
        boss.ForcePatternForTest(BossPatternKind.AttackIntentBelief);
        float armed = Time.time;
        Assert.That(boss.HasProphecy(BossBeliefKind.AttackIntent), Is.True);
        Assert.That(boss.IsBeliefProcessing, Is.False, "Arming is instant: no staring pose and no blocked pools.");
        yield return new WaitForSeconds(.2f);
        boss.ForcePatternForTest(BossPatternKind.Dash, cancelRunning: false);
        Assert.That(Time.time - armed, Is.LessThan(data.ProphecyReactionTime));
        Assert.That(boss.HasProphecy(BossBeliefKind.AttackIntent), Is.True, "An attack inside the first second does not close it.");
        Assert.That(boss.CurrentBelief, Is.EqualTo(data.MaxBelief));
        yield return Until(() => !boss.IsRegularRunning);
        yield return new WaitUntilTime(armed + data.ProphecyReactionTime + .05f);
        boss.ForcePatternForTest(BossPatternKind.Dash, cancelRunning: false);
        Assert.That(boss.HasProphecy(BossBeliefKind.AttackIntent), Is.False);
        Assert.That(boss.CurrentBelief, Is.EqualTo(data.MaxBelief - data.BeliefReduction), "The player never attacked during the window.");
    }

    [UnityTest]
    public IEnumerator NoAttackProphecyReducesOnceAndDoesNotFreezeTheBoss()
    {
        boss.ForcePhaseTwoForTest(); data.AttackSignalMode = BossAttackSignalMode.Either;
        boss.ForcePatternForTest(BossPatternKind.NoAttackBelief);
        Assert.That(boss.HasProphecy(BossBeliefKind.NoAttack), Is.True);
        var events = player.GetComponent<PlayerCombatEvents>();
        var attack = events.PublishAttackAttempt(boss.gameObject);
        events.PublishHitConfirmed(attack, boss.gameObject, 10f);
        events.PublishAttackAttempt(boss.gameObject);
        Assert.That(boss.CurrentBelief, Is.EqualTo(data.MaxBelief - data.BeliefReduction), "Several events of one prophecy reduce once.");
        PutPlayer((Vector2)boss.transform.position + Vector2.left * 6f);
        boss.ForcePatternForTest(BossPatternKind.Projectile, cancelRunning: false);
        Assert.That(boss.IsRegularRunning, Is.True, "Regular attacks run while the prophecy is pending.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator DodgeProphecyIsSettledOncePerWholeComboAndNeverByDistance()
    {
        boss.ForcePhaseTwoForTest();
        // Far away and unthreatened: the prophecy must stay pending instead of paying a free dodge.
        PutPlayer((Vector2)boss.transform.position + Vector2.left * 12f);
        boss.ForcePatternForTest(BossPatternKind.AttackAndDodgeBelief);
        boss.ForcePatternForTest(BossPatternKind.MeleeCombo, cancelRunning: false);
        yield return Until(() => !boss.IsRegularRunning);
        Assert.That(boss.CurrentBelief, Is.EqualTo(data.MaxBelief));
        Assert.That(boss.HasProphecy(BossBeliefKind.AttackAndDodge), Is.True);
        // Really threatened and then leaving: one reduction for the three-hit combo.
        PutPlayer((Vector2)boss.transform.position + Vector2.right * 1.2f);
        boss.ForcePatternForTest(BossPatternKind.MeleeCombo, cancelRunning: false);
        yield return Until(() => boss.ActiveBossAttackId > 0);
        yield return new WaitForSeconds(.3f);
        PutPlayer((Vector2)boss.transform.position + Vector2.left * 12f);
        yield return Until(() => !boss.IsRegularRunning);
        Assert.That(boss.HasProphecy(BossBeliefKind.AttackAndDodge), Is.False);
        Assert.That(boss.CurrentBelief, Is.EqualTo(data.MaxBelief - data.BeliefReduction), "One result per combo, not one per hit.");
    }

    [UnityTest]
    public IEnumerator ShockwaveWaitsForTheWholeComboBlocksNewAttacksAndAddsNoSwing()
    {
        boss.ForcePhaseTwoForTest();
        PutPlayer((Vector2)boss.transform.position + Vector2.right * 1.2f);
        boss.ForcePatternForTest(BossPatternKind.MeleeCombo);
        yield return Until(() => boss.ActiveBossAttackId > 0);
        boss.ForcePatternForTest(BossPatternKind.ShockwaveBelief, cancelRunning: false);
        Assert.That(boss.IsShockwavePending, Is.True);
        boss.ForcePatternForTest(BossPatternKind.ShockwaveBelief, cancelRunning: false);
        Assert.That(boss.PatternUseCount(BossPatternKind.ShockwaveBelief), Is.EqualTo(1), "The reservation is never stacked.");
        yield return Until(() => boss.IsBodyActionActive);
        Assert.That(Count(BossAttackKind.Melee), Is.EqualTo(3), "The combo finished before the swing took the body.");
        Assert.That(boss.ActiveAttacks.All(record => record.Kind != BossAttackKind.Melee), Is.True);
        boss.ForcePatternForTest(BossPatternKind.Dash, cancelRunning: false);
        Assert.That(boss.PatternUseCount(BossPatternKind.Dash), Is.Zero, "No new regular attack while the body is in the swing.");
        yield return Until(() => !boss.IsBodyActionActive);
        Assert.That(Count(BossAttackKind.Melee), Is.EqualTo(3), "No extra regular attack was appended to the shockwave.");
    }

    [UnityTest]
    public IEnumerator ProjectileAimsAtAPlayerAtADifferentHeight()
    {
        Vector2 bossCenter = boss.BodyCollider.bounds.center;
        PutBodyCenter(bossCenter + new Vector2(-6f, 3f));
        Assert.That(boss.IsPatternEligible(BossPatternKind.Projectile), Is.True);
        boss.ForcePatternForTest(BossPatternKind.Projectile);
        yield return Until(() => boss.ActiveBossAttackId > 0);
        BossAttackRecord record = boss.ActiveAttacks.First();
        Assert.That(Mathf.Abs(Mathf.Sin(record.Geometry.Angle * Mathf.Deg2Rad)), Is.GreaterThan(.2f), "The locked aim is diagonal, not a horizontal line.");
        Assert.That(record.Geometry.Overlaps(player.BodyCollider), Is.True, "Warning corridor and real projectile path reach the player.");
    }

    [UnityTest]
    public IEnumerator ArenaCloudsAreIdempotentPersistThroughPhaseAndAreReleasedWithTheBoss()
    {
        Assert.That(boss.PrepareArenaVisuals(), Is.True);
        BossCloudField field = boss.Clouds;
        var storms = field.StormClouds.ToArray(); var whites = field.WhiteClouds.ToArray();
        Assert.That(storms.Length, Is.InRange(2, 3)); Assert.That(whites.Length, Is.EqualTo(2));
        Assert.That(boss.PrepareArenaVisuals(), Is.True);
        CollectionAssert.AreEqual(storms, field.StormClouds.ToArray(), "Preparing twice must not respawn clouds.");
        boss.ForcePhaseTwoForTest(); yield return null;
        Assert.That(storms.All(cloud => cloud != null) && whites.All(cloud => cloud != null), Is.True, "A phase change keeps the clouds.");
        foreach (BossCloudPlatform white in whites)
        {
            Assert.That(white.Surface, Is.Not.Null);
            Assert.That(white.SurfaceTop - field.FloorY, Is.EqualTo(data.WhiteCloudTopHeight).Within(.02f));
        }
        boss.TakeDamage(new DamageInfo(100000f, DamageSource.Environment)); yield return null; yield return null;
        Assert.That(boss.IsDead, Is.True);
        Assert.That(Object.FindObjectsByType<BossCloudPlatform>(FindObjectsSortMode.None), Is.Empty, "Moving platforms are removed with the boss.");
    }

    [UnityTest]
    public IEnumerator OverlappingStormsStrikeOnceAndAWhiteCloudShieldsTheWholeBody()
    {
        data.CloudDuration = .6f; data.LightningDuration = .2f;
        Assert.That(boss.PrepareArenaVisuals(), Is.True);
        BossCloudField field = boss.Clouds; field.SetMotionEnabled(false);
        float x = field.LimitsX.x + 6f;
        foreach (BossCloudPlatform storm in field.StormClouds) Move(storm, new Vector2(x, storm.Position.y));
        foreach (BossCloudPlatform white in field.WhiteClouds) Move(white, new Vector2(field.LimitsX.y - 3f, white.Position.y));
        Physics2D.SyncTransforms();

        PutBodyCenter(new Vector2(x, field.FloorY + 1f));
        boss.ForcePatternForTest(BossPatternKind.CloudBelief);
        Assert.That(field.LightningActive, Is.True);
        yield return Until(() => field.Lightning == null || field.Lightning.HasStruck);
        Assert.That(player.CurrentHp, Is.EqualTo(100f - data.LightningDamage), "Overlapping storms deal the strike once.");
        Assert.That(field.Lightning.GetComponentsInChildren<BossLightningStrikeVisual>(), Is.Not.Empty);
        Assert.That(field.Lightning.GetComponentsInChildren<LineRenderer>(), Is.Empty);
        Assert.That(boss.AttackHistory.Count(record => record.Kind == BossAttackKind.Lightning && record.Outcome == AttackOutcome.Hit), Is.EqualTo(1));
        yield return Until(() => !field.LightningActive);

        player.ResetHealth(); boss.PauseAndCancelForTest();
        BossCloudPlatform shield = field.WhiteClouds[0];
        Move(shield, new Vector2(x, shield.Position.y)); Physics2D.SyncTransforms();
        PutBodyCenter(new Vector2(x, field.FloorY + 1f));
        float belief = boss.CurrentBelief;
        boss.ForcePatternForTest(BossPatternKind.CloudBelief);
        yield return Until(() => field.Lightning == null || field.Lightning.HasStruck);
        Assert.That(player.CurrentHp, Is.EqualTo(100f), "Duplicate storm coverage cannot cancel a shelter.");
        Assert.That(boss.CurrentBelief, Is.EqualTo(belief - data.BeliefReduction));
    }

    [UnityTest]
    public IEnumerator WhiteCloudCarriesWhoeverStandsOnItAndPassesFromBelow()
    {
        Assert.That(boss.PrepareArenaVisuals(), Is.True);
        BossCloudField field = boss.Clouds; field.SetMotionEnabled(false);
        BossCloudPlatform cloud = field.WhiteClouds[0];
        SpriteRenderer visual = cloud.GetComponent<SpriteRenderer>();
        Assert.That(visual.sprite.name, Is.EqualTo("BossWhiteCloud"));
        Assert.That(visual.color, Is.EqualTo(Color.white));
        Assert.That(visual.bounds.size.x, Is.EqualTo(cloud.Size.x).Within(.001f));
        Assert.That(visual.bounds.size.y, Is.EqualTo(cloud.Size.y).Within(.001f));
        foreach (BossCloudPlatform other in field.WhiteClouds.Skip(1)) Move(other, new Vector2(field.LimitsX.y - 2f, other.Position.y));
        Move(cloud, new Vector2(0f, cloud.Position.y)); Physics2D.SyncTransforms();
        // Under the cloud first: the one-way surface is not a floor for a body below it.
        PutBodyCenter(new Vector2(0f, field.FloorY + 1.05f));
        yield return new WaitForFixedUpdate();
        Assert.That(cloud.Surface.gameObject.layer, Is.Not.EqualTo(data.PlatformLayer), "Not visible to the ground check from below.");
        PutBodyCenter(new Vector2(0f, cloud.SurfaceTop + 1.02f), freeze: false);
        yield return new WaitForSeconds(.6f);
        Assert.That(cloud.IsCarrying(player.BodyCollider), Is.True, "The body lands on the white cloud's top.");
        float cloudX = cloud.Position.x, bodyX = player.BodyCollider.bounds.center.x;
        cloud.Step(.5f);
        yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
        Assert.That(Mathf.Abs(cloud.Position.x - cloudX), Is.GreaterThan(.1f));
        Assert.That(player.BodyCollider.bounds.center.x - bodyX, Is.EqualTo(cloud.Position.x - cloudX).Within(.06f), "The rider moves with the platform.");
    }

    [UnityTest]
    public IEnumerator ArrivalSealsTheArenaAndPreparesCloudsButOnlyBeginEncounterStartsTheFight()
    {
        yield return Load(true);
        Assert.That(entry.AutoBeginEncounter, Is.False);
        PutPlayer(new Vector2(-4.21f, -2.33f));
        yield return Until(() => entry.HasEntered);
        yield return new WaitForSeconds(.5f);
        Assert.That(boss.EncounterStarted, Is.False);
        Assert.That(entry.HoldingForIntro, Is.True);
        Assert.That(boss.Clouds != null && boss.Clouds.IsPrepared, Is.True, "Clouds are prepared on entry.");
        Assert.That(boss.AttackHistory.Count, Is.Zero); Assert.That(boss.IsPatternRunning, Is.False);
        Assert.That(player.CurrentHp, Is.EqualTo(100f));
        boss.BeginEncounter(); boss.BeginEncounter();
        Assert.That(boss.EncounterStarted, Is.True);
        Assert.That(boss.Clouds.StormClouds.Count, Is.InRange(2, 3), "Beginning the fight keeps the prepared clouds.");
        Assert.That(entry.HoldingForIntro, Is.False);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (PlayerDeathCoordinator.Active != null)
        {
            Currency.Service?.CompleteDeferredDeath(PlayerDeathCoordinator.Active.DeathId);
            Object.Destroy(PlayerDeathCoordinator.Active.gameObject);
        }
        Time.timeScale = 1f;
        SceneManager.sceneLoaded -= PrepareLoadedScene;
        if (boss != null) { boss.PauseAndCancelForTest(); boss.gameObject.SetActive(false); if (originalData != null) Set(boss, "settings", originalData); }
        if (data != null) Object.Destroy(data);
        yield return null;
    }

    private sealed class WaitUntilTime : CustomYieldInstruction
    {
        private readonly float time;
        public WaitUntilTime(float time) { this.time = time; }
        public override bool keepWaiting => Time.time < time;
    }
}
