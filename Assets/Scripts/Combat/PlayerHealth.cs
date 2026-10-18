using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHp = 100f;

    [Header("Invulnerability")]
    [SerializeField] private float invulnerabilityDuration = 1f;
    [SerializeField] private float blinkInterval = 0.1f;
    [SerializeField] private Collider2D bodyCollider;

    private float currentHp;
    private bool isInvulnerable;
    private Coroutine invulnerabilityRoutine;
    private SpriteRenderer[] spriteRenderers;
    private bool[] originalRendererStates;

    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public bool IsInvulnerable => isInvulnerable;
    public Collider2D BodyCollider => bodyCollider;

    private void Awake()
    {
        currentHp = maxHp;
        if (bodyCollider == null)
            bodyCollider = GetComponent<Collider2D>();
        if (bodyCollider == null || bodyCollider.isTrigger)
            Debug.LogError($"{name}: Assign one non-trigger Player body Collider2D on the Player root.", this);

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalRendererStates = new bool[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
            originalRendererStates[i] = spriteRenderers[i].enabled;
    }

    public bool TakeDamage(DamageInfo damageInfo)
    {
        float damage = damageInfo.Amount;
        if (damage <= 0f || isInvulnerable || currentHp <= 0f)
            return false;

        currentHp = Mathf.Max(0f, currentHp - damage);
        Debug.Log($"[Player] Damage: {damage} / HP: {currentHp}/{maxHp}");
        if (currentHp <= 0f)
        {
            Die();
            return true;
        }

        StartInvulnerability();
        return true;
    }

    public void ResetHealth()
    {
        if (invulnerabilityRoutine != null)
        {
            StopCoroutine(invulnerabilityRoutine);
            invulnerabilityRoutine = null;
        }
        currentHp = maxHp;
        isInvulnerable = false;
        RestoreRenderers();
    }

    private void StartInvulnerability()
    {
        isInvulnerable = true;
        if (invulnerabilityRoutine != null)
            StopCoroutine(invulnerabilityRoutine);
        invulnerabilityRoutine = StartCoroutine(InvulnerabilityRoutine());
    }

    private IEnumerator InvulnerabilityRoutine()
    {
        float elapsed = 0f;
        bool visible = true;
        while (elapsed < invulnerabilityDuration)
        {
            visible = !visible;
            SetRenderersVisible(visible);
            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        RestoreRenderers();
        isInvulnerable = false;
        invulnerabilityRoutine = null;
    }

    private void SetRenderersVisible(bool visible)
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (originalRendererStates[i])
                spriteRenderers[i].enabled = visible;
        }
    }

    private void RestoreRenderers()
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
            spriteRenderers[i].enabled = originalRendererStates[i];
    }

    private void Die()
    {
        isInvulnerable = true;
        RestoreRenderers();
        Debug.Log("[Player] Dead");
    }

    private void OnDisable()
    {
        RestoreRenderers();
    }
}
