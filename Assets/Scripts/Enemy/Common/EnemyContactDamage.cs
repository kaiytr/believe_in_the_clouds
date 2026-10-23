using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyContactDamage : MonoBehaviour
{
    [SerializeField, Min(0f)] private float damage = 5f;
    private bool suppressed;

    public float Damage => damage;

    private void Awake()
    {
        Collider2D contactCollider = GetComponent<Collider2D>();
        if (!contactCollider.isTrigger)
        {
            Debug.LogWarning($"{name}: Contact damage collider must be a trigger; enabling trigger mode.", this);
            contactCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void TryDamagePlayer(Collider2D other)
    {
        if (suppressed)
            return;

        Transform playerRoot = GetPlayerRoot(other);
        if (playerRoot == null)
            return;

        IDamageable damageable = playerRoot.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = playerRoot.GetComponentInChildren<IDamageable>();
        damageable?.TakeDamage(new DamageInfo(damage, DamageSource.Enemy));
    }

    private static Transform GetPlayerRoot(Collider2D other)
    {
        if (other.CompareTag("Player"))
            return other.transform;

        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player"))
            return other.attachedRigidbody.transform;

        Transform root = other.transform.root;
        return root != null && root.CompareTag("Player") ? root : null;
    }

    public void SetSuppressed(bool value)
    {
        suppressed = value;
    }
}
