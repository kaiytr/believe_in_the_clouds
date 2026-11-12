using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class FirstAltarStoryPreviewPlayModeTests
{
    private EconomyService previous, original;
    private string directory, savePath;
    private readonly System.Collections.Generic.List<FirstAltarStoryDefinition> definitions = new System.Collections.Generic.List<FirstAltarStoryDefinition>();

    private static IEnumerator Load(string name)
    {
        Type editor = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("UnityEditor.SceneManagement.EditorSceneManager", false)).First(type => type != null);
        MethodInfo method = editor.GetMethod("LoadSceneAsyncInPlayMode", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(string), typeof(LoadSceneParameters) }, null);
        var load = (AsyncOperation)method.Invoke(null, new object[] { ProjectScenes.ForName(name), new LoadSceneParameters(LoadSceneMode.Single) });
        while (!load.isDone) yield return null;
        yield return null;
    }

    private void PreparePreview(Scene scene, LoadSceneMode mode)
    {
        if (!FirstAltarStoryPreview.IsActive || scene.name != LobbyFlowRuntime.FirstAltarScene) return;
        StoryRoomFlow flow = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<StoryRoomFlow>()).Single();
        FieldInfo field = typeof(StoryRoomFlow).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic);
        var copy = Object.Instantiate((FirstAltarStoryDefinition)field.GetValue(flow));
        copy.charactersPerSecond = 10000; copy.rainRiseSeconds = .1f;
        field.SetValue(flow, copy); definitions.Add(copy);
        flow.GetComponent<CutsceneRunner>().UseManualDialogueInput = true;
    }

    private static DevToolsRefs Tools => Object.FindFirstObjectByType<DevToolsRefs>(FindObjectsInactive.Include);
    private static void OpenTools()
    {
        DevToolsPanel panel = Tools.GetComponentInParent<DevToolsPanel>();
        typeof(DevToolsPanel).GetMethod("SetOpen", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, new object[] { true });
    }
    private static IEnumerator Until(Func<bool> condition)
    {
        float until = Time.realtimeSinceStartup + 10;
        while (!condition() && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(condition(), Is.True, FirstAltarStoryPreview.Message);
    }

    [UnitySetUp] public IEnumerator SetUp()
    {
        Time.timeScale = 1;
        EconomyRuntime.Ensure(); previous = Currency.Service;
        if (LobbyFlowRuntime.Instance != null) LobbyFlowRuntime.Instance.DiscardDeveloperPreviewRouter();
        yield return null;
        directory = Path.GetFullPath(Path.Combine("Temp/FirstAltarPreviewTests", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory); savePath = Path.Combine(directory, "original.json");
        EconomyService source = FirstAltarStoryPreview.CreateMemoryFixture(new EconomyProfileData { balance = 137 }, 150);
        string id = source.FirstAltarSnapshot().instanceId;
        Assert.That(source.TryCommitFirstAltarDeath(id).Success, Is.True);
        Assert.That(source.TryMarkFirstAltarStoryRead(id).Success, Is.True);
        Assert.That(source.TryLeaveFirstAltar(id, 99).Success, Is.True);
        var disk = new JsonEconomyStore(savePath); disk.Load();
        Assert.That(disk.TrySave(source.Snapshot()), Is.True);
        original = new EconomyService(disk); EconomyRuntime.Instance.SetService(original);
        SceneManager.sceneLoaded += PreparePreview;
        yield return Load("Lobby");
        yield return Until(() => LobbyFlowRuntime.Instance != null && LobbyFlowRuntime.Instance.InputReady);
        yield return new WaitForFixedUpdate();
    }

    [UnityTearDown] public IEnumerator TearDown()
    {
        if (FirstAltarStoryPreview.IsActive)
        {
            yield return Until(() => !FirstAltarStoryPreview.IsTransitioning);
            FirstAltarStoryPreview.TryReturn();
            yield return Until(() => !FirstAltarStoryPreview.IsActive && !FirstAltarStoryPreview.IsTransitioning);
        }
        SceneManager.sceneLoaded -= PreparePreview;
        if (FirstAltarStoryPreview.Instance != null) Object.Destroy(FirstAltarStoryPreview.Instance.gameObject);
        if (LobbyFlowRuntime.Instance != null) LobbyFlowRuntime.Instance.DiscardDeveloperPreviewRouter();
        yield return null; yield return Load("Title");
        EconomyRuntime.Instance.SetService(previous);
        foreach (FirstAltarStoryDefinition definition in definitions) if (definition != null) Object.Destroy(definition);
        string allowed = Path.GetFullPath("Temp/FirstAltarPreviewTests") + Path.DirectorySeparatorChar;
        Assert.That(directory.StartsWith(allowed, StringComparison.OrdinalIgnoreCase), Is.True);
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [UnityTest] public IEnumerator ButtonRestartsCompletedStoryAndReturnsOriginalFileObjectsAndUnfinishedStory()
    {
        PlayerHealth player = LobbyFlowRuntime.Instance.Scene.Player;
        Assert.That(original.TryCheckpointRun(player.CurrentHp, player.transform.position).Success, Is.True);
        yield return null;
        string before = JsonUtility.ToJson(original.Snapshot());
        byte[] bytes = File.ReadAllBytes(savePath);
        Scene scene = SceneManager.GetActiveScene();
        Vector3 position = player.transform.position; float hp = player.CurrentHp;
        Assert.That(Tools.IsComplete, Is.True);
        OpenTools(); yield return null;
        Assert.That(Tools.StoryPreview.interactable, Is.True);
        Tools.StoryPreview.onClick.Invoke();
        yield return Until(() => FirstAltarStoryPreview.IsActive && !FirstAltarStoryPreview.IsTransitioning &&
            StoryRoomFlow.Instance != null && StoryRoomFlow.Instance.GetComponent<CutsceneRunner>().IsRunning);
        Assert.That(player.gameObject.activeInHierarchy, Is.False);
        Assert.That(scene.isLoaded, Is.True);
        Assert.That(Currency.Service, Is.Not.SameAs(original));
        Assert.That(JsonUtility.ToJson(original.Snapshot()), Is.EqualTo(before));
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(savePath));

        StoryRoomFlow first = StoryRoomFlow.Instance;
        string firstId = Currency.Service.FirstAltarSnapshot().instanceId;
        OpenTools(); yield return null;
        Assert.That(Tools.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(32760));
        Assert.That(Tools.ResetAndGoToTitle.interactable, Is.False);
        Tools.StoryPreview.onClick.Invoke(); // restart even while the introduction is locked
        yield return Until(() => !FirstAltarStoryPreview.IsTransitioning && StoryRoomFlow.Instance != null &&
            StoryRoomFlow.Instance != first && StoryRoomFlow.Instance.GetComponent<CutsceneRunner>().IsRunning);
        Assert.That(Currency.Service.FirstAltarSnapshot().instanceId, Is.Not.EqualTo(firstId));
        CutsceneRunner runner = StoryRoomFlow.Instance.GetComponent<CutsceneRunner>();
        Assert.That(runner.ActiveDefinition.Steps[0].Text, Is.EqualTo("하나. 구름은 모든 것을 내려다본다"));
        Assert.That(JsonUtility.ToJson(original.Snapshot()), Is.EqualTo(before));
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(savePath));
        var gate = (CutsceneDialogueInputGate)typeof(CutsceneRunner).GetField("dialogueGate", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runner);
        int lines = runner.ActiveDefinition.Steps.Length;
        for (int i = 0; i < lines; i++)
        {
            yield return Until(() => gate.IsTyping || gate.WaitingForAdvance);
            if (gate.IsTyping) { runner.RequestDialogueClick(); yield return null; yield return null; }
            runner.RequestDialogueClick(); yield return null; yield return null;
        }
        yield return Until(() => StoryRoomFlow.Instance.CanInteract);
        PlayerHealth testPlayer = LobbyFlowRuntime.Instance.Scene.Player;
        testPlayer.transform.position = new Vector3(1, -1.2f, 0);
        testPlayer.GetComponent<Rigidbody2D>().position = testPlayer.transform.position;
        yield return Until(() => StoryRoomFlow.Instance.Phase == FirstAltarPhase.BelieverDead && StoryRoomFlow.Instance.CanInteract);
        StoryRoomFlow.Instance.OpenDocument(); yield return null; yield return null; yield return null;
        Object.FindFirstObjectByType<StoryDocumentScreen>().RequestClose();
        yield return Until(() => StoryRoomFlow.Instance.CanInteract);
        StoryRoomFlow.Instance.LeaveRoom(); // normal story exit must return to the parked scene, not load a new lobby
        yield return Until(() => !FirstAltarStoryPreview.IsActive && !FirstAltarStoryPreview.IsTransitioning);
        Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(scene.handle));
        Assert.That(LobbyFlowRuntime.Instance.Scene.Player, Is.SameAs(player));
        Assert.That(player.CurrentHp, Is.EqualTo(hp));
        Assert.That(Vector3.Distance(player.transform.position, position), Is.LessThan(.03f));
        Assert.That(JsonUtility.ToJson(original.Snapshot()), Is.EqualTo(before));
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(savePath));

        // Also preserve a real, unfinished StoryRoom instance when cancelling a preview in the intro.
        EconomyService unfinished = FirstAltarStoryPreview.CreateMemoryFixture(original.Snapshot(), player.MaxHp);
        Assert.That(unfinished.TryCommitFirstAltarDeath(unfinished.FirstAltarSnapshot().instanceId).Success, Is.True);
        LobbyFlowRuntime.Instance.DiscardDeveloperPreviewRouter(); yield return null;
        EconomyRuntime.Instance.SetService(unfinished);
        yield return Load(LobbyFlowRuntime.FirstAltarScene);
        yield return Until(() => StoryRoomFlow.Instance != null && StoryRoomFlow.Instance.CanInteract);
        StoryRoomFlow parkedStory = StoryRoomFlow.Instance;
        string unfinishedBefore = JsonUtility.ToJson(unfinished.Snapshot());
        OpenTools(); yield return null; Tools.StoryPreview.onClick.Invoke();
        yield return Until(() => FirstAltarStoryPreview.IsActive && !FirstAltarStoryPreview.IsTransitioning);
        OpenTools(); yield return null;
        Assert.That(Tools.EndStoryPreview.interactable, Is.True); Tools.EndStoryPreview.onClick.Invoke();
        yield return Until(() => !FirstAltarStoryPreview.IsActive && !FirstAltarStoryPreview.IsTransitioning &&
            StoryRoomFlow.Instance == parkedStory && parkedStory.CanInteract);
        Assert.That(Currency.Service, Is.SameAs(unfinished));
        Assert.That(parkedStory.Phase, Is.EqualTo(FirstAltarPhase.BelieverDead));
        Assert.That(JsonUtility.ToJson(unfinished.Snapshot()), Is.EqualTo(unfinishedBefore));
    }
}
