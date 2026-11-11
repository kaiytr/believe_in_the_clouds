using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class FirstAltarStoryPlayModeTests
{
    private EconomyService previous, service;
    private FirstAltarStoryDefinition fastDefinition;
    private MemoryEconomyStore store;
    private StoryRoomFlow Story => StoryRoomFlow.Instance;

    private static IEnumerator Load(string name)
    {
        Type editor = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("UnityEditor.SceneManagement.EditorSceneManager", false)).First(type => type != null);
        MethodInfo method = editor.GetMethod("LoadSceneAsyncInPlayMode", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(string), typeof(LoadSceneParameters) }, null);
        var load = (AsyncOperation)method.Invoke(null, new object[] { ProjectScenes.ForName(name), new LoadSceneParameters(LoadSceneMode.Single) });
        while (!load.isDone) yield return null;
        yield return null;
    }

    private void PrepareScene(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != LobbyFlowRuntime.FirstAltarScene) return;
        StoryRoomFlow flow = Object.FindFirstObjectByType<StoryRoomFlow>();
        FieldInfo field = typeof(StoryRoomFlow).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic);
        fastDefinition = Object.Instantiate((FirstAltarStoryDefinition)field.GetValue(flow));
        // Shorten only ambient waiting. The required post-impact blackout stays at the real 3 seconds.
        fastDefinition.introCues = fastDefinition.introCues.Select(cue => new PrayerCue(cue.text, .01f)).ToArray();
        fastDefinition.charactersPerSecond = 10000; fastDefinition.apologySeconds = .03f; fastDefinition.rainRiseSeconds = .1f;
        field.SetValue(flow, fastDefinition);
    }

    [UnitySetUp] public IEnumerator SetUp()
    {
        Time.timeScale = 1;
        if (LobbyFlowRuntime.Instance != null) Object.Destroy(LobbyFlowRuntime.Instance.gameObject);
        yield return null;
        EconomyRuntime.Ensure(); previous = Currency.Service;
        store = new MemoryEconomyStore(); service = new EconomyService(store, new EconomyRandom(23));
        EconomyRuntime.Instance.SetService(service);
        Assert.That(service.TryMarkTutorialCompleted().Success, Is.True);
        Assert.That(service.TryInitializeRun(100).Success, Is.True);
        LobbyRunData run = service.RunSnapshot();
        Assert.That(service.TryDepartBranch(run.candidates.First(candidate => candidate.role == BranchRole.Combat).id, 100).Success, Is.True);
        string stage = service.RunSnapshot().stage.id;
        Assert.That(service.TryCompleteTemporaryStage(stage).Success, Is.True);
        Assert.That(service.TryReturnFromStage(stage, 73).Success, Is.True);
        SceneManager.sceneLoaded += PrepareScene;
        yield return Load(LobbyFlowRuntime.FirstAltarScene);
        float until = Time.realtimeSinceStartup + 8;
        while ((Story == null || !Story.CanInteract) && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(Story, Is.Not.Null);
        Assert.That(Story.CanInteract, Is.True);
    }

    [UnityTearDown] public IEnumerator TearDown()
    {
        if (InteractionController.Instance != null) InteractionController.Instance.UseManualInput = false;
        SceneManager.sceneLoaded -= PrepareScene;
        if (LobbyFlowRuntime.Instance != null) Object.Destroy(LobbyFlowRuntime.Instance.gameObject);
        yield return null;
        yield return Load("Title");
        if (fastDefinition != null) Object.Destroy(fastDefinition);
        EconomyRuntime.Instance.SetService(previous);
        Time.timeScale = 1;
    }

    [UnityTest] public IEnumerator PrayerBlackoutFailedSaveDocumentAndNewExitPressUseRealScene()
    {
        Assert.That(Object.FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None), Is.Empty);
        Assert.That(Object.FindObjectsByType<FoundationInteractable>(FindObjectsSortMode.None), Is.Empty);
        PlayerHealth player = LobbyFlowRuntime.Instance.Scene.Player;
        var altar = Object.FindFirstObjectByType<StoryAltarInteractable>();
        var exit = Object.FindFirstObjectByType<StoryRoomExit>();
        Assert.That(altar.CanInteract(player.gameObject), Is.False);
        Assert.That(exit.CanInteract(player.gameObject), Is.False);
        AmbientPrayerController prayer = Object.FindFirstObjectByType<AmbientPrayerController>();
        float until = Time.realtimeSinceStartup + 8;
        while (!prayer.IsApologyLoopReady && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(prayer.IsApologyLoopReady, Is.True);
        yield return new WaitForSeconds(.2f);
        Assert.That(prayer.IsApologyLoopReady, Is.True); // apology repeats, no automatic death
        store.FailWrites = true;
        player.transform.position = new Vector3(1, -1.2f, 0);
        var rigidbody = player.GetComponent<Rigidbody2D>(); rigidbody.position = player.transform.position; rigidbody.linearVelocity = Vector2.zero;
        float approachedAt = Time.realtimeSinceStartup;
        yield return null; yield return null;
        Assert.That(UiModal.IsOpen, Is.True);
        Assert.That(StoryRoomFlow.BlocksDeveloperActions, Is.True);
        yield return new WaitForSecondsRealtime(2.7f);
        Assert.That(GameObject.Find("First altar blackout"), Is.Not.Null);
        Assert.That(service.FirstAltarSnapshot().phase, Is.EqualTo(FirstAltarPhase.AwaitingApproach));
        yield return new WaitForSecondsRealtime(.65f);
        Assert.That(Time.realtimeSinceStartup - approachedAt, Is.GreaterThan(3));
        Assert.That(GameObject.Find("First altar blackout"), Is.Null); // hidden after exactly the timed reveal, even if save fails
        Assert.That(Story.CanInteract, Is.False);
        Assert.That(Object.FindFirstObjectByType<StoryBelieverView>().GetComponent<EnemyUnit>(), Is.Null);
        store.FailWrites = false;
        until = Time.realtimeSinceStartup + 5;
        while (!Story.CanInteract && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(Story.Phase, Is.EqualTo(FirstAltarPhase.BelieverDead));
        Assert.That(altar.CanInteract(player.gameObject), Is.True);
        Assert.That(exit.CanInteract(player.gameObject), Is.False);
        int visit = service.RunSnapshot().visit, clears = service.RunSnapshot().combatClears;
        Story.OpenDocument();
        yield return null; yield return null; yield return null;
        var document = Object.FindFirstObjectByType<StoryDocumentScreen>();
        Assert.That(document.IsOpen, Is.True);
        store.FailWrites = true; document.RequestClose();
        Assert.That(document.IsOpen, Is.True);
        Assert.That(exit.CanInteract(player.gameObject), Is.False);
        store.FailWrites = false; document.RequestClose();
        yield return null; yield return null; yield return null;
        Assert.That(document.IsOpen, Is.False);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(LobbyFlowRuntime.FirstAltarScene));
        Assert.That(service.RunSnapshot().visit, Is.EqualTo(visit));
        Assert.That(service.RunSnapshot().combatClears, Is.EqualTo(clears));
        Assert.That(exit.CanInteract(player.gameObject), Is.True);
        player.transform.position = new Vector3(11.5f, -1.2f, 0);
        rigidbody.position = player.transform.position; rigidbody.linearVelocity = Vector2.zero; Physics2D.SyncTransforms();
        InteractionController controller = InteractionController.Instance;
        controller.UseManualInput = true;
        controller.ProcessInput(new InteractionFrame(), .02f); // release the close input
        controller.ProcessInput(new InteractionFrame(pressed: true, held: true), .02f);
        until = Time.realtimeSinceStartup + 8;
        while (SceneManager.GetActiveScene().name != "Lobby" && Time.realtimeSinceStartup < until) yield return null;
        controller.UseManualInput = false;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Lobby"));
        Assert.That(service.FirstAltarSnapshot().phase, Is.EqualTo(FirstAltarPhase.Completed));
        Assert.That(service.RunSnapshot().visit, Is.EqualTo(visit + 1));
        Assert.That(service.RunSnapshot().combatClears, Is.EqualTo(clears));
    }

}
