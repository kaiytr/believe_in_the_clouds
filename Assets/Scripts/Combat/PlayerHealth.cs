using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable, IHealth
{
    [Header("Health")]
    [SerializeField] private float maxHp = 100f;

    [Header("Invulnerability")]
    [SerializeField] private float invulnerabilityDuration = 1f;
    [SerializeField] private float blinkInterval = 0.1f;

    [Header("Doctrine Link")]
    [Tooltip("LowHealth event fires when HP falls to this ratio or below.")]
    [SerializeField] [Range(0f, 0.95f)] private float lowHealthRatio = 0.3f;

    private float currentHp;

    private bool isInvulnerable;
    private bool lowHealthNotified;
    private Coroutine invulnerabilityRoutine;

    private SpriteRenderer[] spriteRenderers;
    private bool[] originalRendererStates;

    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public bool IsInvulnerable => isInvulnerable;

    // IHealth (Doctrine contract). Existing CurrentHp/MaxHp kept untouched.
    public float CurrentHealth => currentHp;
    public float MaxHealth => maxHp;
    public bool IsDead => currentHp <= 0f;
    private bool IsLowHealth => !IsDead && currentHp <= maxHp * lowHealthRatio;

    private void Awake()
    {
        currentHp = maxHp;

        spriteRenderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        originalRendererStates =
            new bool[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            originalRendererStates[i] =
                spriteRenderers[i].enabled;
        }
    }

    public void TakeDamage(float damage)
    {
        ApplyDamage(damage, null);
    }

    // IHealth. Source is only used for Doctrine event attribution.
    public void TakeDamage(float damage, GameObject source)
    {
        ApplyDamage(damage, source);
    }

    private void ApplyDamage(float damage, GameObject source)
    {
        if (damage <= 0f)
            return;

        if (isInvulnerable)
            return;

        if (currentHp <= 0f)
            return;

        currentHp -= damage;
        currentHp = Mathf.Max(currentHp, 0f);

        Debug.Log(
            $"[Player] Damage: {damage} / HP: {currentHp}/{maxHp}"
        );

        if (DoctrineEventBus.HasListeners)
        {
            DoctrineEventBus.Raise(ConditionType.OnHit, gameObject, source);
        }

        if (!lowHealthNotified && IsLowHealth)
        {
            lowHealthNotified = true;
            if (DoctrineEventBus.HasListeners)
            {
                DoctrineEventBus.Raise(ConditionType.LowHealth, gameObject, source);
            }
        }

        if (currentHp <= 0f)
        {
            Die(source);
            return;
        }

        StartInvulnerability();
    }

    // IHealth. No healing while dead (same rule as before).
    public void Heal(float amount)
    {
        if (currentHp <= 0f || amount <= 0f)
            return;

        currentHp = Mathf.Min(maxHp, currentHp + amount);

        if (!IsLowHealth)
        {
            lowHealthNotified = false;
        }
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
        lowHealthNotified = false;
        RestoreRenderers();
    }

    private void StartInvulnerability()
    {
        // ���� �����ӿ� ���� ������ ������ �͵� �����ϱ� ����
        // Coroutine ���� ���� ��� true ó��.
        isInvulnerable = true;

        if (invulnerabilityRoutine != null)
        {
            StopCoroutine(invulnerabilityRoutine);
        }

        invulnerabilityRoutine =
            StartCoroutine(
                InvulnerabilityRoutine()
            );
    }

    private IEnumerator InvulnerabilityRoutine()
    {
        float elapsed = 0f;
        bool visible = true;

        while (elapsed < invulnerabilityDuration)
        {
            visible = !visible;

            SetRenderersVisible(visible);

            yield return new WaitForSeconds(
                blinkInterval
            );

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
            if (!originalRendererStates[i])
                continue;

            spriteRenderers[i].enabled = visible;
        }
    }

    private void RestoreRenderers()
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            spriteRenderers[i].enabled =
                originalRendererStates[i];
        }
    }

    private void Die(GameObject source = null)
    {
        isInvulnerable = true;

        RestoreRenderers();

        Debug.Log("[Player] Dead");

        if (DoctrineEventBus.HasListeners)
        {
            DoctrineEventBus.Raise(ConditionType.OnDeath, gameObject, source);
        }

        // ���� Player ��� ������ ���߿� ����
    }

    private void OnDisable()
    {
        RestoreRenderers();
    }
}