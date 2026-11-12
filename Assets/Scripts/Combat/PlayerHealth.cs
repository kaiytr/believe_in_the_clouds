using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable, IHealth
{
    [Header("Health")]
    [SerializeField] private float maxHp = 150f;

    [Header("Invulnerability")]
    [Tooltip("Seconds of invulnerability after a hit.")]
    [SerializeField] private float invulnerabilityDuration = 0.6f;
    [SerializeField] private Collider2D bodyCollider;

    [Header("Doctrine Link")]
    [Tooltip("LowHealth event fires when HP falls to this ratio or below.")]
    [SerializeField, Range(0f, 0.95f)] private float lowHealthRatio = 0.3f;

    [Header("Death Sound")]
    [Tooltip("사망하는 순간 재생할 클립. 비어 있으면 소리 없이 동작한다.")]
    [SerializeField] private AudioClip deathSound;
    [Tooltip("사망 사운드 볼륨 (0~1).")]
    [SerializeField, Range(0f, 1f)] private float deathSoundVolume = 0.9f;

    private float currentHp;
    private bool isInvulnerable;
    private bool isDashInvulnerable;
    private bool lowHealthNotified;
    private Coroutine invulnerabilityRoutine;
    private PlayerDeathCoordinator deathCoordinator;
    private AudioSource deathAudioSource;
    public event System.Action<PlayerHealth> HealthChanged;

    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public bool IsInvulnerable => isInvulnerable || isDashInvulnerable;
    public Collider2D BodyCollider => bodyCollider;

    public float CurrentHealth => currentHp;
    public float MaxHealth => maxHp;
    public bool IsDead => currentHp <= 0f;
    private bool IsLowHealth => !IsDead && currentHp <= maxHp * lowHealthRatio;

    internal void SetDashInvulnerability(bool active)
    {
        isDashInvulnerable = active && !IsDead;
    }

    private void Awake()
    {
        currentHp = maxHp;
        deathCoordinator = GetComponent<PlayerDeathCoordinator>();
        if (deathCoordinator == null) deathCoordinator = gameObject.AddComponent<PlayerDeathCoordinator>();

        if (bodyCollider == null)
            bodyCollider = GetComponent<Collider2D>();
        if (bodyCollider == null || bodyCollider.isTrigger)
            Debug.LogError($"{name}: Assign one non-trigger Player body Collider2D on the Player root.", this);

        ResolveDeathAudio();
    }

    // 사망음은 사망 연출(타임스케일 저하) 중에도 들려야 하므로
    // Awake에서 미리 AudioSource를 준비해 두고 Die()에서 즉시 재생한다.
    private void ResolveDeathAudio()
    {
        // 공격음용 AudioSource가 이미 있으면 함께 쓴다 (PlayOneShot은 겹쳐 재생 가능).
        deathAudioSource = GetComponent<AudioSource>();
        if (deathAudioSource == null)
            deathAudioSource = gameObject.AddComponent<AudioSource>();
        deathAudioSource.playOnAwake = false;
        deathAudioSource.loop = false;
        deathAudioSource.spatialBlend = 0f; // 2D
        if (deathAudioSource.volume <= 0f)
            deathAudioSource.volume = 1f;
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
        if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f ||
            isDashInvulnerable || (isInvulnerable && !allowDuringInvulnerability) || currentHp <= 0f)
            return false;

        currentHp = Mathf.Max(0f, currentHp - damage);
        PlayerHitVignette.Pulse(maxHp > 0f ? damage / maxHp : 0f);
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
        yield return new WaitForSeconds(invulnerabilityDuration);

        isInvulnerable = false;
        invulnerabilityRoutine = null;
        // Resume trigger-stay callbacks even if the player stood still during immunity.
        Rigidbody2D body = bodyCollider != null ? bodyCollider.attachedRigidbody : null;
        if (body != null) body.WakeUp();
    }


    private void Die(GameObject source, string cause)
    {
        isInvulnerable = true;
        isDashInvulnerable = false;
        Debug.Log("[Player] Dead");

        // 사망 확정 순간에 재생. PlayOneShot은 타임스케일 영향을 받지 않아
        // 사망 슬로우(0.25배)·결과창 정지(0배) 중에도 소리가 끊기지 않는다.
        PlayDeathSoundEffect();

        deathCoordinator.CaptureDeath(() =>
        {
            if (DoctrineEventBus.HasListeners)
                DoctrineEventBus.Raise(ConditionType.OnDeath, gameObject, source);
        }, cause);
    }

    public void PlayDeathSoundEffect()
    {
        if (deathSound != null && deathAudioSource != null)
            deathAudioSource.PlayOneShot(deathSound, deathSoundVolume);
    }

    private void OnDestroy() { DoctrineManager.Instance?.ClearForOwner(gameObject); }
}
