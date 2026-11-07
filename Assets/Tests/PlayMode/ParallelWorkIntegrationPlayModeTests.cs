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

public sealed class ParallelWorkIntegrationPlayModeTests
{
    private readonly List<CutsceneDefinition> definitions = new List<CutsceneDefinition>();

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private CutsceneDefinition External(string id)
    {
        var definition = ScriptableObject.CreateInstance<CutsceneDefinition>();
        definition.Configure(id, new[] { CutsceneStep.External(id) });
        definitions.Add(definition);
        return definition;
    }

    private static IEnumerator Load(string path, Action prepare = null)
    {
        Time.timeScale = 1f;
        void Loaded(Scene scene, LoadSceneMode mode) => prepare?.Invoke();
        SceneManager.sceneLoaded += Loaded;
        try
        {
            Type editor = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("UnityEditor.SceneManagement.EditorSceneManager", false))
                .First(type => type != null);
            MethodInfo method = editor.GetMethod("LoadSceneAsyncInPlayMode", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(string), typeof(LoadSceneParameters) }, null);
            var operation = (AsyncOperation)method.Invoke(null,
                new object[] { path, new LoadSceneParameters(LoadSceneMode.Single) });
            while (!operation.isDone) yield return null;
        }
        finally { SceneManager.sceneLoaded -= Loaded; }
        yield return null;
    }

    private static IEnumerator Until(Func<bool> predicate)
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(predicate(), Is.True, "통합 흐름이 제한 시간 안에 완료되지 않았습니다.");
    }

    private static PlayerHealth Player => GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerHealth>();

    private static void MovePlayer(Vector2 position)
    {
        var body = Player.GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        body.position = position;
        Player.transform.position = position;
        Physics2D.SyncTransforms();
    }

    [UnityTest]
    public IEnumerator BossIntroKeepsArenaBoundsAndCloudsThenStartsOnceAndOpensOnDefeat()
    {
        yield return Load(ProjectScenes.Boss, () =>
            Object.FindFirstObjectByType<BossCombatController>().AutomaticPatterns = false);
        BossEncounterTrigger arena = Object.FindFirstObjectByType<BossEncounterTrigger>();
        CutsceneBossEncounterCoordinator coordinator = Object.FindFirstObjectByType<CutsceneBossEncounterCoordinator>();
        Assert.That(coordinator, Is.Not.Null);
        Assert.That(arena.AutoBeginEncounter, Is.False);
        var runner = coordinator.GetComponentInChildren<CutsceneRunner>();
        var definition = External("integration.boss.pause");
        Set(coordinator, "introDefinition", definition);
        CutsceneExternalStage pending = null;
        int completions = 0;
        runner.ExternalStageRequested += stage => pending = stage;
        runner.Completed += _ => completions++;
        Player.GetComponent<PlayerMove>().enabled = false;
        MovePlayer(new Vector2(-10f, -2.3f));
        yield return Until(() => pending != null);

        BossCombatController boss = arena.Boss;
        Assert.That(arena.HoldingForIntro, Is.True);
        Assert.That(boss.EncounterStarted, Is.False);
        Assert.That(boss.Clouds.IsPrepared, Is.True);
        Assert.That(boss.Clouds.LimitsX, Is.EqualTo(arena.PatternXLimits));
        Assert.That(UiModal.IsOpen, Is.True);
        var gates = (Collider2D[])typeof(BossEncounterTrigger)
            .GetField("gates", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(arena);
        Assert.That(gates.All(gate => !gate.isTrigger), Is.True);

        pending.Complete();
        pending.Complete();
        yield return Until(() => boss.EncounterStarted);
        Assert.That(completions, Is.EqualTo(1));
        Assert.That(coordinator.BeginEncounterAttempted, Is.True);
        Assert.That(UiModal.IsOpen, Is.False);
        int generation = boss.Generation;
        boss.BeginEncounter();
        Assert.That(boss.Generation, Is.EqualTo(generation));
        boss.ForcePhaseTwoForTest();
        boss.SetHpForTest(0f);
        yield return null;
        Assert.That(boss.IsDead, Is.True);
        Assert.That(gates.All(gate => gate.isTrigger), Is.True);
    }

    [UnityTest]
    public IEnumerator LeavingDuringBossIntroCancelsWithoutStartingCombatOrKeepingTheModal()
    {
        yield return Load(ProjectScenes.Boss, () =>
            Object.FindFirstObjectByType<BossCombatController>().AutomaticPatterns = false);
        var coordinator = Object.FindFirstObjectByType<CutsceneBossEncounterCoordinator>();
        var runner = coordinator.GetComponentInChildren<CutsceneRunner>();
        Set(coordinator, "introDefinition", External("integration.boss.cancel"));
        CutsceneExternalStage pending = null;
        runner.ExternalStageRequested += stage => pending = stage;
        int starts = 0;
        BossCombatController boss = coordinator.EncounterBoss;
        boss.StatusChanged += () => { if (boss != null && boss.EncounterStarted) starts++; };
        Player.GetComponent<PlayerMove>().enabled = false;
        MovePlayer(new Vector2(-10f, -2.3f));
        yield return Until(() => pending != null);
        Scene oldScene = coordinator.gameObject.scene;
        Scene empty = SceneManager.CreateScene("Cancelled Boss Intro");
        SceneManager.SetActiveScene(empty);
        yield return SceneManager.UnloadSceneAsync(oldScene);
        pending.Complete();
        yield return null;
        Assert.That(starts, Is.Zero);
        Assert.That(UiModal.IsOpen, Is.False);
    }

    [UnityTest]
    public IEnumerator TutorialHoldsBelieverThroughDialogueThenPassesMissingTipAndDetectsTheSameDeath()
    {
        CutsceneExternalStage introStage = null;
        CutsceneDefinition intro = External("integration.tutorial.intro");
        CutsceneDefinition encounter = null;
        CutsceneDefinition afterDeath = External("integration.tutorial.after-death");
        Action<CutsceneExternalStage> capture = stage => introStage = stage;
        yield return Load(ProjectScenes.Tutorial, () =>
        {
            var flow = Object.FindFirstObjectByType<CutsceneTutorialFlow>();
            Assert.That(flow, Is.Not.Null);
            GameObject stone = flow.EncounterDefinition.Steps
                .First(step => step.Type == CutsceneStepType.HarmlessProjectile).HarmlessProjectilePrefab;
            encounter = ScriptableObject.CreateInstance<CutsceneDefinition>();
            encounter.Configure("integration.tutorial.encounter", new[]
            {
                CutsceneStep.Move("believer", "believerNear", 30f),
                CutsceneStep.Move("believer", "believerThrow", 30f),
                CutsceneStep.ThrowHarmlessly("believer", "playerTalk", stone, .1f, .2f),
                CutsceneStep.External("tutorial.believer.first-stone.tip")
            });
            definitions.Add(encounter);
            Set(flow, "introDefinition", intro);
            Set(flow, "encounterDefinition", encounter);
            Set(flow, "afterDeathDefinition", afterDeath);
            flow.Runner.ExternalStageRequested += capture;
            Object.FindFirstObjectByType<CutsceneTrigger>().Configure(flow.Runner, encounter, true, true);
        });
        var tutorial = Object.FindFirstObjectByType<CutsceneTutorialFlow>();
        BelieverEnemy believer = Object.FindObjectsByType<BelieverEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(enemy => enemy.name == "TutorialStoryBeliever");
        var body = believer.GetComponent<Rigidbody2D>();
        Assert.That(believer.enabled, Is.False);
        Assert.That(body.simulated, Is.False);
        yield return Until(() => introStage != null);
        introStage.Complete();
        yield return Until(() => !tutorial.Runner.IsRunning);
        Assert.That(believer.enabled, Is.False, "시작 독백 뒤에도 조우 대화 전에는 전투하지 않는다.");
        tutorial.Runner.ExternalStageRequested -= capture;
        float hp = Player.CurrentHp;
        Player.GetComponent<PlayerMove>().enabled = false;
        Transform zone = GameObject.Find("TutorialCutsceneAnchors").transform.Find("BelieverEncounter");
        MovePlayer(zone.position);
        yield return Until(() => tutorial.Runner.LastCompletedDefinition == encounter && !tutorial.Runner.IsRunning);
        Assert.That(believer.enabled, Is.True);
        Assert.That(body.simulated, Is.True);
        // 연출 돌은 TutorialStoryBeliever의 ThrowStoneAttack 피해와 동일하게 적용된다.
        // (ScriptedHitDamage = damage × DoctrineStats AttackMultiplier)
        float expected = believer.GetComponent<ThrowStoneAttack>().ScriptedHitDamage;
        Assert.That(Player.CurrentHp, Is.EqualTo(hp - expected).Within(0.001f),
            "첫 돌 연출은 실제 신도 돌과 같은 피해를 입힌다.");
        Assert.That(UiModal.IsOpen, Is.False);
        yield return null;
        var attack = Player.GetComponent<PlayerAttack>();
        typeof(PlayerAttack).GetMethod("OnAttackInput", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(attack, null);
        Assert.That(attack.IsAttacking, Is.True);
        Assert.That(Player.GetComponent<PlayerMove>().ControlLocked, Is.True);
        Assert.That(believer.TakeDamage(new DamageInfo(100000f, DamageSource.Doctrine, Player.gameObject)), Is.True);
        var watcher = tutorial.GetComponent<CutsceneDeathWatcher>();
        yield return Until(() => watcher.HasPlayed);
        yield return Until(() => tutorial.Runner.LastCompletedDefinition == afterDeath);
        Assert.That(watcher.IsWatching, Is.False);
        Assert.That(attack.IsAttacking, Is.False);
        Assert.That(Player.GetComponent<PlayerMove>().ControlLocked, Is.False, "취소한 공격의 이동 잠금을 복원하면 안 된다.");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;
        Scene empty = SceneManager.CreateScene("Parallel Integration Cleanup " + Guid.NewGuid());
        SceneManager.SetActiveScene(empty);
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene != empty && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
        foreach (CutsceneDefinition definition in definitions) if (definition != null) Object.Destroy(definition);
        definitions.Clear();
        yield return null;
    }
}
