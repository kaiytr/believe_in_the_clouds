using UnityEngine;
using UnityEngine.UI;

// 상호작용 안내 문구.
// 하단 HUD 밴드 위에 "E : 상점 열기" 같은 작은 알약(pill)을 띄운다.
// 표시 전용이며 어떤 게임 규칙도 바꾸지 않는다.
public class InteractionPrompt : MonoBehaviour
{
    private GameObject pill;
    private Text label;
    private bool visible;

    public bool IsVisible => visible;

    public static InteractionPrompt BuildRuntime(Vector2 offset)
    {
        GameObject root = new GameObject("[InteractionPrompt]");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject pill = UiTheme.CreateImage("PromptPill", root.transform,
            new Color(0.10f, 0.11f, 0.13f, 0.82f));
        RectTransform pillRect = pill.GetComponent<RectTransform>();
        pillRect.anchorMin = new Vector2(0.5f, 0f);
        pillRect.anchorMax = new Vector2(0.5f, 0f);
        pillRect.pivot = new Vector2(0.5f, 0f);
        pillRect.anchoredPosition = offset;
        pillRect.sizeDelta = new Vector2(340f, 56f);

        Text label = UiTheme.CreateText("PromptLabel", pill.transform, string.Empty,
            20, Color.white, TextAnchor.MiddleCenter);
        UiTheme.Stretch(label.rectTransform, 6f);

        InteractionPrompt prompt = root.AddComponent<InteractionPrompt>();
        prompt.pill = pill;
        prompt.label = label;
        pill.SetActive(false);
        return prompt;
    }

    public void SetMessage(string message)
    {
        if (label == null || label.text == message)
        {
            return;
        }

        label.font = UiTheme.GetFont();
        label.text = message;
    }

    public void SetVisible(bool value)
    {
        if (visible == value)
        {
            return;
        }

        visible = value;
        if (pill != null)
        {
            pill.SetActive(value);
        }
    }
}
