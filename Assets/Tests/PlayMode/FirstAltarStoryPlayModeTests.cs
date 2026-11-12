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
        // Slow typing to verify the fill click, then click through the actual manuscript.
        // Only rain waiting is shortened; the required blackout stays at 3 seconds.
        fastDefinition.charactersPerSecond = 1; fastDefinition.rainRiseSeconds = .1f;
        field.SetValue(flow, fastDefinition);
        flow.GetComponent<CutsceneRunner>().UseManualDialogueInput = true;
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
        while ((Story == null || !Story.GetComponent<CutsceneRunner>().IsRunning) && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(Story, Is.Not.Null);
        Assert.That(Story.GetComponent<CutsceneRunner>().IsRunning, Is.True);
        Assert.That(Story.CanInteract, Is.False);
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

    [UnityTest] public IEnumerator RuntimeLightingUsesSharedPixelLightFactory()
    {
        Scene scene = Story.gameObject.scene;
        GameObject[] roots = scene.GetRootGameObjects();
        Component[] lights = roots.SelectMany(root => root.GetComponentsInChildren<Component>(true))
            .Where(component => component != null && component.GetType().Name == "Light2D").ToArray();
        Component ambient = lights.Single(light => light.gameObject.name == "Stage atmosphere");
        Assert.That(ambient.GetType().GetProperty("lightType").GetValue(ambient).ToString(), Is.EqualTo("Global"));
        Assert.That((float)ambient.GetType().GetProperty("intensity").GetValue(ambient), Is.EqualTo(.26f));
        Assert.That((Color)ambient.GetType().GetProperty("color").GetValue(ambient), Is.EqualTo(new Color(.5f, .55f, .7f)));
        Transform[] torches = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Where(transform => transform.name == "Wall Torch").ToArray();
        Assert.That(torches.Length, Is.EqualTo(2));
        Sprite sharedCookie = PixelLight.Radial(7.5f, .6f / 7.5f, .7f);
        foreach (Transform torch in torches)
        {
            Assert.That(torch.GetComponent<TorchLight>(), Is.Not.Null);
            Component light = torch.GetComponentsInChildren<Component>(true)
                .Single(component => component != null && component.GetType().Name == "Light2D");
            Assert.That(light.GetType().GetProperty("lightType").GetValue(light).ToString(), Is.EqualTo("Sprite"));
            Sprite cookie = (Sprite)light.GetType().GetProperty("lightCookieSprite").GetValue(light);
            Assert.That(cookie, Is.SameAs(sharedCookie), "Story torches must use the same runtime cookie as combat torches.");
            Assert.That(cookie.hideFlags, Is.EqualTo(HideFlags.DontSave));
            Assert.That(cookie.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That((float)light.GetType().GetProperty("intensity").GetValue(light), Is.EqualTo(1.15f));
            float diameter = light.transform.lossyScale.x;
            Assert.That(Mathf.Min(Mathf.Abs(diameter - 15f), Mathf.Abs(diameter - 14.5f)), Is.LessThan(.0001f));
            Assert.That(light.transform.lossyScale.y, Is.EqualTo(diameter).Within(.0001f));
            Assert.That(Vector3.Distance(light.transform.position,
                PixelLight.Snap(torch.TransformPoint(new Vector3(0f, .6f, 0f)))), Is.LessThan(.0001f));
        }
        typeof(StageAtmosphere).GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { scene });
        Assert.That(scene.GetRootGameObjects().Count(root => root.name == "Stage atmosphere"), Is.EqualTo(1));
        Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true))
            .Count(component => component != null && component.GetType().Name == "Light2D"), Is.EqualTo(lights.Length));
        // End the active introduction before unloading its scene during this lighting-only test.
        Story.enabled = false;
        yield return null;
    }

    [UnityTest] public IEnumerator PrayerBlackoutFailedSaveDocumentAndNewExitPressUseRealScene()
    {
        Assert.That(Object.FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None), Is.Empty);
        Assert.That(Object.FindObjectsByType<FoundationInteractable>(FindObjectsSortMode.None), Is.Empty);
        PlayerHealth player = LobbyFlowRuntime.Instance.Scene.Player;
        var altar = Object.FindFirstObjectByType<StoryAltarInteractable>();
        var exit = Object.FindFirstObjectByType<StoryRoomExit>();
        var npc = Object.FindFirstObjectByType<StoryBelieverView>();
        SpriteRenderer bodyView = npc.GetComponentInChildren<SpriteRenderer>();
        Assert.That(bodyView.transform.lossyScale.x, Is.EqualTo(13f / 6f).Within(.0001f));
        Assert.That(bodyView.transform.lossyScale.y, Is.EqualTo(13f / 6f).Within(.0001f));
        Assert.That(bodyView.flipX, Is.True, "원본 스프라이트를 반전해 오른쪽 제단을 바라본다.");
        Assert.That(bodyView.sharedMaterial.shader.name, Is.Not.EqualTo("Story/Believer Glitch"));
        Assert.That(altar.transform.position.x, Is.GreaterThan(npc.transform.position.x));
        Assert.That(altar.CanInteract(player.gameObject), Is.False);
        Assert.That(exit.CanInteract(player.gameObject), Is.False);
        CutsceneRunner intro = Story.GetComponent<CutsceneRunner>();
        var gate = (CutsceneDialogueInputGate)typeof(CutsceneRunner).GetField("dialogueGate", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(intro);
        var text = (UnityEngine.UI.Text)typeof(CutsceneBubbleView).GetField("bodyText", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(intro.GetComponent<CutsceneBubbleView>());
        Assert.That(UiModal.IsOpen, Is.True);
        Assert.That(player.GetComponent<PlayerDash>().TryDash(), Is.False);
        Assert.That(StoryRoomFlow.BlocksDeveloperActions, Is.True);
        yield return new WaitForSecondsRealtime(.1f);
        Assert.That(gate.IsTyping, Is.True);
        intro.RequestDialogueClick();
        yield return null; yield return null;
        Assert.That(text.text, Is.EqualTo(fastDefinition.introCues[0].text));
        Assert.That(gate.WaitingForAdvance, Is.True, "타이핑 완료 클릭이 다음 대사를 넘기지 않는다.");
        yield return new WaitForSecondsRealtime(.1f);
        Assert.That(text.text, Is.EqualTo(fastDefinition.introCues[0].text), "클릭 없이 자동으로 넘어가지 않는다.");
        for (int i = 0; i < fastDefinition.introCues.Length; i++)
        {
            if (i > 0)
            {
                float typingUntil = Time.realtimeSinceStartup + 2;
                while (!gate.IsTyping && Time.realtimeSinceStartup < typingUntil) yield return null;
                Assert.That(gate.IsTyping, Is.True);
                intro.RequestDialogueClick();
                yield return null; yield return null;
            }
            string expected = string.IsNullOrEmpty(fastDefinition.introCues[i].text) ? "..." : fastDefinition.introCues[i].text;
            Assert.That(text.text, Is.EqualTo(expected));
            Assert.That(UiModal.IsOpen, Is.True);
            Assert.That(Story.CanInteract, Is.False);
            intro.RequestDialogueClick();
            yield return null; yield return null;
        }
        fastDefinition.charactersPerSecond = 10000;
        float releasedUntil = Time.realtimeSinceStartup + 3;
        while (!Story.CanInteract && Time.realtimeSinceStartup < releasedUntil) yield return null;
        Assert.That(Story.CanInteract, Is.True, "도입부 종료 후에는 자유 이동과 상호작용을 복원한다.");
        AmbientPrayerController prayer = Object.FindFirstObjectByType<AmbientPrayerController>();
        float until = Time.realtimeSinceStartup + 8;
        while (!prayer.IsApologyLoopReady && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(prayer.IsApologyLoopReady, Is.True);
        yield return new WaitForSeconds(.2f);
        Assert.That(prayer.IsApologyLoopReady, Is.True);
        Assert.That(prayer.IsLooping, Is.False, "20번을 마쳐도 접근 사망 조건은 유지한다.");
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
