using UnityEngine;
using UnityEngine.UI;

// 상호작용 안내 문구.
// 기본 HUD 안내 또는 대상 월드 위치를 따라가는 안내를 표시한다.
// 표시 전용이며 어떤 게임 규칙도 바꾸지 않는다.
[DefaultExecutionOrder(1000)]
public class InteractionPrompt : MonoBehaviour
{
    private GameObject pill;
    private Text label;
    private bool visible;
    private GameObject keycap;
    private Text keyLabel;
    private SquareHoldProgressGraphic holdProgress;
    private RectTransform pillRect, canvasRect;
    private Canvas canvas;
    private Transform followTarget;
    private Vector3 followOffset;
    private Vector2 dockedOffset;
    private bool worldAnchored;

    public bool IsVisible => pill != null && pill.activeSelf;

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
        prompt.pillRect = pillRect;
        prompt.canvasRect = root.GetComponent<RectTransform>();
        prompt.canvas = canvas;
        prompt.dockedOffset = offset;
        GameObject keycap = UiTheme.CreateImage("HoldKey", pill.transform, new Color(.06f, .07f, .09f));
        RectTransform keyRect = keycap.GetComponent<RectTransform>();
        keyRect.anchorMin = keyRect.anchorMax = new Vector2(0, .5f);
        keyRect.anchoredPosition = new Vector2(30, 0); keyRect.sizeDelta = new Vector2(44, 44);
        var baseLine = new GameObject("KeyBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(SquareHoldProgressGraphic));
        baseLine.transform.SetParent(keycap.transform, false); UiTheme.Stretch(baseLine.GetComponent<RectTransform>(), 0);
        var baseGraphic = baseLine.GetComponent<SquareHoldProgressGraphic>();
        baseGraphic.color = new Color(.35f, .38f, .43f); baseGraphic.Thickness = 2; baseGraphic.Progress = 1; baseGraphic.raycastTarget = false;
        var progressRoot = new GameObject("HoldProgress", typeof(RectTransform), typeof(CanvasRenderer), typeof(SquareHoldProgressGraphic));
        progressRoot.transform.SetParent(keycap.transform, false); UiTheme.Stretch(progressRoot.GetComponent<RectTransform>(), 0);
        prompt.holdProgress = progressRoot.GetComponent<SquareHoldProgressGraphic>();
        prompt.holdProgress.color = new Color(1, .9f, .6f); prompt.holdProgress.raycastTarget = false;
        prompt.keyLabel = UiTheme.CreateText("KeyLabel", keycap.transform, "E", 24, Color.white);
        UiTheme.Stretch(prompt.keyLabel.rectTransform, 5);
        prompt.keycap = keycap; keycap.SetActive(false);
        pill.SetActive(false);
        return prompt;
    }

    public void SetMessage(string message)
    {
        SetHoldMode(false);
        if (label == null || label.text == message)
        {
            return;
        }

        label.font = UiTheme.GetFont();
        label.text = message;
        // Branch E/F instructions can be longer than a single action label.
        // Fit the content while preserving the target's bottom-center anchor.
        pillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Clamp(label.preferredWidth + 12f, 340f, 640f));
        pillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(56f, label.preferredHeight + 12f));
    }

    public void SetHoldMessage(string key, string message, float progress)
    {
        pillRect.sizeDelta = new Vector2(340f, 56f);
        SetHoldMode(true);
        keyLabel.text = key; label.text = message; holdProgress.Progress = progress;
    }

    private void SetHoldMode(bool enabled)
    {
        if (keycap == null) return;
        keycap.SetActive(enabled);
        label.rectTransform.offsetMin = new Vector2(enabled ? 62 : 6, 6);
        label.rectTransform.offsetMax = new Vector2(-6, -6);
        if (!enabled) holdProgress.Progress = 0;
    }

    public void SetVisible(bool value)
    {
        if (!value && holdProgress != null) holdProgress.Progress = 0;
        visible = value;
        RefreshPosition();
    }

    public void Follow(Transform target, Vector3 localOffset)
    {
        worldAnchored = true; followTarget = target; followOffset = localOffset;
        pillRect.anchorMin = pillRect.anchorMax = new Vector2(.5f, .5f);
        RefreshPosition();
    }

    public void Dock()
    {
        worldAnchored = false; followTarget = null;
        pillRect.anchorMin = pillRect.anchorMax = new Vector2(.5f, 0);
        pillRect.anchoredPosition = dockedOffset;
        RefreshPosition();
    }

    // Run after camera follow, including when input is paused or manually driven.
    private void LateUpdate() => RefreshPosition();

    private void RefreshPosition()
    {
        if (pill == null) return;
        bool show = visible;
        if (show && worldAnchored)
        {
            Camera camera = Camera.main;
            show = followTarget != null && followTarget.gameObject.activeInHierarchy && camera != null;
            if (show)
            {
                Vector3 position = followTarget.TransformPoint(followOffset);
                Vector3 viewport = camera.WorldToViewportPoint(position);
                show = viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1;
                if (show)
                {
                    Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    show = RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                        camera.WorldToScreenPoint(position), uiCamera, out Vector2 local);
                    if (show) pillRect.anchoredPosition = local;
                }
            }
        }
        if (pill.activeSelf != show) pill.SetActive(show);
    }
}
