using UnityEngine;

public class MagicCircle : MonoBehaviour
{
    private float radius;

    public bool Activate(
        float radius,
        float damage,
        float slowMultiplier,
        float slowDuration,
        float visualDuration,
        Collider2D targetBodyCollider,
        out bool hpDamageApplied)
    {
        this.radius = radius;
        float diameter = radius * 2f;
        transform.localScale = new Vector3(diameter, diameter, 1f);
        bool applied = ApplyEffect(damage, slowMultiplier, slowDuration, targetBodyCollider, out hpDamageApplied);
        Destroy(gameObject, visualDuration);
        return applied;
    }

    private bool ApplyEffect(float damage, float slowMultiplier, float slowDuration, Collider2D targetBodyCollider,
        out bool hpDamageApplied)
    {
        hpDamageApplied = false;
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
        if (damageable != null)
        {
            hpDamageApplied = damageable.TakeDamage(new DamageInfo(damage, DamageSource.Enemy));
            harmfulEffectApplied |= hpDamageApplied;
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
