using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
[RequireComponent(typeof(EnemyHitReaction))]
public sealed class BossCombatController : MonoBehaviour, IDamageable, IHealth, ICombatGeneration
{
    [SerializeField] private bool startOnAwake;
    [SerializeField] private BossCombatSettings settings;
    [SerializeField] private BossAttackExecutor attacks;
    [SerializeField] private BossCombatView presentation;
    [SerializeField] private BossCombatHud hud;
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private Transform projectileOrigin;
    [SerializeField] private bool automaticPatterns = true;
    [SerializeField] private bool allowManualEvadeInjection;
    [SerializeField] private bool showAttackGizmos;

    private const int LaneRegular = 1, LaneBelief = 2;
    private readonly Dictionary<BossPatternKind, int> regularUses = new Dictionary<BossPatternKind, int>();
    private readonly Dictionary<BossPatternKind, int> beliefUses = new Dictionary<BossPatternKind, int>();
    private readonly Dictionary<int, BossAttackRecord> activeAttacks = new Dictionary<int, BossAttackRecord>();
    private readonly List<BossAttackRecord> history = new List<BossAttackRecord>();
    private readonly List<BossProphecy> prophecies = new List<BossProphecy>();
    private readonly List<BossBeliefKind> beliefOrder = new List<BossBeliefKind>(4);
    private readonly List<GameObject> effects = new List<GameObject>();
    private readonly List<float> candidateWeights = new List<float>(9);
    private System.Predicate<BossPatternKind> eligibleAny;
    private float nextRegularAt, nextBeliefAt;
    private Rigidbody2D body;
    private EnemyHitReaction hitReaction;
    private CapsuleCollider2D bodyCollider;
    private PlayerHealth playerHealth;
    private PlayerAttack playerAttack;
    private PlayerCombatEvents playerEvents;
    private EnemyContactDamage[] contactDamageComponents;
    private BossSharedBelief belief;
    // Two independent lanes: regular attacks and beliefs never share a routine, pattern id, cancel flag or cooldown.
    private Coroutine regularRoutine, beliefRoutine;
    private int advancingLanes;
    private Transform effectRoot;
    private BossCloudField clouds;
    private BossPatternKind activeRegular, activeBeliefPattern;
    private bool regularRunning, regularAttackActive, beliefProcessing, shockwaveReserved, bodyActionActive, initialized;
    private bool phaseCutscenePlaying;
    private Coroutine phaseCutscene;
    private CutsceneBubbleView phaseBubble;
    private readonly object phaseCutsceneLock = new object();
    private static readonly string[] PhaseTwoLines = { "여전하네. 하나도 변하지 않았어.", "...참 짓궂게." };
    private static readonly string[] DeathLines =
    {
        "...이상하네.",
        "분명 다시 만나면 할 말이 많았는데...",
        "막상 네 얼굴을 보니",
        "아무것도 생각나지가 않아.",
        "......",
        "네게는 이곳이 그저 늘러붙은 낙인에 불과했던 걸까.",
        "그게 조금은 억울해.",
        "정말 조금은...."
    };
    private bool beliefAwaitingEnd;
    private BossRegularAttackContext runningRegularContext;
    private string phraseCache = "";
    private int phraseSignature = -1;
    private float encounterStartedAt, burnEndsAt;
    [Tooltip("How long (seconds) the player must stay on the opposite side before the boss turns toward them.")]
    [SerializeField, Min(0f)] private float turnDelaySeconds = 0.5f;
    // Pending player-tracking turn: side (+1/-1, 0 = none), since when, and when it was last evaluated.
    private int pendingTurn;
    private float pendingTurnSince, pendingTurnLastSeen;
    private const float PendingTurnStaleSeconds = 0.25f;
    private int generation = 1, regularPatternId, beliefPatternId, patternCounter, nextAttackId = 1, facing = 1, initialFacing;
    private int lastAcceptedComboPatternId = -1;
    private float currentHp;
    private Vector3 spawnPosition, playerSpawnPosition, playerSpawnScale;
    private bool savedPlayerSpawn;
    private string statusMessage = "전투 대기";
    private BossEncounterTrigger arena;
    private float cueEndsAt, cueDuration, feedbackEndsAt;
    private string cueTitle = "전투 대기", cueHint = "공격 예고를 보고 대응하세요.", feedback;
    private Color feedbackColor = Color.white;

