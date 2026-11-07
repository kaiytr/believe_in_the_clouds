using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CutsceneRunnerPlayModeTests
{
    [UnityTest]
    public IEnumerator MissingExternalProviderCompletesAndReleasesItsModalToken()
    {
        bool modalWasOpen = UiModal.IsOpen;
        CutsceneDefinition definition = ScriptableObject.CreateInstance<CutsceneDefinition>();
        definition.Configure("tip.missing", new[] { CutsceneStep.External("tutorial.tip") });
        GameObject root = new GameObject("Cutscene Runner Test");
        root.AddComponent<CutsceneBubbleView>();
        CutsceneRunner runner = root.AddComponent<CutsceneRunner>();
        int completed = 0;
        runner.Completed += _ => completed++;

        Assert.That(runner.Play(definition), Is.True);
        Assert.That(UiModal.IsOpen, Is.True);
        yield return null;
        yield return null;

        Assert.That(runner.IsRunning, Is.False);
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(UiModal.IsOpen, Is.EqualTo(modalWasOpen));

        Object.Destroy(root);
        Object.Destroy(definition);
        yield return null;
    }

    [UnityTest]
    public IEnumerator CancelDuringExternalStageCleansUpOnceAndRejectsLateCallback()
    {
        bool modalWasOpen = UiModal.IsOpen;
        CutsceneDefinition definition = ScriptableObject.CreateInstance<CutsceneDefinition>();
        definition.Configure("tip.pending", new[] { CutsceneStep.External("tutorial.tip") });
        GameObject root = new GameObject("Cutscene External Stage Test");
        root.AddComponent<CutsceneBubbleView>();
        CutsceneRunner runner = root.AddComponent<CutsceneRunner>();
        CutsceneExternalStage captured = null;
        int completed = 0;
        int cancelled = 0;
        runner.ExternalStageRequested += stage => captured = stage;
        runner.Completed += _ => completed++;
        runner.Cancelled += _ => cancelled++;

        Assert.That(runner.Play(definition), Is.True);
        Assert.That(captured, Is.Not.Null);
        Assert.That(UiModal.IsOpen, Is.True);

        captured.Cancel();
        captured.Complete();

        Assert.That(runner.IsRunning, Is.False);
        Assert.That(completed, Is.Zero);
        Assert.That(cancelled, Is.EqualTo(1));
        Assert.That(UiModal.IsOpen, Is.EqualTo(modalWasOpen));

        Object.Destroy(root);
        Object.Destroy(definition);
        yield return null;
    }

    [Test]
    public void CutsceneUsesBundledFontWithKoreanGlyphs()
    {
        Font font = CombatUiResources.KoreanFont();
        Assert.That(font, Is.Not.Null);
        Assert.That(font.HasCharacter('죄'), Is.True);
    }

    [UnityTest]
    public IEnumerator BubbleFollowsSpeakerAnchorAndStaysInsideScreenSafeArea()
    {
        GameObject cameraObject = new GameObject("Cutscene Bubble Test Camera", typeof(Camera));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        GameObject root = new GameObject("Bubble View Test");
        CutsceneBubbleView view = root.AddComponent<CutsceneBubbleView>();
        typeof(CutsceneBubbleView).GetField("trackingCamera", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(view, camera);
        GameObject actorObject = new GameObject("Speaker");
        CutsceneCharacter actor = actorObject.AddComponent<CutsceneCharacter>();
        actor.transform.position = Vector3.zero;
        view.ShowLine(actor, "한글 대사");
        yield return null;
        var background = view.BubbleRect.GetComponent<UnityEngine.UI.Image>();
        Assert.That(background.sprite.texture, Is.SameAs(HudArtworkResources.Load("EnemyBeliefSpeechBubble").Sprite.texture));
        Assert.That(background.sprite.border.y, Is.GreaterThan(0f));
        Assert.That(view.BubbleRect.pivot.y, Is.Zero, "말풍선 꼬리를 화자 머리 위에 붙인다.");
        Vector2 firstPosition = view.BubbleRect.anchoredPosition;

        actor.transform.position = new Vector3(1.4f, 0.8f, 0f);
        yield return null;
        Vector2 movedPosition = view.BubbleRect.anchoredPosition;
        Assert.That((movedPosition - firstPosition).sqrMagnitude, Is.GreaterThan(1f));

        actor.transform.position = new Vector3(100f, 100f, 0f);
        yield return null;
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(null, view.BubbleRect.position);
        Rect safe = Screen.safeArea;
        Assert.That(view.IsVisible, Is.True);
        Assert.That(screenPosition.x, Is.InRange(safe.xMin, safe.xMax));
        Assert.That(screenPosition.y, Is.InRange(safe.yMin, safe.yMax));

        Object.Destroy(root);
        Object.Destroy(actorObject);
        Object.Destroy(cameraObject);
        yield return null;
    }

    [UnityTest]
    public IEnumerator CutsceneStoneCloneCannotCollideOrRunDamageBehavior()
    {
        GameObject clone = new GameObject("Harmless Stone Visual Clone");
        Rigidbody2D body = clone.AddComponent<Rigidbody2D>();
        BoxCollider2D collider = clone.AddComponent<BoxCollider2D>();
        StoneProjectile damageBehavior = clone.AddComponent<StoneProjectile>();

        Assert.That(CutsceneHarmlessProjectile.Prepare(clone), Is.True);
        Assert.That(collider.enabled, Is.False);
        Assert.That(body.simulated, Is.False);
        Assert.That(damageBehavior.enabled, Is.False);

        Object.Destroy(clone);
        yield return null;
    }
}
