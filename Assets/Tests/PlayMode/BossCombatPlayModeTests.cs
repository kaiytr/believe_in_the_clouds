using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BossCombatPlayModeTests
{
    private BossCombatController boss;
    private PlayerHealth player;
    private Rigidbody2D playerBody;
    private BossCombatSettings originalData, data;
    private readonly List<GameObject> fixtures = new List<GameObject>();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        SceneManager.sceneLoaded += PauseOnLoad;
        Type editorSceneManager = null;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        { editorSceneManager = assembly.GetType("UnityEditor.SceneManagement.EditorSceneManager",false); if (editorSceneManager != null) break; }
        var method = editorSceneManager.GetMethod("LoadSceneAsyncInPlayMode",BindingFlags.Public|BindingFlags.Static,null,
            new[] {typeof(string),typeof(LoadSceneParameters)},null);
        var load = (AsyncOperation)method.Invoke(null,new object[] {"Assets/Scenes/BossTest.unity",new LoadSceneParameters(LoadSceneMode.Single)});
        while (!load.isDone) yield return null;
        SceneManager.sceneLoaded -= PauseOnLoad;
        yield return null;
        boss = Object.FindFirstObjectByType<BossCombatController>();
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerHealth>();
        playerBody = player.GetComponent<Rigidbody2D>();
        player.GetComponent<PlayerMove>().enabled = false;
        // BossTest now waits for arena entry; put combat fixtures inside before preparing each case.
        PutPlayer(new Vector2(-4.21f,-2.33f));
        yield return Until(() => boss.EncounterStarted);
        Assert.That(boss.EncounterStarted,Is.True,boss.StatusMessage);
        originalData = boss.Settings; data = Object.Instantiate(originalData); Set(boss,"settings",data);
        yield return new WaitForSeconds(.65f);
        boss.PauseAndCancelForTest(); player.ResetHealth();
    }
    private static void PauseOnLoad(Scene scene, LoadSceneMode mode)
    { var controller = Object.FindFirstObjectByType<BossCombatController>(); if (controller != null) controller.AutomaticPatterns = false; }
    private static void Set(object target,string field,object value)
    { target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value); }
    private void PutPlayer(Vector2 position, bool freeze = true)
    {
        playerBody.gravityScale = freeze ? 0f : 4f;
        playerBody.position = position; player.transform.position = position; playerBody.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }
    private IEnumerator Until(Func<bool> predicate,float timeout = 6f)
    { float until=Time.time+timeout; while(!predicate() && Time.time<until) yield return null; Assert.That(predicate(),Is.True,"Timed out: "+boss.StatusMessage); }
    private BossAttackRecord Last => boss.AttackHistory[boss.AttackHistory.Count-1];
    private int Count(BossAttackKind kind)
    { int count=0; foreach(var record in boss.AttackHistory) if(record.Kind==kind) count++; return count; }
    private GameObject Wall(float x)
    {
        var obj = new GameObject("Test wall"); obj.layer=6; obj.transform.position=new Vector2(x,-1.5f);
        obj.AddComponent<BoxCollider2D>().size=new Vector2(.15f,5f); fixtures.Add(obj); Physics2D.SyncTransforms(); return obj;
    }

    [UnityTest]
    public IEnumerator MeleeComboMirrorsPlayerHitboxesAndAutomaticallyLandsFourHits()
    {
        PutPlayer((Vector2)boss.transform.position+Vector2.left*1.5f);
        var playerAttack=player.GetComponent<PlayerAttack>();
        Assert.That(playerAttack,Is.Not.Null);
        Assert.That(playerAttack.ComboCount,Is.GreaterThanOrEqualTo(4));
        PlayerAttack.ComboStep firstStep;
        Assert.That(playerAttack.TryGetComboStep(0,out firstStep),Is.True);
        boss.ForcePatternForTest(BossPatternKind.MeleeCombo);
        yield return Until(()=>boss.ActiveBossAttackId>0);
        var warning=Object.FindFirstObjectByType<DangerIndicator>(); Assert.That(warning,Is.Not.Null);
        Assert.That(player.CurrentHp,Is.EqualTo(100f));
        var record = new List<BossAttackRecord>(boss.ActiveAttacks)[0];
        Assert.That(record.Geometry.Size,Is.EqualTo(firstStep.hitboxSize));
        Assert.That(record.Geometry.Angle,Is.EqualTo(-firstStep.hitboxAngle).Within(.01f));
        Assert.That(record.Geometry.Center.x,Is.LessThan(boss.transform.position.x));
        yield return new WaitForSeconds(data.MeleeComboFirstWarning*.5f);
        Assert.That(player.CurrentHp,Is.EqualTo(100f));
        yield return Until(()=>Count(BossAttackKind.Melee)>=1);
        Assert.That(player.CurrentHp,Is.EqualTo(100f-data.MeleeComboDamage[0]));
        Assert.That(Last.Outcome,Is.EqualTo(AttackOutcome.Hit));
        yield return Until(()=>!boss.IsPatternRunning);
        Assert.That(Count(BossAttackKind.Melee),Is.EqualTo(4));
        Assert.That(player.CurrentHp,Is.EqualTo(100f-(data.MeleeComboDamage[0]+data.MeleeComboDamage[1]+
            data.MeleeComboDamage[2]+data.MeleeComboDamage[3])));
        Assert.That(playerBody.linearVelocity.y,Is.GreaterThan(0f));
        Assert.That(boss.transform.localScale,Is.EqualTo(Vector3.one));
        Assert.That(Object.FindFirstObjectByType<DangerIndicator>(),Is.Null);
    }

    [UnityTest]
    public IEnumerator LeavingLockedMeleeIsEvadeButInvulnerableContactAndOutsideAreNot()
    {
        PutPlayer((Vector2)boss.transform.position+Vector2.left*1.5f);
        boss.ForcePatternForTest(BossPatternKind.MeleeCombo);
        yield return Until(()=>boss.ActiveBossAttackId>0);
        yield return new WaitForFixedUpdate();
        PutPlayer((Vector2)boss.transform.position+Vector2.right*1.5f);
        yield return Until(()=>Count(BossAttackKind.Melee)>=1);
        Assert.That(Last.Outcome,Is.EqualTo(AttackOutcome.EvadedByPlayer));
        Assert.That(player.CurrentHp,Is.EqualTo(100f));
        boss.PauseAndCancelForTest();
        PutPlayer((Vector2)boss.transform.position+Vector2.left*1.5f);
        player.TakeDamage(new DamageInfo(1f,DamageSource.Environment));
        boss.ForcePatternForTest(BossPatternKind.MeleeCombo);
        int prior=Count(BossAttackKind.Melee);
        yield return Until(()=>Count(BossAttackKind.Melee)>prior);
        Assert.That(Last.Contacted,Is.True); Assert.That(Last.DamageAccepted,Is.False);
        Assert.That(Last.Outcome,Is.EqualTo(AttackOutcome.Missed));
        boss.PauseAndCancelForTest(); PutPlayer(new Vector2(-14f,4f));
        prior=Count(BossAttackKind.Melee); boss.ForcePatternForTest(BossPatternKind.MeleeCombo);
        yield return Until(()=>Count(BossAttackKind.Melee)>prior);
        Assert.That(Last.Outcome,Is.EqualTo(AttackOutcome.Missed)); Assert.That(Last.WasThreatened,Is.False);
    }

    [UnityTest]
    public IEnumerator FastProjectileAndDashStopAtThinWallWithoutPlayerDamage()
    {
        PutPlayer(new Vector2(-5f,boss.transform.position.y)); Wall(0f);
        data.ProjectileSpeed=200f;
        boss.ForcePatternForTest(BossPatternKind.Projectile);
        yield return Until(()=>Count(BossAttackKind.Projectile)>0);
        Assert.That(Last.Outcome,Is.EqualTo(AttackOutcome.Blocked)); Assert.That(player.CurrentHp,Is.EqualTo(100f));
        data.DashSpeed=200f;
        boss.ForcePatternForTest(BossPatternKind.Dash);
        yield return Until(()=>Count(BossAttackKind.Dash)>0);
        Assert.That(Last.Outcome,Is.EqualTo(AttackOutcome.Blocked));
        Assert.That(boss.BodyCollider.bounds.min.x,Is.GreaterThanOrEqualTo(.05f));
        Assert.That(player.CurrentHp,Is.EqualTo(100f));
    }

    [UnityTest]
    public IEnumerator ProjectileIsVisibleLocksAimAndSweepsThePlayerAtHighSpeed()
    {
        PutPlayer(new Vector2(-5f,boss.transform.position.y)); data.ProjectileSpeed=200f;
        boss.ForcePatternForTest(BossPatternKind.Projectile);
        yield return Until(()=>Object.FindFirstObjectByType<BossAttackProjectile>()!=null);
        var projectile=Object.FindFirstObjectByType<BossAttackProjectile>();
        Assert.That(projectile.GetComponent<SpriteRenderer>().bounds.size.x,Is.EqualTo(.38f).Within(.005f));
        Assert.That(projectile.GetComponent<CircleCollider2D>().bounds.size.x,Is.EqualTo(.38f).Within(.005f));
        yield return Until(()=>Count(BossAttackKind.Projectile)>0);
        Assert.That(Last.Outcome,Is.EqualTo(AttackOutcome.Hit)); Assert.That(player.CurrentHp,Is.EqualTo(88f));
        Assert.That(Object.FindFirstObjectByType<BossAttackProjectile>(),Is.Null);
    }

    [UnityTest]
    public IEnumerator DistantAttackAndDodgeUsesProjectileAndCountsMovementEvade()
    {
        data.StareDuration=.15f;
        // The follow-up now samples the regular attack pool at every distance.
        // Keep this projectile-evade test deterministic instead of waiting for a
        // projectile after a randomly selected melee or dash follow-up.
        data.PhaseTwoPool=new[] { new BossPatternWeight(BossPatternKind.Projectile,1f,.5f) };
        boss.ForcePhaseTwoForTest();
        PutPlayer((Vector2)boss.transform.position+Vector2.right*2f);
        Assert.That(boss.IsPatternEligible(BossPatternKind.MeleeCombo),Is.True);
        Assert.That(boss.IsPatternEligible(BossPatternKind.Dash),Is.True);
        PutPlayer((Vector2)boss.transform.position+Vector2.right*8f);
        Assert.That(boss.IsPatternEligible(BossPatternKind.MeleeCombo),Is.True);
        Assert.That(boss.IsPatternEligible(BossPatternKind.Dash),Is.True);
        Assert.That(boss.IsPatternEligible(BossPatternKind.Projectile),Is.True);

        boss.ForcePatternForTest(BossPatternKind.AttackAndDodgeBelief);
        yield return Until(()=>Object.FindFirstObjectByType<BossAttackProjectile>()!=null);
        playerBody.linearVelocity=Vector2.up*10f;
        yield return Until(()=>Count(BossAttackKind.Projectile)>0);
        Assert.That(Last.Belief,Is.EqualTo(BossBeliefKind.AttackAndDodge));
        Assert.That(Last.Outcome,Is.EqualTo(AttackOutcome.EvadedByPlayer));
        Assert.That(boss.CurrentBelief,Is.EqualTo(data.MaxBelief-data.BeliefReduction));
        Assert.That(player.CurrentHp,Is.EqualTo(100f));
    }

    [UnityTest]
    public IEnumerator BurnBeliefDistantProjectileEvadeReducesBelief()
    {
        data.BurnPreparation=.15f; data.BurnWindow=1.2f;
        data.PhaseOnePool=new[] { new BossPatternWeight(BossPatternKind.BurnBelief,1f,.5f),
            new BossPatternWeight(BossPatternKind.Projectile,1f,.5f) };
        PutPlayer((Vector2)boss.transform.position+Vector2.right*8f);
        boss.ForcePatternForTest(BossPatternKind.BurnBelief);
        yield return Until(()=>Object.FindFirstObjectByType<BossAttackProjectile>()!=null);
        playerBody.linearVelocity=Vector2.up*10f;
        yield return Until(()=>Count(BossAttackKind.Projectile)>0);
        Assert.That(Last.Belief,Is.EqualTo(BossBeliefKind.Burn));
        Assert.That(Last.Outcome,Is.EqualTo(AttackOutcome.EvadedByPlayer));
        Assert.That(boss.CurrentBelief,Is.EqualTo(data.MaxBelief-data.BeliefReduction));
        Assert.That(player.CurrentHp,Is.EqualTo(100f));
    }

    [UnityTest]
    public IEnumerator BurnBeliefDashApproachesWhenPlayerIsBeyondDashRange()
    {
        data.BurnPreparation=.1f; data.BurnWindow=.8f;
        data.PhaseOnePool=new[] { new BossPatternWeight(BossPatternKind.BurnBelief,1f,.5f),
            new BossPatternWeight(BossPatternKind.Dash,1f,.5f) };
        PutPlayer((Vector2)boss.transform.position+Vector2.right*8f);
        float startX=boss.Body.position.x;
        Assert.That(boss.IsPatternEligible(BossPatternKind.Dash),Is.True);
        boss.ForcePatternForTest(BossPatternKind.BurnBelief);
        yield return Until(()=>Count(BossAttackKind.Dash)>0);
        Assert.That(boss.Body.position.x,Is.GreaterThan(startX+4f));
        Assert.That(Last.WasThreatened,Is.False);
        Assert.That(boss.CurrentBelief,Is.EqualTo(data.MaxBelief));
    }

    [UnityTest]
    public IEnumerator DistantDashRemainsCandidateWhenObstacleBlocksApproach()
    {
        PutPlayer((Vector2)boss.transform.position+Vector2.right*8f);
        Wall(boss.transform.position.x+2f);
        Assert.That(boss.IsPatternEligible(BossPatternKind.Dash),Is.True);
        yield return null;
    }

    [UnityTest]
    public IEnumerator PhaseBoundaryCancelsLiveAttacksPreservesBeliefAndVulnerabilityDeadline()
    {
        PutPlayer(new Vector2(-5f,boss.transform.position.y));
        boss.ForcePatternForTest(BossPatternKind.Projectile);
        yield return Until(()=>Object.FindFirstObjectByType<BossAttackProjectile>()!=null);
        var old = new List<BossAttackRecord>(boss.ActiveAttacks)[0]; int token=boss.Generation;
        boss.ReduceBeliefForTest(20f);
        boss.TakeDamage(new DamageInfo(10000f,DamageSource.PlayerOwned));
        Assert.That(boss.CurrentHp,Is.EqualTo(250f)); Assert.That(boss.Phase,Is.EqualTo(BossPhase.Two));
        Assert.That(boss.CurrentBelief,Is.EqualTo(80f)); Assert.That(boss.Generation,Is.GreaterThan(token));
        Assert.That(old.Outcome,Is.EqualTo(AttackOutcome.Cancelled));
        boss.TryHit(old); boss.CompleteAttack(old,BossAttackEndReason.Completed);
        yield return new WaitForFixedUpdate();
        Assert.That(player.CurrentHp,Is.EqualTo(100f)); Assert.That(boss.ActiveBossAttackId,Is.Zero);
        boss.ResetEncounterForTest(); boss.AutomaticPatterns=false;
        boss.ReduceBeliefForTest(100f); yield return new WaitForSeconds(.3f);
        float remaining=boss.VulnerableRemaining;
        boss.TakeDamage(new DamageInfo(1000f,DamageSource.PlayerOwned));
        Assert.That(boss.IsVulnerable,Is.True); Assert.That(boss.VulnerableRemaining,Is.EqualTo(remaining).Within(.05f));
        Assert.That(boss.CurrentHp,Is.EqualTo(250f));
    }

    [UnityTest]
    public IEnumerator StareWindowsDeduplicateAttackSignalsAndDoNotLeakIntoOtherPatterns()
    {
        data.StareDuration=.35f;
        boss.ForcePatternForTest(BossPatternKind.AttackIntentBelief);
        yield return Until(()=>boss.ConditionRemaining>0f);
        yield return Until(()=>!boss.IsPatternRunning);
        Assert.That(boss.CurrentBelief,Is.EqualTo(80f)); Assert.That(boss.ActiveBeliefPhrase,Is.Empty);
        var events=player.GetComponent<PlayerCombatEvents>();
        boss.ForcePatternForTest(BossPatternKind.AttackIntentBelief); yield return Until(()=>boss.ConditionRemaining>0f);
        events.PublishAttackAttempt(boss.gameObject); yield return Until(()=>!boss.IsPatternRunning);
        Assert.That(boss.CurrentBelief,Is.EqualTo(80f));
        boss.ForcePhaseTwoForTest(); data.AttackSignalMode=BossAttackSignalMode.Either;
        boss.ForcePatternForTest(BossPatternKind.NoAttackBelief); yield return Until(()=>boss.ConditionRemaining>0f);
        var id=events.PublishAttackAttempt(boss.gameObject); events.PublishHitConfirmed(id,boss.gameObject,10f);
        events.PublishAttackAttempt(boss.gameObject);
        Assert.That(boss.CurrentBelief,Is.EqualTo(60f)); Assert.That(boss.CurrentHp,Is.EqualTo(250f),"A notification never applies damage a second time.");
        int before=Count(BossAttackKind.Melee); yield return Until(()=>!boss.IsPatternRunning);
        Assert.That(Count(BossAttackKind.Melee),Is.EqualTo(before+1));
    }

    [UnityTest]
    public IEnumerator PhaseTwoStareCompletesTheFourHitFollowupAndTestHitAppliesOnce()
    {
        data.StareDuration=.2f;
        data.PhaseTwoPool=new[] { new BossPatternWeight(BossPatternKind.MeleeCombo,1f,.5f) };
        PutPlayer((Vector2)boss.transform.position+Vector2.right*8f);
        boss.ForcePatternForTest(BossPatternKind.AttackAndDodgeBelief);
        yield return Until(()=>boss.ActiveBossAttackId>0);
        Assert.That(boss.ActiveBeliefPhrase,Is.Not.Empty);
        yield return Until(()=>!boss.IsPatternRunning);
        Assert.That(Count(BossAttackKind.Melee),Is.EqualTo(4));
        var hitIndices = new List<int>();
        foreach (BossAttackRecord record in boss.AttackHistory)
            if (record.Kind == BossAttackKind.Melee)
            {
                hitIndices.Add(record.HitIndex);
                Assert.That(record.EndReason,Is.EqualTo(BossAttackEndReason.Completed));
                Assert.That(record.Belief,Is.EqualTo(BossBeliefKind.AttackAndDodge));
            }
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, hitIndices);
        Assert.That(boss.ActiveAttacks.Count,Is.Zero);
        Assert.That(Object.FindFirstObjectByType<DangerIndicator>(),Is.Null);
        Assert.That(Last.Belief,Is.EqualTo(BossBeliefKind.AttackAndDodge));
        var events=player.GetComponent<PlayerCombatEvents>(); var id=events.PublishAttackAttempt(boss.gameObject);
        float hp=boss.CurrentHp;
        Assert.That(events.TryApplyHitAndPublish(id,boss.gameObject,10f),Is.True);
        events.PublishHitConfirmed(id,boss.gameObject,10f);
        Assert.That(events.TryApplyHitAndPublish(id,boss.gameObject,10f),Is.False);
        Assert.That(boss.CurrentHp,Is.EqualTo(hp-6f));
    }

    [UnityTest]
    public IEnumerator RestartingPhaseTwoDuringFirstWarningCancelsOldHitAndCompletesNewCombo()
    {
        data.StareDuration=.2f;
        data.PhaseTwoPool=new[] { new BossPatternWeight(BossPatternKind.MeleeCombo,1f,.5f) };
        PutPlayer((Vector2)boss.transform.position+Vector2.right*8f);
        boss.ForcePatternForTest(BossPatternKind.AttackAndDodgeBelief);
        yield return Until(()=>boss.ActiveBossAttackId>0);
        var interrupted = new List<BossAttackRecord>(boss.ActiveAttacks)[0];
        Assert.That(interrupted.HitIndex,Is.EqualTo(1));
        Assert.That(interrupted.Activated,Is.False);

        boss.ForcePatternForTest(BossPatternKind.AttackAndDodgeBelief);
        Assert.That(interrupted.EndReason,Is.EqualTo(BossAttackEndReason.Cancelled));
        Assert.That(Object.FindFirstObjectByType<DangerIndicator>(),Is.Null);
        int restartedPattern = boss.PatternId;
        yield return Until(()=>!boss.IsPatternRunning);

        var completedHits = new List<int>();
        foreach (BossAttackRecord record in boss.AttackHistory)
            if (record.PatternId == restartedPattern)
            {
                Assert.That(record.EndReason,Is.EqualTo(BossAttackEndReason.Completed));
                completedHits.Add(record.HitIndex);
            }
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, completedHits);
        Assert.That(boss.ActiveAttacks.Count,Is.Zero);
        Assert.That(Object.FindFirstObjectByType<DangerIndicator>(),Is.Null);
    }

    private void PutBodyCenter(Vector2 center)
    {
        Vector2 offset = (Vector2)player.BodyCollider.bounds.center - (Vector2)player.transform.position;
        PutPlayer(center - offset);
    }
    private IEnumerator BeginCloud(float deadline = .4f)
    {
        data.CloudWarning = .1f; data.CloudDuration = deadline; data.LightningDuration = .3f;
        PutPlayer(new Vector2(-4f,-2.33f));
        boss.ForcePatternForTest(BossPatternKind.CloudBelief);
        yield return Until(()=>Object.FindFirstObjectByType<BossCloudLightning>() != null);
    }

    [UnityTest]
    public IEnumerator CloudShelterProtectsWholeBodyBelowWhiteCloudWithoutExtraUi()
    {
        yield return BeginCloud();
        var lightning = Object.FindFirstObjectByType<BossCloudLightning>();
        var clouds = Object.FindObjectsByType<BossCloudPlatform>(FindObjectsSortMode.None);
        Assert.That(clouds.Length, Is.EqualTo(2));
        foreach (var cloud in clouds)
        {
            Assert.That(cloud.GetComponent<BoxCollider2D>().isTrigger, Is.True);
            Assert.That(cloud.GetComponent<PlatformEffector2D>(), Is.Null);
            Bounds visible = cloud.GetComponent<SpriteRenderer>().bounds;
            Assert.That(visible.size.x, Is.EqualTo(cloud.IsWhite ? data.CloudSize.x : data.StormCloudSize.x).Within(.001f));
            if (cloud.IsWhite)
            {
                Assert.That(visible.min.x, Is.EqualTo(lightning.Shelter.Center.x-lightning.Shelter.Size.x*.5f).Within(.001f));
                Assert.That(visible.max.x, Is.EqualTo(lightning.Shelter.Center.x+lightning.Shelter.Size.x*.5f).Within(.001f));
                Assert.That(visible.min.y, Is.EqualTo(lightning.Shelter.Center.y+lightning.Shelter.Size.y*.5f).Within(.001f));
            }
            Assert.That(cloud.GetComponentsInChildren<Canvas>(true), Is.Empty);
            Assert.That(cloud.GetComponentsInChildren<LineRenderer>(true), Is.Empty, "No cyan shelter outline before the strike.");
            Assert.That(cloud.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(1), "Only the cloud sprite; no protection fill.");
        }
        Assert.That(Object.FindObjectsByType<DangerIndicator>(FindObjectsSortMode.None), Is.Not.Empty);
        PutBodyCenter(lightning.Shelter.Center);
        yield return new WaitForSeconds(.15f);
        Assert.That(player.CurrentHp, Is.EqualTo(100f)); Assert.That(lightning.HasStruck, Is.False);
        Assert.That(boss.CueHint, Does.Contain("흰 구름 바로 아래"));
        yield return Until(()=>Count(BossAttackKind.Lightning)>0);
        Assert.That(lightning.HasStruck, Is.True);
        Assert.That(Object.FindObjectsByType<DangerIndicator>(FindObjectsSortMode.None), Is.Empty, "The warning must disappear on the lightning frame.");
        Assert.That(lightning.GetComponentsInChildren<LineRenderer>().Length, Is.GreaterThan(0));
        Assert.That(lightning.GetComponentsInChildren<Canvas>(true), Is.Empty);
        Assert.That(Last.Outcome, Is.EqualTo(AttackOutcome.EvadedByPlayer));
        Assert.That(boss.CurrentBelief, Is.EqualTo(80f)); Assert.That(player.CurrentHp, Is.EqualTo(100f));
        Assert.That(Object.FindFirstObjectByType<BossTimedDamageHazard>(), Is.Null);
        yield return Until(()=>!boss.IsPatternRunning);
        Assert.That(Object.FindFirstObjectByType<BossCloudLightning>(), Is.Null);
        Assert.That(Object.FindObjectsByType<BossCloudPlatform>(FindObjectsSortMode.None), Is.Empty);
    }

    [UnityTest]
    public IEnumerator WhiteCloudRandomizesEachCastInsideStormAndKeepsShelterUnderIt()
    {
        var randomState = UnityEngine.Random.state;
        var positions = new HashSet<int>();
        try
        {
            UnityEngine.Random.InitState(72851);
            for (int i = 0; i < 12; i++)
            {
                yield return BeginCloud();
                var lightning = Object.FindFirstObjectByType<BossCloudLightning>();
                BossCloudPlatform white = null, black = null;
                foreach (var cloud in Object.FindObjectsByType<BossCloudPlatform>(FindObjectsSortMode.None))
                    if (cloud.IsWhite) white = cloud; else black = cloud;
                Bounds storm = black.GetComponent<SpriteRenderer>().bounds;
                Bounds shelterCloud = white.GetComponent<SpriteRenderer>().bounds;
                Assert.That(shelterCloud.min.x, Is.GreaterThanOrEqualTo(storm.min.x+data.CloudEdgePadding-.001f));
                Assert.That(shelterCloud.max.x, Is.LessThanOrEqualTo(storm.max.x-data.CloudEdgePadding+.001f));
                Assert.That(lightning.Shelter.Center.x, Is.EqualTo(shelterCloud.center.x).Within(.001f));
                positions.Add(Mathf.RoundToInt(shelterCloud.center.x * 1000f));
                boss.PauseAndCancelForTest();
                yield return null;
            }
            Assert.That(positions.Count, Is.GreaterThan(4), "Repeated casts must sample new shelter positions.");
        }
        finally { UnityEngine.Random.state = randomState; }
    }

    [UnityTest]
    public IEnumerator LeavingShelterBeforeDeadlineHitsOnceAndLateEntryDoesNotUndoStrike()
    {
        yield return BeginCloud();
        var lightning = Object.FindFirstObjectByType<BossCloudLightning>();
        PutBodyCenter(lightning.Shelter.Center);
        yield return new WaitForSeconds(.15f);
        PutBodyCenter(lightning.Shelter.Center + Vector2.left * 2f);
        Assert.That(lightning.ContainsPlayer(), Is.False);
        yield return Until(()=>Count(BossAttackKind.Lightning)>0);
        Assert.That(player.CurrentHp, Is.EqualTo(100f-data.LightningDamage));
        Assert.That(Last.Outcome, Is.EqualTo(AttackOutcome.Hit));
        Assert.That(boss.CurrentBelief, Is.EqualTo(100f));
        PutBodyCenter(lightning.Shelter.Center);
        yield return Until(()=>!boss.IsPatternRunning);
        Assert.That(player.CurrentHp, Is.EqualTo(100f-data.LightningDamage));
        Assert.That(Count(BossAttackKind.Lightning), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator ShelterSideBoundaryUsesColliderEdgeRatherThanTransformCenter()
    {
        for (int i = 0; i < 2; i++)
        {
            yield return BeginCloud(.25f);
            var lightning = Object.FindFirstObjectByType<BossCloudLightning>();
            float centerX = lightning.Shelter.Center.x + lightning.Shelter.Size.x*.5f - player.BodyCollider.bounds.extents.x;
            PutBodyCenter(new Vector2(centerX + (i == 0 ? 0f : .02f),lightning.Shelter.Center.y));
            Assert.That(lightning.ContainsPlayer(), Is.EqualTo(i == 0));
            int before = Count(BossAttackKind.Lightning);
            yield return Until(()=>Count(BossAttackKind.Lightning)>before);
            Assert.That(player.CurrentHp, Is.EqualTo(i == 0 ? 100f : 100f-data.LightningDamage));
            yield return Until(()=>!boss.IsPatternRunning);
        }
    }

    [UnityTest]
    public IEnumerator OutsideStormAndAboveWhiteCloudAreNotShelterSuccesses()
    {
        yield return BeginCloud(.25f);
        var lightning = Object.FindFirstObjectByType<BossCloudLightning>();
        PutBodyCenter(lightning.Shelter.Center + Vector2.left * data.StormCloudSize.x);
        yield return Until(()=>Count(BossAttackKind.Lightning)>0);
        Assert.That(player.CurrentHp, Is.EqualTo(100f)); Assert.That(boss.CurrentBelief, Is.EqualTo(100f));
        yield return Until(()=>!boss.IsPatternRunning);
        yield return BeginCloud(.25f);
        lightning = Object.FindFirstObjectByType<BossCloudLightning>();
        PutBodyCenter(new Vector2(lightning.Shelter.Center.x,lightning.Shelter.Center.y + lightning.Shelter.Size.y*.5f + .9f));
        int before = Count(BossAttackKind.Lightning);
        yield return Until(()=>Count(BossAttackKind.Lightning)>before);
        Assert.That(player.CurrentHp, Is.EqualTo(100f-data.LightningDamage));
        Assert.That(boss.CurrentBelief, Is.EqualTo(100f));
    }

    private IEnumerator AssertCloudCancellation(Action cancel)
    {
        yield return BeginCloud(.25f);
        var record = new List<BossAttackRecord>(boss.ActiveAttacks)[0];
        cancel();
        float hp = player.CurrentHp;
        yield return new WaitForSeconds(.8f);
        Assert.That(record.Outcome, Is.EqualTo(AttackOutcome.Cancelled));
        Assert.That(player.CurrentHp, Is.EqualTo(hp));
        Assert.That(Object.FindFirstObjectByType<BossCloudLightning>(), Is.Null);
        Assert.That(Object.FindObjectsByType<BossCloudPlatform>(FindObjectsSortMode.None), Is.Empty);
        Assert.That(Object.FindFirstObjectByType<DangerIndicator>(), Is.Null);
    }
    [UnityTest] public IEnumerator CloudPatternCancelHasNoDelayedStrike() { yield return AssertCloudCancellation(()=>boss.PauseAndCancelForTest()); }
    [UnityTest] public IEnumerator CloudBossDisableHasNoDelayedStrike() { yield return AssertCloudCancellation(()=>boss.gameObject.SetActive(false)); }
    [UnityTest] public IEnumerator CloudPhaseChangeHasNoDelayedStrike() { yield return AssertCloudCancellation(()=>boss.ForcePhaseTwoForTest()); }
    [UnityTest] public IEnumerator CloudVulnerabilityHasNoDelayedStrike() { yield return AssertCloudCancellation(()=>boss.ReduceBeliefForTest(100f)); }
    [UnityTest] public IEnumerator CloudPlayerDeathHasNoDelayedStrike() { yield return AssertCloudCancellation(()=>player.TakeDamage(new DamageInfo(1000f,DamageSource.Environment))); }
    [UnityTest] public IEnumerator CloudBossDeathHasNoDelayedStrike()
    {
        yield return AssertCloudCancellation(()=>{ boss.ForcePhaseTwoForTest(); boss.TakeDamage(new DamageInfo(10000f,DamageSource.PlayerOwned)); });
    }
    [UnityTest] public IEnumerator CloudSceneUnloadRemovesOwnedEffectsBeforeDeadline()
    {
        yield return BeginCloud(.25f);
        Scene scene = boss.gameObject.scene;
        Scene replacement = SceneManager.CreateScene("Cloud unload destination");
        SceneManager.SetActiveScene(replacement);
        yield return SceneManager.UnloadSceneAsync(scene);
        yield return new WaitForSeconds(.8f);
        Assert.That(Object.FindFirstObjectByType<BossCloudLightning>(), Is.Null);
        Assert.That(Object.FindObjectsByType<BossCloudPlatform>(FindObjectsSortMode.None), Is.Empty);
        Assert.That(Object.FindFirstObjectByType<DangerIndicator>(), Is.Null);
        boss = null; player = null;
    }

    [UnityTest]
    public IEnumerator CancellingVisibleLightningRejectsOldRecordAndHasNoResidualDamage()
    {
        yield return BeginCloud(.25f);
        var lightning = Object.FindFirstObjectByType<BossCloudLightning>();
        yield return Until(()=>lightning.HasStruck);
        var record = Last;
        boss.PauseAndCancelForTest();
        float hp = player.CurrentHp;
        Assert.That(boss.TryHit(record), Is.False);
        yield return new WaitForSeconds(.8f);
        Assert.That(player.CurrentHp, Is.EqualTo(hp));
        Assert.That(Object.FindFirstObjectByType<BossCloudLightning>(), Is.Null);
        Assert.That(Object.FindFirstObjectByType<DangerIndicator>(), Is.Null);
    }

    [UnityTest]
    public IEnumerator VeryFastGroundWaveSweepsThinPlayerAndSharedRecordPreventsDoubleDamage()
    {
        data.WaveSpeed = 200f;
        PutPlayer(new Vector2(-2f,-2.33f));
        var body = (CapsuleCollider2D)player.BodyCollider;
        body.size = new Vector2(.08f,body.size.y);
        Physics2D.SyncTransforms();
        boss.ForcePatternForTest(BossPatternKind.ShockwaveBelief);
        yield return Until(()=>Count(BossAttackKind.Shockwave)>0,5f);
        Assert.That(Last.Outcome, Is.EqualTo(AttackOutcome.Hit));
        Assert.That(player.CurrentHp, Is.EqualTo(100f-data.WaveDamage));
        Assert.That(boss.TryHit(Last), Is.False);
        yield return Until(()=>!boss.IsPatternRunning);
        Assert.That(player.CurrentHp, Is.EqualTo(100f-data.WaveDamage));
    }

    [UnityTest]
    public IEnumerator GroundWavesHitOnGroundAndARealJumpEvadesOnceForBothBranches()
    {
        PutPlayer(new Vector2(-2f,-2.33f));
        boss.ForcePatternForTest(BossPatternKind.ShockwaveBelief);
        yield return Until(()=>Count(BossAttackKind.Shockwave)>0,7f);
        Assert.That(Last.Outcome,Is.EqualTo(AttackOutcome.Hit)); Assert.That(player.CurrentHp,Is.EqualTo(84f));
        Assert.That(boss.CurrentBelief,Is.EqualTo(100f));
        player.ResetHealth();
        PutPlayer(new Vector2(-2f,-2.33f),false);
        boss.ForcePatternForTest(BossPatternKind.ShockwaveBelief);
        yield return Until(()=>Object.FindObjectsByType<BossGroundWave>(FindObjectsSortMode.None).Length==2);
        // Wait until the wave is close enough to require the existing jump, then leave its shown height.
        yield return new WaitForSeconds(.08f); playerBody.linearVelocity=Vector2.up*14f;
        int before=Count(BossAttackKind.Shockwave);
        yield return Until(()=>Count(BossAttackKind.Shockwave)>before,4f);
        Assert.That(Last.Outcome,Is.EqualTo(AttackOutcome.EvadedByPlayer));
        Assert.That(player.CurrentHp,Is.EqualTo(100f)); Assert.That(boss.CurrentBelief,Is.EqualTo(80f));
    }

    [UnityTest]
    public IEnumerator BurnPreparationThenTenSecondWindowRunsSeveralRegularAttacksAndRefreshesBurn()
    {
        // Keep the production 5s/10s times; force repeatable projectiles within the window.
        data.PhaseOnePool=new[] { new BossPatternWeight(BossPatternKind.Projectile,1f,.5f) };
        data.ProjectileSpeed=30f;
        PutPlayer(new Vector2(-7f,-2.33f));
        boss.ForcePatternForTest(BossPatternKind.BurnBelief);
        yield return new WaitForSeconds(4.8f);
        Assert.That(boss.ActiveBossAttackId,Is.Zero); Assert.That(boss.IsBurning,Is.False);
        yield return Until(()=>boss.IsBurning,1f);
        float began=Time.time;
        yield return Until(()=>Count(BossAttackKind.Projectile)>0,3f);
        var burn=player.GetComponent<BossBurnEffect>(); Assert.That(burn,Is.Not.Null); Assert.That(burn.IsBurning,Is.True);
        burn.Apply(boss,data); float remaining=burn.Remaining;
        Assert.That(remaining,Is.EqualTo(5f).Within(.05f));
        // Escape remaining shots while the same belief keeps selecting regular attacks.
        PutPlayer(new Vector2(-7f,4f));
        yield return Until(()=>!boss.IsBurning,12f);
        Assert.That(Time.time-began,Is.EqualTo(10f).Within(.15f));
        yield return Until(()=>!boss.IsPatternRunning,4f);
        Assert.That(Count(BossAttackKind.Projectile),Is.GreaterThanOrEqualTo(3));
        Assert.That(boss.PatternUseCount(BossPatternKind.Projectile),Is.GreaterThanOrEqualTo(3));
        Assert.That(boss.ActiveBeliefPhrase,Is.Empty);
    }

    [UnityTest]
    public IEnumerator BurnTicksRespectInvulnerabilityAndResetDisableAndDeathRemoveOwnedEffects()
    {
        var burn=player.gameObject.AddComponent<BossBurnEffect>(); data.BurnTickInterval=.2f; data.BurnDuration=.7f;
        player.TakeDamage(new DamageInfo(1f,DamageSource.Environment)); burn.Apply(boss,data);
        yield return new WaitForSeconds(.65f);
        Assert.That(burn.TickAttempts,Is.GreaterThanOrEqualTo(3)); Assert.That(burn.TicksAccepted,Is.Zero);
        boss.GetComponent<BossHazardDirector>().ScheduleSpike(new Vector2(-4f,-2f));
        Assert.That(boss.ActiveBossAttackId,Is.GreaterThan(0));
        PutPlayer(new Vector2(8f,4f)); boss.Body.position=new Vector2(6f,0f);
        boss.ResetEncounterForTest(); yield return null;
        Assert.That(burn.IsBurning,Is.False); Assert.That(player.CurrentHp,Is.EqualTo(100f));
        Assert.That(player.transform.position.x,Is.EqualTo(-4.21f).Within(.01f)); Assert.That(boss.transform.position.x,Is.EqualTo(2f).Within(.01f));
        Assert.That(Object.FindFirstObjectByType<BossTimedDamageHazard>(),Is.Null);
        boss.GetComponent<BossHazardDirector>().ScheduleSpike(new Vector2(-4f,-2f));
        burn.Apply(boss,data); boss.gameObject.SetActive(false); yield return null;
        Assert.That(burn.IsBurning,Is.False); Assert.That(Object.FindFirstObjectByType<DangerIndicator>(),Is.Null);
        Assert.That(Object.FindFirstObjectByType<BossTimedDamageHazard>(),Is.Null);
        boss.gameObject.SetActive(true); boss.BeginEncounter(); boss.ForcePhaseTwoForTest(); burn.Apply(boss,data);
        boss.TakeDamage(new DamageInfo(10000f,DamageSource.PlayerOwned)); yield return null;
        Assert.That(boss.IsDead,Is.True); Assert.That(burn.IsBurning,Is.False); Assert.That(boss.ActiveBossAttackId,Is.Zero);
    }

    [UnityTest]
    public IEnumerator FinalAreaTriggerStartsOnceAndIgnoresUnrelatedColliders()
    {
        Object.FindFirstObjectByType<BossEncounterTrigger>().enabled = false;
        boss.gameObject.SetActive(false); boss.gameObject.SetActive(true);
        var root=new GameObject("Final area test trigger"); fixtures.Add(root);
        var trigger=root.AddComponent<BoxCollider2D>(); trigger.isTrigger=true; trigger.size=new Vector2(3f,5f);
        root.transform.position=new Vector2(-12f,-1f);
        var entry=root.AddComponent<BossEncounterTrigger>(); Set(entry,"boss",boss);
        yield return new WaitForFixedUpdate(); Assert.That(boss.EncounterStarted,Is.False);
        PutPlayer(new Vector2(-12f,-2.33f)); yield return Until(()=>boss.EncounterStarted,1f);
        int token=boss.Generation;
        PutPlayer(new Vector2(-7f,-2.33f)); yield return new WaitForFixedUpdate();
        PutPlayer(new Vector2(-12f,-2.33f)); yield return new WaitForFixedUpdate();
        Assert.That(boss.Generation,Is.EqualTo(token));
    }

    [UnityTest]
    public IEnumerator VulnerabilityCancelsScheduledSpikesAndRejectsPreviousGenerationPlayerHits()
    {
        PutPlayer(new Vector2(-4f,-2.33f));
        var events=player.GetComponent<PlayerCombatEvents>();
        var oldAttempt=events.PublishAttackAttempt(boss.gameObject);
        Assert.That(boss.GetComponent<BossHazardDirector>().ScheduleSpike(new Vector2(-4f,-1f)),Is.True);
        var record=new List<BossAttackRecord>(boss.ActiveAttacks)[0];
        boss.ReduceBeliefForTest(100f);
        yield return new WaitForSeconds(data.SpikeWarning+.2f);
        Assert.That(player.CurrentHp,Is.EqualTo(100f)); Assert.That(record.Outcome,Is.EqualTo(AttackOutcome.Cancelled));
        Assert.That(Object.FindFirstObjectByType<BossTimedDamageHazard>(),Is.Null);
        Assert.That(events.TryApplyHitAndPublish(oldAttempt,boss.gameObject,100f),Is.False);
        yield return Until(()=>!boss.IsVulnerable,boss.VulnerableRemaining+.25f);
        Assert.That(boss.CurrentBelief,Is.EqualTo(100f)); Assert.That(boss.CurrentHp,Is.EqualTo(500f));
    }

    [UnityTest]
    public IEnumerator GroundWaveFollowsSmallDropWithinItsLockedWarningAndStopsAtAnUnwarnedGroundChange()
    {
        var step=new GameObject("Small drop fixture"); fixtures.Add(step); step.layer=6;
        step.transform.position=new Vector2(-1f,-3.4f); step.AddComponent<BoxCollider2D>().size=new Vector2(2f,.5f);
        Physics2D.SyncTransforms(); PutPlayer(new Vector2(-10f,3f));
        var path=BossCombatPhysics.TraceGroundPath(new Vector2(-.4f,-3.15f),1,1.2f,boss);
        Assert.That(path.Blocked,Is.False); Assert.That(path.Warnings.Count,Is.GreaterThan(1));
        var record=boss.CreateAttack(BossAttackKind.Shockwave,0,path.Warnings[0],16f,BossBeliefKind.None,false);
        for(int i=1;i<path.Warnings.Count;i++) record.AddWarningGeometry(path.Warnings[i]);
        record.Activate(); var wave=boss.SpawnEffect(data.WavePrefab); wave.Initialize(boss,record,path);
        while(!wave.Finished)
        {
            var bounds=wave.GetComponent<SpriteRenderer>().bounds;
            Assert.That(path.Encloses(new AttackGeometry(bounds.center,bounds.size)),Is.True);
            yield return new WaitForFixedUpdate();
        }
        Assert.That(wave.Blocked,Is.False);
        Assert.That(wave.transform.position.y-data.WaveSize.y*.5f,Is.EqualTo(-3.33f).Within(.03f));
        boss.CompleteAttack(record,BossAttackEndReason.Completed); boss.DestroyEffect(wave.gameObject);
        // Terrain introduced after warning cannot extend the damage above that warning.
        path=BossCombatPhysics.TraceGroundPath(new Vector2(2f,-3.35f),1,2f,boss);
        var raised=new GameObject("Late ground change"); fixtures.Add(raised); raised.layer=6;
        raised.transform.position=new Vector2(3f,-3.1f); raised.AddComponent<BoxCollider2D>().size=new Vector2(.5f,.1f);
        Physics2D.SyncTransforms();
        record=boss.CreateAttack(BossAttackKind.Shockwave,0,path.Warnings[0],16f,BossBeliefKind.None,false); record.Activate();
        wave=boss.SpawnEffect(data.WavePrefab); wave.Initialize(boss,record,path);
        yield return Until(()=>wave.Finished,2f); Assert.That(wave.Blocked,Is.True);
        boss.CompleteAttack(record,BossAttackEndReason.Wall); boss.DestroyEffect(wave.gameObject);
    }

    [UnityTest]
    public IEnumerator GameplayUiIsDefaultAndDebugTogglePreservesHazardsAndEncounter()
    {
        var tools=Object.FindFirstObjectByType<BossTestBootstrap>();
        var hud=boss.GetComponent<BossCombatHud>();
        Assert.That(tools.ShowDebugPanel,Is.False);
        Assert.That(hud.DebugVisible,Is.False); Assert.That(hud.DebugCanvas.gameObject.activeInHierarchy,Is.False);
        Assert.That(hud.GameplayHud.HealthCanvas.enabled,Is.True);
        Assert.That(hud.GameplayHud.BeliefCanvas.renderMode,Is.EqualTo(RenderMode.WorldSpace));
        PutPlayer(new Vector2(-4f,-2.33f)); boss.ForcePatternForTest(BossPatternKind.CloudBelief);
        yield return Until(()=>Object.FindObjectsByType<BossCloudPlatform>(FindObjectsSortMode.None).Length==2);
        Assert.That(boss.GetComponent<BossHazardDirector>().ScheduleSpike(new Vector2(-8f,-1f)),Is.True);
        yield return null;
        Assert.That(Object.FindObjectsByType<BossWorldCue>(FindObjectsSortMode.None),Is.Empty,"Hidden debug annotations should not allocate UI.");
        var warning=Object.FindFirstObjectByType<DangerIndicator>().GetComponent<SpriteRenderer>();
        Assert.That(warning.enabled,Is.True,"Gameplay must retain the actual danger envelope.");
        int generation=boss.Generation, pattern=boss.PatternId, attack=boss.ActiveBossAttackId;
        float health=player.CurrentHp, belief=boss.CurrentBelief;
        tools.ShowDebugPanel=true; yield return null;
        Assert.That(hud.DebugVisible,Is.True); Assert.That(hud.DebugCanvas.gameObject.activeInHierarchy,Is.True);
        Assert.That(hud.GameplayHud.HealthCanvas.enabled,Is.False,"Debug mode avoids duplicate gameplay bars.");
        Assert.That(boss.transform.Find("Gameplay HUD/Player gameplay HUD"),Is.Null);
        Assert.That(Object.FindFirstObjectByType<DoctrineHud>(),Is.Not.Null);
        var visibleCues=Object.FindObjectsByType<BossWorldCue>(FindObjectsSortMode.None);
        Assert.That(visibleCues.Length,Is.GreaterThanOrEqualTo(1),"Only the independently scheduled spike has debug world text.");
        foreach(var cue in visibleCues) Assert.That(cue.GetComponent<Canvas>().enabled,Is.True);
        tools.ShowDebugPanel=false; yield return null;
        Assert.That(hud.DebugVisible,Is.False); Assert.That(warning.enabled,Is.True);
        Assert.That(hud.GameplayHud.HealthCanvas.enabled,Is.True);
        Assert.That(boss.Generation,Is.EqualTo(generation)); Assert.That(boss.PatternId,Is.EqualTo(pattern));
        Assert.That(boss.ActiveBossAttackId,Is.EqualTo(attack));
        Assert.That(player.CurrentHp,Is.EqualTo(health)); Assert.That(boss.CurrentBelief,Is.EqualTo(belief));
        boss.SetHpForTest(400f); boss.ReduceBeliefForTest(20f); yield return null;
        foreach(var image in boss.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            if(image.name=="EnemyHealthGaugeFill" || image.name=="BeliefGaugeFill")
                Assert.That(image.fillAmount,Is.EqualTo(.8f).Within(.001f));
        // The playable UI and encounter remain valid without any development HUD.
        tools.enabled=false; Object.Destroy(hud.DebugCanvas.gameObject); yield return null;
        Assert.That(hud.IsConfigured,Is.True); Assert.That(boss.ValidateEncounter(),Is.Empty);
        boss.ResetEncounterForTest(); boss.AutomaticPatterns=false; yield return null;
        Assert.That(boss.EncounterStarted,Is.True); Assert.That(hud.GameplayHud.HealthCanvas.enabled,Is.True);
    }

    [UnityTest]
    public IEnumerator SharedPlayerContextDeduplicatesRawAndAdapterHitsAndPublishesOneBeliefSignal()
    {
        data.AttackSignalMode=BossAttackSignalMode.HitConfirmed;
        boss.ForcePatternForTest(BossPatternKind.NoAttackBelief);
        yield return Until(()=>boss.ConditionRemaining>0f);
        float hp=boss.CurrentHp, belief=boss.CurrentBelief;
        using(var attack=new PlayerAttackContext(player.gameObject))
        {
            DamageInfo hit=attack.Damage(10f);
            Assert.That(boss.TakeDamage(hit),Is.True);
            Assert.That(boss.TakeDamage(hit),Is.False);
            Assert.That(attack.TryHit(boss,10f),Is.False);
            Assert.That(attack.TryHit(boss,10f,1),Is.True);
            Assert.That(boss.CurrentHp,Is.EqualTo(hp-20f*data.MaintainedDamageMultiplier).Within(.001f));
            Assert.That(boss.CurrentBelief,Is.EqualTo(belief-data.BeliefReduction));
        }
        var stale=new PlayerAttackContext(player.gameObject);
        Assert.That(stale.Begin(boss),Is.True);
        boss.PauseAndCancelForTest(); hp=boss.CurrentHp;
        Assert.That(stale.TryHit(boss,100f),Is.False);
        Assert.That(boss.TakeDamage(stale.Damage(100f)),Is.False);
        Assert.That(boss.CurrentHp,Is.EqualTo(hp)); stale.Dispose();
        var finished=player.GetComponent<PlayerCombatEvents>().PublishAttackAttempt(boss.gameObject);
        finished.Dispose(); Assert.That(finished.TryHit(boss,10f),Is.False);
    }

    [UnityTest]
    public IEnumerator AfterimagesReuseInstancesAndEncounterCancellationReturnsActiveTrails()
    {
        var emitter=boss.GetComponent<CombatAfterimageEmitter>();
        int created=emitter.CreatedCount;
        for(int i=0;i<24;i++)
        {
            for(int repeat=0;repeat<5;repeat++) emitter.Emit(data.DashPrefab,boss.BodyRenderer,20);
            yield return new WaitForSeconds(.06f);
        }
        Assert.That(emitter.CreatedCount-created,Is.LessThanOrEqualTo(6),"Repeated emissions should reuse short-lived instances.");
        emitter.Emit(data.DashPrefab,boss.BodyRenderer,20);
        Assert.That(emitter.ActiveCount,Is.GreaterThan(0));
        Assert.That(data.DashPrefab.GetComponentsInChildren<LineRenderer>(true),Is.Empty);
        boss.PauseAndCancelForTest(); Assert.That(emitter.ActiveCount,Is.Zero);
    }

    [UnityTest]
    public IEnumerator VisualCaptureActualPrefabWarningActivationCloudWaveAndBurn()
    {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
            Assert.Ignore("Run this capture test with graphics enabled.");
        Object.FindFirstObjectByType<BossTestBootstrap>().ShowDebugPanel=false;
        Camera.main.transform.position=new Vector3(0f,.2f,-10f); Camera.main.orthographicSize=5.5f;
        PutPlayer(new Vector2(-.2f,-2.33f));
        boss.ForcePatternForTest(BossPatternKind.MeleeCombo);
        yield return new WaitForSeconds(.25f); yield return Capture("01-melee-warning");
        yield return Until(()=>Object.FindObjectsByType<BossAttackVisual>(FindObjectsSortMode.None).Length>1,1f);
        yield return Capture("02-melee-active");
        boss.PauseAndCancelForTest(); player.ResetHealth();
        PutPlayer(new Vector2(-4f,-2.33f),false); boss.ForcePatternForTest(BossPatternKind.CloudBelief);
        yield return Until(()=>Object.FindObjectsByType<BossCloudPlatform>(FindObjectsSortMode.None).Length==2);
        yield return Capture("03-clouds");
        yield return Capture("17-clouds-1024x576",1024,576);
        Object.FindFirstObjectByType<BossTestBootstrap>().ShowDebugPanel=true;
        yield return Capture("18-debug-cloud-details");
        Object.FindFirstObjectByType<BossTestBootstrap>().ShowDebugPanel=false;
        var lightning = Object.FindFirstObjectByType<BossCloudLightning>();
        PutBodyCenter(lightning.Shelter.Center);
        yield return Until(()=>lightning.HasStruck,4f);
        yield return Capture("04-cloud-lightning-sheltered");
        yield return Capture("05-cloud-lightning-1024x576",1024,576);
        boss.PauseAndCancelForTest(); player.ResetHealth();
        PutPlayer(new Vector2(-3f,-2.33f)); boss.ForcePatternForTest(BossPatternKind.ShockwaveBelief);
        yield return Until(()=>Object.FindObjectsByType<BossGroundWave>(FindObjectsSortMode.None).Length==2);
        yield return new WaitForSeconds(.28f); yield return Capture("06-ground-waves");
        boss.ResetEncounterForTest(); boss.AutomaticPatterns=false;
        PutPlayer(new Vector2(-4.2f,-2.33f)); boss.ForcePatternForTest(BossPatternKind.Dash);
        yield return new WaitForSeconds(.25f); yield return Capture("12-dash-warning");
        boss.PauseAndCancelForTest(); player.ResetHealth();
        boss.ForcePatternForTest(BossPatternKind.Projectile);
        yield return new WaitForSeconds(.25f); yield return Capture("13-projectile-warning");
        boss.PauseAndCancelForTest();
        boss.ForcePatternForTest(BossPatternKind.AttackIntentBelief);
        yield return new WaitForSeconds(.25f); yield return Capture("14-stare-no-attack-goal");
        boss.PauseAndCancelForTest();
        boss.ForcePatternForTest(BossPatternKind.AttackAndDodgeBelief);
        yield return new WaitForSeconds(.25f); yield return Capture("15-stare-followup-goal");
        boss.PauseAndCancelForTest();
        boss.ForcePatternForTest(BossPatternKind.NoAttackBelief);
        yield return new WaitForSeconds(.25f); yield return Capture("16-stare-attack-goal");
        boss.ResetEncounterForTest(); boss.AutomaticPatterns=false; data.BurnPreparation=.25f;
        PutPlayer(new Vector2(-.2f,-2.33f));
        boss.ForcePatternForTest(BossPatternKind.BurnBelief);
        yield return Until(()=>boss.IsBurning);
        var burn=player.GetComponent<BossBurnEffect>(); if(burn==null) burn=player.gameObject.AddComponent<BossBurnEffect>();
        burn.Apply(boss,data); yield return null;
        yield return Capture("07-burn-status");
        boss.ReduceBeliefForTest(100f); yield return null; yield return Capture("08-vulnerable");
        boss.ResetEncounterForTest(); boss.AutomaticPatterns=false;
        PutPlayer(new Vector2(-4.2f,-2.33f));
        var tools=Object.FindFirstObjectByType<BossTestBootstrap>(); tools.ShowDebugPanel=true;
        yield return Capture("09-debug-panel-1600x900");
        foreach(var button in tools.DebugPanel.GetComponentsInChildren<UnityEngine.UI.Button>())
            if(button.GetComponentInChildren<UnityEngine.UI.Text>().text=="공격 진단 펼치기") button.onClick.Invoke();
        boss.ForcePatternForTest(BossPatternKind.MeleeCombo);
        yield return Capture("10-debug-panel-1024x576",1024,576);
        tools.DebugPanel.GetComponentInChildren<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition=0f;
        yield return Capture("11-debug-details-1024x576",1024,576);
    }

    private PlayerAttack CreateRealPlayerAttack(int steps = 1, float windup = .12f, float lunge = 0f)
    {
        var attack = player.gameObject.AddComponent<PlayerAttack>();
        var combo = new PlayerAttack.ComboStep[steps];
        for (int i = 0; i < steps; i++) combo[i] = new PlayerAttack.ComboStep
        {
            name = "Integration swing " + i, damage = 2f,
            hitboxSize = new Vector2(1.5f, 1.5f), hitboxOffset = Vector2.right,
            windupTime = windup, recoveryTime = .04f, comboWindow = .2f, lungeDistance = lunge
        };
        Set(attack, "comboSteps", combo);
        Set(attack, "finishHitStop", 0f);
        Set(player.GetComponent<PlayerMove>(), "isGrounded", true);
        return attack;
    }

    private static void AttackInput(PlayerAttack attack) => typeof(PlayerAttack)
        .GetMethod("OnAttackInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(attack, null);

    private void FacePlayer(float direction)
    {
        Vector3 scale = player.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        player.transform.localScale = scale;
    }

    [UnityTest]
    public IEnumerator RealPlayerAttackAttemptsRequireStartingRangeAndDirectionEvenWhenTheSwingMisses()
    {
        var attack = CreateRealPlayerAttack();
        var signals = new List<PlayerCombatSignal>();
        player.GetComponent<PlayerCombatEvents>().SignalPublished += value =>
        { if (value.Target == boss.gameObject) signals.Add(value); };
        float hp = boss.CurrentHp;
        PutPlayer((Vector2)boss.transform.position + Vector2.left * 10f); FacePlayer(1f);
        AttackInput(attack); yield return Until(() => !attack.IsAttacking);
        Assert.That(signals, Is.Empty, "A distant swing must not count as attacking this boss.");
        PutPlayer((Vector2)boss.transform.position + Vector2.left * 1.2f); FacePlayer(-1f);
        AttackInput(attack); yield return Until(() => !attack.IsAttacking);
        Assert.That(signals, Is.Empty, "A swing facing away must not count as attacking this boss.");

        data.StareDuration = 2f; data.AttackSignalMode = BossAttackSignalMode.Attempt;
        boss.ForcePatternForTest(BossPatternKind.NoAttackBelief);
        yield return Until(() => boss.ConditionRemaining > 0f);
        hp = boss.CurrentHp; // This phase-two pattern moves the fixture to its phase threshold.
        FacePlayer(1f); AttackInput(attack);
        yield return Until(() => signals.Count > 0);
        PutPlayer((Vector2)boss.transform.position + Vector2.left * 10f);
        yield return Until(() => !attack.IsAttacking);
        Assert.That(signals.Count, Is.EqualTo(1));
        Assert.That(signals[0].Type, Is.EqualTo(PlayerCombatSignalType.AttackAttempt));
        Assert.That(boss.CurrentHp, Is.EqualTo(hp));
        Assert.That(boss.CurrentBelief, Is.EqualTo(data.MaxBelief - data.BeliefReduction));
    }

    [UnityTest]
    public IEnumerator RealPlayerHitOnALateEntrantDoesNotInventAStartingAttackAttempt()
    {
        var attack = CreateRealPlayerAttack(windup: .25f);
        var signals = new List<PlayerCombatSignal>();
        player.GetComponent<PlayerCombatEvents>().SignalPublished += value =>
        { if (value.Target == boss.gameObject) signals.Add(value); };
        data.StareDuration = 2f; data.AttackSignalMode = BossAttackSignalMode.Attempt;
        boss.ForcePatternForTest(BossPatternKind.NoAttackBelief);
        yield return Until(() => boss.ConditionRemaining > 0f);
        float hp = boss.CurrentHp;
        PutPlayer((Vector2)boss.transform.position + Vector2.left * 10f); FacePlayer(1f);
        AttackInput(attack); yield return null;
        PutPlayer((Vector2)boss.transform.position + Vector2.left * 1.2f);
        yield return Until(() => !attack.IsAttacking);
        Assert.That(signals.Count, Is.EqualTo(1));
        Assert.That(signals[0].Type, Is.EqualTo(PlayerCombatSignalType.HitConfirmed));
        Assert.That(boss.CurrentHp, Is.EqualTo(hp - 2f * data.MaintainedDamageMultiplier).Within(.001f));
        Assert.That(boss.CurrentBelief, Is.EqualTo(data.MaxBelief));
    }

    [UnityTest]
    public IEnumerator RealFourHitComboUsesFourIdsAndDeduplicatesMultipleBossColliders()
    {
        var extra = new GameObject("Additional boss hit collider");
        extra.transform.SetParent(boss.transform, false);
        extra.layer = boss.gameObject.layer; extra.AddComponent<BoxCollider2D>().isTrigger = true;
        var attack = CreateRealPlayerAttack(steps: 4, windup: .04f);
        var attempts = new HashSet<long>(); var hits = new HashSet<long>();
        int confirmed = 0, doctrines = 0;
        player.GetComponent<PlayerCombatEvents>().SignalPublished += value =>
        {
            if (value.Target != boss.gameObject) return;
            if (value.Type == PlayerCombatSignalType.AttackAttempt) attempts.Add(value.AttackId);
            if (value.Type == PlayerCombatSignalType.HitConfirmed) { hits.Add(value.AttackId); confirmed++; }
        };
        Action<DoctrineEventData> listener = evt =>
        { if (evt.Condition == ConditionType.OnAttack && evt.SubjectObject == player.gameObject) doctrines++; };
        DoctrineEventBus.EventRaised += listener;
        try
        {
            PutPlayer((Vector2)boss.transform.position + Vector2.left * 1.2f); FacePlayer(1f);
            float hp = boss.CurrentHp;
            AttackInput(attack);
            float deadline = Time.time + 3f;
            while (attack.IsAttacking && Time.time < deadline) { AttackInput(attack); yield return null; }
            Assert.That(attack.IsAttacking, Is.False);
            Assert.That(attempts.Count, Is.EqualTo(4)); Assert.That(hits.Count, Is.EqualTo(4));
            CollectionAssert.AreEquivalent(attempts, hits);
            Assert.That(confirmed, Is.EqualTo(4)); Assert.That(doctrines, Is.EqualTo(4));
            Assert.That(boss.CurrentHp, Is.EqualTo(hp - 8f * data.MaintainedDamageMultiplier).Within(.001f));
        }
        finally { DoctrineEventBus.EventRaised -= listener; Object.Destroy(extra); }
    }

    [UnityTest]
    public IEnumerator PlayerSwingCancellationDisposesTheAttackAndNeverAppliesItsPendingHit()
    {
        var attack = CreateRealPlayerAttack(windup: .3f);
        PutPlayer((Vector2)boss.transform.position + Vector2.left * 1.2f); FacePlayer(1f);
        float hp = boss.CurrentHp;
        AttackInput(attack); yield return null;
        var context = (PlayerAttackContext)typeof(PlayerAttack).GetField("activeSwing",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(attack);
        Assert.That(context, Is.Not.Null);
        attack.enabled = false;
        Assert.That(context.TryHit(boss, 100f), Is.False);
        Assert.That(player.GetComponent<PlayerMove>().ControlLocked, Is.False);
        yield return new WaitForSeconds(.35f);
        Assert.That(boss.CurrentHp, Is.EqualTo(hp));
        Assert.That(attack.IsAttacking, Is.False);
    }

    [UnityTest]
    public IEnumerator PlayerSwingAttemptIncludesItsLungeReachAtTheStart()
    {
        var attack = CreateRealPlayerAttack(windup: .2f, lunge: 2f);
        int attempts = 0;
        player.GetComponent<PlayerCombatEvents>().SignalPublished += value =>
        { if (value.Target == boss.gameObject && value.Type == PlayerCombatSignalType.AttackAttempt) attempts++; };
        PutPlayer((Vector2)boss.transform.position + Vector2.left * 3f); FacePlayer(1f);
        AttackInput(attack); yield return null;
        Assert.That(attempts, Is.EqualTo(1), "The windup's reachable forward sweep belongs to this attack.");
        attack.CancelAttack();
    }

    private IEnumerator Capture(string filename,int width=1600,int height=900)
    {
        yield return null;
        Camera camera=Camera.main;
        var canvases=new List<Canvas>();
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if(canvas.renderMode!=RenderMode.WorldSpace) canvases.Add(canvas);
        var texture=new RenderTexture(width,height,24);
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        camera.targetTexture=texture;
        foreach(var canvas in canvases)
        { canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1f; }
        Canvas.ForceUpdateCanvases();
        Object.FindFirstObjectByType<BossTestBootstrap>().DebugPanel.RefreshLayout();
        yield return null;
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=texture;
        image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
        string directory=System.IO.Path.Combine(Application.dataPath,"../Logs/BossCombatVerification");
        System.IO.Directory.CreateDirectory(directory); System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,filename+".png"),image.EncodeToPNG());
        camera.targetTexture=null; RenderTexture.active=previous;
        foreach(var canvas in canvases) { canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null; }
        texture.Release(); Object.Destroy(texture); Object.Destroy(image);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        SceneManager.sceneLoaded-=PauseOnLoad;
        if(boss!=null) { boss.PauseAndCancelForTest(); boss.gameObject.SetActive(false); if(originalData!=null) Set(boss,"settings",originalData); }
        foreach(var fixture in fixtures) if(fixture!=null) Object.Destroy(fixture); fixtures.Clear();
        if(data!=null) Object.Destroy(data);
        yield return null;
    }
}
