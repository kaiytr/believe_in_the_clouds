using UnityEngine;

public class DebugBeliefCondition : BeliefCondition
{
    [Header("Debug Input")]
    [SerializeField] private bool enableKeyboardInput;
    [SerializeField] private KeyCode reduceKey = KeyCode.B;

    private void Update()
    {
        if (!enableKeyboardInput)
            return;

        if (Input.GetKeyDown(reduceKey))
        {
            ReduceBelief();
        }
    }

    [ContextMenu("Debug/Reduce Belief")]
    private void DebugReduceBelief()
    {
        ReduceBelief();
    }
}