    public event Action StatusChanged;
    public event Action<BossAttackRecord> AttackResolved;
    public BossCombatSettings Settings => settings;
    public Rigidbody2D Body => body;
    public EnemyHitReaction HitReaction => hitReaction;
    public bool IsHitMovementLocked => hitReaction != null && hitReaction.IsMovementSuppressed;
    public Collider2D BodyCollider => bodyCollider;
    public SpriteRenderer BodyRenderer => presentation != null ? presentation.BodyRenderer : null;
    internal BossCombatView Presentation => presentation;
    public PlayerHealth PlayerHealth => playerHealth;
    public PlayerAttack PlayerAttack => playerAttack;
    public Collider2D PlayerCollider => playerHealth != null ? playerHealth.BodyCollider : null;
    public BossPhase Phase { get; private set; }
    public bool IsDead { get; private set; }
    public bool EncounterStarted { get; private set; }
    public bool WaitingForArenaEntry => arena != null && !arena.HasEntered;
    public Vector2 PatternXLimits => arena != null ? arena.PatternXLimits : settings.ArenaX;
    public BossArenaCamera ArenaCamera => arena != null ? arena.ArenaCamera : null;
    internal void BindArena(BossEncounterTrigger owner) { arena = owner; }
    internal void UnbindArena(BossEncounterTrigger owner)
    {
        if (arena != owner) return;
        arena = null;
        ReleaseArenaVisuals();
    }
    public bool IsVulnerable => belief != null && belief.IsVulnerable;
    /// <summary>Any lane busy: a regular attack, a belief being activated, or a lightning still in progress.</summary>
    public bool IsPatternRunning => regularRunning || beliefProcessing || (clouds != null && clouds.LightningActive);
    public bool IsRegularRunning => regularRunning;
    public bool IsBeliefProcessing => beliefProcessing;
    public bool HasActiveBelief => beliefProcessing || prophecies.Count > 0 || HasBurnBelief ||
        (clouds != null && clouds.LightningActive) || shockwaveReserved || bodyActionActive ||
        (attacks != null && attacks.ShockwaveTracking > 0);
    private bool HasBurnBelief
    {
        get
        {
            if (IsBurning || runningRegularContext != null && runningRegularContext.BurnEnhanced) return true;
            foreach (BossAttackRecord record in activeAttacks.Values)
                if (record.BurnEnhanced) return true;
            return false;
        }
    }
    /// <summary>
    /// True while an attack whose warning is laid out around the boss itself (melee, dash, projectile, ground wave)
    /// is announced but not active yet. Lightning warnings sit under the clouds and do not count.
    /// </summary>
    public bool IsAttackWarningActive
    {
        get
        {
            foreach (BossAttackRecord record in activeAttacks.Values)
            {
                bool ownerBased = record.Kind == BossAttackKind.Melee || record.Kind == BossAttackKind.Dash ||
                                  record.Kind == BossAttackKind.Projectile || record.Kind == BossAttackKind.Shockwave;
                if (ownerBased && !record.Activated && !record.IsComplete) return true;
            }
            return false;
        }
    }
    public bool IsShockwavePending => shockwaveReserved;
    public bool IsBodyActionActive => bodyActionActive;
    public BossCloudField Clouds => clouds;
    public IReadOnlyList<BossProphecy> Prophecies => prophecies;
    public bool HasProphecy(BossBeliefKind kind)
    {
        foreach (BossProphecy prophecy in prophecies) if (prophecy.Kind == kind) return true;
        return false;
    }
    public bool AutomaticPatterns { get => automaticPatterns; set { automaticPatterns = value; Notify(); } }
    public bool AllowManualEvadeInjection { get => allowManualEvadeInjection; set => allowManualEvadeInjection = value; }
    public int Generation => generation;
    public int CombatGeneration => generation;
    /// <summary>Id of the latest regular attack (records of a combo share it).</summary>
    public int PatternId => regularPatternId;
    public int BeliefPatternId => beliefPatternId;
    public int Facing => facing;
    public float CurrentHp => currentHp;
    public float MaxHp => settings != null ? settings.MaxHp : 0f;
    float IHealth.CurrentHealth => currentHp;
    float IHealth.MaxHealth => MaxHp;
    void IHealth.TakeDamage(float amount, GameObject source)
    {
        TakeDamage(new DamageInfo(amount, DamageSource.Doctrine, source));
    }
    void IHealth.Heal(float amount)
    {
        if (IsDead || amount <= 0f)
            return;
        currentHp = Mathf.Max(0f, Mathf.Min(MaxHp, currentHp + amount));
        if (!IsLowHealthForDoctrine)
            lowHealthNotified = false;
        Notify();
    }
    private bool lowHealthNotified;
    private bool IsLowHealthForDoctrine => !IsDead && currentHp <= MaxHp * 0.3f;
    public float CurrentBelief => belief != null ? belief.Current : 0f;
    public float MaxBelief => belief != null ? belief.Maximum : 0f;
    public float NormalizedBelief => MaxBelief > 0f ? CurrentBelief / MaxBelief : 0f;
    public float VulnerableRemaining => belief != null ? belief.Remaining(Time.time) : 0f;
    public float BurnRemaining => Mathf.Max(0f, burnEndsAt - Time.time);
    public bool IsBurning => EncounterStarted && BurnRemaining > 0f;
    public float SpeedMultiplier => Phase == BossPhase.Two ? settings.PhaseTwoSpeedMultiplier : 1f;
    public float DamageMultiplier
    {
        get
        {
            float phaseMultiplier = Phase == BossPhase.Two ? settings.PhaseTwoDamageMultiplier : 1f;
            DoctrineStats stats = GetComponent<DoctrineStats>();
            float doctrineMultiplier = stats != null ? stats.AttackMultiplier : 1f;
            return phaseMultiplier * doctrineMultiplier;
        }
    }
    public bool IsPhaseCutscenePlaying => phaseCutscenePlaying;
    public bool IsDeathDialoguePlaying { get; private set; }
    public string ActivePatternName => regularRunning && beliefProcessing ? PatternName(activeRegular) + " + " + PatternName(activeBeliefPattern) :
        regularRunning ? PatternName(activeRegular) : beliefProcessing ? PatternName(activeBeliefPattern) : "대기";
    public BossPatternKind ActivePattern => regularRunning || !beliefProcessing ? activeRegular : activeBeliefPattern;
    public BossPatternKind ActiveRegularPattern => activeRegular;
    public BossPatternKind ActiveBeliefPattern => activeBeliefPattern;
    /// <summary>현재 유지 중인 믿음 한 종류의 문구.</summary>
    public string ActiveBeliefPhrase
    {
        get
        {
            int signature = 0;
            for (int i = beliefOrder.Count - 1; i >= 0; i--)
                if (!IsBeliefActive(beliefOrder[i])) beliefOrder.RemoveAt(i);
            foreach (BossBeliefKind kind in beliefOrder) signature = signature * 8 + (int)kind;
            if (signature != phraseSignature)
            {
                phraseSignature = signature;
                var phrases = new List<string>(beliefOrder.Count);
                foreach (BossBeliefKind kind in beliefOrder)
                {
                    string phrase = BeliefPhrase(kind);
                    if (phrase.Length > 0 && !phrases.Contains(phrase)) phrases.Add(phrase);
                }
                phraseCache = string.Join(" ", phrases);
            }
            return phraseCache;
        }
    }
    public bool IsBeliefActive(BossBeliefKind kind)
    {
        switch (kind)
        {
            case BossBeliefKind.AttackIntent: case BossBeliefKind.NoAttack: case BossBeliefKind.AttackAndDodge: return HasProphecy(kind);
            case BossBeliefKind.Cloud: return clouds != null && clouds.LightningActive;
            case BossBeliefKind.Burn: return HasBurnBelief;
            case BossBeliefKind.Shockwave: return shockwaveReserved || bodyActionActive || (attacks != null && attacks.ShockwaveTracking > 0);
            default: return false;
        }
    }
    private void NoteBelief(BossBeliefKind kind) { beliefOrder.Clear(); beliefOrder.Add(kind); }
    public Font PresentationFont => hud.KoreanFont;
    public bool DebugUiVisible => hud != null && hud.DebugVisible;
    public string CueTitle => cueTitle;
    public string CueHint => cueHint;
    public BossPresentationState CueState { get; private set; }
    public float CueRemaining => Mathf.Max(0f, cueEndsAt - Time.time);
    public float CueProgress => cueDuration > 0f ? Mathf.Clamp01(1f - CueRemaining / cueDuration) : 0f;
    public bool HasCueTimer => cueDuration > 0f;
    public string Feedback => Time.time < feedbackEndsAt ? feedback : "";
    public Color FeedbackColor => feedbackColor;
    public string StatusMessage => statusMessage;
    public IReadOnlyList<BossAttackRecord> AttackHistory => history;
    public ICollection<BossAttackRecord> ActiveAttacks => activeAttacks.Values;
    public int ActiveBossAttackId
    {
        get { int latest = 0; foreach (int id in activeAttacks.Keys) latest = Mathf.Max(latest, id); return latest; }
    }
    public Vector2 AttackOrigin => attackOrigin.position;
    public Vector2 ProjectileOrigin => projectileOrigin.position;
    /// <summary>Muzzle position as it will be once the boss faces <paramref name="forFacing"/>.</summary>
    public Vector2 ProjectileOriginFor(int forFacing)
    {
        Vector3 local = projectileOrigin.localPosition;
        local.x = Mathf.Abs(local.x) * forFacing;
        return projectileOrigin.parent != null ? (Vector2)projectileOrigin.parent.TransformPoint(local) : (Vector2)local;
    }
    public bool TargetValid => playerHealth != null && !playerHealth.IsDead && playerHealth.isActiveAndEnabled && PlayerCollider != null && PlayerCollider.enabled;
    public bool CanContinue => isActiveAndEnabled && EncounterStarted && !IsDead && !phaseCutscenePlaying && TargetValid;
    public bool CanStartNextHit => CanContinue;

