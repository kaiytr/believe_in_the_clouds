using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scrolling end credits. The text is plain and easy to replace: one line per row, "# " starts a big title,
/// "## " a section heading, an empty line is a gap, anything else is a normal line.
/// </summary>
public sealed class EndingCredits : MonoBehaviour
{
    private const float TitleSize = 44f, HeadingSize = 30f, LineSize = 24f;
    private const float TitleHeight = 76f, HeadingHeight = 54f, LineHeight = 40f, GapHeight = 34f;

    private RectTransform content;
    private RectTransform viewport;
    private float totalHeight, scrolled;
    private bool running;

    public float ScrollSpeed = 55f;
    public bool IsRunning => running;
    public bool IsFinished { get; private set; }

    public static EndingCredits Build(Transform canvasRoot, string text)
    {
        var root = new GameObject("Credits roll", typeof(RectTransform));
        root.transform.SetParent(canvasRoot, false);
        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var credits = root.AddComponent<EndingCredits>();
        credits.viewport = rect;
        credits.Populate(text);
        return credits;
    }

    private void Populate(string text)
    {
        var contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(transform, false);
        content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = content.anchorMax = new Vector2(.5f, 0f);
        content.pivot = new Vector2(.5f, 1f);

        var rows = new List<(string text, float size, float height, Color color, bool gap)>();
        foreach (string raw in (text ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
        {
            string line = raw.TrimEnd();
            if (line.Length == 0) rows.Add((string.Empty, 0f, GapHeight, Color.white, true));
            else if (line.StartsWith("## ", StringComparison.Ordinal))
                rows.Add((line.Substring(3), HeadingSize, HeadingHeight, new Color(1f, .82f, .55f), false));
            else if (line.StartsWith("# ", StringComparison.Ordinal))
                rows.Add((line.Substring(2), TitleSize, TitleHeight, new Color(1f, .95f, .85f), false));
            else rows.Add((line, LineSize, LineHeight, new Color(1f, .97f, .92f), false));
        }

        Font font = CombatUiResources.KoreanFont() ?? UiTheme.GetFont();
        float y = 0f;
        foreach (var row in rows)
        {
            if (!row.gap)
            {
                Text label = UiTheme.CreateText("Line", content, row.text, Mathf.RoundToInt(row.size), row.color);
                label.font = font;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                // Dark edge keeps the text readable over the bright sunset.
                var outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(.1f, .04f, .12f, .85f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
                RectTransform lineRect = label.rectTransform;
                lineRect.anchorMin = lineRect.anchorMax = new Vector2(.5f, 1f);
                lineRect.pivot = new Vector2(.5f, 1f);
                lineRect.sizeDelta = new Vector2(900f, row.height);
                lineRect.anchoredPosition = new Vector2(0f, -y);
            }
            y += row.height;
        }
        totalHeight = y;
        content.sizeDelta = new Vector2(900f, totalHeight);
        content.anchoredPosition = Vector2.zero;   // the top edge starts just below the screen
    }

    public void Begin() { running = true; }

    private void Update()
    {
        if (!running || content == null) return;
        scrolled += ScrollSpeed * Time.deltaTime;
        content.anchoredPosition = new Vector2(0f, scrolled);
        // Done once the last line has left through the top of the screen.
        if (scrolled > totalHeight + viewport.rect.height + 20f)
        {
            running = false;
            IsFinished = true;
        }
    }
}
