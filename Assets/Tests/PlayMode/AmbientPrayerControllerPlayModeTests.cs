using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class AmbientPrayerControllerPlayModeTests
{
    private GameObject root, actorObject, cameraObject;
    private AmbientPrayerController prayer;
    private CutsceneBubbleView bubble;
    private CutsceneCharacter actor;
    private FirstAltarStoryDefinition definition;
    private readonly object pauseToken = new object();
    private float previousTimeScale;

    [SetUp]
    public void SetUp()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 1f;
        cameraObject = new GameObject("Apology test camera", typeof(Camera));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        actorObject = new GameObject("Apology speaker", typeof(CutsceneCharacter));
        actor = actorObject.GetComponent<CutsceneCharacter>();
        root = new GameObject("Accumulating apology test", typeof(AmbientPrayerController));
        prayer = root.GetComponent<AmbientPrayerController>();
        definition = ScriptableObject.CreateInstance<FirstAltarStoryDefinition>();
        definition.charactersPerSecond = 60f;
        prayer.BeginLoop(definition, actor);
        bubble = root.GetComponent<CutsceneBubbleView>();
        typeof(CutsceneBubbleView).GetField("trackingCamera", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(bubble, camera);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        UiModal.Pop(pauseToken);
        Time.timeScale = previousTimeScale;
        Object.Destroy(root);
        Object.Destroy(actorObject);
        Object.Destroy(cameraObject);
        Object.Destroy(definition);
        yield return null;
    }

    private Text Body => bubble.GetComponentInChildren<Text>();

    private IEnumerator WaitForText(string prefix, bool checkGrowth = false)
    {
        float deadline = Time.realtimeSinceStartup + 4f;
        Vector2 previousSize = bubble.BubbleRect.sizeDelta;
        while (!Body.text.StartsWith(prefix) && Time.realtimeSinceStartup < deadline)
        {
            Assert.That(bubble.IsVisible, Is.True, "반복 사이에도 같은 말풍선을 계속 표시한다.");
            yield return null;
            if (checkGrowth)
            {
                Vector2 size = bubble.BubbleRect.sizeDelta;
                Assert.That(size.x, Is.GreaterThanOrEqualTo(previousSize.x - .1f));
                Assert.That(size.y, Is.GreaterThanOrEqualTo(previousSize.y - .1f));
                previousSize = size;
            }
        }
        Assert.That(Body.text, Does.StartWith(prefix));
    }

    [UnityTest]
    public IEnumerator ApologiesAccumulateWrapAndGrowWithoutResettingTheBubble()
    {
        RectTransform panel = bubble.BubbleRect;
        string apology = definition.apologyLoopText;
        yield return WaitForText(apology);
        Vector2 firstSize = panel.sizeDelta;
        string row = apology + " " + apology;
        yield return WaitForText(row + "\n" + row, true);

        Assert.That(bubble.BubbleRect, Is.SameAs(panel));
        Assert.That(panel.sizeDelta.x, Is.GreaterThan(firstSize.x));
        Assert.That(panel.sizeDelta.y, Is.GreaterThan(firstSize.y));
        Assert.That(prayer.IsApologyLoopReady, Is.True);

        UiModal.Push(pauseToken);
        string paused = Body.text;
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(Body.text, Is.EqualTo(paused));
        UiModal.Pop(pauseToken);
        yield return WaitForText(row + "\n" + row + "\n" + apology);

        prayer.StopPrayer();
        Assert.That(bubble.IsVisible, Is.False);
        Assert.That(prayer.IsApologyLoopReady, Is.False);
        prayer.BeginLoop(definition, actor);
        Assert.That(Body.text, Is.Empty, "재시작하면 이전 누적 문장을 비운다.");
        yield return WaitForText(apology);
        Assert.That(Body.text, Does.Not.Contain("\n"));
    }

    [UnityTest]
    public IEnumerator ExactlyTwentyApologiesTypeWithoutPausesOrGlitchesAndRemainVisible()
    {
        definition.charactersPerSecond = 120f;
        prayer.BeginLoop(definition, actor);
        string row = definition.apologyLoopText + " " + definition.apologyLoopText;
        string[] rows = new string[10];
        for (int i = 0; i < rows.Length; i++) rows[i] = row;
        string expected = string.Join("\n", rows);
        float started = Time.time;
        float deadline = Time.realtimeSinceStartup + 4f;
        while (prayer.IsLooping && Time.realtimeSinceStartup < deadline)
        {
            Assert.That(expected, Does.StartWith(Body.text), "타이핑 중 문자를 다른 기호로 치환하지 않는다.");
            yield return null;
        }
        float typingSeconds = definition.apologyLoopText.Length * 20 / definition.charactersPerSecond;
        Assert.That(Time.time - started, Is.LessThanOrEqualTo(typingSeconds + Time.maximumDeltaTime + .05f),
            "문장 사이 대기와 줄바꿈 대기 없이 연속으로 타이핑한다.");
        Assert.That(Body.text, Is.EqualTo(expected));
        Assert.That(prayer.IsLooping, Is.False);
        Assert.That(prayer.IsApologyLoopReady, Is.True);
        Assert.That(bubble.IsVisible, Is.True);
        yield return new WaitForSeconds(.3f);
        Assert.That(Body.text, Is.EqualTo(expected), "20번 뒤에는 추가 문장이나 글리치 없이 유지한다.");
        Assert.That(bubble.IsVisible, Is.True);
    }

    [UnityTest]
    public IEnumerator AccumulatedTextHasTheSameTopMarginAsOrdinaryDialogueWhileItFits()
    {
        prayer.StopPrayer();
        foreach (int lines in new[] { 1, 2, 5, 10 })
        {
            string[] rows = new string[lines];
            for (int i = 0; i < lines; i++) rows[i] = definition.apologyLoopText;
            string text = string.Join("\n", rows);
            bubble.ShowLine(actor, text);
            yield return null;
            Canvas.ForceUpdateCanvases();
            float ordinaryMargin = GlyphTopMargin();

            bubble.ShowAccumulatingLine(actor);
            bubble.SetAccumulatedText(text);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(GlyphTopMargin(), Is.EqualTo(ordinaryMargin).Within(2f),
                lines + "줄 누적 대사의 실제 글자 상단 여백이 일반 대사와 같아야 한다.");
        }
    }

    private float GlyphTopMargin()
    {
        Text body = Body;
        var vertices = body.cachedTextGenerator.verts;
        Assert.That(vertices.Count, Is.GreaterThan(0));
        float glyphTop = float.NegativeInfinity;
        for (int i = 0; i + 3 < vertices.Count; i += 4)
        {
            Vector3 first = vertices[i].position;
            Vector3 opposite = vertices[i + 2].position;
            if (Mathf.Approximately(first.x, opposite.x) || Mathf.Approximately(first.y, opposite.y)) continue;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 world = body.rectTransform.TransformPoint(vertices[i + corner].position / body.pixelsPerUnit);
                glyphTop = Mathf.Max(glyphTop, bubble.BubbleRect.InverseTransformPoint(world).y);
            }
        }
        Assert.That(float.IsNegativeInfinity(glyphTop), Is.False);
        return bubble.BubbleRect.rect.yMax - glyphTop;
    }

    [UnityTest]
    public IEnumerator FullBubbleKeepsNewestRowsInsideItsViewportAndRegularDialogueKeepsStableSize()
    {
        prayer.StopPrayer();
        bubble.ShowAccumulatingLine(actor);
        string row = definition.apologyLoopText + " " + definition.apologyLoopText;
        bubble.SetAccumulatedText(row);
        yield return null;
        Vector2 rowSize = bubble.BubbleRect.sizeDelta;

        string[] rows = new string[80];
        for (int i = 0; i < rows.Length; i++) rows[i] = row;
        bubble.SetAccumulatedText(string.Join("\n", rows));
        yield return null;
        Canvas.ForceUpdateCanvases();

        Text body = Body;
        RectMask2D mask = body.GetComponentInParent<RectMask2D>();
        Assert.That(mask.enabled, Is.True);
        Assert.That(body.resizeTextForBestFit, Is.False, "누적된 문장 때문에 글씨를 축소하지 않는다.");
        Assert.That(body.rectTransform.rect.height, Is.GreaterThan(mask.rectTransform.rect.height));
        Assert.That(body.rectTransform.anchoredPosition.y, Is.Zero, "최신 문장이 말풍선 아래에 남는다.");
        Assert.That(bubble.BubbleRect.sizeDelta.x, Is.EqualTo(rowSize.x).Within(.1f));
        Assert.That(bubble.BubbleRect.sizeDelta.y, Is.GreaterThan(rowSize.y));
        Vector3[] corners = new Vector3[4];
        bubble.BubbleRect.GetWorldCorners(corners);
        Rect safe = Screen.safeArea;
        foreach (Vector3 corner in corners)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, corner);
            Assert.That(screen.x, Is.InRange(safe.xMin, safe.xMax));
            Assert.That(screen.y, Is.InRange(safe.yMin, safe.yMax));
        }

        bubble.ShowLine(actor, "", "일반 컷씬 대사는 타이핑 전에 전체 문장 크기를 확보합니다.");
        Vector2 reservedSize = bubble.BubbleRect.sizeDelta;
        bubble.SetVisibleText("일반");
        yield return null;
        Assert.That(bubble.BubbleRect.sizeDelta, Is.EqualTo(reservedSize));
        Assert.That(mask.enabled, Is.False);
        Assert.That(body.resizeTextForBestFit, Is.True);
    }
}