    private void Awake()
    {
        eligibleAny = IsPatternEligible;
        body = GetComponent<Rigidbody2D>();
        hitReaction = GetComponent<EnemyHitReaction>();
        if (hitReaction == null) hitReaction = gameObject.AddComponent<EnemyHitReaction>();
        bodyCollider = GetComponent<CapsuleCollider2D>();
        contactDamageComponents = GetComponentsInChildren<EnemyContactDamage>(true);
        SetContactDamageSuppressed(true);
        spawnPosition = transform.position;
        initialFacing = facing;
        InitializeState();
    }
    private void InitializeState()
    {
        if (settings == null || initialized) return;
        currentHp = settings.MaxHp;
        belief = new BossSharedBelief(settings.MaxBelief);
        initialized = true;
    }
    // A boss bound to an arena starts only through the arena (or an explicit BeginEncounter after an intro).
    private void Start() { if (startOnAwake && !EncounterStarted && arena == null) BeginEncounter(); }
    private void FixedUpdate()
    {
        if (EncounterStarted && !IsDead) hitReaction.TickScriptedMovement();
    }
    private void Update()
    {
        if (!EncounterStarted || IsDead || phaseCutscenePlaying) return;
        if (!TargetValid)
        {
            EncounterStarted = false;
            SetContactDamageSuppressed(true);
            CancelCombat(true);
            SetStatus("플레이어 전투 종료 · R 재시작");
            return;
        }
        // 공격 동작 중이 아닐 때는 항상 플레이어 쪽을 바라본다.
        // (베기/돌진 판정이 이미 만들어진 뒤에 돌면 예고와 어긋나므로 공격이 끝난 뒤에만 돈다.)
        if (!regularAttackActive && !beliefProcessing && !bodyActionActive && !shockwaveReserved &&
            (clouds == null || !clouds.LightningActive))
            FacePlayerWhileIdle();
        if (belief.Tick(Time.time, settings.BeliefRecoveryRatio))
        {
            if (!regularRunning && !bodyActionActive)
                SetActionCue("믿음 회복", "다음 공격의 예고를 확인하세요.", BossPresentationState.Idle);
            SetStatus("믿음 회복");
        }
        float now = Time.time;
        if (beliefAwaitingEnd && !HasActiveBelief)
        {
            beliefAwaitingEnd = false;
            nextBeliefAt = now + settings.BeliefCooldown;
        }
        if (!automaticPatterns) return;
        if (!regularRunning && !shockwaveReserved && !bodyActionActive && now >= nextRegularAt)
        {
            if (ChoosePattern(true, out BossPatternKind regular)) StartRegular(regular);
            else { nextRegularAt = now + .1f; SetStatus("실행 가능한 일반 공격 없음 · 대상 거리/지면/설정 확인"); }
        }
        if (EncounterStarted && !HasActiveBelief && !beliefAwaitingEnd && now >= nextBeliefAt)
        {
            if (ChoosePattern(false, out BossPatternKind beliefPattern)) StartBelief(beliefPattern);
            else nextBeliefAt = now + .1f;
        }
    }
    public List<string> ValidateEncounter()
    {
        var errors = settings != null ? settings.ValidateSettings() : new List<string> { "Boss.settings is missing." };
        if (attacks == null) errors.Add("Boss.attacks is missing.");
        if (presentation == null || !presentation.IsConfigured) errors.Add("Boss.presentation / VisualRoot / body renderer references are missing.");
        if (hud == null || !hud.IsConfigured) errors.Add("Boss.hud / gameplay health, belief and speech references are missing.");
        else if (!hud.HasKoreanFont) errors.Add("Boss.hud requires a Korean font (preferredFont / installed OS fonts).");
        if (attackOrigin == null || projectileOrigin == null) errors.Add("Boss.AttackOrigin / ProjectileOrigin is missing.");
        if (transform.localScale != Vector3.one) errors.Add("Boss physics root scale must be (1,1,1).");
        return errors;
    }
    public void BeginEncounter()
    {
        if (!isActiveAndEnabled || IsDead || EncounterStarted || WaitingForArenaEntry) return;
        InitializeState();
        List<string> errors = ValidateEncounter();
        if (errors.Count > 0)
        {
            SetStatus("보스 설정 오류 · 전투 중지");
            Debug.LogError(name + ": cannot start boss encounter:\n" + string.Join("\n", errors), this);
            return;
        }
        if (!BindPlayer())
        {
            SetStatus("PlayerHealth, 콤보 데이터, 몸체 콜라이더 필요");
            Debug.LogError(name + ": tagged Player requires PlayerHealth, a multi-step PlayerAttack, a non-trigger body Collider2D and PlayerCombatEvents.", this);
            return;
        }
        attacks.Initialize(this);
        presentation.Initialize(this);
        hud.Initialize(this);
        encounterStartedAt = Time.time;
        PrepareArenaVisuals();
        nextRegularAt = 0f;
        nextBeliefAt = Time.time + settings.BeliefInitialDelay;
        EncounterStarted = true;
        SetContactDamageSuppressed(false);
        SetPresentation(BossPresentationState.Idle);
        SetStatus("전투 시작");
    }
    /// <summary>
    /// Idempotent: creates the persistent storm clouds and white platforms for this arena (never starts combat,
    /// contact damage, patterns or lightning). Safe to call before BeginEncounter, e.g. while an intro cutscene plays.
    /// </summary>
    public bool PrepareArenaVisuals()
    {
        if (IsDead || settings == null || !isActiveAndEnabled || bodyCollider == null) return false;
        if (clouds == null)
        {
            clouds = GetComponent<BossCloudField>();
            if (clouds == null) clouds = gameObject.AddComponent<BossCloudField>();
        }
        return clouds.Prepare(this);
    }

    /// <summary>Removes the arena clouds and any lightning (arena released, boss dead, scene ending).</summary>
    public void ReleaseArenaVisuals()
    {
        if (clouds != null) clouds.Release();
    }

