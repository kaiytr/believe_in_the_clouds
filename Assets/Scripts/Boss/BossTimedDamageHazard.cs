using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class BossTimedDamageHazard : MonoBehaviour
{
    private float damage;
    private bool hasHitPlayer;

    public void Initialize(float hitDamage, float duration)
    {
        damage = Mathf.Max(0f, hitDamage);
        StartCoroutine(ExpireAfter(duration));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHitPlayer)
            return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null)
            return;

        hasHitPlayer = true;
        health.TakeDamage(new DamageInfo(damage, DamageSource.Environment));
    }

    private IEnumerator ExpireAfter(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, duration));
        Destroy(gameObject);
    }
}
