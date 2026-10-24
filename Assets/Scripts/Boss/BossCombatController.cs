using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public sealed class BossCombatController : MonoBehaviour, IDamageable
{
    private static Sprite placeholderSprite;

    [Header("Health and phase")]
    [SerializeField] private bool startOnAwake = true;
    [SerializeField, Min(1f)] private float maxHp = 500f;
    [SerializeField, Range(0.1f, 0.9f)] private float phaseTwoHealthRatio = 0.5f;
    [SerializeField, Min(1f)] private float maxBelief = 100f;

    [Header("Shared belief gauge")]
    [SerializeField, Min(0f)] private float beliefReductionPerCondition = 20f;
    [SerializeField, Min(0f)] private float vulnerableDuration = 5f;
    [SerializeField, Range(0f, 1f)] private float recoveryRatio = 1f;
    [SerializeField, Min(0f)] private float maintainedDamageMultiplier = 0.6f;
    [SerializeField, Min(0f)] private float vulnerableDamageMultiplier = 2f;
    [SerializeField] private BossAttackSignalMode attackSignalMode = BossAttackSignalMode.Attempt;

    [Header("Pattern selection")]
    [SerializeField, Min(0f)] private float patternRecovery = 0.65f;
    [SerializeField] private BossPatternWeight[] phaseOnePatternPool =
    {
        new BossPatternWeight(BossPatternKind.MeleeCombo, 1f, 0.5f),
        new BossPatternWeight(BossPatternKind.Dash, 1f, 0.5f),
        new BossPatternWeight(BossPatternKind.Projectile, 1f, 0.5f),
        new BossPatternWeight(BossPatternKind.AttackIntentBelief, 1f, 0.5f),
        new BossPatternWeight(BossPatternKind.CloudBelief, 1f, 0.5f),
        new BossPatternWeight(BossPatternKind.BurnBelief, 1f, 0.5f)
    };
    [SerializeField] private BossPatternWeight[] phaseTwoPatternPool =
    {
        new BossPatternWeight(BossPatternKind.MeleeCombo, 1f, 0.5f),
        new BossPatternWeight(BossPatternKind.Dash, 1f, 0.5f),
        new BossPatternWeight(BossPatternKind.Projectile, 1f, 0.5f),
        new BossPatternWeight(BossPatternKind.AttackAndDodgeBelief, 1f, 0.5f),
        new BossPatternWeight(BossPatternKind.NoAttackBelief, 1f, 0.5f),
        new BossPatternWeight(BossPatternKind.ShockwaveBelief, 1f, 0.5f)
    };
    [SerializeField, Min(0.1f)] private float phaseTwoAttackSpeedMultiplier = 1f;
    [SerializeField, Min(0f)] private float phaseTwoAttackDamageMultiplier = 1f;

    [Header("Pattern timings")]
    [SerializeField, Min(0f)] private float stareDuration = 3f;
    [SerializeField, Min(0f)] private float cloudPatternDuration = 4f;
    [SerializeField, Min(0f)] private float firePreparationDuration = 5f;
    [SerializeField, Min(0f)] private float burnApplicationWindow = 10f;
    [SerializeField, Min(0f)] private float burnDuration = 5f;
    [SerializeField, Min(0f)] private float burnDamagePerTick = 2f;
    [SerializeField, Min(0.1f)] private float burnTickInterval = 1f;

    [Header("General attacks")]
    [SerializeField, Min(0f)] private float attackWindup = 0.65f;
    [SerializeField, Min(0f)] private float attackRecovery = 0.45f;
    [SerializeField, Min(0f)] private float meleeDamage = 14f;
    [SerializeField, Min(0.1f)] private float meleeRange = 2.6f;
    [SerializeField, Min(0f)] private float meleeVerticalRange = 1.5f;
    [SerializeField, Min(0f)] private float dashDamage = 18f;
    [SerializeField, Min(0.1f)] private float dashSpeed = 10f;
    [SerializeField, Min(0.1f)] private float dashDuration = 0.45f;
    [SerializeField, Min(0f)] private float projectileDamage = 12f;
    [SerializeField, Min(0.1f)] private float projectileSpeed = 8f;
    [SerializeField, Min(0.1f)] private float projectileLifetime = 3f;
    [SerializeField, Min(0.1f)] private float shockwaveRadius = 7f;
    [SerializeField, Min(0.1f)] private float shockwaveExpansionSpeed = 6f;
    [SerializeField, Min(0f)] private float shockwaveDamage = 16f;
    [SerializeField, Min(0f)] private float spikeDamage = 18f;
    [SerializeField, Min(0f)] private float spikeDelay = 3f;

    private readonly Dictionary<BossPatternKind, int> patternUseCounts = new Dictionary<BossPatternKind, int>();
    private readonly HashSet<int> attackSignalIdsInWindow = new HashSet<int>();
    private readonly HashSet<int> appliedPlayerAttackIds = new HashSet<int>();
    private readonly Queue<int> appliedPlayerAttackOrder = new Queue<int>();
    private readonly List<GameObject> transientObjects = new List<GameObject>();

    private Rigidbody2D body;
    private CapsuleCollider2D bodyCollider;
    private Transform player;
    private PlayerHealth playerHealth;
    private Collider2D playerBodyCollider;
    private BossPlayerCombatEvents playerCombatEvents;
    private Coroutine patternRoutine;
    private Coroutine vulnerabilityRoutine;
    private BossPhase phase = BossPhase.One;
    private BossBeliefKind activeBelief = BossBeliefKind.None;
    private BossPatternKind activePattern;
    private bool hasActivePattern;
    private bool encounterStarted;
    private bool isVulnerable;
    private bool isDead;
    private bool attackSignalWindowOpen;
    private bool playerActedDuringSignalWindow;
    private bool cloudTouched;
    private bool attackThreatened;
    private bool attackHit;
    private bool attackEvadeReported;
    private bool phaseTwoStarted;
    private float currentHp;
    private float currentBelief;
    private float burnWindowEndsAt;
    private int nextBossAttackId = 1;
    private int activeBossAttackId;
    private int effectGeneration;
    private BossAttackKind activeBossAttackKind;
    private string statusMessage = "Waiting for encounter";
    private SpriteRenderer robeRenderer;

    public event Action StatusChanged;

    public BossPhase Phase => phase;
    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public float CurrentBelief => currentBelief;
    public float MaxBelief => maxBelief;
    public float NormalizedBelief => maxBelief <= 0f ? 0f : Mathf.Clamp01(currentBelief / maxBelief);
    public bool IsVulnerable => isVulnerable;
    public bool IsDead => isDead;
    public bool EncounterStarted => encounterStarted;
    public int ActiveBossAttackId => activeBossAttackId;
    public string ActiveBeliefPhrase => GetBeliefPhrase(activeBelief);
    public string ActivePatternName => hasActivePattern ? activePattern.ToString() : "Waiting";
    public string StatusMessage => statusMessage;

    private float CurrentPhaseDamageMultiplier => phase == BossPhase.Two ? phaseTwoAttackDamageMultiplier : 1f;
    private float CurrentPhaseSpeedMultiplier => phase == BossPhase.Two ? phaseTwoAttackSpeedMultiplier : 1f;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<CapsuleCollider2D>();
        currentHp = maxHp;
        currentBelief = maxBelief;
        BuildPlaceholderVisual();
    }

    private void Start()
    {
        BindPlayer();
        if (encounterStarted)
            return;

        // The boss test scene starts immediately. Production scenes can disable this
        // and call BeginEncounter from BossEncounterTrigger.
        if (startOnAwake && gameObject.activeInHierarchy)
            BeginEncounter();
    }

    private void Update()
    {
        if (!encounterStarted || isDead)
            return;

        if (player == null || playerHealth == null || playerBodyCollider == null)
            BindPlayer();

        if (!isVulnerable && patternRoutine == null && player != null)
            patternRoutine = StartCoroutine(RunPatternLoop());
    }

    public void BeginEncounter()
    {
        if (isDead || encounterStarted)
            return;

        BindPlayer();
        encounterStarted = true;
        SetStatus("Encounter started");
    }

    public bool TakeDamage(DamageInfo damageInfo)
    {
        if (isDead || damageInfo.Amount <= 0f)
            return false;

        float multiplier = 1f;
        if (damageInfo.Source == DamageSource.PlayerOwned)
            multiplier = isVulnerable ? vulnerableDamageMultiplier : maintainedDamageMultiplier;

        float finalDamage = damageInfo.Amount * multiplier;
        if (finalDamage <= 0f)
            return false;

        float phaseThreshold = maxHp * phaseTwoHealthRatio;
        if (!phaseTwoStarted)
            finalDamage = Mathf.Min(finalDamage, Mathf.Max(0f, currentHp - phaseThreshold));

        currentHp = Mathf.Max(0f, currentHp - finalDamage);
        if (!phaseTwoStarted && currentHp <= phaseThreshold)
        {
            currentHp = phaseThreshold;
            EnterPhaseTwo();
        }

        SetStatus(isVulnerable ? "Boss damaged while vulnerable" : "Boss damaged");
        if (currentHp <= 0f)
            Die();
        else
            StatusChanged?.Invoke();
        return true;
    }

    public void ForcePhaseTwoForTest()
    {
        if (!phaseTwoStarted && !isDead)
        {
            currentHp = Mathf.Min(currentHp, maxHp * phaseTwoHealthRatio);
            EnterPhaseTwo();
            StatusChanged?.Invoke();
        }
    }

    public void ForcePatternForTest(BossPatternKind pattern)
    {
        if (!encounterStarted || isDead || isVulnerable)
            return;

        CancelCurrentPattern();
        CleanupTransientObjects();
        patternRoutine = StartCoroutine(RunForcedPattern(pattern));
    }

    public void ReduceBeliefForTest(float amount)
    {
        ReduceBelief(amount, "Test signal reduced shared belief");
    }

    public void ResetEncounterForTest()
    {
        StopAllCoroutines();
        patternRoutine = null;
        vulnerabilityRoutine = null;
        CleanupTransientObjects();
        isDead = false;
        encounterStarted = true;
        isVulnerable = false;
        phase = BossPhase.One;
        phaseTwoStarted = false;
        activeBelief = BossBeliefKind.None;
        hasActivePattern = false;
        attackSignalWindowOpen = false;
        playerActedDuringSignalWindow = false;
        burnWindowEndsAt = 0f;
        activeBossAttackId = 0;
        patternUseCounts.Clear();
        currentHp = maxHp;
        currentBelief = maxBelief;
        if (bodyCollider != null)
            bodyCollider.enabled = true;
        appliedPlayerAttackIds.Clear();
        appliedPlayerAttackOrder.Clear();
        if (body != null)
            body.linearVelocity = Vector2.zero;
        SetStatus("Encounter reset");
    }

    internal void ResolveBossAttackHit(int attackId, GameObject hitObject, float damage, bool allowBurn)
    {
        if (isDead || hitObject == null || attackId != activeBossAttackId)
            return;

        PlayerHealth hitHealth = hitObject.GetComponentInParent<PlayerHealth>();
        if (hitHealth == null || hitHealth.gameObject != (player != null ? player.gameObject : null))
            return;

        attackHit = true;
        bool damageApplied = hitHealth.TakeDamage(new DamageInfo(
            Mathf.Max(0f, damage * CurrentPhaseDamageMultiplier),
            DamageSource.Enemy));
        if (damageApplied && allowBurn && Time.time <= burnWindowEndsAt)
            ApplyBurn(hitHealth);
        StatusChanged?.Invoke();
    }

    internal void ReportProjectileMissed(int attackId)
    {
        if (attackId == activeBossAttackId && attackThreatened && !attackHit)
            ReportAttackEvadedIfNeeded(attackId);
    }

    internal void OnCloudInteraction(bool isWhiteCloud, GameObject playerObject)
    {
        if (playerObject == null || player == null || playerObject.transform.root != player)
            return;

        cloudTouched = true;
        if (!isWhiteCloud)
        {
            Vector2 dropPoint = player.position;
            StartCoroutine(SpawnSpikesAfterDrop(dropPoint, effectGeneration));
        }
        StatusChanged?.Invoke();
    }

    private void BindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
            return;

        Transform nextPlayer = playerObject.transform;
        if (player == nextPlayer && playerHealth != null)
            return;

        if (playerCombatEvents != null)
            playerCombatEvents.SignalPublished -= HandlePlayerSignal;

        player = nextPlayer;
        playerHealth = player.GetComponent<PlayerHealth>();
        playerBodyCollider = playerHealth != null ? playerHealth.BodyCollider : player.GetComponent<Collider2D>();
        if (playerHealth == null)
            playerHealth = player.gameObject.AddComponent<PlayerHealth>();
        playerCombatEvents = player.GetComponent<BossPlayerCombatEvents>();
        if (playerCombatEvents == null)
            playerCombatEvents = player.gameObject.AddComponent<BossPlayerCombatEvents>();
        playerCombatEvents.SignalPublished += HandlePlayerSignal;

        if (bodyCollider != null && playerBodyCollider != null)
            Physics2D.IgnoreCollision(bodyCollider, playerBodyCollider, true);
    }

    private void HandlePlayerSignal(BossPlayerCombatSignal signal)
    {
        if (!encounterStarted || isDead || player == null || signal.Actor != player.gameObject)
            return;

        if (signal.Type == BossPlayerCombatSignalType.AttackEvaded)
        {
            if (signal.Target == gameObject && signal.AttackId == activeBossAttackId)
                HandleAttackEvaded(signal.AttackId);
            return;
        }

        if (signal.Target != gameObject)
            return;

        if (attackSignalWindowOpen && ShouldCountAttackSignal(signal.Type))
        {
            if (attackSignalIdsInWindow.Add(signal.AttackId))
                playerActedDuringSignalWindow = true;
        }

        if (signal.Type == BossPlayerCombatSignalType.HitConfirmed &&
            RememberAttackId(signal.AttackId))
        {
            TakeDamage(new DamageInfo(signal.Damage, DamageSource.PlayerOwned));
        }
    }

    private bool ShouldCountAttackSignal(BossPlayerCombatSignalType type)
    {
        switch (attackSignalMode)
        {
            case BossAttackSignalMode.Attempt:
                return type == BossPlayerCombatSignalType.AttackAttempt;
            case BossAttackSignalMode.HitConfirmed:
                return type == BossPlayerCombatSignalType.HitConfirmed;
            default:
                return type == BossPlayerCombatSignalType.AttackAttempt ||
                       type == BossPlayerCombatSignalType.HitConfirmed;
        }
    }

    private bool RememberAttackId(int attackId)
    {
        if (attackId <= 0 || !appliedPlayerAttackIds.Add(attackId))
            return false;

        appliedPlayerAttackOrder.Enqueue(attackId);
        while (appliedPlayerAttackOrder.Count > 256)
            appliedPlayerAttackIds.Remove(appliedPlayerAttackOrder.Dequeue());
        return true;
    }

    private void EnterPhaseTwo()
    {
        phaseTwoStarted = true;
        phase = BossPhase.Two;
        patternUseCounts.Clear();
        activeBelief = BossBeliefKind.None;
        burnWindowEndsAt = 0f;
        CancelCurrentPattern();
        SetStatus("Phase 2");
    }

    private void StartVulnerableState()
    {
        if (isDead || isVulnerable)
            return;

        CancelCurrentPattern();
        CleanupTransientObjects();
        isVulnerable = true;
        attackSignalWindowOpen = false;
        burnWindowEndsAt = 0f;
        if (body != null)
            body.linearVelocity = Vector2.zero;
        SetStatus("Vulnerable");
        vulnerabilityRoutine = StartCoroutine(VulnerabilityRoutine());
    }

    private IEnumerator VulnerabilityRoutine()
    {
        yield return new WaitForSeconds(vulnerableDuration);
        vulnerabilityRoutine = null;
        if (isDead)
            yield break;

        currentBelief = maxBelief * recoveryRatio;
        isVulnerable = false;
        SetStatus("Belief recovered");
    }

    private void ReduceBelief(float amount, string reason)
    {
        if (isDead || isVulnerable || amount <= 0f)
            return;

        float previous = currentBelief;
        currentBelief = Mathf.Max(0f, currentBelief - amount);
        if (Mathf.Approximately(previous, currentBelief))
            return;

        statusMessage = reason;
        StatusChanged?.Invoke();
        if (currentBelief <= 0f)
            StartVulnerableState();
    }

    private IEnumerator RunPatternLoop()
    {
        BossPatternKind selectedPattern = ChoosePattern();
        activePattern = selectedPattern;
        hasActivePattern = true;
        RecordPatternUse(selectedPattern);
        SetStatus($"Pattern: {selectedPattern}");

        yield return ExecutePattern(selectedPattern);
        if (isDead || isVulnerable)
        {
            patternRoutine = null;
            yield break;
        }

        if (patternRecovery > 0f)
            yield return new WaitForSeconds(patternRecovery / CurrentPhaseSpeedMultiplier);

        hasActivePattern = false;
        patternRoutine = null;
        StatusChanged?.Invoke();
    }

    private IEnumerator RunForcedPattern(BossPatternKind pattern)
    {
        activePattern = pattern;
        hasActivePattern = true;
        SetStatus($"Forced test pattern: {pattern}");
        yield return ExecutePattern(pattern);
        hasActivePattern = false;
        patternRoutine = null;
        if (!isDead && !isVulnerable)
            StatusChanged?.Invoke();
    }

    private BossPatternKind ChoosePattern()
    {
        BossPatternWeight[] pool = phase == BossPhase.One ? phaseOnePatternPool : phaseTwoPatternPool;
        if (pool == null || pool.Length == 0)
            return BossPatternKind.MeleeCombo;

        float total = 0f;
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i] == null || pool[i].BaseWeight <= 0f)
                continue;
            patternUseCounts.TryGetValue(pool[i].Pattern, out int uses);
            total += pool[i].EffectiveWeight(uses);
        }

        if (total <= 0f)
            return BossPatternKind.MeleeCombo;

        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i] == null || pool[i].BaseWeight <= 0f)
                continue;
            patternUseCounts.TryGetValue(pool[i].Pattern, out int uses);
            roll -= pool[i].EffectiveWeight(uses);
            if (roll <= 0f)
                return pool[i].Pattern;
        }

        for (int i = pool.Length - 1; i >= 0; i--)
        {
            if (pool[i] != null && pool[i].BaseWeight > 0f)
                return pool[i].Pattern;
        }
        return BossPatternKind.MeleeCombo;
    }

    private BossPatternKind ChooseRegularAttack()
    {
        BossPatternWeight[] pool = phase == BossPhase.One ? phaseOnePatternPool : phaseTwoPatternPool;
        float total = 0f;
        if (pool == null)
            return BossPatternKind.MeleeCombo;

        foreach (BossPatternWeight entry in pool)
        {
            if (entry == null || !IsRegularAttack(entry.Pattern) || entry.BaseWeight <= 0f)
                continue;
            patternUseCounts.TryGetValue(entry.Pattern, out int uses);
            total += entry.EffectiveWeight(uses);
        }

        if (total <= 0f)
            return BossPatternKind.MeleeCombo;

        float roll = UnityEngine.Random.value * total;
        foreach (BossPatternWeight entry in pool)
        {
            if (entry == null || !IsRegularAttack(entry.Pattern) || entry.BaseWeight <= 0f)
                continue;
            patternUseCounts.TryGetValue(entry.Pattern, out int uses);
            roll -= entry.EffectiveWeight(uses);
            if (roll <= 0f)
                return entry.Pattern;
        }
        return BossPatternKind.MeleeCombo;
    }

    private void RecordPatternUse(BossPatternKind pattern)
    {
        patternUseCounts.TryGetValue(pattern, out int used);
        patternUseCounts[pattern] = used + 1;
    }

    private static bool IsRegularAttack(BossPatternKind pattern)
    {
        return pattern == BossPatternKind.MeleeCombo ||
               pattern == BossPatternKind.Dash ||
               pattern == BossPatternKind.Projectile;
    }

    private IEnumerator ExecutePattern(BossPatternKind pattern)
    {
        switch (pattern)
        {
            case BossPatternKind.MeleeCombo:
                yield return ExecuteMeleeCombo();
                break;
            case BossPatternKind.Dash:
                yield return ExecuteDash();
                break;
            case BossPatternKind.Projectile:
                yield return ExecuteProjectile();
                break;
            case BossPatternKind.AttackIntentBelief:
                yield return ExecuteAttackIntentBelief();
                break;
            case BossPatternKind.CloudBelief:
                yield return ExecuteCloudBelief();
                break;
            case BossPatternKind.BurnBelief:
                yield return ExecuteBurnBelief();
                break;
            case BossPatternKind.AttackAndDodgeBelief:
                yield return ExecuteAttackAndDodgeBelief();
                break;
            case BossPatternKind.NoAttackBelief:
                yield return ExecuteNoAttackBelief();
                break;
            case BossPatternKind.ShockwaveBelief:
                yield return ExecuteShockwaveBelief();
                break;
        }
    }

    private IEnumerator ExecuteMeleeCombo()
    {
        int hitCount = UnityEngine.Random.Range(2, 4);
        for (int i = 0; i < hitCount && !isDead && !isVulnerable; i++)
        {
            yield return PerformMeleeSwing(attackWindup, meleeDamage, BossAttackKind.Melee);
            if (i < hitCount - 1 && attackRecovery > 0f)
                yield return new WaitForSeconds(attackRecovery / CurrentPhaseSpeedMultiplier);
        }
    }

    private IEnumerator ExecuteDash()
    {
        FacePlayer();
        int attackId = BeginAttack(BossAttackKind.Dash, meleeRange + 3f, meleeVerticalRange + 0.8f);
        yield return WaitAndTrackThreat(attackWindup, meleeRange + 3f, meleeVerticalRange + 0.8f);
        if (isDead || isVulnerable)
            yield break;

        SetStatus("Dash");
        float elapsed = 0f;
        float direction = GetFacingDirection();
        while (elapsed < dashDuration / CurrentPhaseSpeedMultiplier && !isDead && !isVulnerable)
        {
            body.linearVelocity = new Vector2(direction * dashSpeed * CurrentPhaseSpeedMultiplier, body.linearVelocity.y);
            if (IsTargetWithinAttackRange(0.9f, meleeVerticalRange))
                attackThreatened = true;
            if (!attackHit && bodyCollider.bounds.Intersects(playerBodyCollider.bounds))
                ResolveBossAttackHit(attackId, player.gameObject, dashDamage, true);
            elapsed += Time.deltaTime;
            yield return null;
        }

        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        ReportAttackEvadedIfNeeded(attackId);
        if (attackRecovery > 0f)
            yield return new WaitForSeconds(attackRecovery / CurrentPhaseSpeedMultiplier);
    }

    private IEnumerator ExecuteProjectile()
    {
        FacePlayer();
        int attackId = BeginAttack(BossAttackKind.Projectile, 40f, 30f);
        yield return WaitAndTrackThreat(attackWindup, 40f, 30f);
        if (isDead || isVulnerable)
            yield break;

        Vector2 spawn = transform.position + Vector3.up * 0.25f + Vector3.right * GetFacingDirection() * 0.7f;
        Vector2 direction = player != null
            ? ((Vector2)player.position + Vector2.up * 0.25f - spawn).normalized
            : Vector2.right * GetFacingDirection();
        GameObject projectileObject = CreateTransientObject("BossProjectile", spawn);
        SpriteRenderer renderer = projectileObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetPlaceholderSprite();
        renderer.color = new Color(0.95f, 0.76f, 0.25f, 1f);
        renderer.sortingOrder = 14;
        projectileObject.transform.localScale = Vector3.one * 0.42f;
        Rigidbody2D projectileBody = projectileObject.AddComponent<Rigidbody2D>();
        projectileBody.bodyType = RigidbodyType2D.Kinematic;
        projectileBody.gravityScale = 0f;
        CircleCollider2D projectileCollider = projectileObject.AddComponent<CircleCollider2D>();
        projectileCollider.isTrigger = true;
        projectileCollider.radius = 0.45f;
        BossAttackProjectile projectile = projectileObject.AddComponent<BossAttackProjectile>();
        projectile.Initialize(this, attackId, direction, projectileSpeed * CurrentPhaseSpeedMultiplier, projectileDamage, projectileLifetime);
        SetStatus("Projectile");

        float wait = projectileLifetime + 0.1f;
        while (wait > 0f && !isDead && !isVulnerable)
        {
            wait -= Time.deltaTime;
            yield return null;
        }

        if (!isDead && !isVulnerable)
            ReportAttackEvadedIfNeeded(attackId);
    }

    private IEnumerator ExecuteAttackIntentBelief()
    {
        activeBelief = BossBeliefKind.AttackIntent;
        SetStatus(ActiveBeliefPhrase);
        BeginAttackSignalWindow();
        yield return WaitAndTrackThreat(stareDuration, 0f, 0f);
        bool playerActed = EndAttackSignalWindow();
        if (!playerActed)
            ReduceBelief(beliefReductionPerCondition, "Belief decreased: player did not attack");
    }

    private IEnumerator ExecuteCloudBelief()
    {
        activeBelief = BossBeliefKind.Cloud;
        cloudTouched = false;
        SetStatus(ActiveBeliefPhrase);
        Vector3 center = player != null ? player.position : transform.position + Vector3.right * 2f;
        float cloudY = Mathf.Max(transform.position.y + 2.5f, center.y + 2.2f);
        GameObject blackCloud = CreateCloud(new Vector2(center.x - 1.4f, cloudY), false);
        GameObject whiteCloud = CreateCloud(new Vector2(center.x + 1.4f, cloudY), true);

        yield return new WaitForSeconds(cloudPatternDuration);
        if (!cloudTouched)
            ReduceBelief(beliefReductionPerCondition, "Belief decreased: neither cloud was reached");
        DestroyTransient(blackCloud);
        DestroyTransient(whiteCloud);
    }

    private IEnumerator ExecuteBurnBelief()
    {
        activeBelief = BossBeliefKind.Burn;
        SetStatus(ActiveBeliefPhrase);
        yield return new WaitForSeconds(firePreparationDuration);
        if (isDead || isVulnerable)
            yield break;

        burnWindowEndsAt = Time.time + burnApplicationWindow;
        BossPatternKind attackChoice = ChooseRegularAttack();
        RecordPatternUse(attackChoice);
        if (attackChoice == BossPatternKind.MeleeCombo)
            yield return ExecuteMeleeCombo();
        else if (attackChoice == BossPatternKind.Dash)
            yield return ExecuteDash();
        else
            yield return ExecuteProjectile();
    }

    private IEnumerator ExecuteAttackAndDodgeBelief()
    {
        activeBelief = BossBeliefKind.AttackAndDodge;
        SetStatus(ActiveBeliefPhrase);
        yield return new WaitForSeconds(stareDuration);
    }

    private IEnumerator ExecuteNoAttackBelief()
    {
        activeBelief = BossBeliefKind.NoAttack;
        SetStatus(ActiveBeliefPhrase);
        BeginAttackSignalWindow();
        yield return WaitAndTrackThreat(stareDuration, 0f, 0f);
        bool playerActed = EndAttackSignalWindow();
        if (playerActed)
            ReduceBelief(beliefReductionPerCondition, "Belief decreased: player attacked during the stare");
        if (isDead || isVulnerable)
            yield break;

        yield return PerformMeleeSwing(Mathf.Min(0.18f, attackWindup), meleeDamage * 1.15f, BossAttackKind.Melee);
    }

    private IEnumerator ExecuteShockwaveBelief()
    {
        activeBelief = BossBeliefKind.Shockwave;
        SetStatus(ActiveBeliefPhrase);
        yield return PerformMeleeSwing(attackWindup, meleeDamage, BossAttackKind.Melee);
        if (isDead || isVulnerable)
            yield break;

        yield return ExecuteShockwave();
    }

    private IEnumerator PerformMeleeSwing(float windup, float damage, BossAttackKind attackKind)
    {
        FacePlayer();
        int attackId = BeginAttack(attackKind, meleeRange + 0.6f, meleeVerticalRange);
        yield return WaitAndTrackThreat(windup, meleeRange + 0.6f, meleeVerticalRange);
        if (isDead || isVulnerable)
            yield break;

        float direction = GetFacingDirection();
        Vector2 boxSize = new Vector2(meleeRange, meleeVerticalRange * 2f);
        Vector2 center = (Vector2)transform.position + Vector2.right * direction * meleeRange * 0.55f;
        Bounds attackBounds = new Bounds(center, boxSize);
        if (playerBodyCollider != null && attackBounds.Intersects(playerBodyCollider.bounds))
            ResolveBossAttackHit(attackId, player.gameObject, damage, true);

        ReportAttackEvadedIfNeeded(attackId);
        if (attackRecovery > 0f)
            yield return new WaitForSeconds(attackRecovery / CurrentPhaseSpeedMultiplier);
    }

    private IEnumerator ExecuteShockwave()
    {
        int attackId = BeginAttack(BossAttackKind.Shockwave, shockwaveRadius, shockwaveRadius);
        float radius = 0.2f;
        float previousRadius = 0f;
        float duration = shockwaveRadius / (shockwaveExpansionSpeed * CurrentPhaseSpeedMultiplier);
        GameObject visual = CreateTransientObject("ExpandingShockwave", transform.position);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = GetPlaceholderSprite();
        renderer.color = new Color(1f, 0.82f, 0.32f, 0.22f);
        renderer.sortingOrder = 11;

        float elapsed = 0f;
        while (elapsed < duration && !isDead && !isVulnerable)
        {
            previousRadius = radius;
            radius = Mathf.Min(shockwaveRadius, radius + shockwaveExpansionSpeed * CurrentPhaseSpeedMultiplier * Time.deltaTime);
            visual.transform.position = transform.position;
            visual.transform.localScale = Vector3.one * radius * 2f;

            if (playerBodyCollider != null)
            {
                float distance = Vector2.Distance(transform.position, playerBodyCollider.bounds.center);
                float bodyRadius = Mathf.Max(playerBodyCollider.bounds.extents.x, playerBodyCollider.bounds.extents.y);
                if (distance <= shockwaveRadius + bodyRadius)
                    attackThreatened = true;
                bool crossedPlayer = distance + bodyRadius >= previousRadius && distance - bodyRadius <= radius;
                if (!attackHit && crossedPlayer)
                    ResolveBossAttackHit(attackId, player.gameObject, shockwaveDamage, false);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        ReportAttackEvadedIfNeeded(attackId);
        DestroyTransient(visual);
    }

    private IEnumerator WaitAndTrackThreat(float duration, float horizontalRange, float verticalRange)
    {
        float elapsed = 0f;
        while (elapsed < duration && !isDead && !isVulnerable)
        {
            if (horizontalRange > 0f && IsTargetWithinAttackRange(horizontalRange, verticalRange))
                attackThreatened = true;
            elapsed += Time.deltaTime * CurrentPhaseSpeedMultiplier;
            yield return null;
        }
    }

    private int BeginAttack(BossAttackKind kind, float threatRange, float verticalRange)
    {
        activeBossAttackId = nextBossAttackId++;
        activeBossAttackKind = kind;
        attackHit = false;
        attackEvadeReported = false;
        attackThreatened = IsTargetWithinAttackRange(threatRange, verticalRange);
        return activeBossAttackId;
    }

    private void ReportAttackEvadedIfNeeded(int attackId)
    {
        if (attackId <= 0 || attackId != activeBossAttackId || !attackThreatened || attackHit || attackEvadeReported)
            return;

        if (playerCombatEvents != null)
            playerCombatEvents.PublishBossAttackEvaded(attackId, gameObject);
        else
            HandleAttackEvaded(attackId);
    }

    private void HandleAttackEvaded(int attackId)
    {
        if (attackId != activeBossAttackId || !attackThreatened || attackHit || attackEvadeReported)
            return;

        attackEvadeReported = true;
        if (phase == BossPhase.One && activeBelief == BossBeliefKind.Burn && Time.time <= burnWindowEndsAt)
        {
            ReduceBelief(beliefReductionPerCondition, "Belief decreased: player evaded a burning attack");
        }
        else if (phase == BossPhase.Two && activeBelief == BossBeliefKind.AttackAndDodge)
        {
            ReduceBelief(beliefReductionPerCondition, "Belief decreased: player evaded an attack");
        }
        else if (phase == BossPhase.Two && activeBelief == BossBeliefKind.Shockwave &&
                 activeBossAttackKind == BossAttackKind.Shockwave)
        {
            ReduceBelief(beliefReductionPerCondition, "Belief decreased: player evaded the shockwave");
        }
        else
        {
            StatusChanged?.Invoke();
        }
    }

    private bool IsTargetWithinAttackRange(float horizontalRange, float verticalRange)
    {
        if (playerBodyCollider == null)
            return false;

        return Mathf.Abs(playerBodyCollider.bounds.center.x - transform.position.x) <= horizontalRange &&
               Mathf.Abs(playerBodyCollider.bounds.center.y - transform.position.y) <= verticalRange;
    }

    private void BeginAttackSignalWindow()
    {
        attackSignalIdsInWindow.Clear();
        playerActedDuringSignalWindow = false;
        attackSignalWindowOpen = true;
    }

    private bool EndAttackSignalWindow()
    {
        attackSignalWindowOpen = false;
        return playerActedDuringSignalWindow;
    }

    private GameObject CreateCloud(Vector2 position, bool isWhite)
    {
        GameObject cloud = CreateTransientObject(isWhite ? "WhitePlatformCloud" : "BlackPassThroughCloud", position);
        SpriteRenderer renderer = cloud.AddComponent<SpriteRenderer>();
        renderer.sprite = GetPlaceholderSprite();
        renderer.color = isWhite
            ? new Color(0.93f, 0.95f, 1f, 1f)
            : new Color(0.15f, 0.17f, 0.22f, 0.9f);
        renderer.sortingOrder = 8;
        cloud.transform.localScale = new Vector3(1.6f, 0.28f, 1f);

        BoxCollider2D collider = cloud.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        collider.isTrigger = !isWhite;
        BossCloudPlatform platform = cloud.AddComponent<BossCloudPlatform>();
        platform.Initialize(this, isWhite);
        return cloud;
    }

    private IEnumerator SpawnSpikesAfterDrop(Vector2 dropPoint, int generation)
    {
        yield return new WaitForSeconds(spikeDelay);
        if (generation != effectGeneration || isDead || isVulnerable)
            yield break;

        float floorY = 0f;
        RaycastHit2D[] hits = Physics2D.RaycastAll(
            new Vector2(dropPoint.x, dropPoint.y + 4f),
            Vector2.down,
            40f);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger ||
                (player != null && hit.collider.transform.root == player) ||
                hit.collider.transform.root == transform)
            {
                continue;
            }

            floorY = hit.point.y;
            break;
        }

        GameObject spikes = CreateTransientObject("BeliefSpikes", new Vector2(dropPoint.x, floorY + 0.45f));
        SpriteRenderer renderer = spikes.AddComponent<SpriteRenderer>();
        renderer.sprite = GetPlaceholderSprite();
        renderer.color = new Color(0.8f, 0.19f, 0.27f, 1f);
        renderer.sortingOrder = 13;
        spikes.transform.localScale = new Vector3(1.25f, 0.8f, 1f);
        BoxCollider2D collider = spikes.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        spikes.AddComponent<BossTimedDamageHazard>().Initialize(spikeDamage, 2f);
    }

    private void ApplyBurn(PlayerHealth targetHealth)
    {
        if (targetHealth == null)
            return;

        BossBurnEffect effect = targetHealth.GetComponent<BossBurnEffect>();
        if (effect == null)
            effect = targetHealth.gameObject.AddComponent<BossBurnEffect>();
        effect.Apply(burnDamagePerTick, burnDuration, burnTickInterval);
    }

    private GameObject CreateTransientObject(string objectName, Vector3 position)
    {
        GameObject created = new GameObject(objectName);
        created.transform.position = position;
        transientObjects.Add(created);
        return created;
    }

    private void DestroyTransient(GameObject targetObject)
    {
        if (targetObject == null)
            return;
        targetObject.SetActive(false);
        transientObjects.Remove(targetObject);
        Destroy(targetObject);
    }

    private void CleanupTransientObjects()
    {
        effectGeneration++;
        foreach (GameObject transient in transientObjects)
        {
            if (transient == null)
                continue;
            transient.SetActive(false);
            Destroy(transient);
        }
        transientObjects.Clear();
    }

    private void CancelCurrentPattern()
    {
        if (patternRoutine == null)
            return;
        StopCoroutine(patternRoutine);
        patternRoutine = null;
        hasActivePattern = false;
        attackSignalWindowOpen = false;
        if (body != null)
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    private void Die()
    {
        if (isDead)
            return;
        isDead = true;
        encounterStarted = false;
        isVulnerable = false;
        StopAllCoroutines();
        patternRoutine = null;
        vulnerabilityRoutine = null;
        attackSignalWindowOpen = false;
        if (body != null)
            body.linearVelocity = Vector2.zero;
        CleanupTransientObjects();
        if (bodyCollider != null)
            bodyCollider.enabled = false;
        SetStatus("Boss defeated");
    }

    private void SetStatus(string message)
    {
        statusMessage = message;
        StatusChanged?.Invoke();
    }

    private void FacePlayer()
    {
        if (player == null)
            return;
        float direction = player.position.x - transform.position.x;
        if (Mathf.Abs(direction) < 0.01f)
            return;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction);
        transform.localScale = scale;
    }

    private float GetFacingDirection()
    {
        return transform.localScale.x >= 0f ? 1f : -1f;
    }

    private string GetBeliefPhrase(BossBeliefKind kind)
    {
        switch (kind)
        {
            case BossBeliefKind.AttackIntent:
            case BossBeliefKind.AttackAndDodge:
                return "너는 날 공격할 것이다.";
            case BossBeliefKind.Cloud:
                return "너는 구름 위에 올라갈 것이다.";
            case BossBeliefKind.Burn:
                return "너는 불타게 될 것이다.";
            case BossBeliefKind.NoAttack:
                return "너는 날 공격하지 않을 것이다.";
            case BossBeliefKind.Shockwave:
                return "너는 피하지 못할 것이다.";
            default:
                return "";
        }
    }

    private void BuildPlaceholderVisual()
    {
        if (GetComponentInChildren<SpriteRenderer>() != null)
            return;

        GameObject robe = CreateVisualPart("WhiteAndGoldRobe", Vector3.zero, new Vector3(0.95f, 1.45f, 1f), Color.white, 10);
        robeRenderer = robe.GetComponent<SpriteRenderer>();
        CreateVisualPart("GoldTrim", new Vector3(0f, -0.15f, -0.02f), new Vector3(0.72f, 0.12f, 1f), new Color(0.85f, 0.65f, 0.18f), 11);
        CreateVisualPart("Face", new Vector3(0f, 0.92f, 0f), new Vector3(0.42f, 0.43f, 1f), new Color(0.93f, 0.78f, 0.65f), 12);
        CreateVisualPart("WhiteHair", new Vector3(0f, 1.12f, -0.02f), new Vector3(0.5f, 0.22f, 1f), new Color(0.96f, 0.96f, 1f), 13);
        CreateVisualPart("LeftGoldEye", new Vector3(-0.1f, 0.95f, -0.03f), new Vector3(0.07f, 0.035f, 1f), new Color(0.93f, 0.72f, 0.12f), 14);
        CreateVisualPart("RightGoldEye", new Vector3(0.1f, 0.95f, -0.03f), new Vector3(0.07f, 0.035f, 1f), new Color(0.93f, 0.72f, 0.12f), 14);
        bodyCollider.size = new Vector2(0.85f, 1.9f);
    }

    private GameObject CreateVisualPart(string partName, Vector3 localPosition, Vector3 localScale, Color color, int sortingOrder)
    {
        GameObject part = new GameObject(partName);
        part.transform.SetParent(transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = GetPlaceholderSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return part;
    }

    private static Sprite GetPlaceholderSprite()
    {
        if (placeholderSprite == null)
            placeholderSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        return placeholderSprite;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.78f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, shockwaveRadius);
    }

    private void OnDestroy()
    {
        if (playerCombatEvents != null)
            playerCombatEvents.SignalPublished -= HandlePlayerSignal;
        CleanupTransientObjects();
    }
}