    private bool BindPlayer()
    {
        GameObject actor = GameObject.FindGameObjectWithTag("Player");
        if (actor == null) return false;
        if (playerEvents != null) playerEvents.SignalPublished -= HandlePlayerSignal;
        playerHealth = actor.GetComponent<PlayerHealth>();
        playerAttack = actor.GetComponent<PlayerAttack>();
        playerEvents = actor.GetComponent<PlayerCombatEvents>();
        if (playerHealth == null || playerAttack == null || playerAttack.ComboCount < 2 ||
            PlayerCollider == null || PlayerCollider.isTrigger || playerEvents == null) return false;
        playerEvents.SignalPublished += HandlePlayerSignal;
        Physics2D.IgnoreCollision(bodyCollider, PlayerCollider, true);
        if (!savedPlayerSpawn)
        {
            playerSpawnPosition = actor.transform.position;
            playerSpawnScale = actor.transform.localScale;
            savedPlayerSpawn = true;
        }
        return true;
    }
    public bool TakeDamage(DamageInfo info)
    {
        if (!EncounterStarted || IsDead || phaseCutscenePlaying || info.Amount <= 0f || float.IsNaN(info.Amount) || float.IsInfinity(info.Amount)) return false;
        PlayerAttackContext attack = info.PlayerAttack;
        if (attack != null && (playerHealth == null || attack.Actor != playerHealth.gameObject || !attack.Begin(this))) return false;
        float damage = info.Amount * (info.Source == DamageSource.PlayerOwned ?
            (IsVulnerable ? settings.VulnerableDamageMultiplier : settings.MaintainedDamageMultiplier) : 1f);
        if (damage <= 0f) return false;
        if (attack != null && !attack.TryConsumeHit(this, info.HitIndex)) return false;
        float before = currentHp;
        float threshold = MaxHp * settings.PhaseTwoHealthRatio;
        currentHp = Mathf.Max(Phase == BossPhase.One ? threshold : 0f, currentHp - damage);
        PlayHitFeedback(info);
        if (currentHp <= 0f) Die();
        else if (Phase == BossPhase.One && currentHp <= threshold) EnterPhaseTwo();
        attack?.ConfirmHit(this, before - currentHp, info.HitIndex);
        if (DoctrineEventBus.HasListeners)
        {
            DoctrineEventBus.Raise(ConditionType.OnHit, gameObject, info.Attacker);
            if (!IsDead && !lowHealthNotified && IsLowHealthForDoctrine)
            {
                lowHealthNotified = true;
                DoctrineEventBus.Raise(ConditionType.LowHealth, gameObject, info.Attacker);
            }
        }
        Notify();
        return true;
    }
    // Same burst and white flash as ordinary enemies, scaled up for the boss body.
    private void PlayHitFeedback(DamageInfo info)
    {
        Bounds body = BodyCollider != null ? BodyCollider.bounds : new Bounds(transform.position, Vector3.one);
        Vector2 from = info.Attacker != null ? (Vector2)info.Attacker.transform.position : (Vector2)body.center - Vector2.right * Facing;
        Vector2 contact = BodyCollider != null ? BodyCollider.ClosestPoint(from) : (Vector2)body.center;
        CombatHitEffect.Spawn(Vector2.Lerp(contact, body.center, .4f), (Vector2)body.center - from, BodyRenderer, Mathf.Clamp(body.size.x / 1.2f, 1f, 2.2f));
        CombatSpriteFlash.Of(gameObject)?.Flash(.09f);
    }
    private void EnterPhaseTwo()
    {
        CancelCombat(false);
        Phase = BossPhase.Two;
        regularUses.Clear();
        beliefUses.Clear();
        SetPresentation(BossPresentationState.Idle);
        presentation.FlashPhaseChange();
        SetStatus("2페이즈");
        ShowFeedback("2페이즈 · 새로운 믿음 패턴", new Color(.6f, .8f, 1f));
        if (phaseCutscene != null) StopCoroutine(phaseCutscene);
        phaseCutscene = StartCoroutine(PlayPhaseTwoCutscene());
    }

    // 체력이 절반이 되면 보스와 플레이어 공격을 멈추고, 보스 머리 위 말풍선으로 두 대사를 보여 준다.
    // 클릭으로 현재 문장을 완성하고, 다음 클릭으로 넘긴다. 끝나면 2페이즈 전투가 다시 시작된다.
    private IEnumerator PlayPhaseTwoCutscene()
    {
        phaseCutscenePlaying = true;
        UiModal.Push(phaseCutsceneLock);
        if (body != null) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        Rigidbody2D playerBody = playerHealth != null ? playerHealth.GetComponent<Rigidbody2D>() : null;
        if (playerBody != null) playerBody.linearVelocity = new Vector2(0f, playerBody.linearVelocity.y);
        SetStatus("2페이즈 전환");

        yield return SpeakLines(PhaseTwoLines);
        EndSpokenLines();
        nextRegularAt = Time.time + 0.4f;
        nextBeliefAt = Time.time + (settings != null ? settings.BeliefInitialDelay : 0.4f);
        phaseCutscene = null;
        SetStatus("2페이즈");
        Notify();
    }
    public void ForcePhaseTwoForTest()
    {
        if (!EncounterStarted || IsDead || Phase == BossPhase.Two) return;
        currentHp = Mathf.Min(currentHp, MaxHp * settings.PhaseTwoHealthRatio);
        EnterPhaseTwo();
    }
    public void ForcePatternForTest(BossPatternKind pattern, bool cancelRunning = true)
    {
        if (!CanContinue) return;
        automaticPatterns = false;
        if (!BossPatternSelection.AllowedInPhase(pattern, Phase))
        {
            if (BossPatternSelection.AllowedInPhase(pattern, BossPhase.Two)) ForcePhaseTwoForTest();
            else { SetStatus("1페이즈 전용 패턴 · R 재시작 후 실행"); return; }
        }
        if (cancelRunning) CancelCombat(false);
        if (BossPatternSelection.IsRegular(pattern)) { if (!regularRunning) StartRegular(pattern); }
        else if (!beliefProcessing) StartBelief(pattern);
    }
    public void PauseAndCancelForTest()
    { automaticPatterns = false; CancelCombat(false); SetStatus("단일 패턴 대기"); }
    public void ResetEncounterForTest()
    {
        InitializeState();
        if (!initialized) { BeginEncounter(); return; }
        CancelCombat(true);
        IsDead = false;
        EncounterStarted = false;
        Phase = BossPhase.One;
        currentHp = MaxHp;
        belief.Reset();
        regularUses.Clear();
        beliefUses.Clear();
        history.Clear();
        feedbackEndsAt = 0f;
        facing = initialFacing;
        transform.position = spawnPosition;
        body.position = spawnPosition;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.simulated = true;
        bodyCollider.enabled = true;
        if (playerHealth != null && savedPlayerSpawn)
        {
            playerHealth.transform.SetPositionAndRotation(playerSpawnPosition, Quaternion.identity);
            playerHealth.transform.localScale = playerSpawnScale;
            Rigidbody2D playerBody = playerHealth.GetComponent<Rigidbody2D>();
            if (playerBody != null) { playerBody.position = playerSpawnPosition; playerBody.linearVelocity = Vector2.zero; playerBody.angularVelocity = 0f; }
            playerHealth.ResetHealth();
        }
        Physics2D.SyncTransforms();
        BeginEncounter();
        Face(facing);
        SetStatus("전투 재시작");
    }
    public void SetHpForTest(float hp)
    {
        if (IsDead || !EncounterStarted) return;
        currentHp = Mathf.Clamp(hp, 0f, MaxHp);
        if (Phase == BossPhase.One && currentHp <= MaxHp * settings.PhaseTwoHealthRatio)
        { currentHp = MaxHp * settings.PhaseTwoHealthRatio; EnterPhaseTwo(); }
        else if (currentHp <= 0f) Die();
        Notify();
    }
    public void ReduceBeliefForTest(float amount) => ReduceBelief(amount, "테스트 믿음 감소");
    private void ReduceBelief(float amount, string reason)
    {
        if (!EncounterStarted || IsDead || IsVulnerable || amount <= 0f) return;
        float before = CurrentBelief;
        bool entered = belief.Reduce(amount, Time.time, settings.VulnerableDuration);
        if (entered)
        {
            CancelCombat(false);
            SetPresentation(BossPresentationState.Idle);
            SetStatus("방어력 저하 · " + reason);
        }
        else SetStatus(reason);
        ShowFeedback($"믿음 -{before - CurrentBelief:0} · {reason}", new Color(.5f, 1f, .77f));
    }
    // ---- Regular-attack lane -------------------------------------------------------------------------------------
    private void StartRegular(BossPatternKind kind)
    {
        // A reserved / running shockwave owns the body: no new regular attack may begin until it is released.
        if (regularRunning || shockwaveReserved || bodyActionActive) return;
        int token = generation;
        // A newly starting regular attack closes observation prophecies that have waited their minimum reaction time.
        ResolveObservationsBeforeAttack();
        if (token != generation || !CanContinue) return;
        var context = new BossRegularAttackContext(++patternCounter, kind, IsBurning);
        runningRegularContext = context;
        foreach (BossProphecy prophecy in prophecies)
            if (prophecy.Kind == BossBeliefKind.AttackAndDodge && prophecy.Linked == null)
            { prophecy.Linked = context; context.Dodge = prophecy; break; }
        regularPatternId = context.PatternId;
        activeRegular = kind;
        regularRunning = true;
        nextRegularAt = 0f;
        RecordUse(regularUses, kind);
        Coroutine routine = StartCoroutine(ExecuteLane(RunRegular(kind, context, token), token, LaneRegular));
        if (token == generation && regularRunning) regularRoutine = routine;
    }

