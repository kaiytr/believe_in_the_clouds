using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class StageIntegrationPlayModeTests
{
    private static IEnumerator Load(string name)
    {
        Time.timeScale = 1f;
        Type editor = AppDomain.CurrentDomain.GetAssemblies().Select(assembly =>
            assembly.GetType("UnityEditor.SceneManagement.EditorSceneManager",false)).First(type => type != null);
        MethodInfo method = editor.GetMethod("LoadSceneAsyncInPlayMode",BindingFlags.Public | BindingFlags.Static,
            null,new[] { typeof(string),typeof(LoadSceneParameters) },null);
        var load = (AsyncOperation)method.Invoke(null,new object[] {
            "Assets/Scenes/" + name + ".unity",new LoadSceneParameters(LoadSceneMode.Single) });
        while (!load.isDone) yield return null;
        yield return null;
        yield return new WaitForFixedUpdate();
    }

    private static void Set(object target,string name,object value)
    {
        Type type = target.GetType();
        FieldInfo field = null;
        while (type != null && field == null)
        {
            field = type.GetField(name,BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            type = type.BaseType;
        }
        field.SetValue(target,value);
    }
    private static void Attack(PlayerAttack attack) => typeof(PlayerAttack)
        .GetMethod("OnAttackInput",BindingFlags.Instance | BindingFlags.NonPublic).Invoke(attack,null);
    private static GameObject Player => GameObject.FindGameObjectWithTag("Player");

    [UnityTest]
    public IEnumerator AllTestScenesHaveTheCompletePlayerAndOneSharedHud()
    {
        foreach (string scene in new[] { "PlayerTEST","BeliefTest","BossTest","StageTest" })
        {
            yield return Load(scene);
            GameObject player = Player;
            Assert.That(player.GetComponent<PlayerMove>().BaseMoveSpeed,Is.EqualTo(7f),scene);
            Assert.That(player.GetComponent<PlayerAttack>().ComboCount,Is.EqualTo(4),scene);
            Assert.That(player.GetComponent<PlayerDash>(),Is.Not.Null,scene);
            Assert.That(player.GetComponent<PlayerHitReact>(),Is.Not.Null,scene);
            Assert.That(player.GetComponents<PlayerBody>().Length,Is.EqualTo(1),scene);
            Assert.That(player.GetComponent<PlayerCombatEvents>(),Is.Not.Null,scene);
            Assert.That(player.GetComponent<PlayerHealth>().BodyCollider,Is.SameAs(player.GetComponent<Collider2D>()),scene);
            Assert.That(player.GetComponent<DoctrineStats>(),Is.Not.Null,scene);
            Assert.That(Object.FindObjectsByType<DoctrineHud>(FindObjectsSortMode.None).Length,Is.EqualTo(1),scene);
            foreach (BossCombatController boss in Object.FindObjectsByType<BossCombatController>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                Assert.That(boss.transform.Find("Gameplay HUD/Player gameplay HUD"),Is.Null,scene);
        }
    }

    [UnityTest]
    public IEnumerator AuthoredBeliefPlayerMeleeDamagesEachEnemySpecies()
    {
        yield return Load("BeliefTest");
        var move = Player.GetComponent<PlayerMove>(); move.enabled = false; Set(move,"isGrounded",true);
        var playerBody = Player.GetComponent<Rigidbody2D>(); playerBody.bodyType = RigidbodyType2D.Kinematic;
        Player.transform.localScale = Vector3.one;
        var enemies = Object.FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None);
        foreach (EnemyUnit enemy in enemies)
        {
            enemy.enabled = false;
            enemy.GetComponent<EnemyAttack>().enabled = false;
            enemy.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            enemy.transform.position = new Vector3(50f,0f);
        }
        var attack = Player.GetComponent<PlayerAttack>(); Set(attack,"finishHitStop",0f);
        foreach (EnemyUnit enemy in enemies)
        {
            playerBody.position = Vector2.zero;
            enemy.transform.position = Vector2.right;
            Physics2D.SyncTransforms();
            float hp = enemy.CurrentHp;
            Attack(attack);
            yield return new WaitForSeconds(.13f);
            Assert.That(enemy.CurrentHp,Is.LessThan(hp),enemy.name);
            attack.CancelAttack();
            enemy.transform.position = new Vector3(50f,0f);
        }
    }

    [UnityTest]
    public IEnumerator AuthoredBossPlayerMeleePublishesARealHit()
    {
        yield return Load("BossTest");
        var boss = Object.FindFirstObjectByType<BossCombatController>(); boss.PauseAndCancelForTest();
        var move = Player.GetComponent<PlayerMove>(); move.enabled = false; Set(move,"isGrounded",true);
        var body = Player.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
        body.position = (Vector2)boss.transform.position + Vector2.left;
        Player.transform.localScale = Vector3.one;
        Physics2D.SyncTransforms();
        bool confirmed = false;
        Player.GetComponent<PlayerCombatEvents>().SignalPublished += signal =>
        { if (signal.Type == PlayerCombatSignalType.HitConfirmed && signal.Target == boss.gameObject) confirmed = true; };
        float hp = boss.CurrentHp;
        Attack(Player.GetComponent<PlayerAttack>());
        yield return new WaitForSeconds(.15f);
        Assert.That(boss.CurrentHp,Is.LessThan(hp)); Assert.That(confirmed,Is.True);
    }

    [UnityTest]
    public IEnumerator DashCancelsAttackRejectsNewAttacksAndReusesFadingAfterimages()
    {
        yield return Load("PlayerTEST");
        var move = Player.GetComponent<PlayerMove>(); move.enabled = false; Set(move,"isGrounded",true);
        Player.GetComponent<Rigidbody2D>().gravityScale = 0f;
        var attack = Player.GetComponent<PlayerAttack>();
        var dash = Player.GetComponent<PlayerDash>();
        var emitter = Player.GetComponent<CombatAfterimageEmitter>();
        Attack(attack); Assert.That(attack.IsAttacking,Is.True);
        Assert.That(dash.TryDash(),Is.True);
        Attack(attack);
        Assert.That(attack.IsAttacking,Is.False); Assert.That(move.ControlLocked,Is.True);
        yield return new WaitForSeconds(.09f);
        Assert.That(emitter.ActiveCount,Is.GreaterThan(0));
        foreach (var image in Object.FindObjectsByType<CombatAfterimage>(FindObjectsSortMode.None))
        {
            Assert.That(image.GetComponent<Collider2D>(),Is.Null);
            Assert.That(image.GetComponent<SpriteRenderer>().sprite,Is.EqualTo(Player.GetComponent<SpriteRenderer>().sprite));
        }
        dash.enabled = false;
        Assert.That(move.ControlLocked,Is.False); Assert.That(dash.IsDashing,Is.False);
        Assert.That(emitter.ActiveCount,Is.Zero); Assert.That(Player.GetComponent<Rigidbody2D>().linearVelocity.x,Is.Zero);
        dash.enabled = true;
        Assert.That(dash.TryDash(),Is.True);
        yield return new WaitForSeconds(.5f);
        Assert.That(dash.IsDashing,Is.False); Assert.That(move.ControlLocked,Is.False);
        Assert.That(emitter.ActiveCount,Is.Zero);
        Assert.That(emitter.CreatedCount,Is.LessThanOrEqualTo(5),"Subsequent dashes reuse the bounded pool.");
    }

    [UnityTest]
    public IEnumerator StageActivatesThreeSectionsThenTheBossOnEntry()
    {
        yield return Load("StageTest");
        var enemies = Object.FindObjectsByType<EnemyUnit>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        Assert.That(enemies.Length,Is.EqualTo(3));
        Assert.That(enemies.All(enemy => !enemy.gameObject.activeSelf),Is.True);
        var boss = Object.FindFirstObjectByType<BossCombatController>(FindObjectsInactive.Include);
        Assert.That(boss.gameObject.activeSelf,Is.False);
        Player.GetComponent<PlayerMove>().enabled = false;
        var body = Player.GetComponent<Rigidbody2D>(); body.gravityScale = 0f;
        foreach (var section in new[] { ("Dog",-54f),("Believer",-42f),("Executive",-30f) })
        {
            body.position = new Vector2(section.Item2,-2.3f); body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return null;
            var enemy = enemies.First(candidate => candidate.name == section.Item1);
            Assert.That(enemy.gameObject.activeSelf,Is.True,section.Item1);
            Assert.That(enemy.HasCombatTarget,Is.True,section.Item1);
            Assert.That(enemy.GetComponent<EnemyAttack>().IsConfigured,Is.True,section.Item1);
            Assert.That(boss.gameObject.activeSelf,Is.False);
        }
        body.position = new Vector2(-16f,-2.3f); body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate(); yield return null;
        Assert.That(boss.gameObject.activeSelf,Is.True);
        Assert.That(boss.EncounterStarted,Is.True,boss.StatusMessage);
        Assert.That(boss.ValidateEncounter(),Is.Empty);
        Assert.That(boss.Settings.WaveSpeed,Is.EqualTo(9f));
        Assert.That(boss.Settings.CloudDuration,Is.EqualTo(3f));
        Assert.That(Player.GetComponent<PlayerHealth>().IsDead,Is.False);
    }

    private static IEnumerator Until(Func<bool> predicate, float timeout = 8f)
    {
        float deadline = Time.time + timeout;
        while (!predicate() && Time.time < deadline) yield return null;
        Assert.That(predicate(), Is.True, "StageTest integration timed out.");
    }

    private static void PutPlayer(Vector2 position)
    {
        var body = Player.GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.position = position;
        Player.transform.position = position;
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }

    private static IEnumerator EnterStageBoss()
    {
        yield return Load("StageTest");
        Player.GetComponent<PlayerMove>().enabled = false;
        var boss = Object.FindFirstObjectByType<BossCombatController>(FindObjectsInactive.Include);
        boss.AutomaticPatterns = false;
        PutPlayer(new Vector2(-16f, -2.3f));
        yield return Until(() => boss.EncounterStarted);
        boss.PauseAndCancelForTest();
        Player.GetComponent<PlayerHealth>().ResetHealth();
    }

    [UnityTest]
    public IEnumerator StageEnemyCoinsWaitForDeathThenRestAtTheWorldPositionAsCameraMoves()
    {
        yield return Load("StageTest");
        EconomyRuntime.Ensure();
        EconomyService previous = Currency.Service;
        EconomyRuntime.Instance.SetService(new EconomyService(new MemoryEconomyStore(), new EconomyRandom(18)));
        try
        {
            yield return null;
            Player.GetComponent<PlayerMove>().enabled = false;
            PutPlayer(new Vector2(-54f, -2.3f));
            var enemy = Object.FindObjectsByType<EnemyUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(item => item is DogEnemy);
            yield return Until(() => enemy.gameObject.activeInHierarchy);
            enemy.enabled = false;
            foreach (var attack in enemy.GetComponents<EnemyAttack>()) attack.enabled = false;
            float deathDuration = (float)typeof(EnemyUnit).GetField("destroyDelay",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(enemy);
            var service = Currency.Service;
            Assert.That(enemy.TakeDamage(new DamageInfo(100000, DamageSource.Environment)), Is.True);
            Assert.That(enemy.TakeDamage(new DamageInfo(100000, DamageSource.Environment)), Is.False);
            int reward = service.PendingBalance;
            Assert.That(reward, Is.InRange(5, 7));
            var view = Object.FindObjectsByType<CoinFlightView>(FindObjectsSortMode.None).Single(item => item.HasFlights);
            yield return new WaitForSecondsRealtime(deathDuration * .5f);
            Assert.That(enemy != null, Is.True, "The death motion is still running.");
            Assert.That(view.GetComponentsInChildren<Image>().Any(item => item.enabled), Is.False);
            Assert.That(service.Balance, Is.Zero);
            yield return Until(() => enemy == null);
            Image coin = null;
            yield return Until(() => (coin = view.GetComponentsInChildren<Image>().FirstOrDefault(item => item.enabled)) != null);
            yield return new WaitForSecondsRealtime(EconomyRuntime.Instance.Settings.scatterSeconds + .03f);
            Assert.That(coin.rectTransform.sizeDelta, Is.EqualTo(Vector2.one * 24f));
            Assert.That(service.Balance, Is.Zero, "Ground rest precedes HUD settlement.");
            var camera = Camera.main;
            foreach (var behaviour in camera.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            Vector2 before = RectTransformUtility.WorldToScreenPoint(null, coin.rectTransform.position);
            Vector3 worldBefore = camera.WorldToScreenPoint(Vector3.zero);
            camera.transform.position += Vector3.right;
            Vector3 worldAfter = camera.WorldToScreenPoint(Vector3.zero);
            yield return null;
            yield return null;
            Vector2 after = RectTransformUtility.WorldToScreenPoint(null, coin.rectTransform.position);
            Assert.That(after.x - before.x, Is.EqualTo(worldAfter.x - worldBefore.x).Within(2f));
            yield return Until(() => !view.HasFlights, 5f);
            Assert.That(service.Balance, Is.EqualTo(reward));
            Assert.That(service.PendingBalance, Is.Zero);
        }
        finally { EconomyRuntime.Instance.SetService(previous); }
    }

    [UnityTest]
    public IEnumerator StageCloudShelterProtectsThenExposedPlayerIsHitAndCancellationLeavesNoStrike()
    {
        yield return EnterStageBoss();
        var boss = Object.FindFirstObjectByType<BossCombatController>();
        var player = Player.GetComponent<PlayerHealth>();
        // Use the actual StageTest settings, prefabs, terrain and collider at their authored values.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            boss.PauseAndCancelForTest();
            player.ResetHealth();
            PutPlayer(new Vector2(-4f, -2.3f));
            boss.ForcePatternForTest(BossPatternKind.CloudBelief);
            yield return Until(() => Object.FindFirstObjectByType<BossCloudLightning>() != null);
            var lightning = Object.FindFirstObjectByType<BossCloudLightning>();
            var clouds = Object.FindObjectsByType<BossCloudPlatform>(FindObjectsSortMode.None);
            Assert.That(clouds.Length, Is.EqualTo(2));
            var white = clouds.Single(item => item.IsWhite).GetComponent<SpriteRenderer>().bounds;
            Assert.That(white.min.x, Is.EqualTo(lightning.Shelter.Center.x - lightning.Shelter.Size.x * .5f).Within(.001f));
            Assert.That(white.max.x, Is.EqualTo(lightning.Shelter.Center.x + lightning.Shelter.Size.x * .5f).Within(.001f));
            Vector2 offset = attempt == 1 ? Vector2.left * (lightning.Shelter.Size.x * .5f + .1f) : Vector2.zero;
            PutPlayer((Vector2)Player.transform.position + lightning.Shelter.Center + offset - (Vector2)player.BodyCollider.bounds.center);
            float belief = boss.CurrentBelief;
            if (attempt == 2)
            {
                boss.PauseAndCancelForTest();
                yield return new WaitForSeconds(boss.Settings.CloudWarning + boss.Settings.CloudDuration + .1f);
                Assert.That(player.CurrentHp, Is.EqualTo(100f));
                Assert.That(Object.FindFirstObjectByType<BossCloudLightning>(), Is.Null);
                Assert.That(Object.FindObjectsByType<BossCloudPlatform>(FindObjectsSortMode.None), Is.Empty);
                continue;
            }
            yield return Until(() => lightning.HasStruck);
            Assert.That(Object.FindObjectsByType<DangerIndicator>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindFirstObjectByType<BossTimedDamageHazard>(), Is.Null);
            Assert.That(player.CurrentHp, Is.EqualTo(attempt == 0 ? 100f : 100f - boss.Settings.LightningDamage));
            Assert.That(boss.CurrentBelief, Is.EqualTo(attempt == 0 ? belief - boss.Settings.BeliefReduction : belief));
            yield return Until(() => !boss.IsPatternRunning);
        }
    }

    [UnityTest]
    public IEnumerator VisualCaptureStageLayoutSharedUiAndPlayerAfterimages()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            Assert.Ignore("Run the visual check with graphics enabled.");
        yield return Load("StageTest");
        yield return new WaitForSeconds(.35f);
        yield return Capture("01-stage-start");
        Player.GetComponent<PlayerMove>().enabled = false;
        var body = Player.GetComponent<Rigidbody2D>(); body.gravityScale = 0f;
        body.position = new Vector2(-52f,-2.3f); body.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(.35f);
        yield return Capture("02-dog-section");
        Player.GetComponent<PlayerDash>().TryDash();
        yield return new WaitForSeconds(.08f);
        yield return Capture("03-player-dash");
        yield return new WaitForSeconds(.2f);
        body.position = new Vector2(-4f,-2.3f); body.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(.35f);
        yield return Capture("04-boss-and-shared-ui");
    }

    private static IEnumerator Capture(string filename)
    {
        const int width = 1600, height = 900;
        Camera camera = Camera.main;
        var canvases = new List<Canvas>();
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) canvases.Add(canvas);
        var texture = new RenderTexture(width,height,24);
        var image = new Texture2D(width,height,TextureFormat.RGB24,false);
        RenderTexture previous = RenderTexture.active;
        camera.targetTexture = texture;
        foreach (Canvas canvas in canvases)
        { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases(); yield return null;
        camera.Render(); RenderTexture.active = texture;
        image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/StageTestVerification"));
        Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory,filename + ".png"),image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous;
        foreach (Canvas canvas in canvases)
        { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
        texture.Release(); Object.Destroy(texture); Object.Destroy(image);
    }
}
