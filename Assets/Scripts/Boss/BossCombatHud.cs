using UnityEngine;
using UnityEngine.UI;

public sealed class BossCombatHud : MonoBehaviour
{
    [SerializeField] private BossGameplayHud gameplayHud;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Image hpFill;
    [SerializeField] private Image beliefFill;
    [SerializeField] private Image progressFill;
    [SerializeField] private Text title;
    [SerializeField] private Text phrase;
    [SerializeField] private Text state;
    [SerializeField] private Text hint;
    [SerializeField] private Text feedback;
    [SerializeField] private Text playerStatus;
    [SerializeField] private string preferredFont = "Malgun Gothic";
    private BossCombatController owner;
    private static Font koreanFont;
    private float previousHp, previousBelief, feedbackUntil;
    private void Awake() { if (canvas != null) canvas.gameObject.SetActive(false); }
    public bool IsConfigured => gameplayHud != null && gameplayHud.IsConfigured;
    public bool HasDebugUi => canvas != null && hpFill != null && beliefFill != null && progressFill != null &&
        title != null && phrase != null && state != null && hint != null && feedback != null && playerStatus != null;
    public bool DebugVisible { get; private set; }
    public Canvas DebugCanvas => canvas;
    public BossGameplayHud GameplayHud => gameplayHud;
    public bool HasKoreanFont => ResolveFont() != null;
    public Font KoreanFont => ResolveFont();
    public float TopInsetPixels => canvas != null && canvas.gameObject.activeInHierarchy ? 250f * canvas.scaleFactor : 0f;

    private Font ResolveFont()
    {
        if (koreanFont != null) return koreanFont;
        string[] available = Font.GetOSInstalledFontNames();
        string[] choices = { preferredFont, "맑은 고딕", "Noto Sans CJK KR", "Apple SD Gothic Neo" };
        foreach (string family in choices)
        {
            if (System.Array.IndexOf(available, family) < 0) continue;
            Font candidate = Font.CreateDynamicFontFromOSFont(family, 28);
            if (candidate != null && candidate.HasCharacter('너')) { koreanFont = candidate; return candidate; }
        }
        return null;
    }
    public void Initialize(BossCombatController boss)
    {
        owner = boss;
        Font font = ResolveFont();
        if (font == null) Debug.LogError("Boss HUD: install a Korean font or configure preferredFont.", this);
        if (canvas != null)
            foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) if (font != null) text.font = font;
        gameplayHud.Initialize(boss, font);
        previousHp = boss.CurrentHp;
        previousBelief = boss.CurrentBelief;
        SetDebugVisible(DebugVisible);
    }
    public void SetDebugVisible(bool visible)
    {
        DebugVisible = visible && HasDebugUi;
        if (canvas != null) canvas.gameObject.SetActive(owner != null && DebugVisible);
    }
    private void LateUpdate()
    {
        if (owner == null || !DebugVisible || !HasDebugUi) return;
        if (owner.CurrentHp < previousHp || owner.CurrentBelief < previousBelief) feedbackUntil = Time.time + 0.25f;
        previousHp = owner.CurrentHp; previousBelief = owner.CurrentBelief;
        hpFill.fillAmount = owner.MaxHp > 0f ? owner.CurrentHp / owner.MaxHp : 0f;
        beliefFill.fillAmount = owner.NormalizedBelief;
        beliefFill.color = feedbackUntil > Time.time ? Color.white : owner.IsVulnerable ? new Color(0.35f, 0.65f, 1f) : new Color(0.9f, 0.75f, 0.35f);
        title.text = $"흰 예복의 신봉자  ·  {(owner.Phase == BossPhase.One ? "1" : "2")}페이즈     HP {owner.CurrentHp:0}/{owner.MaxHp:0}     믿음 {owner.CurrentBelief:0}/{owner.MaxBelief:0}";
        phrase.text = owner.ActiveBeliefPhrase;
        progressFill.fillAmount = owner.IsVulnerable ? owner.VulnerableRemaining / owner.Settings.VulnerableDuration :
            owner.HasCueTimer ? 1f - owner.CueProgress : 0f;
        Color stageColor = owner.IsVulnerable ? new Color(.45f, .78f, 1f) :
            owner.CueState == BossPresentationState.Windup || owner.CueState == BossPresentationState.Active ?
            new Color(1f, .65f, .46f) : new Color(.86f, .94f, 1f);
        progressFill.color = stageColor; state.color = stageColor;
        string stage = owner.CueState == BossPresentationState.Windup ? "예고" :
            owner.CueState == BossPresentationState.Active ? "발동 중" :
            owner.CueState == BossPresentationState.Stare ? "남은 시간" :
            owner.CueState == BossPresentationState.Charging ? "준비" : "";
        state.text = owner.IsDead ? "보스 격파" : !owner.EncounterStarted ? "전투 대기 / 종료" :
            owner.IsVulnerable ? $"믿음 붕괴 · 취약 {owner.VulnerableRemaining:0.0}초" :
            owner.CueTitle + (stage.Length > 0 ? " · " + stage : "") + (owner.HasCueTimer ? $" {owner.CueRemaining:0.0}초" : "");
        hint.text = owner.IsDead ? "R 키로 다시 도전할 수 있습니다." : owner.IsVulnerable ?
            $"지금 공격하세요 · 받는 피해 ×{owner.Settings.VulnerableDamageMultiplier:0.#}" : owner.CueHint;
        feedback.text = owner.Feedback; feedback.color = owner.FeedbackColor;
        if (owner.IsBurning) phrase.text += $"   [화상 강화 {owner.BurnRemaining:0.0}초]";
        PlayerHealth player = owner.PlayerHealth;
        BossBurnEffect burn = player != null ? player.GetComponent<BossBurnEffect>() : null;
        playerStatus.text = player == null ? "" : player.IsDead ? "플레이어 쓰러짐 · R 재시작" :
            $"플레이어 HP {player.CurrentHp:0} / {player.MaxHp:0}" + (burn != null && burn.IsBurning ? $"    화상 {burn.Remaining:0.0}초" : "");
        playerStatus.color = burn != null && burn.IsBurning ? new Color(1f, .65f, .4f) : Color.white;
    }
    private void OnDisable() { SetDebugVisible(false); }
}