    private IEnumerator RunRegular(BossPatternKind kind, BossRegularAttackContext context, int token)
    {
        SetStatus("패턴: " + PatternName(kind));
        regularAttackActive = true;
        yield return attacks.Regular(kind, context);
        if (token != generation || !CanContinue) yield break;
        regularAttackActive = false;
        runningRegularContext = null;
        ResolveDodgeProphecy(context);
        if (token != generation || !CanContinue) yield break;
        float recovery = settings.PatternRecovery / SpeedMultiplier;
        if (!bodyActionActive)
            SetActionCue("패턴 종료 · 반격 기회", "다음 공격의 예고를 확인하세요.", BossPresentationState.Recovery, recovery);
        yield return new WaitForSeconds(recovery);
        if (token != generation) yield break;
        regularRunning = false;
        regularRoutine = null;
        if (!bodyActionActive)
            SetActionCue("다음 패턴 대기", "공격 예고를 보고 대응하세요.", BossPresentationState.Idle);
        Notify();
    }

    // ---- Belief lane ---------------------------------------------------------------------------------------------
    private void StartBelief(BossPatternKind kind)
    {
        if (HasActiveBelief || beliefAwaitingEnd) return;
        beliefAwaitingEnd = true;
        beliefPatternId = ++patternCounter;
        activeBeliefPattern = kind;
        beliefProcessing = true;
        RecordUse(beliefUses, kind);
        int token = generation;
        Coroutine routine = StartCoroutine(ExecuteLane(RunBelief(kind, token), token, LaneBelief));
        if (token == generation && beliefProcessing) beliefRoutine = routine;
    }

    /// <summary>
    /// 발동 처리가 끝나도 효과/예언/파동이 남아 있으면 다음 믿음을 선택하지 않는다.
    /// 실제 효과 종료를 Update에서 확인한 뒤 믿음 쿨타임을 시작한다.
    /// </summary>
    private IEnumerator RunBelief(BossPatternKind kind, int token)
    {
        switch (kind)
        {
            case BossPatternKind.AttackIntentBelief:
                ArmProphecy(BossBeliefKind.AttackIntent, "공격 예언", $"다음 일반 공격 전까지 공격하지 않으면 믿음 -{settings.BeliefReduction:0}");
                break;
            case BossPatternKind.NoAttackBelief:
                ArmProphecy(BossBeliefKind.NoAttack, "공격 금지 예언", $"다음 일반 공격 전에 공격하면 믿음 -{settings.BeliefReduction:0}");
                break;
            case BossPatternKind.AttackAndDodgeBelief:
                ArmProphecy(BossBeliefKind.AttackAndDodge, "공격·회피 예언", $"다음 일반 공격을 회피하면 믿음 -{settings.BeliefReduction:0}");
                break;
            case BossPatternKind.BurnBelief:
                burnEndsAt = Time.time + settings.BurnWindow;
                NoteBelief(BossBeliefKind.Burn);
                ShowFeedback($"화상 강화 · {settings.BurnWindow:0}초간 새 공격에 화상", new Color(1f, .62f, .3f));
                break;
            case BossPatternKind.CloudBelief:
                if (clouds != null && clouds.BeginLightning())
                {
                    NoteBelief(BossBeliefKind.Cloud);
                    ShowFeedback($"먹구름 번개 · {settings.CloudDuration:0}초 뒤 · 흰 구름 아래나 범위 밖으로", new Color(1f, .87f, .45f));
                }
                break;
            case BossPatternKind.ShockwaveBelief:
                yield return RunShockwaveBelief(token);
                break;
        }
        if (token != generation) yield break;
        beliefProcessing = false;
        beliefRoutine = null;
        Notify();
    }

    private void ArmProphecy(BossBeliefKind kind, string title, string hint)
    {
        prophecies.Add(new BossProphecy(kind, Time.time));
        NoteBelief(kind);
        ShowFeedback(title + " · " + hint, new Color(.82f, .88f, 1f));
    }

    // The shockwave swing needs the boss body: it waits for the current regular attack to finish (never cutting a combo),
    // meanwhile no new regular attack may start, then performs the swing without adding a regular attack.
    private IEnumerator RunShockwaveBelief(int token)
    {
        shockwaveReserved = true;
        NoteBelief(BossBeliefKind.Shockwave);
        while (token == generation && CanContinue && regularAttackActive) yield return null;
        if (token != generation || !CanContinue) yield break;
        bodyActionActive = true;
        yield return attacks.Shockwave();
        if (token != generation) yield break;
        bodyActionActive = false;
        shockwaveReserved = false;
        if (!regularRunning)
            SetActionCue("다음 패턴 대기", "공격 예고를 보고 대응하세요.", BossPresentationState.Idle);
    }

    // ---- Lane runner: advances nested iterators and disposes them with their owner ---------------------------------
    private IEnumerator ExecuteLane(IEnumerator root, int token, int lane)
    {
        var pending = new Stack<IEnumerator>();
        pending.Push(root);
        Exception failure = null;
        bool interrupted = false;
        try
        {
            while (pending.Count > 0 && token == generation && CanContinue)
            {
                IEnumerator current = pending.Peek();
                bool moved;
                object next = null;
                try
                {
                    advancingLanes |= lane;
                    moved = current.MoveNext();
                    if (moved) next = current.Current;
                }
                catch (Exception exception) { failure = exception; break; }
                finally { advancingLanes &= ~lane; }

                if (token != generation || !CanContinue) break;

                if (!moved)
                {
                    pending.Pop();
                    DisposePatternIterator(current);
                }
                else if (next is IEnumerator child) pending.Push(child);
                else yield return next;
            }
            interrupted = pending.Count > 0;
        }
        finally
        {
            while (pending.Count > 0) DisposePatternIterator(pending.Pop());
            bool ownsLane = token == generation;
            if (ownsLane && (interrupted || failure != null))
            {
                if (lane == LaneRegular) regularRoutine = null; else beliefRoutine = null;
                CancelCombat(false);
            }
            if (ownsLane && failure != null)
            {
                automaticPatterns = false;
                SetStatus("패턴 실행 오류 · Console 확인");
                Debug.LogError($"{name}: {Phase} / {(lane == LaneRegular ? "regular" : "belief")} lane stopped.\n{failure}", this);
            }
        }
    }
    private void DisposePatternIterator(IEnumerator iterator)
    {
        try { (iterator as IDisposable)?.Dispose(); }
        catch (Exception exception) { Debug.LogException(exception, this); }
    }

