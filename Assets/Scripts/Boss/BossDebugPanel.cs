using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Pixel-sized, wrapping debug controls with a clipped scroll area.</summary>
public sealed class BossDebugPanel : MonoBehaviour
{
    private BossTestBootstrap tools;
    private BossCombatController boss;
    private BossCombatHud combatHud;
    private Canvas canvas;
    private RectTransform canvasRect, panel;
    private Font font;
    private Text automaticLabel, status, latest, diagnostics, detailsLabel, manualLabel;
    private Button[] comboButtons;
    private GameObject manualButton;
    private bool showDetails;
    private readonly StringBuilder details = new StringBuilder();
    public Canvas Canvas => canvas;

    public void Initialize(BossTestBootstrap testTools, BossCombatController controller)
    {
        tools = testTools; boss = controller;
        combatHud = boss.GetComponent<BossCombatHud>();
        font = combatHud.KoreanFont;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        canvasRect = Rect("Boss debug UI", transform);
        canvas = canvasRect.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasRect.gameObject.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null)
        {
            var input = new GameObject("BossTest UI input", typeof(EventSystem), typeof(StandaloneInputModule));
            input.transform.SetParent(transform, false);
        }

        panel = Image("Debug panel", canvasRect, new Color(.035f, .045f, .07f, .97f)).rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        var heading = Label("Panel heading", panel, "보스 테스트", 16, Color.white);
        Anchor(heading.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -34f), new Vector2(-76f, -6f));
        heading.alignment = TextAnchor.MiddleLeft;
        var close = Button(panel, "F1 닫기", () => tools.ShowDebugPanel = false);
        Object.Destroy(close.GetComponent<LayoutElement>());
        Anchor(close.GetComponent<RectTransform>(), Vector2.one, Vector2.one, new Vector2(-72f, -32f), new Vector2(-8f, -6f));

        var viewport = Image("Scroll viewport", panel, Color.clear).rectTransform;
        Anchor(viewport, Vector2.zero, Vector2.one, new Vector2(0f, 8f), new Vector2(-14f, -40f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Controls", viewport);
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1f); content.offsetMin = content.offsetMax = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 8, 4, 12); layout.spacing = 8f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28f;
        BuildScrollbar(scroll);

        automaticLabel = Button(content, "", tools.ToggleAutomatic).GetComponentInChildren<Text>();
        status = Label("Pattern status", content, "", 14, new Color(.9f, .93f, 1f));
        latest = Label("Latest signal", content, "", 13, new Color(.68f, .75f, .85f));
        Section(content, "패턴 1회 실행");
        string[] names = { "1 근접", "2 돌진", "3 투사체", "4 응시 P1", "5 구름", "6 화상", "7 응시 P2", "8 공격 금지", "9 충격파" };
        for (int rowIndex = 0; rowIndex < 3; rowIndex++)
        {
            Transform row = Row(content);
            for (int column = 0; column < 3; column++)
            {
                int pattern = rowIndex * 3 + column;
                Button(row, names[pattern], () => boss.ForcePatternForTest((BossPatternKind)pattern));
            }
        }
        Section(content, "콤보 타수");
        Transform combos = Row(content);
        comboButtons = new[] {
            Button(combos, "무작위", () => boss.ForcedComboHits = 0),
            Button(combos, "2타", () => boss.ForcedComboHits = 2),
            Button(combos, "3타", () => boss.ForcedComboHits = 3)
        };
        Transform health = Row(content);
        Button(health, "HP -50", () => boss.SetHpForTest(boss.CurrentHp - 50f));
        Button(health, "HP 회복", () => boss.SetHpForTest(boss.MaxHp));
        Button(health, "B 믿음 -20", () => boss.ReduceBeliefForTest(20f));
        Transform signals = Row(content);
        Button(signals, "J 시도", () => tools.PublishTestAttack(false));
        Button(signals, "K 적중", () => tools.PublishTestAttack(true));
        Button(signals, "P 전환", boss.ForcePhaseTwoForTest);
        Button(signals, "R 초기화", tools.RestartForTest);
        detailsLabel = Button(content, "공격 진단 펼치기", ToggleDetails).GetComponentInChildren<Text>();
        diagnostics = Label("Attack diagnostics", content, "", 13, new Color(.75f, .81f, .9f));
        diagnostics.gameObject.SetActive(false);
        var manual = Button(content, "", () => boss.AllowManualEvadeInjection = !boss.AllowManualEvadeInjection);
        manualButton = manual.gameObject; manualLabel = manual.GetComponentInChildren<Text>();
        manualButton.SetActive(false);
        Label("Input help", content, "이동 A/D · 점프 Space · F1 패널 숨기기\nJ/K는 더미 공격 입력입니다.", 13, new Color(.62f, .7f, .81f));
        RefreshLayout();
    }

    private void ToggleDetails()
    {
        showDetails = !showDetails;
        diagnostics.gameObject.SetActive(showDetails);
        manualButton.SetActive(showDetails);
        detailsLabel.text = showDetails ? "공격 진단 접기" : "공격 진단 펼치기";
    }
    public void SetVisible(bool visible)
    {
        if (canvas != null) canvas.gameObject.SetActive(visible);
        if (combatHud != null) combatHud.SetDebugVisible(visible);
    }

    public void RefreshLayout()
    {
        if (canvasRect == null || panel == null) return;
        float width = canvasRect.rect.width, height = canvasRect.rect.height;
        float top = combatHud.TopInsetPixels + 12f;
        panel.anchoredPosition = new Vector2(-12f, -top);
        panel.sizeDelta = new Vector2(Mathf.Min(Mathf.Clamp(width * .3f, 300f, 360f), width - 24f),
            Mathf.Clamp(height - top - 52f, 80f, 610f));
    }
    private void LateUpdate()
    {
        if (canvas == null || tools == null || boss == null) return;
        SetVisible(tools.isActiveAndEnabled && tools.ShowDebugPanel);
        if (!canvas.gameObject.activeInHierarchy) return;
        RefreshLayout();
        automaticLabel.text = boss.AutomaticPatterns ? "T 자동 선택 중지 / 공격 취소" : "T 자동 선택 시작";
        status.text = "패턴: " + boss.ActivePatternName + "\n" +
            (boss.StatusMessage.StartsWith("패턴:") ? "단계: " + boss.CueTitle : boss.StatusMessage);
        latest.text = tools.LatestSignal;
        for (int i = 0; i < comboButtons.Length; i++)
            comboButtons[i].image.color = boss.ForcedComboHits == (i == 0 ? 0 : i + 1) ? new Color(.22f, .4f, .57f) : new Color(.14f, .18f, .25f);
        if (!showDetails) return;
        details.Clear().Append("세대 ").Append(boss.Generation).Append(" / 패턴 ").Append(boss.PatternId);
        foreach (var record in boss.ActiveAttacks)
            details.Append("\n#").Append(record.Id).Append(' ').Append(record.Kind)
                .Append(record.Activated ? " 활성" : " 예고").Append(" / 위협 ").Append(record.WasThreatened ? "있음" : "없음")
                .Append(" / 접촉 ").Append(record.Contacted ? "있음" : "없음");
        if (boss.AttackHistory.Count > 0)
        {
            var last = boss.AttackHistory[boss.AttackHistory.Count - 1];
            details.Append("\n최근 #").Append(last.Id).Append(' ').Append(last.Outcome)
                .Append("\n종료: ").Append(last.EndReason).Append(" / 믿음: ").Append(last.Belief);
        }
        diagnostics.text = details.ToString();
        manualLabel.text = boss.AllowManualEvadeInjection ? "L 수동 회피 진단: 켜짐" : "L 수동 회피 진단: 꺼짐";
    }
    private void OnDisable() { SetVisible(false); }

    private void BuildScrollbar(ScrollRect scroll)
    {
        var track = Image("Scroll track", panel, new Color(.1f, .13f, .18f));
        Anchor(track.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-10f, 10f), new Vector2(-4f, -42f));
        var handle = Image("Scroll handle", track.transform, new Color(.34f, .42f, .54f));
        Stretch(handle.rectTransform);
        var bar = track.gameObject.AddComponent<Scrollbar>();
        bar.handleRect = handle.rectTransform; bar.targetGraphic = handle;
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.navigation = new Navigation { mode = Navigation.Mode.None };
        scroll.verticalScrollbar = bar;
    }
    private Transform Row(Transform parent)
    {
        var row = Rect("Button row", parent);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f; layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
        return row;
    }
    private void Section(Transform parent, string text)
    { Label(text, parent, text, 13, new Color(.65f, .76f, .9f)).fontStyle = FontStyle.Bold; }
    private Button Button(Transform parent, string text, UnityEngine.Events.UnityAction action)
    {
        var image = Image(text + " button", parent, new Color(.14f, .18f, .25f));
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image; button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(action);
        var height = image.gameObject.AddComponent<LayoutElement>();
        height.minHeight = height.preferredHeight = 34f; height.flexibleWidth = 1f;
        var label = Label("Label", image.transform, text, 13, Color.white);
        Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(4f, 2f); label.rectTransform.offsetMax = new Vector2(-4f, -2f);
        label.alignment = TextAnchor.MiddleCenter;
        return button;
    }
    private Text Label(string name, Transform parent, string value, int size, Color color)
    {
        var rect = Rect(name, parent);
        var label = rect.gameObject.AddComponent<Text>();
        label.font = font; label.fontSize = size; label.lineSpacing = 1.15f;
        label.color = color; label.text = value; label.alignment = TextAnchor.UpperLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;
        return label;
    }
    private static Image Image(string name, Transform parent, Color color)
    { var rect = Rect(name, parent); var image = rect.gameObject.AddComponent<Image>(); image.color = color; return image; }
    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); return rect;
    }
    private static void Stretch(RectTransform rect) => Anchor(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = low; rect.offsetMax = high; }
}
