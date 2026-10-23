using System.Collections.Generic;
using UnityEngine;

public sealed class DodgeAttackBeliefCondition : BeliefCondition
{
    private readonly HashSet<EnemyAttack> subscribedAttacks = new HashSet<EnemyAttack>();

    private void Start()
    {
        foreach (EnemyAttack attack in GetComponents<EnemyAttack>())
        {
            if (attack != null && subscribedAttacks.Add(attack))
                attack.OutcomeReported += HandleOutcome;
        }
    }

    private void HandleOutcome(EnemyAttack attack, AttackOutcome outcome)
    {
        if (outcome != AttackOutcome.EvadedByPlayer ||
            Controller == null || Controller.Profile == null)
        {
            return;
        }

        ReduceBelief(Controller.Profile.ReductionPerEvade);
    }

    private void OnDisable()
    {
        foreach (EnemyAttack attack in subscribedAttacks)
        {
            if (attack != null)
                attack.OutcomeReported -= HandleOutcome;
        }

        subscribedAttacks.Clear();
    }
}
