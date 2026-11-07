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

public sealed class DeathResultPlayModeTests
{
    private EconomyService previous;
    private MemoryEconomyStore store;
    private PlayerHealth player;
    [UnitySetUp] public IEnumerator SetUp()
    {
        EconomyRuntime.Ensure(); previous = Currency.Service;
        store = new MemoryEconomyStore(); EconomyRuntime.Instance.SetService(new EconomyService(store));
        yield return SceneManager.LoadSceneAsync("Lobby");
        yield return WaitUntil(() => LobbyFlowRuntime.Instance != null && LobbyFlowRuntime.Instance.InputReady);
        player = Object.FindFirstObjectByType<PlayerHealth>();
    }
    [UnityTearDown] public IEnumerator TearDown()
    {
        if (PlayerDeathCoordinator.Active != null) Object.Destroy(PlayerDeathCoordinator.Active.gameObject);
        if (LobbyFlowRuntime.Instance != null) Object.Destroy(LobbyFlowRuntime.Instance.gameObject);
        yield return null;
        EconomyRuntime.Instance.SetService(previous); Time.timeScale = 1f;
    }
    private static IEnumerator WaitUntil(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 10;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "Unscaled scene transition timeout");
    }
    private PlayerDeathCoordinator Kill()
    {
        Currency.TryGrant(100, "fixture");
        Currency.Service.TryGrantPiece("subject.player", 13, "fixture");
        Assert.That(player.TakeDamage(new DamageInfo(10000, DamageSource.Environment, null, "낙하")), Is.True);
        return player.GetComponent<PlayerDeathCoordinator>();
    }
    private static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(item => item.name == name);
    private static IEnumerator WaitForResult() => WaitUntil(() => Object.FindFirstObjectByType<DeathResultView>() != null);

    [UnityTest] public IEnumerator DeathZoomAndSlowMotionFinishBeforeResultWithoutSquashingThePlayer()
    {
        var animation = player.GetComponent<PlayerIdleSpriteAnimator>();
        Assert.That(animation, Is.Not.Null);
        var frames = (Sprite[])typeof(PlayerIdleSpriteAnimator).GetField("deadFrames", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(animation);
        Assert.That(frames.Length, Is.GreaterThan(1));
        var renderer = player.GetComponent<SpriteRenderer>();
        var shownFrames = new List<Sprite>();
        void ObserveFrame()
        {
            if (frames.Contains(renderer.sprite) && (shownFrames.Count == 0 || shownFrames[shownFrames.Count - 1] != renderer.sprite))
                shownFrames.Add(renderer.sprite);
        }

        Vector3 position = player.transform.position;
        Vector3 scale = player.transform.localScale;
        Camera camera = Camera.main;
        float cameraSize = camera.orthographicSize;
        float startedAt = Time.unscaledTime;
        var coordinator = Kill();
        Assert.That(Object.FindFirstObjectByType<DeathResultView>(), Is.Null);
        Assert.That(Time.timeScale, Is.EqualTo(.5f));
        Assert.That(UiModal.IsOpen, Is.True);
        Assert.That(player.GetComponent<PlayerMove>().enabled, Is.False);
        Assert.That(player.GetComponent<PlayerAttack>().enabled, Is.False);
        Assert.That(player.GetComponent<PlayerDash>().enabled, Is.False);
        coordinator.Continue(); coordinator.SaveAndExit();
        Assert.That(Currency.Balance, Is.EqualTo(100), "Choices cannot skip the death animation.");

        float deadline = Time.realtimeSinceStartup + 5f;
        while (!animation.IsDeathAnimationComplete && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            ObserveFrame();
            Assert.That(player.transform.position, Is.EqualTo(position));
            Assert.That(player.transform.localScale, Is.EqualTo(scale));
            if (!animation.IsDeathAnimationComplete)
            {
                Assert.That(Time.timeScale, Is.EqualTo(.5f));
                Assert.That(Object.FindFirstObjectByType<DeathResultView>(), Is.Null);
            }
        }
        Assert.That(animation.IsDeathAnimationComplete, Is.True);
        Assert.That(Time.unscaledTime - startedAt, Is.GreaterThanOrEqualTo(1.1f));
        CollectionAssert.AreEqual(frames, shownFrames);
        Assert.That(renderer.sprite, Is.SameAs(frames[frames.Length - 1]));
        yield return WaitForResult();
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(camera.orthographicSize, Is.EqualTo(cameraSize * .6f).Within(.01f));
        Vector3 focus = camera.WorldToViewportPoint(player.BodyCollider.bounds.center);
        Assert.That(focus.x, Is.EqualTo(.5f).Within(.02f));
        Assert.That(focus.y, Is.EqualTo(.5f).Within(.02f));
        Assert.That(Object.FindObjectsByType<DeathResultView>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        Assert.That(Currency.Balance, Is.EqualTo(100));
    }

    [UnityTest] public IEnumerator DisabledDeathAnimatorDoesNotBlockResult()
    {
        player.GetComponent<PlayerIdleSpriteAnimator>().enabled = false;
        Kill();
        Assert.That(Object.FindFirstObjectByType<DeathResultView>(), Is.Not.Null);
        yield return null;
    }

    [UnityTest] public IEnumerator MissingDeathFramesDoNotBlockResult()
    {
        var animation = player.GetComponent<PlayerIdleSpriteAnimator>();
        typeof(PlayerIdleSpriteAnimator).GetField("deadFrames", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(animation, Array.Empty<Sprite>());
        Kill();
        Assert.That(Object.FindFirstObjectByType<DeathResultView>(), Is.Not.Null);
        yield return null;
    }

    [UnityTest] public IEnumerator LethalDamageShowsOneOverlayFreezesWorldAndCapturesBeforeCanvasExists()
    {
        Camera camera = Camera.main;
        Vector3 cameraPosition = camera.transform.position;
        float cameraSize = camera.orthographicSize;
        var coordinator = Kill();
        Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
        Assert.That(camera.orthographicSize, Is.EqualTo(cameraSize));
        player.TakeDamage(10000); coordinator.CaptureDeath(() => Assert.Fail("Duplicate effects"));
        yield return WaitForResult();
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Lobby"));
        Assert.That(Object.FindObjectsByType<DeathResultView>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        Assert.That(Time.timeScale, Is.Zero); Assert.That(UiModal.IsOpen, Is.True);
        Assert.That(player.GetComponent<PlayerAttack>().enabled, Is.False);
        Assert.That(Currency.Balance, Is.EqualTo(100));
        Assert.That(coordinator.Result.cause, Is.EqualTo("환경 피해 · 낙하"));
        Assert.That(coordinator.Result.statistics.coins, Is.EqualTo(100));
        Assert.That(coordinator.Result.statistics.keywords, Is.EqualTo(13));
        Assert.That(coordinator.Result.screenshotPngBase64, Is.Not.Null.And.Not.Empty);
        var image = Object.FindFirstObjectByType<DeathResultView>().GetComponentInChildren<RawImage>();
        Assert.That(image.texture.width, Is.GreaterThan(0));
        Assert.That((float)image.texture.width / image.texture.height, Is.EqualTo(16f / 9f).Within(.005f));
        Assert.That(image.transform.parent.GetComponent<RectMask2D>(), Is.Not.Null);
        Assert.That(image.GetComponent<AspectRatioFitter>().aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
        Button("이어하기").onClick.Invoke();
        yield return WaitUntil(() => !PlayerDeathCoordinator.IsResultOpen && LobbyFlowRuntime.Instance.InputReady);
        Assert.That(Currency.Balance, Is.EqualTo(70)); Assert.That(Time.timeScale, Is.EqualTo(1));
        Assert.That(UiModal.IsOpen, Is.False);
        Assert.That(Object.FindFirstObjectByType<DeathResultView>(), Is.Null);
    }
    [UnityTest] public IEnumerator SaveFailureKeepsResultThenExitPersistsPenaltyAndOpensWorkingTitle()
    {
        var coordinator = Kill();
        store.FailWrites = true;
        yield return WaitForResult();
        Button("저장 및 종료").onClick.Invoke(); yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Lobby"));
        Assert.That(Time.timeScale, Is.Zero); Assert.That(Button("저장 및 종료").interactable, Is.True);
        Assert.That(Currency.Balance, Is.EqualTo(100));
        store.FailWrites = false;
        Button("저장 및 종료").onClick.Invoke();
        Button("저장 및 종료").onClick.Invoke();
        yield return WaitUntil(() => SceneManager.GetActiveScene().name == TitleScreen.SceneName);
        yield return null;
        var saved = store.Load().Data;
        Assert.That(saved.balance, Is.EqualTo(70)); Assert.That(EconomyLedger.Total(saved.pieces), Is.EqualTo(9));
        Assert.That(saved.death.result.screenshotPngBase64, Is.Not.Empty);
        Assert.That(saved.death.result.statistics.coins, Is.EqualTo(100));
        Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(UiModal.IsOpen, Is.False);
        Assert.That(Object.FindFirstObjectByType<DeathResultView>(), Is.Null);
        Button("로비로 이동").onClick.Invoke();
        yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Lobby" && LobbyFlowRuntime.Instance.InputReady);
        Assert.That(Currency.Balance, Is.EqualTo(70));
        Assert.That(Object.FindFirstObjectByType<PlayerHealth>().CurrentHp, Is.EqualTo(100));
    }
    [UnityTest] public IEnumerator ActualEnemyIdentityReachesLethalDamageAndKillStatistics()
    {
        yield return SceneManager.LoadSceneAsync("CombatLevel1"); yield return null;
        player = Object.FindFirstObjectByType<PlayerHealth>();
        EnemyUnit dog = Object.FindFirstObjectByType<DogEnemy>();
        Assert.That(dog, Is.Not.Null);
        Assert.That(dog.TakeDamage(new DamageInfo(10000, DamageSource.Environment)), Is.True);
        dog.TakeDamage(new DamageInfo(10000, DamageSource.Environment));
        int actualReward = Currency.Service.Snapshot().processedTransactions.Single(item => item.id == dog.DefeatId).result.AppliedAmount;
        Camera camera = Camera.main;
        float cameraSize = camera.orthographicSize;
        Assert.That(player.TakeDamage(new DamageInfo(10000, DamageSource.Enemy, dog.gameObject, "돌진")), Is.True);
        yield return null;
        var coordinator = player.GetComponent<PlayerDeathCoordinator>();
        Assert.That(coordinator.Result.cause, Is.EqualTo("개 · 돌진"));
        Assert.That(coordinator.Result.statistics.Count(EnemyRewardKind.Dog), Is.EqualTo(1));
        Assert.That(coordinator.Result.statistics.coins, Is.EqualTo(actualReward));
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CombatLevel1"));
        yield return WaitForResult();
        yield return null;
        Assert.That(camera.orthographicSize, Is.EqualTo(cameraSize * .6f).Within(.01f));
    }
    [UnityTest] public IEnumerator CaptureFailureHasFallbackAndDoesNotChangeRenderTarget()
    {
        Assert.That(DeathScreenCapture.Capture(null, out string error), Is.Null);
        Assert.That(error, Is.Not.Empty);
        Camera camera = Camera.main;
        RenderTexture target = camera.targetTexture, active = RenderTexture.active;
        Texture2D image = DeathScreenCapture.Capture(camera, out error);
        Assert.That(image, Is.Not.Null); Assert.That(camera.targetTexture, Is.SameAs(target));
        Assert.That(RenderTexture.active, Is.SameAs(active)); Object.Destroy(image);
        float previousAspect = camera.aspect;
        try
        {
            camera.aspect = 9f / 16f;
            image = DeathScreenCapture.Capture(camera, out error);
            Assert.That(image, Is.Not.Null);
            Assert.That((float)image.width / image.height, Is.EqualTo(9f / 16f).Within(.005f));
            Assert.That(image.width, Is.LessThanOrEqualTo(960)); Assert.That(image.height, Is.LessThanOrEqualTo(960));
        }
        finally { camera.aspect = previousAspect; if (image != null) Object.Destroy(image); }
        yield return null;
    }
    [UnityTest] public IEnumerator ResultLayoutFitsWideAndFourByThreeScreens()
    {
        Kill(); yield return WaitForResult();
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("Graphics required");
        string output = Path.GetFullPath("Logs/DeathResultVerification"); Directory.CreateDirectory(output);
        foreach (Vector2Int resolution in new[] { new Vector2Int(1600, 900), new Vector2Int(1200, 900) })
        {
            Screen.SetResolution(resolution.x, resolution.y, false);
            yield return new WaitForSecondsRealtime(.25f);
            Canvas.ForceUpdateCanvases();
            foreach (string label in new[] { "이어하기", "저장 및 종료" })
            {
                var corners = new Vector3[4]; Button(label).GetComponent<RectTransform>().GetWorldCorners(corners);
                Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(0)); Assert.That(corners[2].x, Is.LessThanOrEqualTo(Screen.width));
                Assert.That(corners[0].y, Is.GreaterThanOrEqualTo(0)); Assert.That(corners[2].y, Is.LessThanOrEqualTo(Screen.height));
            }
            // Batchmode does not reliably yield WaitForEndOfFrame. Render the
            // same canvases through the gameplay camera at the requested size.
            Camera camera = Camera.main;
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(item => item.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            RenderTexture previousTarget = camera.targetTexture, previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(resolution.x, resolution.y, 24);
            Texture2D screenshot = null;
            try
            {
                camera.targetTexture = target;
                foreach (Canvas canvas in canvases)
                { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                screenshot = new Texture2D(resolution.x, resolution.y, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0); screenshot.Apply();
                File.WriteAllBytes(Path.Combine(output, "result-" + resolution.x + "x" + resolution.y + ".png"), screenshot.EncodeToPNG());
            }
            finally
            {
                foreach (Canvas canvas in canvases)
                    if (canvas != null) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target); if (screenshot != null) Object.Destroy(screenshot);
            }
        }
    }
}
