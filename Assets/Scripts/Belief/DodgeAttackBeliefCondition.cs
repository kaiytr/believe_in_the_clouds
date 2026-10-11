using System.Collections.Generic;
using UnityEngine;

public class DodgeAttackBeliefCondition : BeliefCondition
{
    private readonly HashSet<EnemyAttack> subscribedAttacks =
        new HashSet<EnemyAttack>();

    private void OnEnable()
    {
        SubscribeToAttacks();
    }

    private void Start()
    {
        SubscribeToAttacks();
    }

    private void SubscribeToAttacks()
    {
        foreach (EnemyAttack attack in GetComponents<EnemyAttack>())
        {
            if (attack != null && subscribedAttacks.Add(attack))
                attack.OnPlayerDodged += HandlePlayerDodged;
        }
    }

    private void HandlePlayerDodged(EnemyAttack attack)
    {
        ReduceBelief();
    }

    private void OnDisable()
    {
        foreach (EnemyAttack attack in subscribedAttacks)
        {
            if (attack != null)
                attack.OnPlayerDodged -= HandlePlayerDodged;
        }

        subscribedAttacks.Clear();
    }
}
