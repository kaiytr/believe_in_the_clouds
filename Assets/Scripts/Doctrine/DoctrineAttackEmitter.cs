using UnityEngine;

// Attack behavior sensor. The attacker's GameObject is attached.
// PerformAttack raises OnAttack only when damage is actually applied
// (table: attack motion done AND the hit lands on a target).
// Then the target's IHealth damage raises OnHit on the target side.
// "X attacks ..." and "Y is hit ..." doctrines trigger independently.
public class DoctrineAttackEmitter : MonoBehaviour
{
    public void PerformAttack(GameObject target, float damage)
    {
        if (target == null)
        {
            return;
        }

        IHealth health = target.GetComponent<IHealth>();
        if (health == null)
        {
            return;
        }

        float before = health.CurrentHealth;
        health.TakeDamage(damage, gameObject);

        // Zero-damage / immune / already-dead: no landing, no trigger.
        if (health.CurrentHealth < before)
        {
            DoctrineEventBus.Raise(ConditionType.OnAttack, gameObject, target);
        }
    }
}
