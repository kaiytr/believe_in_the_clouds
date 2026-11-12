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

public sealed class BossStagePlayModeTests
{
    private BossCombatController boss;
    private PlayerHealth player;
    private BossEncounterTrigger entry;
    private Camera view;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        SceneManager.sceneLoaded += PauseBoss;
        Type editor = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("UnityEditor.SceneManagement.EditorSceneManager", false))
            .First(type => type != null);
        MethodInfo loadMethod = editor.GetMethod("LoadSceneAsyncInPlayMode", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(string), typeof(LoadSceneParameters) }, null);
        var load = (AsyncOperation)loadMethod.Invoke(null,
            new object[] { ProjectScenes.Boss, new LoadSceneParameters(LoadSceneMode.Single) });
        while (!load.isDone) yield return null;
        SceneManager.sceneLoaded -= PauseBoss;
        yield return null;
        boss = Object.FindFirstObjectByType<BossCombatController>();
        player = Object.FindFirstObjectByType<PlayerHealth>();
        entry = Object.FindFirstObjectByType<BossEncounterTrigger>();
        view = Camera.main;
        player.GetComponent<PlayerMove>().enabled = false;
        MovePlayer(new Vector2(player.transform.position.x, -2.3f));
    }

    private static void PauseBoss(Scene scene, LoadSceneMode mode)
    {
        var intro = Object.FindFirstObjectByType<CutsceneBossEncounterCoordinator>();
        if (intro != null) intro.enabled = false;
        var arena = Object.FindFirstObjectByType<BossEncounterTrigger>();
        if (arena != null) arena.AutoBeginEncounter = true;
        BossCombatController controller = Object.FindFirstObjectByType<BossCombatController>();
        if (controller != null) controller.AutomaticPatterns = false;
    }

    private void MovePlayer(Vector2 position)
    {
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        body.gravityScale = 0;
        body.linearVelocity = Vector2.zero;
        body.position = position;
        player.transform.position = position;
        Physics2D.SyncTransforms();
    }

    private IEnumerator Enter()
    {
        MovePlayer(new Vector2(-10, -2.3f));
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(entry.HasEntered, Is.True);
        Assert.That(boss.EncounterStarted, Is.True);
        yield return new WaitForSecondsRealtime(.7f);
    }

    [UnityTest]
    public IEnumerator WholeBodyEntrySealsArenaAndDefeatRestoresExploration()
    {
        Assert.That(boss.EncounterStarted, Is.False);
        Bounds ground = GameObject.Find("Main ground").GetComponent<BoxCollider2D>().bounds;
        Bounds arena = entry.GetComponent<BoxCollider2D>().bounds;
        Assert.That(ground.size.x, Is.GreaterThanOrEqualTo(80), "The entire physical scene must expand.");
        Assert.That(arena.min.x - ground.min.x, Is.GreaterThanOrEqualTo(25), "Keep a substantial approach outside the arena.");
        Assert.That(ground.max.x - arena.max.x, Is.GreaterThanOrEqualTo(12), "Keep a far-side area outside the arena.");
        Assert.That(player.transform.position.x, Is.LessThan(-30), "Start in the expanded approach.");
        LobbySceneController flow = Object.FindFirstObjectByType<LobbySceneController>();
        Assert.That(flow.ClampPosition(new Vector2(-38, -2)).x, Is.EqualTo(-38));
        Assert.That(flow.ClampPosition(new Vector2(46, -2)).x, Is.EqualTo(46));
        foreach (float x in new[] { -38f, -25f, -13f })
        {
            MovePlayer(new Vector2(x, -2.3f));
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(entry.HasEntered, Is.False, "Walking through the approach must not start the encounter.");
            Assert.That(Physics2D.Raycast(new Vector2(x, -3.1f), Vector2.down, 1,
                LayerMask.GetMask("Ground")).collider, Is.Not.Null, "The approach needs actual ground.");
        }
        Collider2D[] gates = entry.transform.Cast<Transform>().Where(child => child.name.EndsWith("seal"))
            .Select(child => child.GetComponent<Collider2D>()).ToArray();
        Assert.That(gates.Length, Is.EqualTo(2));
        Assert.That(gates.All(gate => gate.isTrigger), Is.True);
        MovePlayer(new Vector2(-12, -2.3f));
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(entry.HasEntered, Is.False, "The player's trailing edge has not crossed the threshold.");
        float explorationSize = view.orthographicSize;
        yield return Enter();
        Assert.That(gates.All(gate => !gate.isTrigger && !gate.GetComponent<SpriteRenderer>().enabled), Is.True);
        Assert.That(view.GetComponent<LobbyCameraFollow>().enabled, Is.False);
        Assert.That(view.orthographicSize, Is.GreaterThan(explorationSize));
        boss.ForcePhaseTwoForTest();
        boss.TakeDamage(new DamageInfo(10000, DamageSource.PlayerOwned));
        yield return new WaitForSecondsRealtime(.7f);
        Assert.That(boss.IsDead, Is.True);
        Assert.That(gates.All(gate => gate.isTrigger && !gate.GetComponent<SpriteRenderer>().enabled), Is.True);
        Assert.That(view.GetComponent<LobbyCameraFollow>().enabled, Is.True);
        Assert.That(view.orthographicSize, Is.EqualTo(explorationSize).Within(.001f));
        MovePlayer(new Vector2(44, -2.3f));
        yield return new WaitForSecondsRealtime(.6f);
        Assert.That(Physics2D.Raycast(new Vector2(44, -3.1f), Vector2.down, 1,
            LayerMask.GetMask("Ground")).collider, Is.Not.Null);
        Assert.That(view.transform.position.x, Is.GreaterThan(33), "Exploration must reach the far-side area.");
        yield return Capture("04-far-side", 1600, 900);
    }

    [UnityTest]
    public IEnumerator InvisibleSealsAlignWithCombatViewportAcrossAspectRatios()
    {
        entry.AutoBeginEncounter = false;
        Bounds arenaBounds = entry.GetComponent<BoxCollider2D>().bounds;
        MovePlayer(new Vector2(arenaBounds.min.x + 2f, -2.3f));
        float entryDeadline = Time.realtimeSinceStartup + 2f;
        while (!entry.HasEntered && Time.realtimeSinceStartup < entryDeadline)
        {
            yield return new WaitForFixedUpdate();
            yield return null;
        }
        Assert.That(entry.HasEntered, Is.True, "The player's body must enter the actual arena trigger.");
        Assert.That(entry.HoldingForIntro, Is.True,
            $"started={boss.EncounterStarted}, dead={boss.IsDead}, playerDead={player.IsDead}, autoBegin={entry.AutoBeginEncounter}");
        Collider2D[] gates = entry.transform.Cast<Transform>().Where(child => child.name.EndsWith("seal"))
            .Select(child => child.GetComponent<Collider2D>()).OrderBy(gate => gate.bounds.center.x).ToArray();
        Assert.That(gates.Length, Is.EqualTo(2));
        foreach (float aspect in new[] { 16f / 9f, 4f / 3f, 32f / 9f })
        {
            view.aspect = aspect;
            yield return new WaitForSecondsRealtime(1.2f);
            Physics2D.SyncTransforms();
            float left = view.transform.position.x - view.orthographicSize * aspect;
            float right = view.transform.position.x + view.orthographicSize * aspect;
            float bottom = view.transform.position.y - view.orthographicSize;
            float top = view.transform.position.y + view.orthographicSize;
            Assert.That(gates[0].bounds.max.x, Is.EqualTo(left).Within(.01f));
            Assert.That(gates[1].bounds.min.x, Is.EqualTo(right).Within(.01f));
            foreach (Collider2D gate in gates)
            {
                Assert.That(gate.isTrigger, Is.False);
                Assert.That(gate.GetComponent<SpriteRenderer>().enabled, Is.False);
                Assert.That(gate.GetComponent<SpriteRenderer>().color.a, Is.Zero);
                Assert.That(gate.bounds.min.y, Is.LessThanOrEqualTo(bottom + .01f));
                Assert.That(gate.bounds.max.y, Is.GreaterThanOrEqualTo(top - .01f));
            }
            Assert.That(Physics2D.Raycast(new Vector2(left + .1f, view.transform.position.y),
                Vector2.left, 1f, LayerMask.GetMask("Ground")).collider, Is.SameAs(gates[0]));
            Assert.That(Physics2D.Raycast(new Vector2(right - .1f, view.transform.position.y),
                Vector2.right, 1f, LayerMask.GetMask("Ground")).collider, Is.SameAs(gates[1]));
        }
        MovePlayer(new Vector2(arenaBounds.min.x - 10f, -2.3f));
        entry.CancelIntro();
        Assert.That(entry.HasEntered, Is.False);
        Assert.That(gates.All(gate => gate.isTrigger && !gate.GetComponent<SpriteRenderer>().enabled), Is.True);
    }

    [UnityTest]
    public IEnumerator OriginalCombatCameraTracksAtWideAndNarrowAspectsAndRainCoversWholeScene()
    {
        yield return new WaitForSecondsRealtime(.4f);
        yield return Capture("01-approach", 1600, 900);
        yield return Enter();
        Bounds bounds = entry.transform.Find("Camera bounds").GetComponent<BoxCollider2D>().bounds;
        foreach (float aspect in new[] { 16f / 9, 4f / 3 })
        {
            view.aspect = aspect;
            MovePlayer(new Vector2(-10, -2.3f));
            yield return new WaitForSecondsRealtime(.8f);
            float leftCameraX = view.transform.position.x;
            Assert.That(view.orthographicSize, Is.EqualTo(8).Within(.02f), "Keep the original combat zoom.");
            Assert.That(view.transform.position.y, Is.EqualTo(1).Within(.02f));
            MovePlayer(new Vector2(28, -2.3f));
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(view.transform.position.x, Is.GreaterThan(leftCameraX + 10), "Combat follows the player horizontally.");
            float halfWidth = view.orthographicSize * view.aspect;
            Assert.That(view.transform.position.x - halfWidth, Is.GreaterThanOrEqualTo(bounds.min.x - .05f));
            Assert.That(view.transform.position.x + halfWidth, Is.LessThanOrEqualTo(bounds.max.x + .05f));
            Assert.That(view.orthographicSize, Is.EqualTo(8).Within(.02f));
            MovePlayer(new Vector2(6, -2.3f));
            yield return new WaitForSecondsRealtime(.8f);
            yield return Capture(aspect > 1.5f ? "02-arena-16x9" : "03-arena-4x3",
                aspect > 1.5f ? 1600 : 1200, 900);
        }
        GameObject scenery = GameObject.Find("Boss ruined sanctuary");
        Assert.That(scenery, Is.Not.Null);
        Assert.That(scenery.GetComponentsInChildren<Collider2D>(), Is.Empty, "Rubble and weather must not alter combat geometry.");
        ParticleSystem[] rain = scenery.GetComponentsInChildren<ParticleSystem>();
        Assert.That(rain.Length, Is.EqualTo(2));
        Assert.That(rain.All(system => system.particleCount > 0), Is.True);
        Assert.That(rain.All(system => !system.collision.enabled), Is.True);
        Assert.That(rain.All(system => system.shape.scale.x >= 90), Is.True, "Rain must cover the approach and far side too.");
        view.ResetAspect();
    }

    private IEnumerator Capture(string name, int width, int height)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
        var texture = new RenderTexture(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        view.targetTexture = texture;
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Where(canvas => canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        foreach (Canvas canvas in canvases)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = view;
            canvas.planeDistance = 1;
        }
        yield return null;
        Canvas.ForceUpdateCanvases();
        view.Render();
        RenderTexture.active = texture;
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        string directory = Path.Combine(Application.dataPath, "../Logs/BossStageVerification");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
        view.targetTexture = null;
        RenderTexture.active = previous;
        foreach (Canvas canvas in canvases)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
        }
        texture.Release();
        Object.Destroy(texture);
        Object.Destroy(image);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        SceneManager.sceneLoaded -= PauseBoss;
        if (view != null) view.ResetAspect();
        if (boss != null) boss.PauseAndCancelForTest();
        Time.timeScale = 1;
        yield return null;
    }
}
