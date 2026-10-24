using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class BossAttackProjectile : MonoBehaviour
{
    private BossCombatController owner;
    private Rigidbody2D body;
    private Vector2 direction;
    private float speed;
    private float damage;
    private float lifetime;
    private float elapsed;
    private int attackId;
    private bool resolved;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    public void Initialize(
        BossCombatController boss,
        int id,
        Vector2 travelDirection,
        float travelSpeed,
        float hitDamage,
        float maxLifetime)
    {
        owner = boss;
        attackId = id;
        direction = travelDirection.sqrMagnitude > 0.001f ? travelDirection.normalized : Vector2.right;
        speed = Mathf.Max(0f, travelSpeed);
        damage = Mathf.Max(0f, hitDamage);
        lifetime = Mathf.Max(0.1f, maxLifetime);
    }

    private void FixedUpdate()
    {
        if (body != null && !resolved)
            body.linearVelocity = direction * speed;
    }

    private void Update()
    {
        if (resolved)
            return;

        elapsed += Time.deltaTime;
        if (elapsed >= lifetime)
            ResolveMiss();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (resolved)
            return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
            return;

        resolved = true;
        owner?.ResolveBossAttackHit(attackId, playerHealth.gameObject, damage, true);
        Destroy(gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!resolved && collision.collider.GetComponentInParent<PlayerHealth>() == null)
            ResolveMiss();
    }

    private void ResolveMiss()
    {
        if (resolved)
            return;

        resolved = true;
        owner?.ReportProjectileMissed(attackId);
        Destroy(gameObject);
    }
}
