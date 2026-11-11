using UnityEngine;

// Only the repeated apology is ambient. The initial manuscript belongs to CutsceneRunner.
public sealed class AmbientPrayerController : MonoBehaviour
{
    private FirstAltarStoryDefinition definition;
    private CutsceneCharacter speaker;
    private CutsceneBubbleView bubble;
    private int shown = -1;
    private float elapsed;
    private bool playing;
    private string line;
    public bool IsApologyLoopReady { get; private set; }
    public bool IsLooping => playing;

    public void BeginLoop(FirstAltarStoryDefinition data, CutsceneCharacter actor)
    {
        StopPrayer();
        definition = data; speaker = actor;
        if (bubble == null) bubble = gameObject.AddComponent<CutsceneBubbleView>();
        playing = true;
        SetLine(data.apologyLoopText);
    }

    private void SetLine(string text)
    {
        line = text ?? ""; elapsed = 0; shown = -1;
        if (line.Length == 0) bubble.Hide();
        else bubble.ShowLine(speaker, "", line);
    }

    private void Update()
    {
        if (!playing || UiModal.IsOpen || Time.timeScale <= 0f) return;
        elapsed += Time.deltaTime;
        int visible = Mathf.Min(line.Length, Mathf.FloorToInt(elapsed * definition.charactersPerSecond));
        if (shown != visible || definition.bubbleGlitch)
        {
            string text = line.Substring(0, visible);
            // The unchanged full line determines layout, so the bubble never jitters in size.
            if (definition.bubbleGlitch && visible > 2 && Time.frameCount % 41 < 2)
                text = text.Substring(0, visible - 1) + "…";
            bubble.SetVisibleText(text); shown = visible;
        }
        if (visible == line.Length) IsApologyLoopReady = true;
        if (elapsed >= line.Length / definition.charactersPerSecond + definition.apologySeconds)
            SetLine(definition.apologyLoopText);
    }

    public void StopPrayer()
    {
        playing = false; IsApologyLoopReady = false;
        bubble?.Hide();
    }
    private void OnDisable() => StopPrayer();
}
