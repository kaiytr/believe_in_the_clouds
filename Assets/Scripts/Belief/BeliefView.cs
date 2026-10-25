using UnityEngine;

public sealed class BeliefView : MonoBehaviour
{
    private BeliefController controller;
    private EnemyUnit owner;
    private CombatantHudView view;
    private bool hidden;
    public CombatantHudView WorldView => view;

    public bool Initialize(BeliefController beliefController)
    {
        if (beliefController == null || beliefController.Profile == null) return false;
        Unsubscribe();
        controller = beliefController;
        owner = GetComponent<EnemyUnit>();
        if (view == null) view = CombatantHudView.Create(transform);
        if (view == null || !view.IsConfigured) return false;
        view.Initialize(CombatUiResources.KoreanFont());
        controller.BeliefChanged += HandleBeliefChanged;
        controller.StateChanged += HandleStateChanged;
        hidden = false;
        Refresh();
        UpdateHealth(owner != null ? owner.CurrentHp : 0f, owner != null ? owner.MaxHp : 0f);
        return true;
    }
    public void UpdateHealth(float current, float maximum) { if (view != null) view.UpdateHealth(current, maximum); }
    private void HandleBeliefChanged(BeliefController source, float current, float max) => Refresh();
    private void HandleStateChanged(BeliefController source, BeliefState state) => Refresh();
    private void Refresh()
    {
        if (view != null && controller != null)
            view.UpdateBelief(controller.NormalizedBelief, controller.IsVulnerable,
                controller.IsVulnerable ? "방어력 저하" : controller.Profile.Phrase);
    }
    public void Hide() { hidden = true; if (view != null) view.SetVisible(false); }
    private void LateUpdate()
    {
        if (view == null) return;
        view.SetVisible(!hidden);
        if (!hidden && owner != null && owner.BodyCollider != null) view.Position(owner.BodyCollider.bounds);
    }
    private void OnDisable() { if (view != null) view.SetVisible(false); }
    private void Unsubscribe()
    {
        if (controller == null) return;
        controller.BeliefChanged -= HandleBeliefChanged;
        controller.StateChanged -= HandleStateChanged;
    }
    private void OnDestroy() => Unsubscribe();
}
