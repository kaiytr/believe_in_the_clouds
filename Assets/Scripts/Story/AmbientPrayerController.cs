using System.Text;
using UnityEngine;

// Only the repeated apology is ambient. The initial manuscript belongs to CutsceneRunner.
public sealed class AmbientPrayerController : MonoBehaviour
{
    private const int ApologiesPerLine = 2;
    private FirstAltarStoryDefinition definition;
    private CutsceneBubbleView bubble;
    private int shown = -1;
    private float elapsed;
    private bool playing;
    private string fullText = "";
    private int phraseLength, totalCharacters;
    public bool IsApologyLoopReady { get; private set; }
    public bool IsLooping => playing;

    public void BeginLoop(FirstAltarStoryDefinition data, CutsceneCharacter actor)
    {
        StopPrayer();
        definition = data;
        if (bubble == null) bubble = gameObject.AddComponent<CutsceneBubbleView>();
        string line = data.apologyLoopText ?? "";
        if (line.Length == 0) return;
        phraseLength = line.Length;
        totalCharacters = phraseLength * FirstAltarStoryDefinition.ApologyRepetitions;
        var text = new StringBuilder();
        for (int i = 0; i < FirstAltarStoryDefinition.ApologyRepetitions; i++)
        {
            if (i > 0) text.Append(i % ApologiesPerLine == 0 ? '\n' : ' ');
            text.Append(line);
        }
        fullText = text.ToString();
        playing = true;
        bubble.ShowAccumulatingLine(actor);
    }

    private void Update()
    {
        if (!playing || UiModal.IsOpen || Time.timeScale <= 0f) return;
        elapsed += Time.deltaTime;
        int visible = Mathf.Min(totalCharacters, Mathf.FloorToInt(elapsed * definition.charactersPerSecond));
        if (shown != visible)
        {
            // Spaces and line breaks appear with the next phrase's first character, with no typing delay.
            int textLength = visible > 0 ? visible + (visible - 1) / phraseLength : 0;
            bubble.SetAccumulatedText(fullText.Substring(0, textLength));
            shown = visible;
        }
        if (visible >= phraseLength) IsApologyLoopReady = true;
        if (visible == totalCharacters) playing = false; // Keep the completed bubble and approach trigger ready.
    }

    public void StopPrayer()
    {
        playing = false; IsApologyLoopReady = false;
        fullText = ""; phraseLength = totalCharacters = 0;
        elapsed = 0; shown = -1;
        bubble?.Hide();
    }
    private void OnDisable() => StopPrayer();
}
