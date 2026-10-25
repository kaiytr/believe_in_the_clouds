using UnityEngine;

/// <summary>Boss data binding; health, belief, speech and layout use the shared view.</summary>
public sealed class BossGameplayHud : MonoBehaviour
{
    [SerializeField] private CombatantHudView worldView;
    private BossCombatController owner;
    public bool IsConfigured => worldView != null && worldView.IsConfigured;
    public CombatantHudView WorldView => worldView;
    public Canvas BeliefCanvas => worldView.BeliefCanvas;
    public Canvas HealthCanvas => worldView.HealthCanvas;
    private void Awake() => SetVisible(false);
    public void Initialize(BossCombatController boss, Font font)
    {
        owner = boss;
        worldView.Initialize(font);
        Refresh();
    }
    private void LateUpdate() { if (owner != null && IsConfigured) Refresh(); }
    private void Refresh()
    {
        bool fighting = owner.isActiveAndEnabled && owner.EncounterStarted && !owner.IsDead;
        bool gameplay = !owner.DebugUiVisible;
        worldView.SetVisible(fighting && gameplay);
        worldView.UpdateHealth(owner.CurrentHp, owner.MaxHp);
        worldView.UpdateBelief(owner.NormalizedBelief, owner.IsVulnerable, owner.IsVulnerable ? "방어력 저하" : owner.ActiveBeliefPhrase);
        if (fighting && gameplay) worldView.Position(owner.BodyCollider.bounds);
    }
    private void SetVisible(bool visible)
    {
        if (worldView != null) worldView.SetVisible(visible);
    }
    private void OnDisable() => SetVisible(false);
}