    // ---- Prophecy resolution -------------------------------------------------------------------------------------
    private void ResolveObservationsBeforeAttack()
    {
        if (prophecies.Count == 0) return;
        int token = generation;
        List<BossProphecy> due = null;
        foreach (BossProphecy prophecy in prophecies)
            if (prophecy.IsObservation && Time.time - prophecy.StartedAt >= settings.ProphecyReactionTime)
                (due ?? (due = new List<BossProphecy>(2))).Add(prophecy);
        if (due == null) return;
        foreach (BossProphecy prophecy in due)
        {
            if (token != generation) return;
            prophecies.Remove(prophecy);
            if (prophecy.Kind == BossBeliefKind.AttackIntent)
            {
                if (!prophecy.PlayerActed) ReduceBelief(settings.BeliefReduction, "공격 예언 · 공격하지 않음");
                else ShowFeedback("공격을 감지했습니다 · 믿음 유지", new Color(1f, .8f, .45f));
            }
            else if (!prophecy.Reduced && !prophecy.PlayerActed)
                ShowFeedback("공격하지 않았습니다 · 믿음 유지", new Color(1f, .8f, .45f));
        }
    }

    // A three-hit combo is one attack: the prophecy is settled once, after the whole attack is over.
    private void ResolveDodgeProphecy(BossRegularAttackContext context)
    {
        BossProphecy prophecy = context.Dodge;
        if (prophecy == null || !prophecies.Contains(prophecy)) return;
        bool hit = false, evaded = false, threatened = false, cancelled = false;
        foreach (BossAttackRecord record in context.Records)
        {
            hit |= record.Outcome == AttackOutcome.Hit;
            evaded |= record.Outcome == AttackOutcome.EvadedByPlayer;
            cancelled |= record.Outcome == AttackOutcome.Cancelled;
            threatened |= record.WasThreatened;
        }
        if (hit) { prophecies.Remove(prophecy); ShowFeedback("공격 적중 · 믿음 유지", new Color(1f, .5f, .42f)); }
        else if (cancelled) prophecies.Remove(prophecy);
        else if (evaded) { prophecies.Remove(prophecy); ReduceBelief(settings.BeliefReduction, "예언된 공격 회피 성공"); }
        else if (!threatened) prophecy.Linked = null; // no real threat happened: stays pending for the next attack
        else prophecies.Remove(prophecy);
    }

    private void HandlePlayerSignal(PlayerCombatSignal signal)
    {
        if (!EncounterStarted || IsDead || !TargetValid || signal.Actor != playerHealth.gameObject || signal.Target != gameObject ||
            signal.AttackId <= 0 || signal.OccurredAt < encounterStartedAt) return;
        if (signal.Type == PlayerCombatSignalType.AttackEvaded)
        {
            if (allowManualEvadeInjection) SetStatus("수동 회피 신호 #" + signal.AttackId + " · 실제 결과/믿음에 미반영");
            return;
        }
        bool eligible = settings.AttackSignalMode == BossAttackSignalMode.Either ||
            settings.AttackSignalMode == BossAttackSignalMode.Attempt && signal.Type == PlayerCombatSignalType.AttackAttempt ||
            settings.AttackSignalMode == BossAttackSignalMode.HitConfirmed && signal.Type == PlayerCombatSignalType.HitConfirmed;
        if (!eligible) return;
        int token = generation;
        for (int i = 0; i < prophecies.Count && token == generation; i++)
        {
            BossProphecy prophecy = prophecies[i];
            if (!prophecy.IsObservation || signal.OccurredAt < prophecy.StartedAt || !prophecy.AttackIds.Add(signal.AttackId)) continue;
            prophecy.PlayerActed = true;
            if (prophecy.Kind == BossBeliefKind.NoAttack && !prophecy.Reduced)
            {
                prophecy.Reduced = true;
                ReduceBelief(settings.BeliefReduction, "공격 금지 예언 · 공격함");
            }
        }
    }

    private bool ChoosePattern(bool regularLane, out BossPatternKind pattern) => BossPatternSelection.TryChooseLane(
        Phase == BossPhase.One ? settings.PhaseOnePool : settings.PhaseTwoPool, regularLane,
        regularLane ? regularUses : beliefUses, eligibleAny, UnityEngine.Random.value, out pattern, candidateWeights);

