using System.Collections;
using UnityEngine;

public class BeliefTestAttack : EnemyAttack
{
    [SerializeField] private float hitRange = 4f;

    public void ResetForTest()
    {
        if (enabled)
            enabled = false;
        enabled = true;
    }

    protected override bool CanAttack(Transform target)
    {
        return target != null &&
               Vector2.Distance(transform.position, target.position) <= hitRange;
    }

    protected override IEnumerator ExecuteAttackRoutine()
    {
        if (owner.Target == null)
            yield break;

        MarkAttackAsLaunched();

        float distance = Vector2.Distance(
            owner.transform.position,
            owner.Target.position
        );

        if (distance <= hitRange)
        {
            IDamageable damageable = owner.Target.GetComponent<IDamageable>();
            if (damageable == null)
                damageable = owner.Target.GetComponentInChildren<IDamageable>();

            if (damageable != null)
            {
                damageable.TakeDamage(damage);
                NotifyPlayerHit();
            }
        }

        yield return null;
    }
}
