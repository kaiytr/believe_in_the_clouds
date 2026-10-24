using UnityEngine;

public abstract class BeliefCondition : MonoBehaviour
{
    [SerializeField] private BeliefController beliefController;

    protected BeliefController Controller => beliefController;
    public abstract BeliefType SupportedType { get; }

    protected virtual void Awake()
    {
        if (beliefController == null)
            beliefController = GetComponent<BeliefController>();
    }

    protected bool ReduceBelief(float amount)
    {
        return beliefController != null && beliefController.ReduceBelief(amount);
    }
}
