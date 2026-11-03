using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable, IHealth
{
    [Header("Health")]
    [SerializeField] private float maxHp = 100f;

    [Header("Invulnerability")]
    [SerializeField] private float invulnerabilityDuration = 1f;
    [SerializeField] private float blinkInterval = 0.1f;
    [SerializeField] private Collider2D bodyCollider;

    [Header("Doctrine Link")]
    [Tooltip("LowHealth event fires when HP falls to this ratio or below.")]
    [SerializeField, Range(0f, 0.95f)] private float lowHealthRatio = 0.3f;

    private float currentHp;
    private bool isInvulnerable;
    private bool lowHealthNotified;
    private Coroutine invulnerabilityRoutine;
    private SpriteRenderer[] spriteRenderers;
    private bool[] originalRendererStates;
    private PlayerDeathCoordinator deathCoordinator;
    public event System.Action<PlayerHealth> HealthChanged;

    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public bool IsInvulnerable => isInvulnerable;
    public Collider2D BodyCollider => bodyCollider;

    public float CurrentHealth => currentHp;
    public float MaxHealth => maxHp;
    public bool IsDead => currentHp <= 0f;
    private bool IsLowHealth => !IsDead && currentHp <= maxHp * lowHealthRatio;

    private void Awake()
    {
        currentHp = maxHp;
        deathCoordinator = GetComponent<PlayerDeathCoordinator>();
        if (deathCoordinator == null) deathCoordinator = gameObject.AddComponent<PlayerDeathCoordinator>();

        if (bodyCollider == null)
            bodyCollider = GetComponent<Collider2D>();
        if (bodyCollider == null || bodyCollider.isTrigger)
            Debug.LogError($"{name}: Assign one non-trigger Player body Collider2D on the Player root.", this);

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalRendererStates = new bool[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
            originalRendererStates[i] = spriteRenderers[i].enabled;
    }

    private void Start() { deathCoordinator.BeginLife(); }

    public bool TakeDamage(DamageInfo damageInfo)
    {
        return ApplyDamage(damageInfo.Amount, damageInfo.Attacker, cause: DeathCause.Describe(damageInfo.Attacker, damageInfo.Source, damageInfo.Detail));
    }

    internal bool TakeDamageFromBossCombo(DamageInfo damageInfo)
    {
        return ApplyDamage(damageInfo.Amount, damageInfo.Attacker, allowDuringInvulnerability: true,
            cause: DeathCause.Describe(damageInfo.Attacker, damageInfo.Source, damageInfo.Detail));
    }

    public void TakeDamage(float damage, GameObject source = null)
    {
        ApplyDamage(damage, source);
    }

    private bool ApplyDamage(float damage, GameObject source, bool allowDuringInvulnerability = false, string cause = null)
    {
        if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f || (isInvulnerable && !allowDuringInvulnerability) || currentHp <= 0f)
            return false;

        currentHp = Mathf.Max(0f, currentHp - damage);
        if (currentHp <= 0f)
        {
            Currency.Service?.BlockDefeatRewards();
            deathCoordinator.PrepareDeath(cause ?? DeathCause.Describe(source, null));
        }
        Debug.Log($"[Player] Damage: {damage} / HP: {currentHp}/{maxHp}");

        if (DoctrineEventBus.HasListeners)
            DoctrineEventBus.Raise(ConditionType.OnHit, gameObject, source);

        if (!lowHealthNotified && IsLowHealth)
        {
            lowHealthNotified = true;
            if (DoctrineEventBus.HasListeners)
                DoctrineEventBus.Raise(ConditionType.LowHealth, gameObject, source);
        }

        if (currentHp <= 0f)
        {
            Die(source, cause ?? DeathCause.Describe(source, null));
            return true;
        }

        StartInvulnerability();
        NotifyHealthChanged();
        return true;
    }

    public void Heal(float amount)
    {
        if (currentHp <= 0f || amount <= 0f)
            return;

        currentHp = Mathf.Min(maxHp, currentHp + amount);
        if (!IsLowHealth)
            lowHealthNotified = false;
        NotifyHealthChanged();
    }

    public void ResetHealth()
    {
        if ((deathCoordinator != null && deathCoordinator.HasResult) || (Currency.Service != null && Currency.Service.HasPendingDeath)) return;
        if (invulnerabilityRoutine != null)
        {
            StopCoroutine(invulnerabilityRoutine);
            invulnerabilityRoutine = null;
        }

        currentHp = maxHp;
        isInvulnerable = false;
        lowHealthNotified = false;
        RestoreRenderers();
        deathCoordinator?.BeginLife();
        NotifyHealthChanged();
    }

    // Restoring a checkpoint must not create a new life or replay damage events.
    public bool RestoreHealth(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0 || value > maxHp) return false;
        if (invulnerabilityRoutine != null) StopCoroutine(invulnerabilityRoutine);
        invulnerabilityRoutine = null;
        currentHp = value; isInvulnerable = false; lowHealthNotified = IsLowHealth;
        RestoreRenderers();
        return true;
    }

    internal void KillForTest() => ApplyDamage(maxHp + currentHp, null, allowDuringInvulnerability: true);

    private void NotifyHealthChanged()
    {
        if (HealthChanged == null) return;
        foreach (System.Action<PlayerHealth> listener in HealthChanged.GetInvocationList())
        {
            try { listener(this); }
            catch (System.Exception ex) { Debug.LogException(ex); }
        }
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
        // Resume trigger-stay callbacks even if the player stood still during immunity.
        Rigidbody2D body = bodyCollider != null ? bodyCollider.attachedRigidbody : null;
        if (body != null) body.WakeUp();
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
        if (spriteRenderers == null || originalRendererStates == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
            spriteRenderers[i].enabled = originalRendererStates[i];
    }

    private void Die(GameObject source, string cause)
    {
        isInvulnerable = true;
        RestoreRenderers();
        Debug.Log("[Player] Dead");

        deathCoordinator.CaptureDeath(() =>
        {
            if (DoctrineEventBus.HasListeners)
                DoctrineEventBus.Raise(ConditionType.OnDeath, gameObject, source);
        }, cause);
    }

    private void OnDisable()
    {
        RestoreRenderers();
    }

    private void OnDestroy() { DoctrineManager.Instance?.ClearForOwner(gameObject); }
}