    public bool IsPatternEligible(BossPatternKind candidate)
    {
        if (!TargetValid || !BossPatternSelection.AllowedInPhase(candidate, Phase)) return false;
        if (!BossPatternSelection.IsRegular(candidate) && HasActiveBelief) return false;
        switch (candidate)
        {
            case BossPatternKind.Projectile:
                int toward = PlayerCollider.bounds.center.x >= transform.position.x ? 1 : -1;
                return attacks != null && attacks.TryPlanProjectile(toward, out _, out _, out _, out _, out _);
            case BossPatternKind.AttackIntentBelief: return !HasProphecy(BossBeliefKind.AttackIntent);
            case BossPatternKind.NoAttackBelief: return !HasProphecy(BossBeliefKind.NoAttack);
            case BossPatternKind.AttackAndDodgeBelief: return !HasProphecy(BossBeliefKind.AttackAndDodge);
            case BossPatternKind.BurnBelief: return !IsBurning;
            case BossPatternKind.CloudBelief: return clouds != null && clouds.IsPrepared && !clouds.LightningActive;
            case BossPatternKind.ShockwaveBelief:
                return !shockwaveReserved && !bodyActionActive && (attacks == null || attacks.ShockwaveTracking == 0) &&
                    BossCombatPhysics.FindGround(bodyCollider.bounds.center, 0.1f, 8f, this, out _);
            default: return true;
        }
    }
    private static void RecordUse(Dictionary<BossPatternKind, int> uses, BossPatternKind pattern)
    { uses[pattern] = (uses.TryGetValue(pattern, out int count) ? count : 0) + 1; }
    public int PatternUseCount(BossPatternKind pattern)
    {
        var uses = BossPatternSelection.IsRegular(pattern) ? regularUses : beliefUses;
        return uses.TryGetValue(pattern, out int count) ? count : 0;
    }
    public BossAttackRecord CreateAttack(BossAttackKind kind, int hitIndex, AttackGeometry geometry, float damage,
        BossBeliefKind attackBelief, bool burn, BossRegularAttackContext context = null, bool observeNow = true)
    {
        int owningPattern = context != null ? context.PatternId : beliefPatternId;
        var record = new BossAttackRecord(generation, owningPattern, nextAttackId++, hitIndex, kind, attackBelief, geometry,
            damage * DamageMultiplier, burn, context);
        context?.Records.Add(record);
        activeAttacks.Add(record.Id, record);
        // Multi-shape attacks (lightning) observe their own live shapes; a bounding box would invent a threat.
        if (observeNow) Observe(record, geometry);
        return record;
    }
    public bool IsAttackValid(BossAttackRecord record) => record != null && record.Generation == generation && !record.IsComplete && CanContinue && activeAttacks.ContainsKey(record.Id);
    public bool Observe(BossAttackRecord record, AttackGeometry shape)
    {
        if (!IsAttackValid(record)) return false;
        bool overlaps = shape.Overlaps(PlayerCollider);
        record.Observe(overlaps, PlayerCollider.bounds.center);
        return overlaps;
    }
    public bool TryHit(BossAttackRecord record)
    {
        if (!IsAttackValid(record)) return false;
        // A dashing player is intangible: the attack passes through without registering contact, so it is not
        // used up and still lands once the dash ends.
        if (playerHealth != null && playerHealth.IsDashIntangible) { record.NoteAvoidedByDash(); return false; }
        if (!record.TryContact()) return false;
        var damage = new DamageInfo(record.Damage, DamageSource.Enemy, gameObject, AttackName(record.Kind));
        bool isComboHit = record.Kind == BossAttackKind.Melee && record.HitIndex > 0;
        bool accepted = isComboHit && record.HitIndex > 1 && lastAcceptedComboPatternId == record.PatternId
            ? playerHealth.TakeDamageFromBossCombo(damage)
            : playerHealth.TakeDamage(damage);
        if (!accepted) return false;
        if (isComboHit) lastAcceptedComboPatternId = record.PatternId;
        record.AcceptDamage();
        ShowFeedback($"피격 -{record.Damage:0}" + (record.BurnEnhanced ? " · 화상 발생" : ""), new Color(1f, .5f, .42f));
        attacks.Impact(PlayerCollider.bounds.center, false);
        // Only an attack that recorded the enhancement at its start, and whose damage was actually accepted, burns.
        if (record.BurnEnhanced)
        {
            BossBurnEffect burn = playerHealth.GetComponent<BossBurnEffect>();
            if (burn == null) burn = playerHealth.gameObject.AddComponent<BossBurnEffect>();
            burn.Apply(this, settings);
        }
        Notify();
        return true;
    }
    public void CompleteAttack(BossAttackRecord record, BossAttackEndReason reason)
    {
        if (record == null || record.Generation != generation || !activeAttacks.ContainsKey(record.Id)) return;
        if (!TargetValid && reason != BossAttackEndReason.Cancelled) reason = BossAttackEndReason.TargetLost;
        if (!record.Finish(reason)) return;
        activeAttacks.Remove(record.Id);
        history.Add(record);
        if (history.Count > 64) history.RemoveAt(0);
        // Dodge prophecies settle once per whole regular attack (ResolveDodgeProphecy), not per hit.
        if (record.Outcome == AttackOutcome.EvadedByPlayer &&
            (record.BurnEnhanced || record.Kind == BossAttackKind.Shockwave || record.Kind == BossAttackKind.Lightning))
            ReduceBelief(settings.BeliefReduction, AttackName(record.Kind) + " 회피 성공");
        else if (record.Outcome == AttackOutcome.EvadedByPlayer)
            ShowFeedback(AttackName(record.Kind) + " 회피 성공", new Color(.6f, .85f, 1f));
        AttackResolved?.Invoke(record);
        Notify();
    }
    public T SpawnEffect<T>(T prefab) where T : Component
    {
        if (effectRoot == null)
        {
            var root = new GameObject(name + " owned effects");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            effectRoot = root.transform;
        }
        T instance = Instantiate(prefab, effectRoot);
        effects.RemoveAll(effect => effect == null);
        effects.Add(instance.gameObject);
        return instance;
    }
    /// <summary>Puts an effect built at run time under the owned-effect root so every cancel path cleans it.</summary>
    public void AdoptEffect(GameObject effect)
    {
        if (effect == null) return;
        if (effectRoot == null)
        {
            var root = new GameObject(name + " owned effects");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            effectRoot = root.transform;
        }
        effect.transform.SetParent(effectRoot, true);
        effects.RemoveAll(item => item == null);
        effects.Add(effect);
    }
    public void DestroyEffect(GameObject effect)
    {
        if (effect == null) return;
        effects.Remove(effect);
        effect.SetActive(false);
        Destroy(effect);
    }
    // Global cancel (phase change, vulnerability, death, disable, target loss): both lanes, every prophecy,
    // the burn window, lightning and shockwave reservation go together. Persistent clouds stay.
    private void CancelCombat(bool removeBurn)
    {
        hitReaction?.Clear();
        pendingTurn = 0;
        Coroutine cancelledRegular = regularRoutine, cancelledBelief = beliefRoutine;
        regularRoutine = beliefRoutine = null;
        regularRunning = beliefProcessing = regularAttackActive = shockwaveReserved = bodyActionActive = false;
        beliefAwaitingEnd = false;
        runningRegularContext = null;
        if (cancelledRegular != null && (advancingLanes & LaneRegular) == 0) StopCoroutine(cancelledRegular);
        if (cancelledBelief != null && (advancingLanes & LaneBelief) == 0) StopCoroutine(cancelledBelief);
        prophecies.Clear();
        beliefOrder.Clear();
        burnEndsAt = 0f;
        cueDuration = cueEndsAt = 0f;
        cueTitle = "전투 대기"; cueHint = "공격 예고를 보고 대응하세요.";
        CueState = BossPresentationState.Idle;
        SetPresentation(BossPresentationState.Idle);
        foreach (var record in new List<BossAttackRecord>(activeAttacks.Values)) CompleteAttack(record, BossAttackEndReason.Cancelled);
        activeAttacks.Clear();
        generation++;
        nextRegularAt = 0f;
        nextBeliefAt = Time.time + (settings != null ? settings.BeliefInitialDelay : 0f);
        if (attacks != null) attacks.ClearPresentationEffects();
        if (clouds != null) clouds.CancelLightning();
        if (effectRoot != null) { effectRoot.gameObject.SetActive(false); Destroy(effectRoot.gameObject); effectRoot = null; }
        effects.Clear();
        if (body != null) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        if (removeBurn && playerHealth != null) playerHealth.GetComponent<BossBurnEffect>()?.Clear(this);
    }
    private readonly string defeatId = "boss:" + System.Guid.NewGuid().ToString("N") + "/defeat";
    private void Die()
    {
        Currency.Service?.ObserveEnemyDefeat(EnemyRewardKind.Boss, defeatId);
        IsDead = true;
        EncounterStarted = false;
        SetContactDamageSuppressed(true);
        CancelCombat(true);
        ReleaseArenaVisuals();
        body.linearVelocity = Vector2.zero;
        body.simulated = false;
        bodyCollider.enabled = false;
        SetPresentation(BossPresentationState.Dead);
        SetStatus("보스 격파");
        if (DoctrineEventBus.HasListeners)
            DoctrineEventBus.Raise(ConditionType.OnDeath, gameObject, null);
        if (phaseCutscene != null) StopCoroutine(phaseCutscene);
        phaseCutscene = StartCoroutine(PlayDeathDialogue());
    }

    // 보스 사망 뒤, 시체 머리 위 말풍선으로 한 줄씩 말한다. 끝나면 엔딩 이동이 시작된다.
    private IEnumerator PlayDeathDialogue()
    {
        IsDeathDialoguePlaying = true;
        UiModal.Push(phaseCutsceneLock);
        yield return SpeakLines(DeathLines);
        IsDeathDialoguePlaying = false;
        EndSpokenLines();
        phaseCutscene = null;
        Notify();
    }

