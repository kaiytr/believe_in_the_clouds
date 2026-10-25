using UnityEngine;
using UnityEngine.UI;

// 상점 패널(프로토타입).
// 표시만 담당한다: 구매 판정/재화/교리 목록 변경은 여기서 하지 않는다.
public class ShopPanelView : MonoBehaviour
{
    private Canvas canvas;
    private GameObject content;
    private Text titleText;
    private Text bodyText;
    private bool isOpen;

    public bool IsOpen => isOpen;

    public static ShopPanelView BuildRuntime()
    {
        GameObject root = new GameObject("[ShopPanel]");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 닫힘 상태에서는 content만 끄고, Canvas/컴포넌트는 살려 둔다.
        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(root.transform, false);
        UiTheme.Stretch(content.GetComponent<RectTransform>());

        GameObject blocker = UiTheme.CreateImage("Blocker", content.transform,
            new Color(0f, 0f, 0f, 0.45f));
        UiTheme.Stretch(blocker.GetComponent<RectTransform>());
        blocker.GetComponent<Image>().raycastTarget = true;

        GameObject panel = UiTheme.CreateImage("Panel", content.transform,
            new Color(0.93f, 0.93f, 0.94f, 1f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(900f, 560f);

        Text title = UiTheme.CreateText("Title", panel.transform, "상점", 34,
            UiTheme.TextDark, TextAnchor.MiddleCenter);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -28f);
        titleRect.sizeDelta = new Vector2(0f, 56f);

        Text body = UiTheme.CreateText("Body", panel.transform,
            "교리 구매 목록은 다음 단계에서 추가됩니다.\n닫기: E 또는 ESC",
            22, UiTheme.TextMuted, TextAnchor.MiddleCenter);
        UiTheme.Stretch(body.rectTransform, 40f);

        ShopPanelView view = root.AddComponent<ShopPanelView>();
        view.canvas = canvas;
        view.content = content;
        view.titleText = title;
        view.bodyText = body;
        content.SetActive(false);
        return view;
    }

    public void Open(string title)
    {
        isOpen = true;
        if (titleText != null)
        {
            titleText.font = UiTheme.GetFont();
            titleText.text = string.IsNullOrEmpty(title) ? "상점" : title;
        }

        if (bodyText != null)
        {
            bodyText.font = UiTheme.GetFont();
        }

        if (content != null)
        {
            content.SetActive(true);
        }

        // 상점이 열린 동안은 커서를 풀어 UI를 다룰 수 있게 한다.
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Close()
    {
        isOpen = false;
        if (content != null)
        {
            content.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (canvas != null && canvas.gameObject != null)
        {
            Destroy(canvas.gameObject);
        }
    }
}
