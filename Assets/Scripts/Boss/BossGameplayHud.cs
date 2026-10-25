using UnityEngine;
using UnityEngine.UI;

/// <summary>Boss data binding; health, belief, speech and layout use the shared view.</summary>
public sealed class BossGameplayHud : MonoBehaviour
{
    [SerializeField] private CombatantHudView worldView;
    [SerializeField] private Canvas playerCanvas;
    [SerializeField] private Image playerHealthFill;
    [SerializeField] private Text playerState;
    private BossCombatController owner;
    public bool IsConfigured => worldView != null && worldView.IsConfigured && playerCanvas != null &&
        playerHealthFill != null && playerState != null;
    public CombatantHudView WorldView => worldView;
    public Canvas BeliefCanvas => worldView.BeliefCanvas;
    public Canvas HealthCanvas => worldView.HealthCanvas;
    public Canvas PlayerCanvas => playerCanvas;
    private void Awake() => SetVisible(false);
    public void Initialize(BossCombatController boss, Font font)
    {
        owner = boss;
        worldView.Initialize(font);
        foreach (Text text in playerCanvas.GetComponentsInChildren<Text>(true)) text.font = font;
        Refresh();
    }
    private void LateUpdate() { if (owner != null && IsConfigured) Refresh(); }
    private void Refresh()
    {
        bool fighting = owner.isActiveAndEnabled && owner.EncounterStarted && !owner.IsDead;
        bool gameplay = !owner.DebugUiVisible;
        worldView.SetVisible(fighting && gameplay);
        PlayerHealth player = owner.PlayerHealth;
        playerCanvas.enabled = gameplay && owner.isActiveAndEnabled && !owner.IsDead && (fighting || player != null && player.IsDead);
        worldView.UpdateHealth(owner.CurrentHp, owner.MaxHp);
        worldView.UpdateBelief(owner.NormalizedBelief, owner.IsVulnerable, owner.IsVulnerable ? "방어력 저하" : owner.ActiveBeliefPhrase);
        if (player != null)
        {
            playerHealthFill.fillAmount = player.MaxHp > 0f ? player.CurrentHp / player.MaxHp : 0f;
            BossBurnEffect burn = player.GetComponent<BossBurnEffect>();
            playerState.text = player.IsDead ? "쓰러짐" : burn != null && burn.IsBurning ? "화상" : "";
        }
        if (fighting && gameplay) worldView.Position(owner.BodyCollider.bounds);
    }
    private void SetVisible(bool visible)
    {
        if (worldView != null) worldView.SetVisible(visible);
        if (playerCanvas != null) playerCanvas.enabled = visible;
    }
    private void OnDisable() => SetVisible(false);
}
