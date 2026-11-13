using UnityEngine;
using UnityEngine.UI;

/// <summary>Boss data binding; screen health and world belief use the shared artwork and view.</summary>
public sealed class BossGameplayHud : MonoBehaviour
{
    [SerializeField] private CombatantHudView worldView;
    [SerializeField] private Vector2 healthBarSize = new Vector2(960f, 48f);
    [SerializeField, Min(0f)] private float healthBarTopMargin = 76f;
    [SerializeField] private string bossName = "보스 · 잠긴 낙원의 주인";
    [SerializeField] private Font bossNameFont;
    private BossCombatController owner;
    private Canvas screenHealthCanvas;
    private RectTransform healthBar;
    private Text bossNameLabel;
    public bool IsConfigured => worldView != null && worldView.IsConfigured;
    public CombatantHudView WorldView => worldView;
    public Canvas BeliefCanvas => worldView.BeliefCanvas;
    public Canvas HealthCanvas => worldView.HealthCanvas;
    private void Awake() => SetVisible(false);
    public void Initialize(BossCombatController boss, Font font)
    {
        owner = boss;
        worldView.FullSizeBar = true;
        worldView.Initialize(font);
        ConfigureScreenHealth();
        Refresh();
    }

    private void ConfigureScreenHealth()
    {
        screenHealthCanvas = worldView.HealthCanvas;
        screenHealthCanvas.transform.SetParent(null, false);
        screenHealthCanvas.name = "Boss health HUD";
        screenHealthCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        screenHealthCanvas.worldCamera = null;
        screenHealthCanvas.sortingOrder = 100;
        screenHealthCanvas.pixelPerfect = true;
        screenHealthCanvas.transform.localScale = Vector3.one;
        screenHealthCanvas.transform.rotation = Quaternion.identity;

        CanvasScaler scaler = screenHealthCanvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = screenHealthCanvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        healthBar = worldView.HealthFill.transform.parent.GetComponent<RectTransform>();
        healthBar.anchorMin = healthBar.anchorMax = new Vector2(.5f, 1f);
        healthBar.pivot = new Vector2(.5f, 1f);
        ConfigureBossName();
        PositionScreenHealth();
    }

    private void ConfigureBossName()
    {
        if (bossNameLabel == null)
        {
            var label = new GameObject("Boss name", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(healthBar, false);
            bossNameLabel = label.GetComponent<Text>();
        }
        bossNameLabel.text = bossName;
        bossNameLabel.font = bossNameFont != null ? bossNameFont : CombatUiResources.KoreanFont();
        bossNameLabel.fontSize = 28;
        bossNameLabel.color = Color.white;
        bossNameLabel.alignment = TextAnchor.MiddleCenter;
        bossNameLabel.raycastTarget = false;
        bossNameLabel.supportRichText = false;
        bossNameLabel.resizeTextForBestFit = true;
        bossNameLabel.resizeTextMinSize = 18;
        bossNameLabel.resizeTextMaxSize = 28;
        RectTransform rect = bossNameLabel.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 8f);
        rect.sizeDelta = new Vector2(0f, 36f);
    }

    private void PositionScreenHealth()
    {
        float canvasWidth = screenHealthCanvas.pixelRect.width / Mathf.Max(.0001f, screenHealthCanvas.scaleFactor);
        float width = Mathf.Min(healthBarSize.x, Mathf.Max(1f, canvasWidth - 48f));
        healthBar.anchoredPosition = new Vector2(0f, -healthBarTopMargin);
        healthBar.sizeDelta = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, healthBarSize.y));
    }
    private void LateUpdate() { if (owner != null && IsConfigured) Refresh(); }
    private void Refresh()
    {
        bool fighting = owner.isActiveAndEnabled && owner.EncounterStarted && !owner.IsDead;
        bool gameplay = !owner.DebugUiVisible;
        worldView.SetVisible(fighting && gameplay);
        worldView.UpdateHealth(owner.CurrentHp, owner.MaxHp);
        // 믿음 붕괴 중(빈 문구)과 2페이즈 대사가 머리 위에 있는 동안에는 믿음 말풍선을 숨긴다. 끝나면 다시 나온다.
        worldView.UpdateBelief(owner.NormalizedBelief, owner.IsVulnerable,
            owner.IsVulnerable ? string.Empty : owner.ActiveBeliefPhrase, owner.IsPhaseCutscenePlaying);
        PositionScreenHealth();
        if (fighting && gameplay) worldView.Position(owner.BodyCollider.bounds);
    }
    private void SetVisible(bool visible)
    {
        if (worldView != null) worldView.SetVisible(visible);
    }
    private void OnDisable() => SetVisible(false);
    private void OnDestroy()
    {
        if (screenHealthCanvas != null) Destroy(screenHealthCanvas.gameObject);
    }
}
