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

public sealed class ChestPlayModeTests
{
    private ChestTestContext context;
    private InteractionController controller;
    private ChestInteractable chest;

    [UnitySetUp] public IEnumerator SetUp()
    {
        Time.timeScale = 1;
        if (LobbyFlowRuntime.Instance != null) Object.Destroy(LobbyFlowRuntime.Instance.gameObject);
        yield return null;
        yield return Load("ChestTest");
        context = Object.FindFirstObjectByType<ChestTestContext>();
        controller = InteractionController.Instance; controller.UseManualInput = true;
        chest = Object.FindObjectsByType<ChestInteractable>(FindObjectsSortMode.None).OrderBy(c => c.SpawnId).First();
        Move(chest.transform.position.x);
        yield return new WaitForSeconds(.3f);
        controller.ProcessInput(new InteractionFrame(), 0);
        Assert.That(context.Player.GetComponent<PlayerMove>().IsGrounded, Is.True);
    }
    [UnityTearDown] public IEnumerator TearDown()
    {
        Time.timeScale = 1;
        context?.CloseModal(); controller.CancelHold(); controller.UseManualInput = false;
        yield return Load("StageTest");
    }
    private static IEnumerator Load(string name)
    {
        Type editor = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.SceneManagement.EditorSceneManager", false)).First(t => t != null);
        MethodInfo method = editor.GetMethod("LoadSceneAsyncInPlayMode", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(string), typeof(LoadSceneParameters) }, null);
        var operation = (AsyncOperation)method.Invoke(null, new object[] { ProjectScenes.ForName(name), new LoadSceneParameters(LoadSceneMode.Single) });
        while (!operation.isDone) yield return null;
        yield return null;
    }
    private void Move(float x)
    {
        Rigidbody2D body = context.Player.GetComponent<Rigidbody2D>();
        body.position = new Vector2(x, context.Player.transform.position.y); body.linearVelocity = Vector2.zero;
        context.Player.transform.position = body.position; Physics2D.SyncTransforms();
    }
    private void Begin()
    {
        controller.ProcessInput(new InteractionFrame(), 0);
        controller.ProcessInput(new InteractionFrame(pressed: true, held: true), 0);
        Assert.That(controller.HoldingTarget, Is.EqualTo(chest));
    }
    private void Advance(float seconds) => controller.ProcessInput(new InteractionFrame(held: true), seconds);

    private static IEnumerator WaitForCoinSettlement(EconomyService service)
    {
        float deadline = Time.realtimeSinceStartup + 4f;
        while (service.PendingBalance > 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(service.PendingBalance, Is.Zero, "Chest coins did not settle within the presentation deadline.");
    }

    [UnityTest] public IEnumerator HoldOpensOnlyAtThresholdGrantsOnePieceAndFliesToHud()
    {
        chest.Interact(context.Player.gameObject); Assert.That(chest.IsOpened, Is.False);
        Begin(); Advance(1.49f);
        Assert.That(chest.IsOpened, Is.False); Assert.That(controller.HoldProgress, Is.GreaterThan(.99f));
        Advance(.02f); Assert.That(chest.IsOpened, Is.True);
        Assert.That(EconomyLedger.Total(context.Service.Snapshot().pieces), Is.EqualTo(1));
        int coins = context.Service.PendingBalance; Assert.That(coins, Is.InRange(7, 17));
        Assert.That(context.Service.Balance, Is.Zero);
        Advance(10); Assert.That(EconomyLedger.Total(context.Service.Snapshot().pieces), Is.EqualTo(1));
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(Object.FindObjectsByType<CoinFlightView>(FindObjectsSortMode.None).Any(v => v.HasFlights), Is.True);
        yield return WaitForCoinSettlement(context.Service);
        Assert.That(context.Service.Balance, Is.EqualTo(coins)); Assert.That(context.Service.PendingBalance, Is.Zero);
        Assert.That(DoctrineHud.CurrencyTarget.GetComponentInChildren<Text>().text, Does.Contain(coins.ToString()));
        context.Reload(); Assert.That(chest.IsOpened, Is.True);
        Assert.That(EconomyLedger.Total(context.Service.Snapshot().pieces), Is.EqualTo(1));
        yield return null;
    }
    [UnityTest] public IEnumerator ReleaseAndRangeExitResetProgressAndRequireANewPress()
    {
        Begin(); Advance(1.4f);
        controller.ProcessInput(new InteractionFrame(), 0); Assert.That(controller.HoldProgress, Is.Zero);
        Begin(); Advance(.2f); Assert.That(chest.IsOpened, Is.False);
        Move(-10); Advance(.5f); Assert.That(controller.HoldingTarget, Is.Null); Assert.That(controller.HoldProgress, Is.Zero);
        Move(chest.transform.position.x); Advance(2); Assert.That(chest.IsOpened, Is.False);
        Begin(); Advance(1.5f); Assert.That(chest.IsOpened, Is.True);
        yield return null;
    }
    [UnityTest] public IEnumerator HoldingBeforeApproachAndModalCloseNeverStartsANewSession()
    {
        Move(-10); controller.ProcessInput(new InteractionFrame(pressed: true, held: true), 0);
        Move(chest.transform.position.x); Advance(3); Assert.That(chest.IsOpened, Is.False);
        context.ShowModal(); controller.ProcessInput(new InteractionFrame(pressed: true, held: true), 0);
        Assert.That(UiModal.IsOpen, Is.False);
        Advance(3); Assert.That(controller.HoldingTarget, Is.Null);
        Begin(); Advance(.5f); context.ShowModal(); Assert.That(controller.HoldProgress, Is.Zero);
        controller.ProcessInput(new InteractionFrame(closePressed: true), 0); Assert.That(UiModal.IsOpen, Is.False);
        yield return null;
    }
    [UnityTest] public IEnumerator AcceptedHitEvenWithImmediateHealingCancelsButInvulnerableHitDoesNot()
    {
        float before = context.Player.CurrentHp;
        void HealOnHit(DoctrineEventData evt) { if (evt.SubjectObject == context.Player.gameObject && evt.Condition == ConditionType.OnHit) context.Player.Heal(10); }
        DoctrineEventBus.EventRaised += HealOnHit;
        Begin(); Advance(1.49f); context.DamagePlayer();
        DoctrineEventBus.EventRaised -= HealOnHit;
        Assert.That(context.Player.CurrentHp, Is.EqualTo(before)); Assert.That(controller.HoldProgress, Is.Zero);
        Advance(.02f); Assert.That(chest.IsOpened, Is.False);
        yield return new WaitForSeconds(.3f);
        Begin(); Advance(.5f); context.DamagePlayer();
        Assert.That(controller.HoldProgress, Is.GreaterThan(0));
        Advance(1); Assert.That(chest.IsOpened, Is.True);
    }
    [UnityTest] public IEnumerator SaveFailureKeepsAllRewardsAndOpenStateUnchangedThenRetries()
    {
        context.Store.FailWrites = true; Begin(); Advance(1.5f);
        Assert.That(chest.IsOpened, Is.False); Assert.That(EconomyLedger.Total(context.Service.Snapshot().pieces), Is.Zero);
        Assert.That(context.Service.PendingBalance, Is.Zero);
        context.Store.FailWrites = false;
        yield return new WaitForSecondsRealtime(2.05f);
        Begin(); Advance(1.5f); Assert.That(chest.IsOpened, Is.True);
        yield return null;
    }
    [UnityTest] public IEnumerator ActionPauseFocusLossAndDisableCancelWithoutTransferringHold()
    {
        Begin(); Advance(.7f);
        controller.ProcessInput(new InteractionFrame(held: true, actionPressed: true), 1);
        Assert.That(controller.HoldProgress, Is.Zero); Assert.That(chest.IsOpened, Is.False);
        Begin(); Advance(.7f); Time.timeScale = 0; Advance(2); Time.timeScale = 1;
        Assert.That(controller.HoldingTarget, Is.Null);
        Begin(); Advance(.7f); controller.SendMessage("OnApplicationFocus", false);
        Assert.That(controller.HoldingTarget, Is.Null); controller.SendMessage("OnApplicationFocus", true);
        Begin(); Advance(.7f); chest.gameObject.SetActive(false); Advance(2);
        Assert.That(controller.HoldingTarget, Is.Null); chest.gameObject.SetActive(true);
        Assert.That(chest.IsOpened, Is.False);
        yield return null;
    }
    [UnityTest] public IEnumerator TargetStaysPinnedAndHoldingAfterCompletionCannotOpenNextChest()
    {
        Begin(); Advance(.7f);
        ChestInteractable next = Object.FindObjectsByType<ChestInteractable>(FindObjectsSortMode.None).OrderBy(c => c.SpawnId).Skip(1).First();
        Move(chest.transform.position.x + 2);
        Advance(.8f); Assert.That(chest.IsOpened, Is.True); Assert.That(next.IsOpened, Is.False);
        Move(next.transform.position.x); Advance(5); Assert.That(next.IsOpened, Is.False);
        yield return new WaitForSecondsRealtime(.15f);
        controller.ProcessInput(new InteractionFrame(), 0); controller.ProcessInput(new InteractionFrame(true, true), 0);
        Advance(1.5f); Assert.That(next.IsOpened, Is.True);
    }
    [UnityTest] public IEnumerator ImmediateNoticeUsesSingleEAndSceneExitRestoresPreviousService()
    {
        Move(9); controller.ProcessInput(new InteractionFrame(), 0);
        controller.ProcessInput(new InteractionFrame(true, true), 0); Assert.That(UiModal.IsOpen, Is.True);
        controller.ProcessInput(new InteractionFrame(closePressed: true), 0); Assert.That(UiModal.IsOpen, Is.False);
        EconomyService fixture = context.Service;
        yield return Load("StageTest"); Assert.That(Currency.Service, Is.Not.SameAs(fixture));
    }
    [UnityTest] public IEnumerator VisualCaptureSquareProgressOpenedChestAndAspectRatios()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            Assert.Ignore("Visual capture requires graphics.");
        Begin(); Advance(.75f); yield return Capture("01-half-square", 1600, 900);
        yield return Capture("02-half-square-4x3", 1200, 900);
        Vector3 cameraPosition = Camera.main.transform.position;
        Camera.main.transform.position += Vector3.right * 2;
        yield return Capture("02b-follow-camera", 1600, 900);
        Camera.main.transform.position = cameraPosition;
        Advance(.75f); yield return new WaitForSecondsRealtime(.2f);
        controller.ProcessInput(new InteractionFrame(held: true), 0);
        yield return Capture("03-open-and-coins", 1600, 900);
        yield return WaitForCoinSettlement(context.Service); yield return Capture("04-reward-settled", 1600, 900);
    }
    private static IEnumerator Capture(string name, int width, int height)
    {
        yield return null;
        Camera camera = Camera.main;
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var texture = new RenderTexture(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture old = RenderTexture.active; camera.targetTexture = texture;
        foreach (Canvas canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
        Canvas.ForceUpdateCanvases(); yield return null; camera.Render(); RenderTexture.active = texture;
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/ChestVerification"));
        Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = old;
        foreach (Canvas canvas in canvases) if (canvas != null) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
        texture.Release(); Object.Destroy(texture); Object.Destroy(image);
        Canvas.ForceUpdateCanvases(); yield return null;
    }
}
