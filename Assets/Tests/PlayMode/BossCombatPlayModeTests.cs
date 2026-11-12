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
        var load = (AsyncOperation)method.Invoke(null,new object[] {ProjectScenes.Boss,new LoadSceneParameters(LoadSceneMode.Single)});
        while (!load.isDone) yield return null;
        SceneManager.sceneLoaded -= PauseOnLoad;
        yield return null;
        boss = Object.FindFirstObjectByType<BossCombatController>();
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerHealth>();
        playerBody = player.GetComponent<Rigidbody2D>();
        player.GetComponent<PlayerMove>().enabled = false;
        // The Boss scene now waits for arena entry; put combat fixtures inside before preparing each case.
        Bounds entryBounds = Object.FindFirstObjectByType<BossEncounterTrigger>().GetComponent<Collider2D>().bounds;
        PutPlayer(new Vector2(entryBounds.min.x + player.BodyCollider.bounds.extents.x + .2f, -2.33f));
        yield return Until(() => boss.EncounterStarted);
        Assert.That(boss.EncounterStarted,Is.True,boss.StatusMessage);
        originalData = boss.Settings; data = Object.Instantiate(originalData); Set(boss,"settings",data);
        yield return new WaitForSeconds(.65f);
        boss.PauseAndCancelForTest(); player.ResetHealth();
    }
    private static void PauseOnLoad(Scene scene, LoadSceneMode mode)
    {
        var intro = Object.FindFirstObjectByType<CutsceneBossEncounterCoordinator>();
        if (intro != null) intro.enabled = false;
        var arena = Object.FindFirstObjectByType<BossEncounterTrigger>();
        if (arena != null) arena.AutoBeginEncounter = true;
        var controller = Object.FindFirstObjectByType<BossCombatController>();
        if (controller != null) controller.AutomaticPatterns = false;
    }
    private static void Set(object target,string field,object value)
    { target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value); }
    // An attack record registered on its own (no pattern, no effect objects), so reset and cancel paths have one to clear.
    private BossAttackRecord StartIndependentAttack(Vector2 position) =>
        boss.CreateAttack(BossAttackKind.Projectile, 0, new AttackGeometry(position, new Vector2(1.25f, .8f)),
            1f, BossBeliefKind.None, false);

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
    public IEnumerator HitReactionPausesDashThenResumesAndResetClearsIt()
    {
        PutPlayer((Vector2)boss.transform.position + Vector2.left * 6f);
        boss.ForcePatternForTest(BossPatternKind.Dash);
        yield return Until(() => new List<BossAttackRecord>(boss.ActiveAttacks).Exists(r => r.Kind == BossAttackKind.Dash && r.Activated));
        PutPlayer(new Vector2(boss.BodyCollider.bounds.center.x, boss.BodyCollider.bounds.max.y + 1.1f));
        float verticalVelocity = boss.Body.linearVelocity.y;
        using (var swing = new PlayerAttackContext(player.gameObject))
        {
            int hits = (int)typeof(PlayerAttack).GetMethod("DoDownFanHit", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(player.GetComponent<PlayerAttack>(), new object[] { swing, 1f });
            Assert.That(hits, Is.EqualTo(1));
        }
        Assert.That(boss.Body.linearVelocity.x, Is.Zero);
        Assert.That(boss.Body.linearVelocity.y, Is.EqualTo(verticalVelocity));
        yield return new WaitForFixedUpdate();
        float pausedX = boss.Body.position.x;
        for (int i = 0; i < 5; i++)
        {
            yield return new WaitForFixedUpdate();
            Assert.That(boss.Body.position.x, Is.EqualTo(pausedX).Within(.02f));
            Assert.That(boss.IsHitMovementLocked, Is.True);
        }
        yield return new WaitForSeconds(.3f);
        Assert.That(Mathf.Abs(boss.Body.position.x - pausedX), Is.GreaterThan(.1f));
        boss.HitReaction.Apply(1f, 2.8f, player.transform.position.x);
        boss.ResetEncounterForTest();
        Assert.That(boss.IsHitMovementLocked, Is.False);
    }

    [UnityTest]
    public IEnumerator GroundKnockbackWinsOverBossMeleeLungeWithoutCancellingAttack()
    {
        PutPlayer((Vector2)boss.transform.position + Vector2.left * 1.5f);
        boss.ForcePatternForTest(BossPatternKind.MeleeCombo);
        PlayerAttack playerAttack = player.GetComponent<PlayerAttack>();
        playerAttack.TryGetComboStep(0, out PlayerAttack.ComboStep first);
        using (var swing = new PlayerAttackContext(player.gameObject))
        {
            int hits = (int)typeof(PlayerAttack).GetMethod("DoHit", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(playerAttack, new object[] { swing, first, 1f });
            Assert.That(hits, Is.EqualTo(1));
        }
        Assert.That(boss.IsHitMovementLocked, Is.True);
        float startX = boss.Body.position.x;
        for (int i = 0; i < 5; i++)
        {
            yield return new WaitForFixedUpdate();
            Assert.That(boss.Body.linearVelocity.x, Is.GreaterThan(0f));
        }
        Assert.That(boss.Body.position.x, Is.GreaterThan(startX));
        Assert.That(boss.IsRegularRunning, Is.True);
        yield return Until(() => Count(BossAttackKind.Melee) >= 1);
        Assert.That(Last.EndReason, Is.EqualTo(BossAttackEndReason.Completed));
    }

    [UnityTest]
    public IEnumerator MeleeComboMirrorsPlayerHitboxesAndAutomaticallyLandsThreeHits()
    {
        // Isolate the three melee hits from the boss body's separate contact damage.
        foreach (EnemyContactDamage contact in boss.GetComponentsInChildren<EnemyContactDamage>())
            contact.SetSuppressed(true);
        PutPlayer((Vector2)boss.transform.position+Vector2.left*1.5f);
        float initialHp = player.CurrentHp;
        var playerAttack=player.GetComponent<PlayerAttack>();
        Assert.That(playerAttack,Is.Not.Null);
        Assert.That(playerAttack.ComboCount,Is.GreaterThanOrEqualTo(3));
        PlayerAttack.ComboStep firstStep;
        Assert.That(playerAttack.TryGetComboStep(0,out firstStep),Is.True);
        boss.ForcePatternForTest(BossPatternKind.MeleeCombo);
        yield return Until(()=>boss.ActiveBossAttackId>0);
        var warning=Object.FindFirstObjectByType<BossFanWarning>(); Assert.That(warning,Is.Not.Null);
        Assert.That(player.CurrentHp,Is.EqualTo(initialHp));
        var record = new List<BossAttackRecord>(boss.ActiveAttacks)[0];
        Assert.That(record.Geometry.Size,Is.EqualTo(firstStep.hitboxSize));
        Assert.That(record.Geometry.Angle,Is.EqualTo(-firstStep.hitboxAngle).Within(.01f));
        Assert.That(record.Geometry.Center.x,Is.LessThan(boss.transform.position.x));
        yield return new WaitForSeconds(data.MeleeComboFirstWarning*.5f);
        Assert.That(player.CurrentHp,Is.EqualTo(initialHp));
        yield return Until(()=>Count(BossAttackKind.Melee)>=1);
        Assert.That(player.CurrentHp,Is.EqualTo(initialHp-data.MeleeComboDamage[0]));
        Assert.That(Last.Outcome,Is.EqualTo(AttackOutcome.Hit));
        yield return Until(()=>!boss.IsPatternRunning);
        Assert.That(Count(BossAttackKind.Melee),Is.EqualTo(3));
        Assert.That(player.CurrentHp,Is.EqualTo(initialHp-(data.MeleeComboDamage[0]+data.MeleeComboDamage[1]+
            data.MeleeComboDamage[2])));
        Assert.That(playerAttack.TryGetComboStep(2,out PlayerAttack.ComboStep finisher),Is.True);
        Assert.That(finisher.launchUp,Is.Zero);
        Assert.That(playerBody.linearVelocity.y,Is.EqualTo(0f).Within(.001f));
        Assert.That(playerBody.linearVelocity.x,Is.LessThan(0f),"The three-hit finisher knocks back without launching.");
        Assert.That(boss.transform.localScale,Is.EqualTo(Vector3.one));
        Assert.That(Object.FindFirstObjectByType<BossFanWarning>(),Is.Null);
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
        float initialHp = player.CurrentHp;
        float wallX = boss.transform.position.x - 2f;
        PutPlayer((Vector2)boss.transform.position + Vector2.left * 6f);
        data.ProjectileSpeed=200f;
        boss.ForcePatternForTest(BossPatternKind.Projectile);
        yield return Until(() => boss.ActiveBossAttackId > 0);
        Wall(wallX); // Introduce a thin obstacle after the projectile's path has been planned.
        yield return Until(()=>Count(BossAttackKind.Projectile)>0);
        Assert.That(Last.Outcome,Is.EqualTo(AttackOutcome.Blocked)); Assert.That(player.CurrentHp,Is.EqualTo(initialHp));
        data.DashSpeed=200f;
        boss.ForcePatternForTest(BossPatternKind.Dash);
        yield return Until(()=>Count(BossAttackKind.Dash)>0);
        Assert.That(Last.Outcome,Is.EqualTo(AttackOutcome.Blocked));
        Assert.That(boss.BodyCollider.bounds.min.x,Is.GreaterThanOrEqualTo(wallX + .05f));
        Assert.That(player.CurrentHp,Is.EqualTo(initialHp));
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

                private void PutBodyCenter(Vector2 center)
    {
        Vector2 offset = (Vector2)player.BodyCollider.bounds.center - (Vector2)player.transform.position;
        PutPlayer(center - offset);
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
    public IEnumerator BurnTicksRespectInvulnerabilityAndResetDisableAndDeathRemoveOwnedEffects()
    {
        var burn=player.gameObject.AddComponent<BossBurnEffect>(); data.BurnTickInterval=.2f; data.BurnDuration=.7f;
        player.TakeDamage(new DamageInfo(1f,DamageSource.Environment)); burn.Apply(boss,data);
        yield return new WaitForSeconds(.65f);
        Assert.That(burn.TickAttempts,Is.GreaterThanOrEqualTo(3)); Assert.That(burn.TicksAccepted,Is.Zero);
        StartIndependentAttack(new Vector2(-4f,-2f));
        Assert.That(boss.ActiveBossAttackId,Is.GreaterThan(0));
        PutPlayer(new Vector2(8f,4f)); boss.Body.position=new Vector2(6f,0f);
        boss.ResetEncounterForTest(); yield return null;
        Assert.That(burn.IsBurning,Is.False); Assert.That(player.CurrentHp,Is.EqualTo(100f));
        Assert.That(player.transform.position.x,Is.EqualTo(-4.21f).Within(.01f)); Assert.That(boss.transform.position.x,Is.EqualTo(2f).Within(.01f));
        StartIndependentAttack(new Vector2(-4f,-2f));
        burn.Apply(boss,data); boss.gameObject.SetActive(false); yield return null;
        Assert.That(burn.IsBurning,Is.False); Assert.That(Object.FindFirstObjectByType<DangerIndicator>(),Is.Null);
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
    public IEnumerator VulnerabilityCancelsActiveAttacksAndRejectsPreviousGenerationPlayerHits()
    {
        PutPlayer(new Vector2(-4f,-2.33f));
        var events=player.GetComponent<PlayerCombatEvents>();
        var oldAttempt=events.PublishAttackAttempt(boss.gameObject);
        var record=StartIndependentAttack(new Vector2(-4f,-1f));
        boss.ReduceBeliefForTest(100f);
        yield return new WaitForSeconds(.2f);
        Assert.That(player.CurrentHp,Is.EqualTo(100f)); Assert.That(record.Outcome,Is.EqualTo(AttackOutcome.Cancelled));
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
        yield return Until(()=>boss.Clouds!=null && boss.Clouds.LightningActive);
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
        yield return Until(()=>boss.HasProphecy(BossBeliefKind.NoAttack));
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
        yield return Until(()=>boss.Clouds!=null && boss.Clouds.LightningActive);
        yield return Capture("03-clouds");
        yield return Capture("17-clouds-1024x576",1024,576);
        Object.FindFirstObjectByType<BossTestBootstrap>().ShowDebugPanel=true;
        yield return Capture("18-debug-cloud-details");
        Object.FindFirstObjectByType<BossTestBootstrap>().ShowDebugPanel=false;
        var lightning = boss.Clouds.Lightning;
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
        boss.ResetEncounterForTest(); boss.AutomaticPatterns=false;
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

        data.AttackSignalMode = BossAttackSignalMode.Attempt;
        boss.ForcePatternForTest(BossPatternKind.NoAttackBelief);
        yield return Until(() => boss.HasProphecy(BossBeliefKind.NoAttack));
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
        data.AttackSignalMode = BossAttackSignalMode.Attempt;
        boss.ForcePatternForTest(BossPatternKind.NoAttackBelief);
        yield return Until(() => boss.HasProphecy(BossBeliefKind.NoAttack));
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
        if (PlayerDeathCoordinator.Active != null)
        {
            Currency.Service?.CompleteDeferredDeath(PlayerDeathCoordinator.Active.DeathId);
            Object.Destroy(PlayerDeathCoordinator.Active.gameObject);
        }
        Time.timeScale = 1f;
        SceneManager.sceneLoaded-=PauseOnLoad;
        if(boss!=null) { boss.PauseAndCancelForTest(); boss.gameObject.SetActive(false); if(originalData!=null) Set(boss,"settings",originalData); }
        foreach(var fixture in fixtures) if(fixture!=null) Object.Destroy(fixture); fixtures.Clear();
        if(data!=null) Object.Destroy(data);
        yield return null;
    }
}