    private IEnumerator SpeakLines(string[] lines)
    {
        CutsceneCharacter speaker = GetComponent<CutsceneCharacter>();
        if (speaker == null) speaker = gameObject.AddComponent<CutsceneCharacter>();
        speaker.Configure("보스", null);
        if (phaseBubble == null) phaseBubble = gameObject.AddComponent<CutsceneBubbleView>();

        const float charactersPerSecond = 22f;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            phaseBubble.ShowLine(speaker, "", line);
            int shown = 0;
            float elapsed = 0f;
            bool completed = false;
            while (!completed)
            {
                elapsed += Time.unscaledDeltaTime;
                int next = Mathf.Min(line.Length, Mathf.FloorToInt(elapsed * charactersPerSecond));
                if (next != shown)
                {
                    shown = next;
                    phaseBubble.SetVisibleText(line.Substring(0, shown));
                }
                if (Input.GetMouseButtonDown(0))
                {
                    if (shown < line.Length)
                    {
                        shown = line.Length;
                        phaseBubble.SetVisibleText(line);
                    }
                    else completed = true;
                }
                if (shown >= line.Length && Input.GetKeyDown(KeyCode.E)) completed = true;
                yield return null;
            }
            while (Input.GetMouseButton(0) || Input.GetKey(KeyCode.E)) yield return null;
        }
    }

    private void EndSpokenLines()
    {
        if (phaseBubble != null) phaseBubble.Hide();
        phaseCutscenePlaying = false;
        UiModal.Pop(phaseCutsceneLock);
    }
    // Explicit, immediate facing (not the delayed player-tracking turn). Attacks no longer use it: they wait for
    // the turn through BossAttackExecutor.TurnToPlayer, so the delay cannot be bypassed just before an attack.
    public void FacePlayer() { if (TargetValid) Face(PlayerCollider.bounds.center.x >= transform.position.x ? 1 : -1); }

    /// <summary>True when the player is on the side the boss faces (or overlapping it, inside the dead zone).</summary>
    public bool IsPlayerOnFacingSide
    {
        get
        {
            if (!TargetValid) return true;
            float deltaX = PlayerCollider.bounds.center.x - transform.position.x;
            return Mathf.Abs(deltaX) < .05f || (deltaX > 0f ? 1 : -1) == facing;
        }
    }

    // 대기 중 매 프레임 호출용. 플레이어가 반대편에 안정적으로 turnDelaySeconds 머문 뒤에야 돈다.
    // 매 프레임 불러도 대기는 한 번만 시작되고, 원래 방향으로 돌아오면 취소된다.
    private void FacePlayerWhileIdle() => TrackPlayerFacing();

    /// <summary>Delayed player-tracking turn. Safe to call every frame (see EnemyUnit.TrackFacingToward).</summary>
    public void TrackPlayerFacing()
    {
        if (!TargetValid) { pendingTurn = 0; return; }
        float deltaX = PlayerCollider.bounds.center.x - transform.position.x;
        // 보스 몸 위에 겹쳐 있을 때 좌우로 덜덜 떨리지 않도록 작은 데드존을 둔다.
        if (Mathf.Abs(deltaX) < .05f) { pendingTurn = 0; return; }
        int direction = deltaX > 0f ? 1 : -1;
        if (direction == facing) { pendingTurn = 0; return; }

        float now = Time.time;
        if (pendingTurn != direction || now - pendingTurnLastSeen > PendingTurnStaleSeconds)
        {
            pendingTurn = direction;
            pendingTurnSince = now;
        }
        pendingTurnLastSeen = now;
        if (now - pendingTurnSince >= turnDelaySeconds) Face(direction);
    }
    private void Face(int direction)
    {
        pendingTurn = 0;
        facing = direction;
        if (attackOrigin != null) { Vector3 p = attackOrigin.localPosition; p.x = Mathf.Abs(p.x) * facing; attackOrigin.localPosition = p; }
        if (projectileOrigin != null) { Vector3 p = projectileOrigin.localPosition; p.x = Mathf.Abs(p.x) * facing; projectileOrigin.localPosition = p; }
        if (presentation != null) presentation.SetFacing(facing);
    }
    public void SetPresentation(BossPresentationState state) { if (presentation != null) presentation.SetState(state); }
    public void SetActionCue(string title, string hint, BossPresentationState state, float duration = 0f)
    {
        cueTitle = title; cueHint = hint; CueState = state;
        cueDuration = Mathf.Max(0f, duration); cueEndsAt = Time.time + cueDuration;
        SetPresentation(state);
    }
    public void ShowFeedback(string message, Color color)
    { feedback = message; feedbackColor = color; feedbackEndsAt = Time.time + 2.6f; }
    public static string AttackName(BossAttackKind kind)
    {
        switch (kind)
        {
            case BossAttackKind.Melee: return "베기";
            case BossAttackKind.Dash: return "돌진";
            case BossAttackKind.Projectile: return "투사체";
            case BossAttackKind.Shockwave: return "지면 충격파";
            case BossAttackKind.Lightning: return "먹구름 번개";
            default: return "공격";
        }
    }
    private static string PatternName(BossPatternKind kind)
    {
        switch (kind)
        {
            case BossPatternKind.MeleeCombo: return "연속 베기";
            case BossPatternKind.Dash: return "돌진";
            case BossPatternKind.Projectile: return "투사체";
            case BossPatternKind.AttackIntentBelief: return "공격 예언";
            case BossPatternKind.CloudBelief: return "먹구름 · 번개";
            case BossPatternKind.BurnBelief: return "화상 강화";
            case BossPatternKind.AttackAndDodgeBelief: return "공격·회피 예언";
            case BossPatternKind.NoAttackBelief: return "공격 금지 예언";
            case BossPatternKind.ShockwaveBelief: return "지면 충격파";
            default: return "대기";
        }
    }
    private void SetStatus(string value) { if (statusMessage == value) return; statusMessage = value; Notify(); }
    private void Notify() => StatusChanged?.Invoke();
    private void SetContactDamageSuppressed(bool suppressed)
    {
        if (contactDamageComponents == null)
            return;

        foreach (EnemyContactDamage contact in contactDamageComponents)
            if (contact != null)
                contact.SetSuppressed(suppressed);
    }
    private void OnDisable()
    {
        EncounterStarted = false;
        SetContactDamageSuppressed(true);
        if (phaseCutscene != null) StopCoroutine(phaseCutscene);
        phaseCutscene = null;
        phaseCutscenePlaying = false;
        IsDeathDialoguePlaying = false;
        if (phaseBubble != null) phaseBubble.Hide();
        UiModal.Pop(phaseCutsceneLock);
        CancelCombat(true);
        ReleaseArenaVisuals();
        if (playerEvents != null) playerEvents.SignalPublished -= HandlePlayerSignal;
        if (presentation != null) presentation.SetState(IsDead ? BossPresentationState.Dead : BossPresentationState.Idle);
        Notify();
    }
    private void OnDrawGizmosSelected()
    {
        if (!showAttackGizmos) return;
        foreach (var record in activeAttacks.Values)
        {
            Gizmos.color = record.Activated ? Color.yellow : Color.red;
            foreach (var shape in record.WarningGeometry)
            {
                Gizmos.matrix = Matrix4x4.TRS(shape.Center, Quaternion.Euler(0, 0, shape.Angle), Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, shape.Size);
            }
        }
        Gizmos.matrix = Matrix4x4.identity;
        if (attackOrigin != null) { Gizmos.color = Color.cyan; Gizmos.DrawSphere(attackOrigin.position, 0.07f); }
    }
    private static string BeliefPhrase(BossBeliefKind kind)
    {
        switch (kind)
        {
            case BossBeliefKind.AttackIntent: case BossBeliefKind.AttackAndDodge: return "너는 날 공격할 것이다.";
            case BossBeliefKind.Cloud: return "너는 번개를 피하지 못할 것이다.";
            case BossBeliefKind.Burn: return "너는 불타게 될 것이다.";
            case BossBeliefKind.NoAttack: return "너는 날 공격하지 않을 것이다.";
            case BossBeliefKind.Shockwave: return "너는 피하지 못할 것이다.";
            default: return "";
        }
    }
}
