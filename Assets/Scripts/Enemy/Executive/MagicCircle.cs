using UnityEngine;

public class MagicCircle : MonoBehaviour
{
    private float radius;
    private GameObject ownerObject;

    public bool Activate(
        float radius,
        float damage,
        float slowMultiplier,
        float slowDuration,
        float visualDuration,
        Collider2D targetBodyCollider,
        GameObject ownerObject = null)
    {
        this.radius = radius;
        this.ownerObject = ownerObject;
        float diameter = radius * 2f;
        transform.localScale = new Vector3(diameter, diameter, 1f);
        bool applied = ApplyEffect(damage, slowMultiplier, slowDuration, targetBodyCollider);
        Destroy(gameObject, visualDuration);
        return applied;
    }

    private bool ApplyEffect(float damage, float slowMultiplier, float slowDuration, Collider2D targetBodyCollider)
    {
        if (targetBodyCollider == null || !targetBodyCollider.enabled)
            return false;

        Vector2 closestPoint = targetBodyCollider.ClosestPoint(transform.position);
        if ((closestPoint - (Vector2)transform.position).sqrMagnitude > radius * radius)
            return false;

        Transform playerRoot = targetBodyCollider.attachedRigidbody != null
            ? targetBodyCollider.attachedRigidbody.transform
            : targetBodyCollider.transform.root;
        if (playerRoot == null)
            return false;

        bool harmfulEffectApplied = false;
        IDamageable damageable = playerRoot.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = playerRoot.GetComponentInChildren<IDamageable>();
        bool damaged = false;
        if (damageable != null)
        {
            damaged = damageable.TakeDamage(new DamageInfo(damage, DamageSource.Enemy));
            harmfulEffectApplied |= damaged;
        }

        // Table rule: OnAttack triggers only when the hit lands.
        if (damaged && ownerObject != null && DoctrineEventBus.HasListeners)
        {
            DoctrineEventBus.Raise(ConditionType.OnAttack, ownerObject, playerRoot.gameObject);
        }

        ISlowable slowable = playerRoot.GetComponent<ISlowable>();
        if (slowable == null)
            slowable = playerRoot.GetComponentInChildren<ISlowable>();
        if (slowable != null && slowDuration > 0f && slowMultiplier < 1f)
        {
            slowable.ApplySlow(slowMultiplier, slowDuration);
            harmfulEffectApplied = true;
        }

        return harmfulEffectApplied;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
