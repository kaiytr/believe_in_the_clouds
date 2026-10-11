using UnityEngine;

public abstract class BeliefCondition : MonoBehaviour
{
    [SerializeField]
    private BeliefController beliefController;

    protected BeliefController BeliefController =>
        beliefController;

    protected virtual void Awake()
    {
        if (beliefController == null)
        {
            beliefController =
                GetComponent<BeliefController>();
        }
    }

    protected bool ReduceBelief()
    {
        if (beliefController == null)
            return false;

        beliefController.ReduceBelief();

        return true;
    }

    protected bool ReduceBelief(float amount)
    {
        if (beliefController == null)
            return false;

        beliefController.ReduceBelief(amount);

        return true;
    }
}
