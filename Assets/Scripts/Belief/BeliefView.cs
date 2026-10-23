using UnityEngine;
using UnityEngine.UI;

public sealed class BeliefView : MonoBehaviour
{
    private static Sprite whiteSprite;
    private BeliefController controller;
    private EnemyUnit owner;
    private GameObject anchor;
    private GameObject healthAnchor;
    private Text phraseText;
    private Image gaugeFill;
    private Image gaugeBackground;
    private Image healthGaugeFill;

    public bool Initialize(BeliefController beliefController)
    {
        if (beliefController == null || beliefController.Profile == null)
            return false;

        controller = beliefController;
        owner = GetComponent<EnemyUnit>();
        BuildWorldSpaceView();
        BuildHealthView();
        controller.BeliefChanged += HandleBeliefChanged;
        controller.StateChanged += HandleStateChanged;
        Refresh();
        UpdateHealth(owner != null ? owner.CurrentHp : 0f, owner != null ? owner.MaxHp : 0f);
        return phraseText != null && gaugeFill != null && healthGaugeFill != null;
    }

    private void BuildWorldSpaceView()
    {
        anchor = new GameObject("BeliefUIAnchor", typeof(RectTransform));
        anchor.transform.SetParent(transform, false);
        anchor.transform.localPosition = Vector3.up * 1.8f;

        GameObject canvasObject = new GameObject("WorldSpaceBeliefUI", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(anchor.transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 20;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(300f, 110f);
        canvasRect.localScale = Vector3.one * 0.01f;

        Color bubbleColor = new Color(0.84f, 0.84f, 0.84f, 0.96f);
        GameObject panelObject = CreateImage("BeliefSpeechBubble", canvasRect, bubbleColor);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.08f, 0.12f);
        panelRect.anchorMax = new Vector2(0.92f, 0.68f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        GameObject textObject = new GameObject("BeliefPhrase", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(panelRect, false);
        phraseText = textObject.GetComponent<Text>();
        phraseText.font = GetBeliefFont();
        phraseText.fontSize = 19;
        phraseText.alignment = TextAnchor.MiddleCenter;
        phraseText.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        phraseText.horizontalOverflow = HorizontalWrapMode.Wrap;
        phraseText.verticalOverflow = VerticalWrapMode.Overflow;
        phraseText.resizeTextForBestFit = true;
        phraseText.resizeTextMinSize = 12;
        phraseText.resizeTextMaxSize = 19;
        Stretch(phraseText.rectTransform);
        phraseText.rectTransform.offsetMin = new Vector2(10f, 4f);
        phraseText.rectTransform.offsetMax = new Vector2(-10f, -4f);

        GameObject tailObject = CreateImage("BeliefSpeechBubbleTail", canvasRect, bubbleColor);
        RectTransform tailRect = tailObject.GetComponent<RectTransform>();
        tailRect.sizeDelta = new Vector2(18f, 18f);
        tailRect.anchorMin = new Vector2(0.5f, 0.12f);
        tailRect.anchorMax = new Vector2(0.5f, 0.12f);
        tailRect.anchoredPosition = new Vector2(0f, -5f);
        tailRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        tailRect.SetAsFirstSibling();

        GameObject gaugeObject = CreateImage("BeliefGaugeBackground", canvasRect, new Color(0.73f, 0.73f, 0.73f, 1f));
        gaugeBackground = gaugeObject.GetComponent<Image>();
        RectTransform gaugeRect = gaugeObject.GetComponent<RectTransform>();
        gaugeRect.anchorMin = new Vector2(0.08f, 0.84f);
        gaugeRect.anchorMax = new Vector2(0.92f, 0.94f);
        gaugeRect.offsetMin = Vector2.zero;
        gaugeRect.offsetMax = Vector2.zero;

        GameObject fillObject = CreateImage("BeliefGaugeFill", gaugeRect, new Color(0.25f, 0.78f, 0.64f, 1f));
        gaugeFill = fillObject.GetComponent<Image>();
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        Stretch(fillRect);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        ConfigureHorizontalFill(gaugeFill);

        Vector3 ownerScale = transform.lossyScale;
        anchor.transform.localScale = new Vector3(
            InverseScale(ownerScale.x),
            InverseScale(ownerScale.y),
            1f);
    }

    private void BuildHealthView()
    {
        healthAnchor = new GameObject("EnemyHealthUIAnchor", typeof(RectTransform));
        healthAnchor.transform.SetParent(transform, false);

        GameObject canvasObject = new GameObject("EnemyHealthWorldSpaceUI", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(healthAnchor.transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 19;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(220f, 32f);
        canvasRect.localScale = Vector3.one * 0.01f;

        GameObject panelObject = CreateImage("EnemyHealthPanel", canvasRect, new Color(0.84f, 0.84f, 0.84f, 0.94f));
        Stretch(panelObject.GetComponent<RectTransform>());

        GameObject gaugeObject = CreateImage("EnemyHealthGaugeBackground", canvasRect, new Color(0.72f, 0.72f, 0.72f, 1f));
        RectTransform gaugeRect = gaugeObject.GetComponent<RectTransform>();
        gaugeRect.anchorMin = new Vector2(0.16f, 0.32f);
        gaugeRect.anchorMax = new Vector2(0.84f, 0.68f);
        gaugeRect.offsetMin = Vector2.zero;
        gaugeRect.offsetMax = Vector2.zero;

        GameObject fillObject = CreateImage("EnemyHealthGaugeFill", gaugeRect, new Color(0.4f, 0.4f, 0.4f, 1f));
        healthGaugeFill = fillObject.GetComponent<Image>();
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        Stretch(fillRect);
        ConfigureHorizontalFill(healthGaugeFill);
    }

    private static void ConfigureHorizontalFill(Image image)
    {
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    public void UpdateHealth(float current, float max)
    {
        if (healthGaugeFill != null)
            healthGaugeFill.fillAmount = max <= 0f ? 0f : Mathf.Clamp01(current / max);
    }

    private static Font GetBeliefFont()
    {
        string[] installedFonts = Font.GetOSInstalledFontNames();
        string[] preferredFonts = { "Malgun Gothic", "맑은 고딕", "Noto Sans CJK KR", "Arial" };
        foreach (string preferredFont in preferredFonts)
        {
            if (System.Array.IndexOf(installedFonts, preferredFont) >= 0)
            {
                Font dynamicFont = Font.CreateDynamicFontFromOSFont(preferredFont, 19);
                if (dynamicFont != null)
                    return dynamicFont;
            }
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static GameObject CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        if (whiteSprite == null)
            whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        image.sprite = whiteSprite;
        image.color = color;
        return imageObject;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void HandleBeliefChanged(BeliefController source, float current, float max)
    {
        Refresh();
    }

    private void HandleStateChanged(BeliefController source, BeliefState state)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (controller == null || phraseText == null || gaugeFill == null)
            return;

        phraseText.text = controller.Profile.Phrase;
        bool vulnerable = controller.IsVulnerable;
        gaugeFill.fillAmount = vulnerable ? 0f : controller.NormalizedBelief;
        gaugeFill.color = vulnerable
            ? new Color(0.94f, 0.31f, 0.25f, 1f)
            : new Color(0.4f, 0.4f, 0.4f, 1f);
        gaugeBackground.color = vulnerable
            ? new Color(0.78f, 0.64f, 0.62f, 1f)
            : new Color(0.73f, 0.73f, 0.73f, 1f);
    }

    public void Hide()
    {
        if (anchor != null)
            anchor.SetActive(false);
        if (healthAnchor != null)
            healthAnchor.SetActive(false);
    }

    private void LateUpdate()
    {
        if (anchor == null || !anchor.activeSelf)
            return;

        if (owner != null && owner.BodyCollider != null)
        {
            anchor.transform.position = owner.BodyCollider.bounds.center + Vector3.up * (owner.BodyCollider.bounds.extents.y + 0.48f);
            healthAnchor.transform.position = new Vector3(
                owner.BodyCollider.bounds.center.x,
                owner.BodyCollider.bounds.min.y - 0.18f,
                owner.BodyCollider.bounds.center.z);
        }

        anchor.transform.rotation = Quaternion.identity;
        Vector3 lossy = transform.lossyScale;
        anchor.transform.localScale = new Vector3(
            InverseScale(lossy.x),
            InverseScale(lossy.y),
            1f);

        healthAnchor.transform.rotation = Quaternion.identity;
        healthAnchor.transform.localScale = new Vector3(
            InverseScale(lossy.x),
            InverseScale(lossy.y),
            1f);
    }

    private static float InverseScale(float scale)
    {
        return Mathf.Abs(scale) > 0.0001f ? 1f / scale : 1f;
    }

    private void OnDestroy()
    {
        if (controller != null)
        {
            controller.BeliefChanged -= HandleBeliefChanged;
            controller.StateChanged -= HandleStateChanged;
        }
    }
}
