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

public sealed class EconomyPlayModeTests
{
    private EconomyService previousService;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        EconomyRuntime.Ensure();
        previousService = Currency.Service;
        EconomyRuntime.Instance.SetService(new EconomyService(new MemoryEconomyStore(), new EconomyRandom(18)));
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (PlayerDeathCoordinator.Active != null) Object.Destroy(PlayerDeathCoordinator.Active.gameObject);
        yield return null;
        Time.timeScale = 1;
        ShopScreen.Instance?.Close();
        DoctrineManager.Instance?.Clear();
        if (previousService != null) EconomyRuntime.Instance.SetService(previousService);
        yield return null;
    }

    private static IEnumerator Load(string name)
    {
        Type editor = AppDomain.CurrentDomain.GetAssemblies().Select(assembly =>
            assembly.GetType("UnityEditor.SceneManagement.EditorSceneManager", false)).First(type => type != null);
        MethodInfo method = editor.GetMethod("LoadSceneAsyncInPlayMode", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(string), typeof(LoadSceneParameters) }, null);
        var load = (AsyncOperation)method.Invoke(null, new object[] {
            ProjectScenes.ForName(name), new LoadSceneParameters(LoadSceneMode.Single) });
        while (!load.isDone) yield return null;
        yield return null; yield return new WaitForFixedUpdate();
    }

    [UnityTest]
    public IEnumerator ActualEnemyDeathsRewardEnvironmentalDamageOnceAndDisableNeverRewards()
    {
        yield return Load("BeliefTest");
        var enemies = Object.FindObjectsByType<EnemyUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Assert.That(enemies.Length, Is.EqualTo(3));
        int before = Currency.Balance + Currency.PendingBalance;
        foreach (EnemyUnit enemy in enemies)
        {
            enemy.enabled = false;
            foreach (EnemyAttack attack in enemy.GetComponents<EnemyAttack>()) attack.enabled = false;
            enemy.gameObject.SetActive(false); enemy.gameObject.SetActive(true);
        }
        Assert.That(Currency.Balance + Currency.PendingBalance, Is.EqualTo(before));
        foreach (EnemyUnit enemy in enemies)
        {
            Assert.That(enemy.TakeDamage(new DamageInfo(100000, DamageSource.Environment)), Is.True);
            Assert.That(enemy.TakeDamage(new DamageInfo(100000, DamageSource.Environment)), Is.False);
        }
        PlayStatistics statistics = Currency.Service.StatisticsSnapshot();
        Assert.That(statistics.Count(EnemyRewardKind.Believer), Is.EqualTo(1));
        Assert.That(statistics.Count(EnemyRewardKind.Dog), Is.EqualTo(1));
        Assert.That(statistics.Count(EnemyRewardKind.Executive), Is.EqualTo(1));
        int total = Currency.Balance + Currency.PendingBalance - before;
        Assert.That(total, Is.InRange(15, 20));
        Assert.That(Currency.Service.Snapshot().processedTransactions.Count(item => item.requestKey.StartsWith("defeat|")), Is.EqualTo(3));
    }

    [UnityTest]
    public IEnumerator RealDeathExecutesEffectsOnceClearsOnlyPlayerEquipmentAndLosesFourOfThirteen()
    {
        yield return Load("EconomyTest");
        var context = Object.FindFirstObjectByType<TestEconomyContext>(); context.PrepareEquipmentFixture();
        var service = Currency.Service;
        var player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerHealth>();
        var other = new GameObject("Enemy doctrine owner");
        other.AddComponent<DoctrineStats>();
        var subject = other.AddComponent<DoctrineSubject>();
        // The owner-filtered clear must preserve another actor, even with a shared rule.
        var manager = DoctrineManager.Instance;
        var enemyRule = new Doctrine(SubjectType.Enemy, ConditionType.OnDeath, ResultType.Explode);
        Assert.That(manager.TryActivate(enemyRule, other, out string error), Is.True, error);
        int deathEvents = 0;
        void Observe(DoctrineEventData evt) { if (evt.SubjectObject == player.gameObject && evt.Condition == ConditionType.OnDeath) deathEvents++; }
        DoctrineEventBus.EventRaised += Observe;
        service.TryGrant(100, "fixture");
        service.TryGrantWithEffect(10, Vector3.zero, "fixture", "pending-before-death");
        player.TakeDamage(100000);
        yield return null;
        Assert.That(player.IsDead, Is.True); Assert.That(deathEvents, Is.EqualTo(1));
        float resultDeadline = Time.realtimeSinceStartup + 5f;
        while (Object.FindFirstObjectByType<DeathResultView>() == null && Time.realtimeSinceStartup < resultDeadline)
            yield return null;
        Assert.That(Object.FindFirstObjectByType<DeathResultView>(), Is.Not.Null);
        Assert.That(service.Balance + service.PendingBalance, Is.EqualTo(110));
        Assert.That(service.CompleteDeferredDeath(player.GetComponent<PlayerDeathCoordinator>().DeathId).Success, Is.True);
        Assert.That(context.Equipment.EquippedCount, Is.Zero);
        Assert.That(EconomyLedger.Total(service.Snapshot().pieces), Is.EqualTo(9));
        Assert.That(service.Balance, Is.EqualTo(77)); Assert.That(service.PendingBalance, Is.Zero);
        Assert.That(manager.ActiveDoctrines.Any(item => item.Owner == other), Is.True);
        Assert.That(manager.ActiveDoctrines.Any(item => item.Owner == player.gameObject), Is.False);
        Assert.That(player.GetComponent<DoctrineStats>().SpeedMultiplier, Is.EqualTo(1f));
        player.TakeDamage(100000); Assert.That(deathEvents, Is.EqualTo(1));
        DoctrineEventBus.EventRaised -= Observe; Object.Destroy(other);
    }

    [UnityTest]
    public IEnumerator CoinsArriveWithPausedTimeReuseThePoolAndStopAtTheHud()
    {
        yield return Load("EconomyTest");
        var context = Object.FindFirstObjectByType<TestEconomyContext>(); context.ResetFixture();
        yield return null;
        Time.timeScale = 0;
        var service = Currency.Service;
        service.TryGrantWithEffect(7, new Vector3(3, 0), "fixture", "first-flight");
        Assert.That(service.Balance, Is.Zero); Assert.That(service.PendingBalance, Is.EqualTo(7));
        yield return new WaitForSecondsRealtime(.1f);
        var flight = Object.FindObjectsByType<CoinFlightView>(FindObjectsSortMode.None).First(item => item.HasFlights);
        Assert.That(flight.ActiveCount, Is.GreaterThan(0));
        foreach (Image image in flight.GetComponentsInChildren<Image>()) Assert.That(image.raycastTarget, Is.False);
        yield return new WaitForSecondsRealtime(2.2f);
        Assert.That(service.Balance, Is.EqualTo(7)); Assert.That(service.PendingBalance, Is.Zero);
        int created = flight.CreatedCount;
        service.TryGrantWithEffect(7, new Vector3(10000, 0), "fixture", "offscreen");
        yield return new WaitForSecondsRealtime(2.2f);
        Assert.That(service.Balance, Is.EqualTo(14)); Assert.That(flight.CreatedCount, Is.EqualTo(created));
        Assert.That(DoctrineHud.CurrencyTarget.GetComponentInChildren<Text>().text, Does.Contain("14"));
    }

    [UnityTest]
    public IEnumerator CoinsLandOnTheGroundSurfaceBeforeRestingAndFlyingToTheHud()
    {
        yield return Load("EconomyTest");
        Object.FindFirstObjectByType<TestEconomyContext>().ResetFixture();
        yield return null;
        var platform = new GameObject("Raised coin landing surface", typeof(BoxCollider2D));
        platform.layer = LayerMask.NameToLayer("Ground");
        platform.transform.position = new Vector3(5f, -1.5f);
        var floor = platform.GetComponent<BoxCollider2D>();
        floor.size = new Vector2(4f, .5f);
        var trigger = new GameObject("Ground trigger is not a landing surface", typeof(BoxCollider2D));
        trigger.layer = platform.layer;
        trigger.transform.position = new Vector3(5f, .25f);
        trigger.GetComponent<BoxCollider2D>().size = new Vector2(4f, .2f);
        trigger.GetComponent<BoxCollider2D>().isTrigger = true;
        var blocker = new GameObject("Non-ground collider is not a landing surface", typeof(BoxCollider2D));
        blocker.transform.position = new Vector3(5f, -.5f);
        blocker.GetComponent<BoxCollider2D>().size = new Vector2(4f, .2f);
        try
        {
            Physics2D.SyncTransforms();
            var service = Currency.Service;
            Vector3 origin = new Vector3(5f, 1f);
            service.TryGrantWithEffect(1, origin, "fixture", "land-before-hud");
            yield return null;
            var view = Object.FindObjectsByType<CoinFlightView>(FindObjectsSortMode.None).Single(item => item.HasFlights);
            Image coin = view.GetComponentsInChildren<Image>().Single(item => item.enabled);
            yield return new WaitForSecondsRealtime(EconomyRuntime.Instance.Settings.scatterSeconds + .03f);
            var corners = new Vector3[4];
            coin.rectTransform.GetWorldCorners(corners);
            float bottom = RectTransformUtility.WorldToScreenPoint(null, corners[0]).y;
            float surface = Camera.main.WorldToScreenPoint(new Vector3(5f, floor.bounds.max.y)).y;
            Assert.That(bottom, Is.EqualTo(surface).Within(1f), "The coin must touch the raised Ground collider, ignoring triggers and actors.");
            Assert.That(bottom, Is.LessThan(Camera.main.WorldToScreenPoint(origin).y - 40f));
            Assert.That(coin.rectTransform.sizeDelta, Is.EqualTo(Vector2.one * 30f));
            Assert.That(service.Balance, Is.Zero);
            Assert.That(service.PendingBalance, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.1f);
            coin.rectTransform.GetWorldCorners(corners);
            Assert.That(RectTransformUtility.WorldToScreenPoint(null, corners[0]).y, Is.EqualTo(surface).Within(1f));
            Assert.That(service.Balance, Is.Zero, "The coin must finish its ground rest before flying to the HUD.");
            float deadline = Time.realtimeSinceStartup + 4f;
            while (view.HasFlights && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(view.HasFlights, Is.False);
            Assert.That(service.Balance, Is.EqualTo(1));
            Assert.That(service.PendingBalance, Is.Zero);
        }
        finally
        {
            Object.Destroy(platform);
            Object.Destroy(trigger);
            Object.Destroy(blocker);
        }
    }

    [UnityTest]
    public IEnumerator PresentationDelayHoldsTheFlightWithoutLosingOrDuplicatingTheReward()
    {
        yield return Load("EconomyTest");
        Object.FindFirstObjectByType<TestEconomyContext>().ResetFixture();
        yield return null;
        var service = Currency.Service;
        var result = service.TryRewardDefeat(EnemyRewardKind.Dog, "delayed-defeat", Vector3.zero, presentationDelay: 0.6f);
        Assert.That(result.Success, Is.True);
        // Recorded immediately, before any coin has visually appeared.
        Assert.That(service.Balance, Is.Zero);
        Assert.That(service.PendingBalance, Is.EqualTo(result.AppliedAmount));
        yield return new WaitForSecondsRealtime(.3f);
        // Still inside the death-motion delay: nothing settled yet, and nothing was lost.
        Assert.That(service.Balance, Is.Zero);
        Assert.That(service.PendingBalance, Is.EqualTo(result.AppliedAmount));
        float deadline = Time.realtimeSinceStartup + 4f;
        while (service.PendingBalance > 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(service.Balance, Is.EqualTo(result.AppliedAmount));
        Assert.That(service.PendingBalance, Is.Zero);
    }

    [UnityTest]
    public IEnumerator FailedDefeatRewardKeepsFinalCorpsePositionAfterSourceIsDestroyed()
    {
        yield return Load("EconomyTest");
        var memory = new MemoryEconomyStore();
        EconomyRuntime.Instance.SetService(new EconomyService(memory, new EconomyRandom(1)));
        var corpse = new GameObject("Moving corpse origin");
        var origin = new CoinDropOrigin(corpse.transform, Vector3.zero);
        CoinDropOrigin requestedOrigin = null;
        int requests = 0;
        Currency.Service.FlightRequested += request => { requestedOrigin = request.DropOrigin; requests++; };
        memory.FailWrites = true;
        EconomyRuntime.Instance.RewardDefeat(EnemyRewardKind.Dog, "moving-corpse-retry", Vector3.zero, dropOrigin: origin);
        corpse.transform.position = new Vector3(3f, 1f, 0f);
        origin.Freeze();
        Object.Destroy(corpse);
        yield return null;
        memory.FailWrites = false;
        yield return new WaitForSecondsRealtime(.7f);
        Assert.That(requestedOrigin, Is.SameAs(origin));
        Assert.That(requestedOrigin.Position, Is.EqualTo(new Vector3(3f, 1f, 0f)));
        Assert.That(requestedOrigin.IsReady, Is.True);
        EconomyRuntime.Instance.RewardDefeat(EnemyRewardKind.Dog, "moving-corpse-retry", Vector3.zero, dropOrigin: origin);
        Assert.That(requests, Is.EqualTo(1), "Retries cannot launch another drop.");
    }

    [UnityTest]
    public IEnumerator MissingHudSettlesAndSceneTransitionFlushesWithoutLateDoublePayment()
    {
        yield return Load("EconomyTest");
        Object.FindFirstObjectByType<TestEconomyContext>().ResetFixture();
        yield return null;
        var service = Currency.Service;
        var hud = Object.FindFirstObjectByType<DoctrineHud>();
        Object.Destroy(hud.gameObject); yield return null;
        service.TryGrantWithEffect(6, Vector3.zero, "fixture", "no-hud");
        yield return new WaitForSecondsRealtime(.1f);
        Assert.That(service.Balance, Is.EqualTo(6));
        service.TryGrantWithEffect(8, Vector3.one, "fixture", "transition");
        yield return Load("PlayerTEST");
        Assert.That(service.Balance, Is.EqualTo(14)); Assert.That(service.PendingBalance, Is.Zero);
        service.TrySettleToken("transition", "0"); Assert.That(service.Balance, Is.EqualTo(14));
    }

    [UnityTest]
    public IEnumerator ShopShowsPricesAndKeepsTheCartWhenSavingFails()
    {
        yield return Load("EconomyTest");
        var context = Object.FindFirstObjectByType<TestEconomyContext>(); context.ResetFixture();
        var failing = new MemoryEconomyStore();
        EconomyRuntime.Instance.SetService(new EconomyService(failing));
        Currency.TryGrant(100, "fixture");
        var shop = Object.FindFirstObjectByType<ShopInteractable>();
        var player = GameObject.FindGameObjectWithTag("Player");
        ShopScreen.Instance.Open(shop, player);
        yield return null;
        var card = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(item => item.name == "Catalog_subject.player");
        var priceLabel = card.GetComponentsInChildren<Text>().First(item => item.name == "Price");
        Assert.That(priceLabel.text, Does.Contain("30"));
        card.onClick.Invoke();
        // 중복 담기: 같은 카드를 두 번 누르면 구매 칸에 2개 쌓인다.
        card.onClick.Invoke();
        var buy = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(item => item.name == "BuyButton");
        failing.FailWrites = true; buy.onClick.Invoke();
        Assert.That(Currency.Balance, Is.EqualTo(100)); Assert.That(Currency.Service.CountPiece("subject.player"), Is.Zero);
        failing.FailWrites = false; buy.onClick.Invoke();
        Assert.That(Currency.Balance, Is.EqualTo(40)); Assert.That(Currency.Service.CountPiece("subject.player"), Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator SlotPurchaseUsesCurrencyAndExpandsTheFoundationWithoutClearingThePieceCart()
    {
        yield return Load("EconomyTest");
        var memory = new MemoryEconomyStore();
        EconomyRuntime.Instance.SetService(new EconomyService(memory));
        Currency.TryGrant(130, "slot-integration");
        var player = GameObject.FindGameObjectWithTag("Player");
        ShopScreen.Instance.Open(Object.FindFirstObjectByType<ShopInteractable>(), player);
        yield return null;
        var slots = DoctrineSlotStore.EnsureExists();
        int before = slots.SlotCount;
        // 조각 1개 + 교리칸 1개를 구매 칸에 담는다 (13번째 자리).
        var buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        var pieceCard = buttons.Single(item => item.name == "Catalog_subject.player");
        var slotCard = buttons.Single(item => item.name == "Catalog_slot");
        pieceCard.onClick.Invoke();
        slotCard.onClick.Invoke();
        buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        var buy = buttons.Single(item => item.name == "BuyButton");
        Canvas.ForceUpdateCanvases();
        var totalLabel = Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Single(item => item.name == "Total");
        Assert.That(totalLabel.text, Does.Contain("130"), "The total must show the cart sum.");
        memory.FailWrites = true;
        buy.onClick.Invoke();
        Assert.That(Currency.Balance, Is.EqualTo(130));
        Assert.That(slots.SlotCount, Is.EqualTo(before));
        memory.FailWrites = false;
        buy.onClick.Invoke();
        Assert.That(Currency.Balance, Is.Zero);
        Assert.That(slots.SlotCount, Is.EqualTo(before + 1));
        Assert.That(FoundationScreen.Instance.DoctrineCount, Is.EqualTo(before + 1));
        Assert.That(Currency.Service.CountPiece("subject.player"), Is.EqualTo(1));
        // 잔액 0에서 교리칸을 다시 담아도 결제되지 않는다.
        buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        buttons.Single(item => item.name == "Catalog_slot").onClick.Invoke();
        buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        buttons.Single(item => item.name == "BuyButton").onClick.Invoke();
        Assert.That(Currency.Balance, Is.Zero);
        Assert.That(slots.SlotCount, Is.EqualTo(before + 1), "An unaffordable slot purchase must not add a slot.");
        var foundationObject = new GameObject("Slot purchase integration foundation", typeof(FoundationInteractable));
        try
        {
            ShopScreen.Instance.Close();
            FoundationScreen.Instance.Open(foundationObject.GetComponent<FoundationInteractable>(), player);
            yield return null;
            Assert.That(FoundationScreen.Instance.IsOpen, Is.True);
            Assert.That(FoundationScreen.Instance.DoctrineCount, Is.EqualTo(before + 1));
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                yield return Capture("06-expanded-foundation", 1600, 900);
                yield return Capture("07-expanded-foundation-4x3", 1200, 900);
            }
        }
        finally
        {
            FoundationScreen.Instance.Close();
            Object.Destroy(foundationObject);
        }
    }

    [UnityTest]
    public IEnumerator ConcurrentRewardsAreGroupedWithin64TokensAndArrivalRetriesAfterSaveFailure()
    {
        yield return Load("EconomyTest");
        var memory = new MemoryEconomyStore();
        EconomyRuntime.Instance.SetService(new EconomyService(memory));
        yield return null;
        Time.timeScale = 0;
        for (int i = 0; i < 16; i++) Currency.TryGrantWithEffect(13, Vector3.zero, "fixture", "burst:" + i);
        memory.FailWrites = true;
        yield return new WaitForSecondsRealtime(1.4f);
        var view = Object.FindObjectsByType<CoinFlightView>(FindObjectsSortMode.None).First(item => item.HasFlights);
        Assert.That(view.ActiveCount, Is.LessThanOrEqualTo(64)); Assert.That(view.CreatedCount, Is.LessThanOrEqualTo(64));
        Assert.That(Currency.Balance, Is.Zero); Assert.That(Currency.PendingBalance, Is.EqualTo(208));
        memory.FailWrites = false;
        yield return new WaitForSecondsRealtime(3.5f);
        Assert.That(Currency.Balance, Is.EqualTo(208)); Assert.That(Currency.PendingBalance, Is.Zero);
        Assert.That(view.CreatedCount, Is.LessThanOrEqualTo(64));
    }

    [UnityTest]
    public IEnumerator ActualBossDefeatGivesZeroCoins()
    {
        yield return Load("BossTest");
        var boss = Object.FindFirstObjectByType<BossCombatController>(); boss.PauseAndCancelForTest();
        var player = GameObject.FindGameObjectWithTag("Player");
        player.GetComponent<PlayerMove>().enabled = false;
        var body = player.GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.position = new Vector2(-4.21f, -2.33f);
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        float deadline = Time.time + 2f;
        while (!boss.EncounterStarted && Time.time < deadline) yield return new WaitForFixedUpdate();
        Assert.That(boss.EncounterStarted, Is.True, "Enter the authored arena before testing a boss defeat.");
        boss.ForcePhaseTwoForTest();
        int total = Currency.Balance + Currency.PendingBalance;
        boss.TakeDamage(new DamageInfo(100000, DamageSource.Environment));
        Assert.That(boss.IsDead, Is.True);
        Assert.That(Currency.Balance + Currency.PendingBalance, Is.EqualTo(total));
    }

    [UnityTest]
    public IEnumerator FailedEnemyRewardRetriesWhileAliveAndIsNotAcceptedAfterDeath()
    {
        yield return Load("EconomyTest");
        var memory = new MemoryEconomyStore();
        EconomyRuntime.Instance.SetService(new EconomyService(memory, new EconomyRandom(1)));
        memory.FailWrites = true;
        EconomyRuntime.Instance.RewardDefeat(EnemyRewardKind.Dog, "retry-defeat", Vector3.zero);
        Assert.That(Currency.PendingBalance, Is.Zero);
        memory.FailWrites = false;
        yield return new WaitForSecondsRealtime(.7f);
        int total = Currency.Balance + Currency.PendingBalance;
        Assert.That(total, Is.InRange(5, 7));
        memory.FailWrites = true;
        EconomyRuntime.Instance.RewardDefeat(EnemyRewardKind.Executive, "too-late", Vector3.zero);
        Currency.Service.BlockDefeatRewards();
        memory.FailWrites = false;
        yield return new WaitForSecondsRealtime(.7f);
        Assert.That(Currency.Balance + Currency.PendingBalance, Is.EqualTo(total));
    }

    [UnityTest]
    public IEnumerator VisualCaptureCurrencyHudFlightShopAndDeathAtTwoAspectRatios()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            Assert.Ignore("Visual verification requires graphics enabled.");
        yield return Load("EconomyTest");
        var context = Object.FindFirstObjectByType<TestEconomyContext>(); context.ResetFixture();
        yield return Capture("01-empty", 1600, 900);
        Currency.TryGrantWithEffect(8, new Vector3(3, 0), "visual", "visual-flight");
        yield return new WaitForSecondsRealtime(.3f);
        yield return Capture("02-flight", 1600, 900);
        yield return new WaitForSecondsRealtime(1.3f);
        Currency.TryGrant(200, "visual");
        ShopScreen.Instance.Open(Object.FindFirstObjectByType<ShopInteractable>(), GameObject.FindGameObjectWithTag("Player"));
        yield return Capture("03-shop", 1600, 900);
        ShopScreen.Instance.Close();
        context.PrepareEquipmentFixture();
        GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerHealth>().TakeDamage(100000);
        float resultDeadline = Time.realtimeSinceStartup + 5f;
        while (Object.FindFirstObjectByType<DeathResultView>() == null && Time.realtimeSinceStartup < resultDeadline)
            yield return null;
        Assert.That(Object.FindFirstObjectByType<DeathResultView>(), Is.Not.Null);
        yield return Capture("04-death", 1600, 900);
        yield return Capture("05-death-4x3", 1200, 900);
    }

    private static IEnumerator Capture(string filename, int width, int height)
    {
        // Profile switches destroy their old flight Canvas at the end of frame.
        yield return null;
        Camera camera = Camera.main;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Where(canvas => canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var texture = new RenderTexture(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active; camera.targetTexture = texture;
        foreach (Canvas canvas in canvases)
        { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
        Canvas.ForceUpdateCanvases(); yield return null;
        camera.Render(); RenderTexture.active = texture;
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/EconomyVerification"));
        Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, filename + ".png"), image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous;
        foreach (Canvas canvas in canvases)
        { if (canvas != null) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; } }
        texture.Release(); Object.Destroy(texture); Object.Destroy(image);
    }
}
