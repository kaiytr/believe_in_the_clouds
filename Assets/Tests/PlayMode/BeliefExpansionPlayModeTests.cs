using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BeliefExpansionPlayModeTests
{
    private readonly List<Object> temporary = new List<Object>();
    private readonly Dictionary<string, EnemyUnit> enemies = new Dictionary<string, EnemyUnit>();
    private readonly Dictionary<string, BeliefType> pinned = new Dictionary<string, BeliefType>();
    private PlayerHealth player;
    private Rigidbody2D playerBody;
    private EnemyUnit Dog => enemies["Dog"];
    private EnemyUnit Believer => enemies["Believer"];
    private EnemyUnit Executive => enemies["Executive"];

    private IEnumerator Load(BeliefType dog = BeliefType.DogRearAttack,
        BeliefType believer = BeliefType.BelieverConsecutiveHits,
        BeliefType executive = BeliefType.ExecutivePrayer, bool productionSelection = false)
    {
        Time.timeScale = 1f;
        pinned.Clear();
        enemies.Clear();
        if (!productionSelection)
        {
            pinned["Dog"] = dog;
            pinned["Believer"] = believer;
            pinned["Executive"] = executive;
        }
        SceneManager.sceneLoaded += OnLoaded;
        Type editorManager = null;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            editorManager = assembly.GetType("UnityEditor.SceneManagement.EditorSceneManager", false);
            if (editorManager != null) break;
        }
        var method = editorManager.GetMethod("LoadSceneAsyncInPlayMode", BindingFlags.Static | BindingFlags.Public,
            null, new[] { typeof(string), typeof(LoadSceneParameters) }, null);
        var operation = (AsyncOperation)method.Invoke(null, new object[] {
            "Assets/Scenes/BeliefTest.unity", new LoadSceneParameters(LoadSceneMode.Single) });
        while (!operation.isDone) yield return null;
        SceneManager.sceneLoaded -= OnLoaded;
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerHealth>();
        player.GetComponent<PlayerMove>().enabled = false;
        playerBody = player.GetComponent<Rigidbody2D>();
        playerBody.bodyType = RigidbodyType2D.Kinematic;
        playerBody.linearVelocity = Vector2.zero;
        foreach (EnemyUnit enemy in enemies.Values) enemy.enabled = true;
        yield return null; // Run the real EnemyUnit.Start configuration and UI setup.
        foreach (EnemyUnit enemy in enemies.Values)
        {
            enemy.enabled = false;
            enemy.LockMovement();
            foreach (EnemyContactDamage contact in enemy.GetComponentsInChildren<EnemyContactDamage>(true))
                contact.SetSuppressed(true);
        }
        player.ResetHealth();
    }

    private void OnLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "BeliefTest") return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            EnemyUnit enemy = root.GetComponent<EnemyUnit>();
            if (enemy == null) continue;
            enemy.enabled = false;
            root.SetActive(true);
            enemies[root.name] = enemy;
            Set(enemy, "maxHp", 1000f);
            Set(enemy, "currentHp", 1000f);
            var body = root.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;
            var attack = root.GetComponent<EnemyAttack>();
            Set(attack, "cooldownRemaining", 1000f);
            Set(attack, "recoveryTime", .02f);
            var belief = enemy.Belief;
            if (pinned.TryGetValue(root.name, out BeliefType type))
            {
                Set(belief, "beliefTypeCandidates", new List<BeliefType> { type });
                typeof(BeliefController).GetMethod("ConfigureProfile", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(belief, null);
            }
            var profile = Object.Instantiate(belief.Profile);
            temporary.Add(profile);
            Set(profile, "vulnerableDuration", .15f);
            Set(belief, "profile", profile);
            var condition = root.GetComponent<ExpandedBeliefCondition>();
            condition.enabled = false;
            condition.enabled = true;
        }
    }

    private void Place(EnemyUnit enemy, Vector2 offset)
    {
        Physics2D.SyncTransforms();
        Vector2 desired = (Vector2)enemy.BodyCollider.bounds.center + offset;
        playerBody.position += desired - (Vector2)player.BodyCollider.bounds.center;
        player.transform.position = new Vector3(playerBody.position.x, playerBody.position.y, player.transform.position.z);
        playerBody.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }

    private EnemyAttack BeginAttack(EnemyUnit enemy, float windup = .18f)
    {
        var attack = enemy.GetComponent<EnemyAttack>();
        Set(attack, "cooldownRemaining", 0f);
        Set(attack, "windupTime", windup);
        Assert.That(attack.TryAttack(player.transform), Is.True,
            $"{enemy.name}: ready={attack.IsReady}, attacking={attack.IsAttacking}, vulnerable={enemy.IsVulnerable}, " +
            $"enemy={enemy.transform.position}, player={player.transform.position}, body={player.BodyCollider.bounds.center}");
        return attack;
    }

    [UnityTest]
    public IEnumerator ProductionSelectionUsesThreeSpeciesCandidatesAndSurvivesRecovery()
    {
        yield return Load(productionSelection: true);
        var selected = new Dictionary<EnemyUnit, BeliefType>();
        foreach (EnemyUnit enemy in enemies.Values)
        {
            var roster = BeliefRules.CandidatesFor(enemy);
            CollectionAssert.AreEquivalent(roster, enemy.Belief.CandidateTypes);
            CollectionAssert.Contains(roster, enemy.Belief.Type);
            selected[enemy] = enemy.Belief.Type;
            var text = enemy.GetComponentInChildren<UnityEngine.UI.Text>();
            Assert.That(text.text, Is.EqualTo(enemy.Belief.Profile.Phrase));
            enemy.Belief.ReduceBelief(100f);
        }
        playerBody.position = new Vector2(1000f, 5f);
        yield return new WaitForSeconds(.2f);
        foreach (EnemyUnit enemy in enemies.Values)
        {
            Assert.That(enemy.Belief.Type, Is.EqualTo(selected[enemy]));
            Assert.That(enemy.Belief.CurrentBelief, Is.EqualTo(100f));
            Assert.That(enemy.gameObject.activeInHierarchy, Is.True);
        }
    }

    [UnityTest]
    public IEnumerator RearHitsDeduplicateDamageAndUseTheStateBeforeTheHit()
    {
        yield return Load();
        Dog.SetFacingDirection(1f);
        Place(Dog, Vector2.left * 2f);
        float hp = Dog.CurrentHp;
        var tick = new PlayerAttackContext(player.gameObject, PlayerAttackKind.DamageOverTime);
        Assert.That(tick.TryHit(Dog, 10f), Is.True);
        Assert.That(Dog.Belief.CurrentBelief, Is.EqualTo(100f));
        hp = Dog.CurrentHp;
        var combo = new PlayerAttackContext(player.gameObject);
        Assert.That(combo.TryHit(Dog, 10f, 0), Is.True);
        Assert.That(Dog.Belief.CurrentBelief, Is.EqualTo(60f));
        Assert.That(combo.TryHit(Dog, 10f, 0), Is.False);
        Assert.That(Dog.CurrentHp, Is.EqualTo(hp - 6f));
        Assert.That(combo.TryHit(Dog, 10f, 1), Is.True);
        Assert.That(Dog.Belief.CurrentBelief, Is.EqualTo(20f));
        Assert.That(new PlayerAttackContext(player.gameObject).TryHit(Dog, 10f), Is.True);
        Assert.That(Dog.IsVulnerable, Is.True);
        Assert.That(Dog.CurrentHp, Is.EqualTo(hp - 18f));
        Assert.That(new PlayerAttackContext(player.gameObject).TryHit(Dog, 10f), Is.True);
        Assert.That(Dog.CurrentHp, Is.EqualTo(hp - 38f));
        yield return new WaitForSeconds(.2f);
        Assert.That(Dog.Belief.Type, Is.EqualTo(BeliefType.DogRearAttack));
        Assert.That(Dog.Belief.CurrentBelief, Is.EqualTo(100f));
        Assert.That(combo.TryHit(Dog, 10f, 0), Is.False, "Recovery must not forget a consumed damage ID.");
    }

    [UnityTest]
    public IEnumerator ConsecutiveHitsIgnoreOtherDamageAndResetWhenTheTargetIsLost()
    {
        yield return Load();
        Place(Believer, Vector2.right * 2f);
        var combo = new PlayerAttackContext(player.gameObject);
        combo.TryHit(Believer, 1f, 0);
        Assert.That(Believer.Belief.CurrentBelief, Is.EqualTo(100f));
        Believer.TakeDamage(new DamageInfo(1f, DamageSource.Environment));
        new PlayerAttackContext(player.gameObject, PlayerAttackKind.Summon).TryHit(Believer, 1f);
        Assert.That(Believer.Belief.CurrentBelief, Is.EqualTo(100f));
        combo.TryHit(Believer, 1f, 1);
        Assert.That(Believer.Belief.CurrentBelief, Is.EqualTo(75f));
        Assert.That(combo.TryHit(Believer, 1f, 1), Is.False);
        player.gameObject.SetActive(false);
        yield return null;
        player.gameObject.SetActive(true);
        yield return null;
        combo.TryHit(Believer, 1f, 2);
        Assert.That(Believer.Belief.CurrentBelief, Is.EqualTo(75f));
        for (int i = 3; i <= 5; i++) combo.TryHit(Believer, 1f, i);
        Assert.That(Believer.Belief.CurrentBelief, Is.Zero);
        Assert.That(Believer.IsVulnerable, Is.True);
    }

    [UnityTest]
    public IEnumerator PrayerHitsApplyOnlyDuringWindupAndCollapseCancelsTheCast()
    {
        yield return Load();
        Place(Executive, Vector2.left * 2f);
        EnemyAttack attack = BeginAttack(Executive, .4f);
        EnemyAttackResult? result = null;
        attack.AttackResolved += (_, value) => result = value;
        yield return null;
        var combo = new PlayerAttackContext(player.gameObject, PlayerAttackKind.Skill);
        combo.TryHit(Executive, 1f, 0);
        Assert.That(Executive.Belief.CurrentBelief, Is.EqualTo(50f));
        Assert.That(attack.IsWindingUp, Is.True);
        combo.TryHit(Executive, 1f, 1);
        Assert.That(Executive.IsVulnerable, Is.True);
        Assert.That(attack.IsWindingUp, Is.False);
        Assert.That(result.HasValue, Is.True);
        Assert.That(result.Value.Outcome, Is.EqualTo(AttackOutcome.Cancelled));
        Assert.That(result.Value.WasLaunched, Is.False);
        Assert.That(Object.FindObjectsByType<MagicCircle>(FindObjectsSortMode.None), Is.Empty);
    }

    [UnityTest]
    public IEnumerator PrayerDoesNotCountHitsAfterTheCircleHasLaunched()
    {
        yield return Load();
        Place(Executive, Vector2.left * 2f);
        EnemyAttack attack = BeginAttack(Executive);
        new PlayerAttackContext(player.gameObject).TryHit(Executive, 1f);
        Assert.That(Executive.Belief.CurrentBelief, Is.EqualTo(50f));
        yield return Until(() => !attack.IsWindingUp);
        new PlayerAttackContext(player.gameObject).TryHit(Executive, 1f);
        Assert.That(Executive.Belief.CurrentBelief, Is.EqualTo(50f));
    }

    [UnityTest]
    public IEnumerator EscapingTheActualCircleBreaksBeliefAtEffectTime()
    {
        yield return Load(executive: BeliefType.ExecutiveEscapeCircle);
        Place(Executive, Vector2.left * 2f);
        EnemyAttack attack = BeginAttack(Executive);
        EnemyAttackResult? result = null;
        attack.AttackResolved += (_, value) => result = value;
        yield return Until(() => attack.WasPlayerThreatenedDuringWarning);
        Place(Executive, Vector2.left * 5f);
        Assert.That(Executive.Belief.CurrentBelief, Is.EqualTo(100f));
        yield return Until(() => result.HasValue);
        Assert.That(result.Value.Outcome, Is.EqualTo(AttackOutcome.EvadedByPlayer));
        Assert.That(result.Value.PlayerHpDamaged, Is.False);
        Assert.That(Executive.Belief.CurrentBelief, Is.Zero);
        Assert.That(player.CurrentHp, Is.EqualTo(player.MaxHp));
    }

    [UnityTest]
    public IEnumerator ActualDogEvadeArmsTheCounterattackWithoutReducingBeliefEarly()
    {
        yield return Load(dog: BeliefType.DogCounterattack);
        Place(Dog, Vector2.right * 2f);
        EnemyAttack attack = BeginAttack(Dog);
        EnemyAttackResult? result = null;
        attack.AttackResolved += (_, value) => result = value;
        yield return Until(() => attack.WasPlayerThreatenedDuringWarning);
        Place(Dog, new Vector2(2f, 3f));
        yield return Until(() => result.HasValue);
        Assert.That(result.Value.Outcome, Is.EqualTo(AttackOutcome.EvadedByPlayer));
        Assert.That(Dog.Belief.CurrentBelief, Is.EqualTo(100f));
        new PlayerAttackContext(player.gameObject).TryHit(Dog, 1f);
        Assert.That(Dog.Belief.CurrentBelief, Is.Zero);
    }

    [UnityTest]
    public IEnumerator DogUnerringBeliefCountsTheFinishedMissButNotAWindupCancellation()
    {
        yield return Load(dog: BeliefType.DogUnerringAttack);
        Place(Dog, Vector2.right * 2f);
        EnemyAttack attack = BeginAttack(Dog);
        EnemyAttackResult? result = null;
        attack.AttackResolved += (_, value) => result = value;
        yield return Until(() => attack.WasPlayerThreatenedDuringWarning);
        Place(Dog, new Vector2(2f, 3f));
        yield return Until(() => result.HasValue);
        Assert.That(Dog.Belief.CurrentBelief, Is.EqualTo(50f));
        yield return Until(() => !attack.IsAttacking);
        Place(Dog, Vector2.right * 2f);
        BeginAttack(Dog);
        attack.SetVulnerable(true); // cancel the unlaunched windup, without reducing the gauge
        Assert.That(Dog.Belief.CurrentBelief, Is.EqualTo(50f));
        Assert.That(result.Value.WasLaunched, Is.False);
        Assert.That(result.Value.CompletedNormally, Is.False);
    }

    [UnityTest]
    public IEnumerator DisablingAnActiveDogChargeCleansMotionCollisionAndTrailsBeforeReuse()
    {
        yield return Load(dog:BeliefType.DogUnerringAttack);
        Place(Dog,Vector2.right*2f);
        var attack=(DogChargeAttack)BeginAttack(Dog,.08f);
        var results=new List<EnemyAttackResult>(); attack.AttackResolved+=(_,value)=>results.Add(value);
        yield return Until(()=>attack.IsCharging);
        attack.enabled=false;
        Assert.That(attack.IsCharging,Is.False); Assert.That(attack.IsAttacking,Is.False);
        Assert.That(Dog.GetComponent<Rigidbody2D>().linearVelocity.x,Is.Zero.Within(.001f));
        Assert.That(Physics2D.GetIgnoreCollision(Dog.BodyCollider,player.BodyCollider),Is.True);
        Assert.That(Dog.GetComponent<CombatAfterimageEmitter>().ActiveCount,Is.Zero);
        Assert.That(results.Count,Is.EqualTo(1)); Assert.That(results[0].Outcome,Is.EqualTo(AttackOutcome.Cancelled));
        attack.enabled=true; Place(Dog,Vector2.right*2f); BeginAttack(Dog,.08f);
        yield return Until(()=>attack.IsCharging);
        Dog.gameObject.SetActive(false);
        Assert.That(attack.IsCharging,Is.False); Assert.That(results.Count,Is.EqualTo(2));
        Dog.gameObject.SetActive(true); yield return null;
        Assert.That(attack.IsAttacking,Is.False);
    }

    [UnityTest]
    public IEnumerator RearDwellIsAchievableDuringTheRealDirectionLockedCircleWindup()
    {
        yield return Load(executive: BeliefType.ExecutiveRearPosition);
        Place(Executive, Vector2.right * 2f);
        EnemyAttack attack = BeginAttack(Executive);
        Assert.That(attack.WindupDuration, Is.GreaterThanOrEqualTo(3f));
        yield return null;
        Place(Executive, Vector2.left * 2f);
        Executive.enabled = true; // real AI must not turn before the interval is complete
        Time.timeScale = 4f;
        yield return new WaitForSeconds(2.1f);
        Assert.That(attack.IsWindingUp, Is.True);
        Assert.That(Executive.FacingDirection, Is.EqualTo(1f));
        Assert.That(Executive.Belief.CurrentBelief, Is.EqualTo(75f));
    }

    [UnityTest]
    public IEnumerator StoneResultsWaitForFlightAndUseOnlyTheirOwnAcceptedHpDamage()
    {
        yield return Load(believer: BeliefType.BelieverUnerringStone);
        Place(Believer, Vector2.left * 2f);
        var results = new List<EnemyAttackResult>();
        var attack = Believer.GetComponent<ThrowStoneAttack>();
        attack.AttackResolved += (_, value) => results.Add(value);
        BeginAttack(Believer);
        yield return Until(() => attack.WasPlayerThreatenedDuringWarning);
        Place(Believer, new Vector2(-2f, 3f));
        yield return Until(() => Object.FindFirstObjectByType<StoneProjectile>() != null);
        Assert.That(Believer.Belief.CurrentBelief, Is.EqualTo(100f), "A flying stone is not a final miss.");
        player.TakeDamage(new DamageInfo(7f, DamageSource.Environment));
        player.ResetHealth();
        yield return Until(() => results.Count == 1);
        Assert.That(results[0].CompletedNormally, Is.True);
        Assert.That(results[0].PlayerHpDamaged, Is.False);
        Assert.That(Believer.Belief.CurrentBelief, Is.EqualTo(50f));
        yield return Until(() => !attack.IsAttacking);

        Place(Believer, Vector2.left * 2f);
        player.ResetHealth();
        BeginAttack(Believer);
        yield return Until(() => results.Count == 2);
        Assert.That(results[1].PlayerHpDamaged, Is.True);
        Assert.That(Believer.Belief.CurrentBelief, Is.EqualTo(50f));
        yield return Until(() => !attack.IsAttacking);

        player.ResetHealth();
        player.TakeDamage(new DamageInfo(1f, DamageSource.Environment)); // existing invulnerability
        BeginAttack(Believer);
        yield return Until(() => results.Count == 3);
        Assert.That(results[2].CompletedNormally, Is.True);
        Assert.That(results[2].Outcome, Is.EqualTo(AttackOutcome.Missed));
        Assert.That(results[2].PlayerHpDamaged, Is.False);
        Assert.That(Believer.Belief.CurrentBelief, Is.Zero);
    }

    [UnityTest]
    public IEnumerator RetreatPunishUsesActualRetreatStateAndOneSuccessPerEpisode()
    {
        yield return Load(believer: BeliefType.BelieverSafeDistance);
        Place(Believer, Vector2.right * 3f);
        Believer.enabled = true;
        yield return Until(() => ((BelieverEnemy)Believer).IsRetreating);
        Believer.enabled = false;
        new PlayerAttackContext(player.gameObject).TryHit(Believer, 1f);
        Assert.That(Believer.Belief.CurrentBelief, Is.EqualTo(50f));
        new PlayerAttackContext(player.gameObject).TryHit(Believer, 1f);
        Assert.That(Believer.Belief.CurrentBelief, Is.EqualTo(50f));
        Believer.ChangeState(EnemyState.Idle);
        yield return null;
        Believer.ChangeState(EnemyState.Retreat);
        yield return null;
        new PlayerAttackContext(player.gameObject).TryHit(Believer, 1f);
        Assert.That(Believer.Belief.CurrentBelief, Is.Zero);
    }

    [UnityTest]
    public IEnumerator RearDwellUsesTwoSecondTicksAndPausesWithGameTime()
    {
        yield return Load(executive: BeliefType.ExecutiveRearPosition);
        Executive.SetFacingDirection(1f);
        Place(Executive, Vector2.left * 2f);
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(.1f);
        Assert.That(Executive.Belief.CurrentBelief, Is.EqualTo(100f));
        Time.timeScale = 4f;
        yield return new WaitForSeconds(2.05f);
        Assert.That(Executive.Belief.CurrentBelief, Is.EqualTo(75f));
        Place(Executive, Vector2.right * 2f);
        yield return null;
        Place(Executive, Vector2.left * 2f);
        yield return new WaitForSeconds(1f);
        Assert.That(Executive.Belief.CurrentBelief, Is.EqualTo(75f));
        Place(Executive, Vector2.right * 2f);
        yield return null;
        Place(Executive, Vector2.left * 2f);
        yield return new WaitForSeconds(2.05f);
        Assert.That(Executive.Belief.CurrentBelief, Is.EqualTo(50f));
    }

    [UnityTest]
    public IEnumerator FatalHitWinsOverBeliefCollapse()
    {
        yield return Load();
        Dog.SetFacingDirection(1f);
        Place(Dog, Vector2.left * 2f);
        Set(Dog, "currentHp", 1f);
        new PlayerAttackContext(player.gameObject).TryHit(Dog, 10f);
        Assert.That(Dog.IsDead, Is.True);
        Assert.That(Dog.Belief.IsStopped, Is.True);
        Assert.That(Dog.Belief.CurrentBelief, Is.EqualTo(100f));
        Assert.That(Dog.GetComponent<BeliefView>().WorldView.BeliefCanvas.enabled, Is.False);
    }

    [UnityTest]
    public IEnumerator ReactivationKeepsSelectionResetsTheChainAndResumesTheRemainingVulnerability()
    {
        yield return Load();
        Place(Believer, Vector2.right * 2f);
        var combo = new PlayerAttackContext(player.gameObject);
        combo.TryHit(Believer, 1f, 0);
        combo.TryHit(Believer, 1f, 1);
        Assert.That(Believer.Belief.CurrentBelief, Is.EqualTo(75f));
        Believer.gameObject.SetActive(false);
        Assert.That(combo.TryHit(Believer, 1f, 2), Is.False);
        yield return null;
        Believer.gameObject.SetActive(true);
        yield return null;
        combo.TryHit(Believer, 1f, 2);
        Assert.That(Believer.Belief.CurrentBelief, Is.EqualTo(75f), "A reactivated chain starts with its first hit.");
        for (int i = 3; i <= 5; i++) combo.TryHit(Believer, 1f, i);
        Assert.That(Believer.IsVulnerable, Is.True);
        Believer.gameObject.SetActive(false);
        yield return new WaitForSeconds(.2f);
        Assert.That(Believer.Belief.CurrentBelief, Is.Zero);
        Believer.gameObject.SetActive(true);
        yield return new WaitForSeconds(.04f);
        Assert.That(Believer.IsVulnerable, Is.True);
        yield return new WaitForSeconds(.15f);
        Assert.That(Believer.Belief.CurrentBelief, Is.EqualTo(100f));
        Assert.That(Believer.Belief.Type, Is.EqualTo(BeliefType.BelieverConsecutiveHits));
    }

    [UnityTest]
    public IEnumerator LongBeliefPhrasesRenderOnTheActualWorldSpaceUi()
    {
        yield return Load(believer: BeliefType.BelieverSafeDistance);
        float groundY = Dog.transform.position.y;
        int index = 0;
        foreach (EnemyUnit enemy in new[] { Dog, Believer, Executive })
        {
            Vector3 position = new Vector3(-4f + index++ * 4f, groundY, 0f);
            enemy.GetComponent<Rigidbody2D>().position = position;
            enemy.transform.position = position;
            enemy.SetFacingDirection(1f);
            Assert.That(enemy.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo(enemy.Belief.Profile.Phrase));
        }
        playerBody.position = new Vector2(-20f, groundY);
        Camera camera = Camera.main;
        camera.transform.position = new Vector3(0f, groundY + 1f, -10f);
        camera.orthographicSize = 3.4f;
        Physics2D.SyncTransforms();
        yield return null;
        Canvas.ForceUpdateCanvases();
        SaveRender(camera, 1600, 900, "01-long-phrases-1600x900.png");
        SaveRender(camera, 1024, 576, "02-long-phrases-1024x576.png");
    }

    [UnityTest]
    public IEnumerator DummyDataToolUsesRealHitsRejectsReplayAndCompletesTheChainWithoutPlayerAttack()
    {
        yield return Load();
        var tool = Object.FindFirstObjectByType<BeliefDummyHitTool>();
        Assert.That(tool, Is.Not.Null, "The test scene needs its serialized dummy-data tool.");
        Assert.That(tool.Player, Is.SameAs(player));
        CollectionAssert.AreEquivalent(new[] { Dog, Believer, Executive }, tool.Targets);
        int dogIndex = Array.IndexOf(tool.Targets, Dog);
        int believerIndex = Array.IndexOf(tool.Targets, Believer);
        Assert.That(tool.SelectTarget(dogIndex), Is.True);
        Dog.SetFacingDirection(1f);
        Place(Dog, Vector2.left * 2f);
        float hp = Dog.CurrentHp;
        Assert.That(tool.SendHit(), Is.True);
        Assert.That(Dog.CurrentHp, Is.EqualTo(hp - .6f).Within(.001f));
        Assert.That(Dog.Belief.CurrentBelief, Is.EqualTo(60f));
        Assert.That(tool.ReplayLastHit(), Is.False);
        Assert.That(Dog.CurrentHp, Is.EqualTo(hp - .6f).Within(.001f));
        Assert.That(Dog.Belief.CurrentBelief, Is.EqualTo(60f));
        tool.SelectTarget(believerIndex);
        Assert.That(tool.StartFiveHits(), Is.True);
        yield return Until(() => Believer.IsVulnerable);
        Assert.That(Believer.Belief.CurrentBelief, Is.Zero);
        Assert.That(Believer.CurrentHp, Is.EqualTo(997f).Within(.001f));
        yield return Until(() => !tool.IsBurstRunning);
    }

    [UnityTest]
    public IEnumerator DummyDataToolInterruptsPrayerAndStopsPendingDataWhenDisabled()
    {
        yield return Load();
        var tool = Object.FindFirstObjectByType<BeliefDummyHitTool>();
        tool.SelectTarget(Array.IndexOf(tool.Targets, Executive));
        Place(Executive, Vector2.left * 2f);
        EnemyAttack attack = BeginAttack(Executive, .4f);
        Assert.That(tool.SendHit(), Is.True);
        Assert.That(Executive.Belief.CurrentBelief, Is.EqualTo(50f));
        Assert.That(tool.SendHit(), Is.True);
        Assert.That(Executive.IsVulnerable, Is.True);
        Assert.That(attack.IsWindingUp, Is.False);
        tool.SelectTarget(Array.IndexOf(tool.Targets, Believer));
        Assert.That(tool.StartFiveHits(), Is.True);
        float hp = Believer.CurrentHp;
        tool.gameObject.SetActive(false);
        yield return new WaitForSeconds(.3f);
        Assert.That(tool.IsBurstRunning, Is.False);
        Assert.That(Believer.CurrentHp, Is.EqualTo(hp));
        tool.gameObject.SetActive(true);
        Assert.That(tool.ReplayLastHit(), Is.False);
    }

    [UnityTest]
    public IEnumerator SharedEnemyHitPostprocessingRecordsAcceptedDamageAndRaisesOneDoctrinePerAttack()
    {
        yield return Load();
        var events = new List<DoctrineEventData>();
        Action<DoctrineEventData> listener = evt =>
        {
            if (evt.Condition == ConditionType.OnHit && evt.SubjectObject == player.gameObject)
                player.Heal(player.MaxHp); // Net HP stays unchanged, but the hit was accepted.
            if (evt.Condition != ConditionType.OnAttack) return;
            events.Add(evt);
            var source = evt.SubjectObject.GetComponent<EnemyAttack>();
            source?.NotifyPlayerHpDamageApplied(source.CurrentAttackId); // Must not notify twice/re-enter.
        };
        DoctrineEventBus.EventRaised += listener;
        try
        {
            foreach (EnemyUnit enemy in new[] { Dog, Believer, Executive })
            {
                Place(enemy, Vector2.right * 2f);
                player.ResetHealth();
                EnemyAttackResult? result = null;
                EnemyAttack attack = enemy.GetComponent<EnemyAttack>();
                Action<EnemyAttack, EnemyAttackResult> resolved = (_, value) => result = value;
                attack.AttackResolved += resolved;
                events.Clear();
                BeginAttack(enemy, .08f);
                attack.NotifyPlayerHpDamageApplied(attack.CurrentAttackId - 1);
                Assert.That(events, Is.Empty, "A stale attack ID must not trigger doctrines.");
                yield return Until(() => result.HasValue);
                Assert.That(result.Value.PlayerHpDamaged, Is.True, enemy.name);
                Assert.That(result.Value.CompletedNormally, Is.True, enemy.name);
                Assert.That(player.CurrentHp, Is.EqualTo(player.MaxHp), "OnHit healing must not erase hit attribution.");
                Assert.That(events.Count, Is.EqualTo(1), enemy.name);
                Assert.That(events[0].SubjectObject, Is.SameAs(enemy.gameObject));
                Assert.That(events[0].SourceObject, Is.SameAs(player.gameObject));
                attack.NotifyPlayerHpDamageApplied(attack.CurrentAttackId);
                Assert.That(events.Count, Is.EqualTo(1));
                attack.AttackResolved -= resolved;
                yield return Until(() => !attack.IsAttacking);
            }
        }
        finally { DoctrineEventBus.EventRaised -= listener; }
    }

    [UnityTest]
    public IEnumerator SlowOnlyMagicCircleReturnsItsEffectWithoutReportingHpDamageOrAttackDoctrine()
    {
        yield return Load();
        Place(Executive, Vector2.left * 2f);
        player.TakeDamage(new DamageInfo(1f, DamageSource.Environment));
        float hp = player.CurrentHp;
        int attackEvents = 0;
        Action<DoctrineEventData> listener = evt =>
        {
            if (evt.Condition == ConditionType.OnAttack && evt.SubjectObject == Executive.gameObject)
                attackEvents++;
        };
        DoctrineEventBus.EventRaised += listener;
        try
        {
            EnemyAttackResult? result = null;
            Executive.GetComponent<EnemyAttack>().AttackResolved += (_, value) => result = value;
            BeginAttack(Executive, .08f);
            yield return Until(() => result.HasValue);
            Assert.That(result.Value.Outcome, Is.EqualTo(AttackOutcome.Hit), "A slow is still a harmful effect.");
            Assert.That(result.Value.PlayerHpDamaged, Is.False);
            Assert.That(result.Value.CompletedNormally, Is.True);
            Assert.That(player.CurrentHp, Is.EqualTo(hp));
            Assert.That(attackEvents, Is.Zero);
            var move = player.GetComponent<PlayerMove>();
            Assert.That((float)typeof(PlayerMove).GetField("speedMultiplier", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(move), Is.LessThan(1f));
        }
        finally { DoctrineEventBus.EventRaised -= listener; }
    }

    [UnityTest]
    public IEnumerator RealPlayerComboFeedsOrdinaryEnemyHitBeliefsWithDistinctSwingIds()
    {
        yield return Load();
        Place(Believer, Vector2.left * 1.2f);
        Vector3 scale = player.transform.localScale; scale.x = Mathf.Abs(scale.x); player.transform.localScale = scale;
        Set(player.GetComponent<PlayerMove>(), "isGrounded", true);
        var attack = player.gameObject.AddComponent<PlayerAttack>();
        var step = new PlayerAttack.ComboStep
        {
            name = "Ordinary enemy integration", damage = 2f,
            hitboxSize = new Vector2(1.5f, 1.5f), hitboxOffset = Vector2.right,
            windupTime = .04f, recoveryTime = .04f, comboWindow = .2f
        };
        Set(attack, "comboSteps", new[] { step, step }); Set(attack, "finishHitStop", 0f);
        var hits = new List<PlayerHitResult>(); Believer.PlayerHitReceived += hits.Add;
        MethodInfo input = typeof(PlayerAttack).GetMethod("OnAttackInput", BindingFlags.Instance | BindingFlags.NonPublic);
        input.Invoke(attack, null);
        float deadline = Time.time + 2f;
        while (attack.IsAttacking && Time.time < deadline) { input.Invoke(attack, null); yield return null; }
        Assert.That(attack.IsAttacking, Is.False);
        Assert.That(hits.Count, Is.EqualTo(2));
        Assert.That(hits[0].AttackId, Is.Not.EqualTo(hits[1].AttackId));
        Assert.That(Believer.Belief.CurrentBelief,
            Is.EqualTo(Believer.Belief.Profile.MaxBelief - Believer.Belief.Profile.ReductionPerSuccess));
    }

    private static void SaveRender(Camera camera, int width, int height, string fileName)
    {
        var target = new RenderTexture(width, height, 24);
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            string directory = Path.GetFullPath("Docs/BeliefExpansionVerification");
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, fileName), texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.Destroy(target);
            Object.Destroy(texture);
        }
    }

    private static IEnumerator Until(Func<bool> condition, float timeout = 4f)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "Timed out waiting for the real combat lifecycle.");
    }

    private static void Set(object target, string name, object value)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field == null) continue;
            field.SetValue(target, value);
            return;
        }
        Assert.Fail("Missing field: " + name);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1f;
        SceneManager.sceneLoaded -= OnLoaded;
        foreach (EnemyUnit enemy in enemies.Values)
            if (enemy != null) Object.Destroy(enemy.gameObject);
        foreach (Object asset in temporary)
            if (asset != null) Object.Destroy(asset);
        temporary.Clear();
        yield return null;
    }
}
