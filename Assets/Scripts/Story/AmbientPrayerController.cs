using UnityEngine;

// Ambient speech never owns a modal token or waits for a click.
public sealed class AmbientPrayerController : MonoBehaviour
{
    private FirstAltarStoryDefinition definition;
    private CutsceneCharacter speaker;
    private CutsceneBubbleView bubble;
    private int cue, shown = -1;
    private float elapsed;
    private bool playing, looping;
    private string line;
    public bool IsApologyLoopReady { get; private set; }
    public bool IsLooping => looping && playing;

    public void Begin(FirstAltarStoryDefinition data, CutsceneCharacter actor)
    {
        StopPrayer();
        definition = data; speaker = actor;
        if (bubble == null) bubble = gameObject.AddComponent<CutsceneBubbleView>();
        cue = 0; looping = false; playing = true;
        SetLine(data.introCues[0].text);
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
        if (shown != visible || definition.bubbleGlitch && looping)
        {
            string text = line.Substring(0, visible);
            // The unchanged full line determines layout, so the bubble never jitters in size.
            if (definition.bubbleGlitch && looping && visible > 2 && Time.frameCount % 41 < 2)
                text = text.Substring(0, visible - 1) + "…";
            bubble.SetVisibleText(text); shown = visible;
        }
        if (looping && visible == line.Length) IsApologyLoopReady = true;
        float hold = looping ? definition.apologySeconds : definition.introCues[cue].holdSeconds;
        if (elapsed < line.Length / definition.charactersPerSecond + hold) return;
        if (looping) SetLine(definition.apologyLoopText);
        else if (++cue < definition.introCues.Length) SetLine(definition.introCues[cue].text);
        else { looping = true; SetLine(definition.apologyLoopText); }
    }

    public void StopPrayer()
    {
        playing = false; looping = false; IsApologyLoopReady = false;
        bubble?.Hide();
    }
    private void OnDisable() => StopPrayer();
}
