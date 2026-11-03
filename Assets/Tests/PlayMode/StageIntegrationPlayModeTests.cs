using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
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
            ProjectScenes.ForName(name),new LoadSceneParameters(LoadSceneMode.Single) });
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
    public IEnumerator AllProjectScenesHaveTheCompletePlayerAndSharedScreens()
    {
        foreach (string path in ProjectScenes.AllPaths)
        {
            string scene = Path.GetFileNameWithoutExtension(path);
            yield return Load(scene);
            GameObject player = Player;
            Assert.That(player.GetComponent<PlayerMove>().BaseMoveSpeed,Is.EqualTo(7f),scene);
            Assert.That(player.GetComponent<PlayerAttack>().ComboCount,Is.EqualTo(3),scene);
            Assert.That(player.GetComponent<PlayerDash>(),Is.Not.Null,scene);
            Assert.That(player.GetComponent<PlayerHitReact>(),Is.Not.Null,scene);
            Assert.That(player.GetComponents<PlayerBody>().Length,Is.EqualTo(1),scene);
            Assert.That(player.GetComponent<PlayerCombatEvents>(),Is.Not.Null,scene);
            Assert.That(player.GetComponent<PlayerHealth>().BodyCollider,Is.SameAs(player.GetComponent<Collider2D>()),scene);
            Assert.That(player.GetComponent<DoctrineStats>(),Is.Not.Null,scene);
            Assert.That(Object.FindObjectsByType<DoctrineHud>(FindObjectsSortMode.None).Length,Is.EqualTo(1),scene);
            Assert.That(DoctrineHud.CurrencyTarget, Is.Not.Null, scene);
            Assert.That(Object.FindObjectsByType<ShopScreen>(FindObjectsSortMode.None).Length, Is.EqualTo(1), scene);
            Assert.That(Object.FindObjectsByType<FoundationScreen>(FindObjectsSortMode.None).Length, Is.EqualTo(1), scene);
            Assert.That(Object.FindObjectsByType<DoctrineSlotStore>(FindObjectsSortMode.None).Length, Is.EqualTo(1), scene);
            var attack = player.GetComponent<PlayerAttack>();
            Assert.That(typeof(PlayerAttack).GetField("downFanRadius", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(attack), Is.EqualTo(2.6f), scene);
            Assert.That(typeof(PlayerAttack).GetField("downFanHalfAngle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(attack), Is.EqualTo(65f), scene);
            Assert.That(typeof(PlayerAttack).GetField("downFanPlayerBounce", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(attack), Is.EqualTo(8f), scene);
            foreach (BossCombatController boss in Object.FindObjectsByType<BossCombatController>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                Assert.That(boss.transform.Find("Gameplay HUD/Player gameplay HUD"),Is.Null,scene);
            yield return CheckHudTooltip(player, scene);
        }
    }

    private static IEnumerator CheckHudTooltip(GameObject player, string scene)
    {
        var manager = DoctrineManager.Instance;
        manager.Clear();
        var rule = new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal);
        Assert.That(manager.TryActivate(rule, player, out string reason), Is.True, scene + ": " + reason);
        Transform tooltip = null;
        try
        {
            DoctrineSlotView slot = null;
            float deadline = Time.realtimeSinceStartup + 1.5f;
            while (slot == null && Time.realtimeSinceStartup < deadline)
            {
                slot = Object.FindObjectsByType<DoctrineSlotView>(FindObjectsSortMode.None)
                    .FirstOrDefault(item => item.Doctrine != null && item.Doctrine.HasSameRule(rule));
                if (slot == null) yield return null;
            }
            Assert.That(slot, Is.Not.Null, scene + ": owned doctrine did not appear after player rebinding.");
            yield return null; // Let the new CanvasRenderer enter the frame's pointer-raycast registry.
            var button = slot.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, scene);
            Assert.That(button.targetGraphic.raycastTarget, Is.True, scene);
            Canvas.ForceUpdateCanvases();
            var eventSystem = UiEventSystemBoot.Ensure();
            var slotRect = slot.GetComponent<RectTransform>();
            var pointer = new PointerEventData(eventSystem) {
                position = RectTransformUtility.WorldToScreenPoint(null, slotRect.TransformPoint(slotRect.rect.center)),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), scene + ": HUD did not receive a pointer raycast.");
            Assert.That(hits[0].gameObject.transform.IsChildOf(slot.transform), Is.True, scene + ": another UI element blocks the doctrine slot.");
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            var hud = Object.FindFirstObjectByType<DoctrineHud>();
            tooltip = hud.GetComponentInParent<Canvas>().transform.Find("DoctrineTooltip");
            Assert.That(tooltip != null && tooltip.gameObject.activeInHierarchy, Is.True, scene);
            Assert.That(tooltip.Find("TooltipTexts/TooltipBody").GetComponent<Text>().text, Is.EqualTo(DoctrineText.Describe(rule)), scene);
            Assert.That(tooltip.GetComponent<Canvas>().overrideSorting, Is.True, scene);
            Assert.That(tooltip.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(110), "Tooltip must render above scene test panels.");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null &&
                (scene == "ShopTest" || scene == "EconomyTest" || scene == "EnemyTest"))
                yield return Capture("06-shared-tooltip-" + scene);
            if (scene == "ShopTest")
            {
                yield return new WaitForSecondsRealtime(3.7f);
                Assert.That(tooltip.gameObject.activeSelf, Is.False, "The tooltip must fade out after its display period.");
            }
        }
        finally
        {
            manager.Deactivate(rule);
            if (tooltip != null) tooltip.gameObject.SetActive(false);
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
    public IEnumerator DownwardAirAttackHitsBelowBouncesAndRequiresLandingBeforeReuse()
    {
        yield return Load("BeliefTest");
        var move = Player.GetComponent<PlayerMove>();
        move.enabled = false;
        Set(move, "isGrounded", false);
        PutPlayer(new Vector2(0f, 4f));
        var enemies = Object.FindObjectsByType<EnemyUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(item => item.name).ToArray();
        Assert.That(enemies.Length, Is.EqualTo(3));
        var positions = new[] { new Vector2(0f, 2f), new Vector2(4f, 4f), new Vector2(0f, 6f) };
        for (int i = 0; i < enemies.Length; i++)
        {
            var enemy = enemies[i];
            enemy.enabled = true;
            enemy.gameObject.SetActive(true);
            foreach (var attack in enemy.GetComponents<EnemyAttack>()) attack.enabled = false;
            foreach (var contact in enemy.GetComponents<EnemyContactDamage>()) contact.SetSuppressed(true);
            var enemyBody = enemy.GetComponent<Rigidbody2D>();
            enemyBody.bodyType = RigidbodyType2D.Kinematic;
            enemyBody.position = positions[i];
            enemyBody.linearVelocity = Vector2.zero;
            enemy.transform.position = positions[i];
        }
        Physics2D.SyncTransforms();
        yield return null; // Let each newly activated EnemyUnit.Start bind its real player target.
        foreach (var enemy in enemies)
        {
            enemy.enabled = false;
            enemy.LockMovement();
            enemy.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        }
        Physics2D.SyncTransforms();
        float[] hp = enemies.Select(item => item.CurrentHp).ToArray();
        var playerAttack = Player.GetComponent<PlayerAttack>();
        Attack(playerAttack);
        yield return new WaitForSeconds(.15f);
        Assert.That(enemies[0].CurrentHp, Is.LessThan(hp[0]));
        Assert.That(enemies[1].CurrentHp, Is.EqualTo(hp[1]));
        Assert.That(enemies[2].CurrentHp, Is.EqualTo(hp[2]));
        Assert.That(Player.GetComponent<Rigidbody2D>().linearVelocity.y, Is.GreaterThan(0f));
        float afterFirst = enemies[0].CurrentHp;
        yield return new WaitForSeconds(.3f);
        Attack(playerAttack);
        yield return new WaitForSeconds(.15f);
        Assert.That(enemies[0].CurrentHp, Is.EqualTo(afterFirst), "A second air attack needs a landing first.");
        Set(move, "isGrounded", true);
        yield return new WaitForSeconds(.04f);
        Set(move, "isGrounded", false);
        PutPlayer(new Vector2(0f, 4f));
        for (int i = 0; i < enemies.Length; i++)
        {
            var body = enemies[i].GetComponent<Rigidbody2D>();
            body.position = positions[i];
            body.linearVelocity = Vector2.zero;
            enemies[i].transform.position = positions[i];
        }
        Physics2D.SyncTransforms();
        Player.transform.localScale = new Vector3(-Mathf.Abs(Player.transform.localScale.x), Player.transform.localScale.y, 1f);
        Attack(playerAttack);
        yield return new WaitForSeconds(.15f);
        Assert.That(enemies[0].CurrentHp, Is.LessThan(afterFirst), "The downward fan must also hit while facing left.");
        Assert.That(enemies[1].CurrentHp, Is.EqualTo(hp[1]));
        Assert.That(enemies[2].CurrentHp, Is.EqualTo(hp[2]));
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
        Sprite emittedSprite = Player.GetComponent<SpriteRenderer>().sprite;
        Assert.That(dash.TryDash(),Is.True);
        var firstImage = Object.FindFirstObjectByType<CombatAfterimage>().GetComponent<SpriteRenderer>();
        Assert.That(firstImage.sprite,Is.SameAs(emittedSprite));
        float firstOpacity = firstImage.color.a;
        Attack(attack);
        Assert.That(attack.IsAttacking,Is.False); Assert.That(move.ControlLocked,Is.True);
        yield return new WaitForSeconds(.09f);
        Assert.That(emitter.ActiveCount,Is.GreaterThan(0));
        Assert.That(firstImage.sprite,Is.SameAs(emittedSprite),"Afterimages retain the emitted frame while the player keeps animating.");
        Assert.That(firstImage.color.a,Is.LessThan(firstOpacity));
        foreach (var image in Object.FindObjectsByType<CombatAfterimage>(FindObjectsSortMode.None))
        {
            Assert.That(image.GetComponent<Collider2D>(),Is.Null);
            Assert.That(image.GetComponent<SpriteRenderer>().sprite,Is.Not.Null);
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

    [UnityTest]
    public IEnumerator DevelopmentStageRestartsWithoutBeingInTheGameplayBuild()
    {
        yield return Load("StageTest");
        Assert.That(SceneUtility.GetBuildIndexByScenePath(ProjectScenes.StageTest), Is.EqualTo(-1));
        var original = Object.FindFirstObjectByType<StageTestController>();
        Player.GetComponent<PlayerHealth>().TakeDamage(10f);
        original.Restart();
        yield return Until(() => original == null && Object.FindFirstObjectByType<StageTestController>() != null);
        yield return null;
        var player = Player.GetComponent<PlayerHealth>();
        Assert.That(player.CurrentHp, Is.EqualTo(player.MaxHp));
        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(ProjectScenes.StageTest));
        Assert.That(Object.FindObjectsByType<EnemyUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .All(enemy => !enemy.gameObject.activeSelf), Is.True);
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
        float previousScatter = EconomyRuntime.Instance.Settings.scatterRadius;
        EconomyRuntime.Instance.Settings.scatterRadius = 0f;
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
            CoinDropOrigin dropOrigin = null;
            service.FlightRequested += request => dropOrigin = request.DropOrigin;
            Vector3 hitPosition = enemy.BodyCollider.bounds.center;
            // Keep both hit and corpse positions visible, so screen-edge clamping
            // cannot hide the world-position movement in the camera assertions.
            var dropCamera = Camera.main;
            foreach (var behaviour in dropCamera.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            dropCamera.transform.position = new Vector3(hitPosition.x + 3f, dropCamera.transform.position.y, dropCamera.transform.position.z);
            Assert.That(enemy.TakeDamage(new DamageInfo(100000, DamageSource.Environment)), Is.True);
            Assert.That(dropOrigin, Is.Not.Null);
            // PlayerAttack applies its knockback after damage has already caused Die().
            Rigidbody2D corpse = enemy.GetComponent<Rigidbody2D>();
            corpse.gravityScale = 0f;
            corpse.AddForce(Vector2.right * 4f, ForceMode2D.Impulse);
            Assert.That(enemy.TakeDamage(new DamageInfo(100000, DamageSource.Environment)), Is.False);
            int reward = service.PendingBalance;
            Assert.That(reward, Is.InRange(5, 7));
            var view = Object.FindObjectsByType<CoinFlightView>(FindObjectsSortMode.None).Single(item => item.HasFlights);
            yield return new WaitForSecondsRealtime(deathDuration * .5f);
            Assert.That(enemy != null, Is.True, "The death motion is still running.");
            Assert.That(view.GetComponentsInChildren<Image>().Any(item => item.enabled), Is.False);
            Assert.That(service.Balance, Is.Zero);
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(deathDuration);
            Assert.That(enemy != null, Is.True, "A paused corpse has not completed its death motion.");
            Assert.That(view.GetComponentsInChildren<Image>().Any(item => item.enabled), Is.False);
            Time.timeScale = 1f;
            yield return Until(() => enemy == null);
            Vector3 finalPosition = dropOrigin.Position;
            Assert.That(dropOrigin.IsReady, Is.True);
            Assert.That(finalPosition.x, Is.GreaterThan(hitPosition.x + .5f), "The corpse moved after the lethal hit.");
            Image coin = null;
            yield return Until(() => (coin = view.GetComponentsInChildren<Image>().FirstOrDefault(item => item.enabled)) != null);
            float expectedDropX = Mathf.Clamp(Camera.main.WorldToScreenPoint(finalPosition).x, 12f, Screen.width - 12f);
            float oldDropX = Mathf.Clamp(Camera.main.WorldToScreenPoint(hitPosition).x, 12f, Screen.width - 12f);
            float actualDropX = RectTransformUtility.WorldToScreenPoint(null, coin.rectTransform.position).x;
            Assert.That(actualDropX, Is.EqualTo(expectedDropX).Within(2f));
            Assert.That(Mathf.Abs(actualDropX - oldDropX), Is.GreaterThan(5f), "Coins must originate from the corpse, not the lethal-hit position.");
            yield return new WaitForSecondsRealtime(EconomyRuntime.Instance.Settings.scatterSeconds + .03f);
            Assert.That(coin.rectTransform.sizeDelta, Is.EqualTo(Vector2.one * 30f));
            Assert.That(service.Balance, Is.Zero, "Ground rest precedes HUD settlement.");
            var corners = new Vector3[4];
            coin.rectTransform.GetWorldCorners(corners);
            float groundScreenY = Camera.main.WorldToScreenPoint(new Vector3(0f, -3.35f)).y;
            Assert.That(RectTransformUtility.WorldToScreenPoint(null, corners[0]).y, Is.EqualTo(groundScreenY).Within(2f));
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
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                yield return Capture("05-coins-resting-on-ground");
            yield return Until(() => !view.HasFlights, 5f);
            Assert.That(service.Balance, Is.EqualTo(reward));
            Assert.That(service.PendingBalance, Is.Zero);
        }
        finally
        {
            Time.timeScale = 1f;
            EconomyRuntime.Instance.Settings.scatterRadius = previousScatter;
            EconomyRuntime.Instance.SetService(previous);
        }
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
