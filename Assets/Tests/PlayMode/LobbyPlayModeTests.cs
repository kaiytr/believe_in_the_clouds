using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class LobbyPlayModeTests
{
    private EconomyService previous;
    private MemoryEconomyStore store;
    private static readonly string[] CombatScenes = { "CombatLevel1", "CombatLevel2", "CombatLevel3", "CombatLevel4", "CombatLevel5", "CombatLevel6" };
    private LobbyFlowRuntime Flow => LobbyFlowRuntime.Instance;
    private PlayerHealth Player => Flow.Scene.Player;

    [UnitySetUp] public IEnumerator SetUp()
    {
        Time.timeScale = 1; EconomyRuntime.Ensure(); previous = Currency.Service;
        SceneManager.sceneLoaded += PauseCombatActors;
        store = new MemoryEconomyStore();
        EconomyRuntime.Instance.SetService(new EconomyService(store, new EconomyRandom(43)));
        yield return Load("Lobby"); yield return Ready("Lobby");
    }

    [UnityTearDown] public IEnumerator TearDown()
    {
        if (PlayerDeathCoordinator.Active != null) Object.Destroy(PlayerDeathCoordinator.Active.gameObject);
        yield return null;
        SceneManager.sceneLoaded -= PauseCombatActors;
        // A persistent router must not route other suites back into this fixture.
        if (Flow != null) Object.Destroy(Flow.gameObject);
        yield return null;
        yield return Load("Title");
        EconomyRuntime.Instance.SetService(previous); Time.timeScale = 1;
    }

    private static IEnumerator Load(string name)
    {
        Type editor = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.SceneManagement.EditorSceneManager", false)).First(t => t != null);
        MethodInfo method = editor.GetMethod("LoadSceneAsyncInPlayMode", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(string), typeof(LoadSceneParameters) }, null);
        var load = (AsyncOperation)method.Invoke(null, new object[] { ProjectScenes.ForName(name), new LoadSceneParameters(LoadSceneMode.Single) });
        while (!load.isDone) yield return null;
        yield return null; yield return new WaitForFixedUpdate();
    }

    private IEnumerator Ready(string scene)
    {
        float until = Time.realtimeSinceStartup + 12;
        while ((SceneManager.GetActiveScene().name != scene || Flow == null || Flow.Scene == null || !Flow.InputReady) && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(scene));
        Assert.That(Flow.InputReady, Is.True, Flow.Message);
        yield return null;
    }

    private IEnumerator ReadyCombat()
    {
        float until = Time.realtimeSinceStartup + 12f;
        while ((!CombatScenes.Contains(SceneManager.GetActiveScene().name) || Flow == null || !Flow.InputReady) &&
            Time.realtimeSinceStartup < until) yield return null;
        Assert.That(CombatScenes, Does.Contain(SceneManager.GetActiveScene().name));
        yield return Ready(SceneManager.GetActiveScene().name);
    }

    private static IEnumerator DeathResultReady()
    {
        float until = Time.realtimeSinceStartup + 5f;
        while (Object.FindFirstObjectByType<DeathResultView>() == null && Time.realtimeSinceStartup < until)
            yield return null;
        Assert.That(Object.FindFirstObjectByType<DeathResultView>(), Is.Not.Null);
    }

    private static void PauseCombatActors(Scene scene, LoadSceneMode mode)
    {
        if (!CombatScenes.Contains(scene.name)) return;
        // These cases isolate routing, saved HP and transactions from random combat damage.
        foreach (var enemy in Object.FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None))
        {
            if (enemy.gameObject.scene != scene) continue;
            enemy.enabled = false;
            foreach (var attack in enemy.GetComponents<EnemyAttack>()) attack.enabled = false;
            foreach (var contact in enemy.GetComponentsInChildren<EnemyContactDamage>()) contact.SetSuppressed(true);
        }
    }

    private LobbyBranchInteractable[] Gates() => Object.FindObjectsByType<LobbyBranchInteractable>(FindObjectsSortMode.None).OrderBy(g => g.transform.position.x).ToArray();
    private void Move(Vector2 position)
    {
        var body = Player.GetComponent<Rigidbody2D>(); body.position = position; body.linearVelocity = Vector2.zero;
        Player.transform.position = position; Physics2D.SyncTransforms();
    }
    private LobbyBranchInteractable Combat() => Gates().First(g => Flow.State.candidates.First(c => c.id == g.BranchId).role == BranchRole.Combat);

    [UnityTest] public IEnumerator GatesAreHiddenTriggersPlayerRendersInFrontAndCameraFollowsAcrossLobby()
    {
        var gates = Gates(); Assert.That(gates.Length, Is.EqualTo(Flow.State.candidates.Count));
        Assert.That(Object.FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None), Is.Empty);
        foreach (var gate in gates)
        {
            Assert.That(gate.GetComponent<Collider2D>().isTrigger, Is.True);
            Assert.That(gate.GetComponentInChildren<TextMesh>().text, Is.EqualTo("?"));
            Assert.That(gate.Body.sortingOrder, Is.LessThan(Player.GetComponentInChildren<SpriteRenderer>().sortingOrder));
        }
        float cameraBefore = Camera.main.transform.position.x;
        Move(new Vector2(gates[0].transform.position.x, -1.95f));
        yield return new WaitForSecondsRealtime(.6f);
        Assert.That(Camera.main.transform.position.x, Is.GreaterThan(cameraBefore + 15));
        Assert.That(InteractionController.Instance.Current, Is.EqualTo(gates[0]));
        Assert.That(Flow.State.area, Is.EqualTo(RunArea.Lobby)); Assert.That(Currency.Balance, Is.Zero);
        Assert.That(Player.GetComponent<PlayerMove>().enabled, Is.True);
        Assert.That(Player.GetComponent<PlayerDash>().enabled, Is.True);
        Assert.That(Player.GetComponent<PlayerAttack>().enabled, Is.True);
    }

    [UnityTest] public IEnumerator PlayLoopTestBuildsOnlyCombatAndShopGatesBeforeBossUnlock()
    {
        Flow.StartPlayLoopTest(); yield return Ready("Lobby");
        CollectionAssert.AreEquivalent(new[] { BranchRole.Combat, BranchRole.Shop }, Flow.State.candidates.Select(candidate => candidate.role));
        Assert.That(Gates(), Has.Length.EqualTo(2));
        foreach (var gate in Gates())
            Assert.That(gate.GetComponentInChildren<TextMesh>().text, Is.EqualTo("?"));
        Assert.That(Application.CanStreamedLevelBeLoaded("TemporaryStage"), Is.False);
    }

    [UnityTest] public IEnumerator RevealFailureHasNoPartialColorOrChargeAndReloadPreservesTheChosenGate()
    {
        Currency.TryGrant(5, "fixture"); var gate = Gates()[0]; Move(new Vector2(gate.transform.position.x, -1.95f));
        yield return null; Color hidden = gate.Body.color; string batch = Flow.State.batchId;
        store.FailWrites = true; gate.InteractSecondary(Player.gameObject); yield return null;
        Assert.That(Currency.Balance, Is.EqualTo(5)); Assert.That(gate.Body.color, Is.EqualTo(hidden));
        Assert.That(Flow.State.candidates.All(c => !c.revealed), Is.True);
        store.FailWrites = false; gate.InteractSecondary(Player.gameObject); yield return null;
        Assert.That(Currency.Balance, Is.Zero); Assert.That(gate.Body.color, Is.Not.EqualTo(hidden));
        Assert.That(Flow.State.candidates.Count(c => c.revealed), Is.EqualTo(1));
        gate.InteractSecondary(Player.gameObject); Assert.That(Currency.Balance, Is.Zero);
        Flow.ReloadForTest(); yield return Ready("Lobby");
        Assert.That(Flow.State.batchId, Is.EqualTo(batch)); Assert.That(Flow.State.candidates.Count(c => c.revealed), Is.EqualTo(1));
        Assert.That(Currency.Balance, Is.Zero);
    }

    [UnityTest] public IEnumerator UnrevealedDepartureLockedExitDamageReloadCompletionAndReturnPreserveHealth()
    {
        var gate = Combat(); string previousBatch = Flow.State.batchId;
        Move(new Vector2(gate.transform.position.x, -1.95f)); gate.Interact(Player.gameObject);
        Assert.That(Flow.IsTransitioning, Is.True); Assert.That(Currency.Balance, Is.Zero);
        // Quitting during a route must not write lobby coordinates into the stage.
        typeof(LobbyFlowRuntime).GetMethod("OnApplicationPause", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Flow, new object[] { true });
        Assert.That(Flow.Service.RunSnapshot().hasPosition, Is.False);
        yield return ReadyCombat();
        string stageScene = SceneManager.GetActiveScene().name;
        var exit = Object.FindFirstObjectByType<TemporaryStageExit>();
        Assert.That(exit.CanInteract(Player.gameObject), Is.False);
        Assert.That(Flow.ReturnToLobby().Success, Is.False);
        Flow.DamageForTest(); Assert.That(Player.CurrentHp, Is.EqualTo(90));
        Assert.That(Flow.Service.RunSnapshot().hp, Is.EqualTo(90));
        string stageId = Flow.State.stage.id;
        Flow.ReloadForTest(); yield return Ready(stageScene);
        Assert.That(Player.CurrentHp, Is.EqualTo(90)); Assert.That(Flow.State.stage.id, Is.EqualTo(stageId));
        Assert.That(Flow.State.stage.completed, Is.False);
        Assert.That(Flow.CompleteStage().Success, Is.True);
        Flow.ReloadForTest(); yield return Ready(stageScene);
        Assert.That(Flow.State.stage.completed, Is.True); Assert.That(Flow.State.combatClears, Is.Zero);
        Move(new Vector2(8, -1.95f));
        Object.FindFirstObjectByType<TemporaryStageExit>().Interact(Player.gameObject);
        yield return Ready("Lobby");
        Assert.That(Player.CurrentHp, Is.EqualTo(90)); Assert.That(Flow.State.combatClears, Is.EqualTo(1));
        Assert.That(Flow.State.batchId, Is.Not.EqualTo(previousBatch));
        Assert.That(Flow.Service.TryReturnFromStage(stageId, 90).Success, Is.True);
        Assert.That(Flow.Service.RunSnapshot().combatClears, Is.EqualTo(1));
    }

    [UnityTest] public IEnumerator LoadingLobbyWithAnUnfinishedSavedStageRoutesBackWithoutGrantingAClear()
    {
        Assert.That(Flow.Depart(Combat().BranchId).Success, Is.True); yield return ReadyCombat();
        string stageScene = SceneManager.GetActiveScene().name;
        Flow.DamageForTest(); string stage = Flow.State.stage.id;
        yield return Load("Lobby"); yield return Ready(stageScene);
        Assert.That(Flow.State.stage.id, Is.EqualTo(stage)); Assert.That(Flow.State.combatClears, Is.Zero);
        Assert.That(Flow.State.stage.completed, Is.False); Assert.That(Player.CurrentHp, Is.EqualTo(90));
    }

    [UnityTest] public IEnumerator DeathCannotChangeSceneBeforeSaveThenReturnsFullHealthWithOneLoss()
    {
        Currency.TryGrant(100, "fixture"); Flow.Service.TryGrantPiece("subject.player", 13, "fixture");
        string oldRun = Flow.State.id;
        Flow.Depart(Combat().BranchId); yield return ReadyCombat();
        string stageScene = SceneManager.GetActiveScene().name;
        string death = Player.GetComponent<PlayerDeathCoordinator>().DeathId;
        store.FailWrites = true; Flow.DieForTest(); yield return new WaitForSecondsRealtime(.65f);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(stageScene));
        Assert.That(Flow.InputReady, Is.False); Assert.That(Currency.Balance, Is.EqualTo(100));
        Assert.That(Flow.Service.HasPendingDeath, Is.True);
        store.FailWrites = false;
        yield return DeathResultReady();
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(stageScene));
        Assert.That(Currency.Balance, Is.EqualTo(100));
        Player.GetComponent<PlayerDeathCoordinator>().Continue();
        yield return Ready("Lobby");
        Assert.That(Flow.State.id, Is.Not.EqualTo(oldRun)); Assert.That(Flow.State.combatClears, Is.Zero);
        Assert.That(Player.CurrentHp, Is.EqualTo(Player.MaxHp)); Assert.That(Currency.Balance, Is.EqualTo(70));
        Assert.That(Flow.Service.CountPiece("subject.player"), Is.EqualTo(9));
        Flow.Service.TryHandleDeath(death, null, () => Assert.Fail("Duplicate death"));
        Assert.That(Currency.Balance, Is.EqualTo(70));
    }

    [UnityTest] public IEnumerator TestToolsUseMemoryAndCanRecoverAnInjectedDeathFailure()
    {
        Flow.StartMemoryFixture(); yield return Ready("Lobby");
        Flow.GrantForTest(); Assert.That(Currency.Balance, Is.EqualTo(100));
        Flow.ToggleSaveFailure(); Assert.That(Flow.FixtureStore.FailWrites, Is.True);
        Flow.DieForTest(); yield return null;
        Assert.That(Flow.Service.HasPendingDeath, Is.True);
        Flow.ToggleSaveFailure();
        yield return DeathResultReady();
        Player.GetComponent<PlayerDeathCoordinator>().Continue(); yield return Ready("Lobby");
        Assert.That(Currency.Balance, Is.EqualTo(70));
    }

    [UnityTest] public IEnumerator ConfirmedDeathLosesOneThirdAndClearsTheFoundationOnLobbyReturn()
    {
        var foundation = FoundationScreen.Instance;
        var manager = DoctrineHudBoot.EnsureManagerExists();
        var rule = new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal);
        Assert.That(manager.TryActivate(rule, null, out string reason, out var unrelated), Is.True, reason);
        try
        {
            Assert.That(Flow.Service.TryGrantPiece("subject.player", 3, "fixture").Success, Is.True);
            Assert.That(Flow.Service.TryGrantPiece("condition.hit", 2, "fixture").Success, Is.True);
            Assert.That(Flow.Service.TryGrantPiece("result.heal", 2, "fixture").Success, Is.True);
            foundation.Open(Object.FindFirstObjectByType<FoundationInteractable>(), Player.gameObject);
            PlaceFoundationPiece(foundation, 0, "subject.player");
            PlaceFoundationPiece(foundation, 0, "condition.hit");
            PlaceFoundationPiece(foundation, 0, "result.heal");
            PlaceFoundationPiece(foundation, 1, "subject.player");
            PlaceFoundationPiece(foundation, 1, "condition.hit");
            PlaceFoundationPiece(foundation, 1, "result.heal");
            PlaceFoundationPiece(foundation, 2, "subject.player");
            Assert.That(manager.ActiveDoctrines.Count, Is.EqualTo(3));
            Assert.That(foundation.SelectedEntry.Id, Is.EqualTo("subject.player"));
            foundation.Close();

            Assert.That(Flow.Depart(Combat().BranchId).Success, Is.True);
            yield return ReadyCombat();
            Flow.DieForTest();
            yield return DeathResultReady();
            Assert.That(EconomyLedger.Total(Flow.Service.Snapshot().pieces), Is.EqualTo(7));
            Assert.That(manager.ActiveDoctrines.Count, Is.EqualTo(3));

            var death = Player.GetComponent<PlayerDeathCoordinator>();
            store.FailWrites = true;
            death.Continue();
            Assert.That(EconomyLedger.Total(Flow.Service.Snapshot().pieces), Is.EqualTo(7));
            Assert.That(manager.ActiveDoctrines.Count, Is.EqualTo(3));
            Assert.That(foundation.SelectedEntry, Is.Not.Null);

            store.FailWrites = false;
            death.Continue();
            yield return Ready("Lobby");
            var saved = Flow.Service.Snapshot();
            Assert.That(saved.death.lostPieces, Is.EqualTo(2));
            Assert.That(EconomyLedger.Total(saved.death.allPiecesAfterUnequip), Is.EqualTo(7));
            Assert.That(EconomyLedger.Total(saved.pieces), Is.EqualTo(5));
            Assert.That(manager.ActiveDoctrines, Is.EqualTo(new[] { unrelated }));

            foundation.Open(Object.FindFirstObjectByType<FoundationInteractable>(), Player.gameObject);
            var getSlot = typeof(FoundationScreen).GetMethod("GetSlot", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int row = 0; row < foundation.DoctrineCount; row++)
                foreach (var column in new[] { ShopColumn.Subject, ShopColumn.Condition, ShopColumn.Result })
                    Assert.That(getSlot.Invoke(foundation, new object[] { row, column }), Is.Null);
        }
        finally
        {
            store.FailWrites = false;
            foundation.Close();
            manager.DeactivateEntry(unrelated);
        }
    }

    private static void PlaceFoundationPiece(FoundationScreen foundation, int row, string id)
    {
        Assert.That(ShopCatalog.TryGet(id, out var entry), Is.True);
        var catalog = ShopCatalog.GetColumn(entry.Column);
        int index = Enumerable.Range(0, catalog.Count).Single(i => catalog[i].Id == id);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(FoundationScreen).GetMethod("HandleTabClicked", flags).Invoke(foundation, new object[] { entry.Column });
        typeof(FoundationScreen).GetMethod("HandleItemDroppedOnSlot", flags).Invoke(foundation, new object[] { index, row, entry.Column });
    }

    [UnityTest] public IEnumerator VisualCaptureHiddenRevealedAndCompletedStageAtTwoAspectRatios()
    {
        Move(new Vector2(15, -1.95f)); yield return new WaitForSecondsRealtime(.5f);
        yield return Capture("01-hidden-16x9", 1600, 900);
        Currency.TryGrant(100, "fixture"); Flow.Reveal(Gates()[0].BranchId); yield return null;
        yield return Capture("02-revealed-16x9", 1600, 900);
        yield return Capture("03-revealed-4x3", 1200, 900);
        Flow.Depart(Combat().BranchId); yield return ReadyCombat();
        Flow.CompleteStage(); Move(new Vector2(5, -1.95f)); yield return new WaitForSecondsRealtime(.5f);
        yield return Capture("04-stage-complete", 1600, 900);
    }

    private static IEnumerator Capture(string name, int width, int height)
    {
        yield return null;
        var camera = Camera.main;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var texture = new RenderTexture(width, height, 24); texture.Create();
        var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
        camera.targetTexture = texture;
        foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = texture;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        string directory = Path.GetFullPath("Logs/LobbyVerification"); Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
        foreach (var canvas in canvases) if (canvas != null) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
        camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
        texture.Release(); Object.Destroy(texture); Object.Destroy(image);
    }
}
