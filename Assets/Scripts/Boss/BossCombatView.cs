using UnityEngine;

public sealed class BossCombatView : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer bodyRenderer;
    private BossCombatController owner;
    private BossPresentationState state;
    private float phaseFlashUntil;
    public bool IsConfigured => visualRoot != null && bodyRenderer != null;
    public BossPresentationState State => state;
    public SpriteRenderer BodyRenderer => bodyRenderer;
    public void Initialize(BossCombatController boss) { owner = boss; }
    public void SetFacing(int direction) { if (bodyRenderer != null) bodyRenderer.flipX = direction < 0; }
    public void SetState(BossPresentationState value) { state = value; }
    public void FlashPhaseChange() { phaseFlashUntil = Time.time + 0.35f; }

    private void LateUpdate()
    {
        if (owner == null || !IsConfigured) return;
        // Temporary body only. The renderer can be replaced by the final sprite later.
        bodyRenderer.color = state == BossPresentationState.Dead ? new Color(.35f, .37f, .43f) :
            owner.IsBurning || state == BossPresentationState.Charging ? new Color(1f, .5f, .2f) :
            state == BossPresentationState.Windup || phaseFlashUntil > Time.time ? new Color(1f, .82f, .4f) :
            state == BossPresentationState.Recovery ? new Color(.72f, .75f, .82f) : Color.white;
    }
}
