using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public sealed class BossCombatController : MonoBehaviour, IDamageable, ICombatGeneration
{
    [SerializeField] private bool startOnAwake;
    [SerializeField] private BossCombatSettings settings;
    [SerializeField] private BossAttackExecutor attacks;
    [SerializeField] private BossHazardDirector hazards;
    [SerializeField] private BossCombatView presentation;
    [SerializeField] private BossCombatHud hud;
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private Transform projectileOrigin;
    [SerializeField] private bool automaticPatterns = true;
    [SerializeField] private bool allowManualEvadeInjection;
    [SerializeField] private bool showAttackGizmos;

    private readonly Dictionary<BossPatternKind, int> patternUses = new Dictionary<BossPatternKind, int>();
    private readonly Dictionary<int, BossAttackRecord> activeAttacks = new Dictionary<int, BossAttackRecord>();
    private readonly List<BossAttackRecord> history = new List<BossAttackRecord>();
    private readonly HashSet<long> windowAttackIds = new HashSet<long>();
    private readonly List<GameObject> effects = new List<GameObject>();
    private readonly List<float> candidateWeights = new List<float>(9);
    private System.Predicate<BossPatternKind> eligibleAny, eligibleRegular;
    private float nextPatternSearchAt;
    private static readonly WaitForSeconds SelectionRetry = new WaitForSeconds(.1f);
    private Rigidbody2D body;
    private CapsuleCollider2D bodyCollider;
    private PlayerHealth playerHealth;
    private PlayerAttack playerAttack;
    private PlayerCombatEvents playerEvents;
    private EnemyContactDamage[] contactDamageComponents;
    private BossSharedBelief belief;
    private Coroutine patternRoutine;
    private Transform effectRoot;
    private BossBeliefKind activeBelief;
    private BossPatternKind activePattern;
    private bool patternRunning, signalWindow, playerActed, cloudTouched, initialized;
    private float windowStartedAt, encounterStartedAt, burnEndsAt, conditionEndsAt, conditionDuration;
    private int generation = 1, patternId, nextAttackId = 1, facing = 1, initialFacing;
    private int lastAcceptedComboPatternId = -1;
    private float currentHp;
    private Vector3 spawnPosition, playerSpawnPosition, playerSpawnScale;
    private bool savedPlayerSpawn;
    private string statusMessage = "전투 대기";
    private float cueEndsAt, cueDuration, feedbackEndsAt;
    private string cueTitle = "전투 대기", cueHint = "공격 예고를 보고 대응하세요.", feedback;
    private Color feedbackColor = Color.white;

    public event Action StatusChanged;
    public event Action<BossAttackRecord> AttackResolved;
    public BossCombatSettings Settings => settings;
    public Rigidbody2D Body => body;
    public Collider2D BodyCollider => bodyCollider;
    public SpriteRenderer BodyRenderer => presentation != null ? presentation.BodyRenderer : null;
    public PlayerHealth PlayerHealth => playerHealth;
    public PlayerAttack PlayerAttack => playerAttack;
    public Collider2D PlayerCollider => playerHealth != null ? playerHealth.BodyCollider : null;
    public BossPhase Phase { get; private set; }
    public bool IsDead { get; private set; }
    public bool EncounterStarted { get; private set; }
    public bool IsVulnerable => belief != null && belief.IsVulnerable;
    public bool IsPatternRunning => patternRunning;
    public bool AutomaticPatterns { get => automaticPatterns; set { automaticPatterns = value; Notify(); } }
    public bool AllowManualEvadeInjection { get => allowManualEvadeInjection; set => allowManualEvadeInjection = value; }
    public int Generation => generation;
    public int CombatGeneration => generation;
    public int PatternId => patternId;
    public int Facing => facing;
    public float CurrentHp => currentHp;
    public float MaxHp => settings != null ? settings.MaxHp : 0f;
    public float CurrentBelief => belief != null ? belief.Current : 0f;
    public float MaxBelief => belief != null ? belief.Maximum : 0f;
    public float NormalizedBelief => MaxBelief > 0f ? CurrentBelief / MaxBelief : 0f;
    public float VulnerableRemaining => belief != null ? belief.Remaining(Time.time) : 0f;
    public float BurnRemaining => Mathf.Max(0f, burnEndsAt - Time.time);
    public bool IsBurning => EncounterStarted && BurnRemaining > 0f;
    public float ConditionRemaining => Mathf.Max(0f, conditionEndsAt - Time.time);
    public float ConditionProgress => conditionDuration > 0f ? 1f - ConditionRemaining / conditionDuration : 0f;
    public float SpeedMultiplier => Phase == BossPhase.Two ? settings.PhaseTwoSpeedMultiplier : 1f;
    public float DamageMultiplier => Phase == BossPhase.Two ? settings.PhaseTwoDamageMultiplier : 1f;
    public string ActivePatternName => patternRunning ? PatternName(activePattern) : "대기";
    public BossPatternKind ActivePattern => activePattern;
    public string ActiveBeliefPhrase => BeliefPhrase(activeBelief);
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
    public bool TargetValid => playerHealth != null && !playerHealth.IsDead && playerHealth.isActiveAndEnabled && PlayerCollider != null && PlayerCollider.enabled;
    public bool CanContinue => isActiveAndEnabled && EncounterStarted && !IsDead && TargetValid;
    public bool CanStartNextHit => CanContinue && (activeBelief != BossBeliefKind.Burn || IsBurning);

    private void Awake()
    {
        eligibleAny = IsPatternEligible;
        eligibleRegular = candidate => BossPatternSelection.IsRegular(candidate) && IsPatternEligible(candidate);
        body = GetComponent<Rigidbody2D>();
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
    private void Start() { if (startOnAwake && !EncounterStarted) BeginEncounter(); }
    private void Update()
    {
        if (!EncounterStarted || IsDead) return;
        if (!TargetValid)
        {
            EncounterStarted = false;
            SetContactDamageSuppressed(true);
            CancelCombat(true);
            SetStatus("플레이어 전투 종료 · R 재시작");
            return;
        }
        if (belief.Tick(Time.time, settings.BeliefRecoveryRatio))
        {
            if (!patternRunning)
                SetActionCue("믿음 회복", "다음 공격의 예고를 확인하세요.", BossPresentationState.Idle);
            SetStatus("믿음 회복");
        }
        if (automaticPatterns && !patternRunning && Time.time >= nextPatternSearchAt)
        {
            if (ChoosePattern(false, out BossPatternKind selected)) StartPattern(selected);
            else { nextPatternSearchAt = Time.time + .1f; SetStatus("실행 가능한 패턴 없음 · 대상 거리/지면/설정 확인"); }
        }
    }
    public List<string> ValidateEncounter()
    {
        var errors = settings != null ? settings.ValidateSettings() : new List<string> { "Boss.settings is missing." };
        if (attacks == null) errors.Add("Boss.attacks is missing.");
        if (hazards == null) errors.Add("Boss.hazards is missing.");
        if (presentation == null || !presentation.IsConfigured) errors.Add("Boss.presentation / VisualRoot / body renderer references are missing.");
        if (hud == null || !hud.IsConfigured) errors.Add("Boss.hud / gameplay health, belief and speech references are missing.");
        else if (!hud.HasKoreanFont) errors.Add("Boss.hud requires a Korean font (preferredFont / installed OS fonts).");
        if (attackOrigin == null || projectileOrigin == null) errors.Add("Boss.AttackOrigin / ProjectileOrigin is missing.");
        if (transform.localScale != Vector3.one) errors.Add("Boss physics root scale must be (1,1,1).");
        return errors;
    }
    public void BeginEncounter()
    {
        if (!isActiveAndEnabled || IsDead || EncounterStarted) return;
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
            SetStatus("PlayerHealth, 4타 콤보 데이터, 몸체 콜라이더 필요");
            Debug.LogError(name + ": tagged Player requires PlayerHealth, a four-step PlayerAttack, a non-trigger body Collider2D and PlayerCombatEvents.", this);
            return;
        }
        attacks.Initialize(this);
        hazards.Initialize(this);
        presentation.Initialize(this);
        hud.Initialize(this);
        encounterStartedAt = Time.time;
        EncounterStarted = true;
        SetContactDamageSuppressed(false);
        SetPresentation(BossPresentationState.Idle);
        SetStatus("전투 시작");
    }
    private bool BindPlayer()
    {
        GameObject actor = GameObject.FindGameObjectWithTag("Player");
        if (actor == null) return false;
        if (playerEvents != null) playerEvents.SignalPublished -= HandlePlayerSignal;
        playerHealth = actor.GetComponent<PlayerHealth>();
        playerAttack = actor.GetComponent<PlayerAttack>();
        playerEvents = actor.GetComponent<PlayerCombatEvents>();
        if (playerHealth == null || playerAttack == null || playerAttack.ComboCount < 4 ||
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
        if (!EncounterStarted || IsDead || info.Amount <= 0f || float.IsNaN(info.Amount) || float.IsInfinity(info.Amount)) return false;
        PlayerAttackContext attack = info.PlayerAttack;
        if (attack != null && (playerHealth == null || attack.Actor != playerHealth.gameObject || !attack.Begin(this))) return false;
        float damage = info.Amount * (info.Source == DamageSource.PlayerOwned ?
            (IsVulnerable ? settings.VulnerableDamageMultiplier : settings.MaintainedDamageMultiplier) : 1f);
        if (damage <= 0f) return false;
        if (attack != null && !attack.TryConsumeHit(this, info.HitIndex)) return false;
        float before = currentHp;
        float threshold = MaxHp * settings.PhaseTwoHealthRatio;
        currentHp = Mathf.Max(Phase == BossPhase.One ? threshold : 0f, currentHp - damage);
        if (currentHp <= 0f) Die();
        else if (Phase == BossPhase.One && currentHp <= threshold) EnterPhaseTwo();
        attack?.ConfirmHit(this, before - currentHp, info.HitIndex);
        Notify();
        return true;
    }
    private void EnterPhaseTwo()
    {
        CancelCombat(false);
        Phase = BossPhase.Two;
        patternUses.Clear();
        SetPresentation(BossPresentationState.Idle);
        presentation.FlashPhaseChange();
        SetStatus("2페이즈");
        ShowFeedback("2페이즈 · 새로운 믿음 패턴", new Color(.6f, .8f, 1f));
    }
    public void ForcePhaseTwoForTest()
    {
        if (!EncounterStarted || IsDead || Phase == BossPhase.Two) return;
        currentHp = Mathf.Min(currentHp, MaxHp * settings.PhaseTwoHealthRatio);
        EnterPhaseTwo();
    }
    public void ForcePatternForTest(BossPatternKind pattern)
    {
        if (!CanContinue) return;
        automaticPatterns = false;
        if (!BossPatternSelection.AllowedInPhase(pattern, Phase))
        {
            if (BossPatternSelection.AllowedInPhase(pattern, BossPhase.Two)) ForcePhaseTwoForTest();
            else { SetStatus("1페이즈 전용 패턴 · R 재시작 후 실행"); return; }
        }
        CancelCombat(false);
        StartPattern(pattern);
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
        patternUses.Clear();
        history.Clear();
        feedbackEndsAt = 0f;
        facing = initialFacing;
        transform.position = spawnPosition;
        body.position = spawnPosition;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
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
    private void StartPattern(BossPatternKind pattern)
    {
        nextPatternSearchAt = 0f;
        activePattern = pattern;
        activeBelief = BossBeliefKind.None;
        patternId++;
        patternRunning = true;
        if (!BossPatternSelection.IsRegular(pattern)) RecordUse(pattern);
        patternRoutine = StartCoroutine(RunPattern(pattern, generation));
    }
    private IEnumerator RunPattern(BossPatternKind pattern, int token)
    {
        SetStatus("패턴: " + PatternName(pattern));
        switch (pattern)
        {
            case BossPatternKind.MeleeCombo: case BossPatternKind.Dash: case BossPatternKind.Projectile:
                yield return attacks.Regular(pattern); break;
            case BossPatternKind.AttackIntentBelief:
                activeBelief = BossBeliefKind.AttackIntent;
                BeginSignalWindow();
                yield return Stare(settings.StareDuration);
                EndSignalWindow();
                if (token == generation && CanContinue && !playerActed) ReduceBelief(settings.BeliefReduction, "응시 중 공격하지 않음");
                else if (token == generation && CanContinue) ShowFeedback("공격을 감지했습니다 · 믿음 유지", new Color(1f, .8f, .45f));
                break;
            case BossPatternKind.CloudBelief:
                activeBelief = BossBeliefKind.Cloud;
                cloudTouched = false;
                yield return hazards.Clouds();
                if (token == generation && CanContinue && !cloudTouched) ReduceBelief(settings.BeliefReduction, "구름에 올라가지 않음");
                else if (token == generation && CanContinue) ShowFeedback("구름을 밟았습니다 · 믿음 유지", new Color(1f, .8f, .45f));
                break;
            case BossPatternKind.BurnBelief:
                activeBelief = BossBeliefKind.Burn;
                SetConditionTimer(settings.BurnPreparation);
                SetActionCue("화상 강화 준비", "강화 후 공격을 피하면 믿음 감소 · 피격 시 화상", BossPresentationState.Charging, settings.BurnPreparation);
                yield return new WaitForSeconds(settings.BurnPreparation);
                if (token != generation || !CanContinue) yield break;
                burnEndsAt = Time.time + settings.BurnWindow;
                conditionEndsAt = 0f;
                while (CanContinue && IsBurning && token == generation)
                {
                    if (!ChoosePattern(true, out BossPatternKind regular)) { SetStatus("강화: 실행 가능한 일반 공격 대기"); yield return SelectionRetry; continue; }
                    yield return attacks.Regular(regular);
                }
                burnEndsAt = 0f;
                break;
            case BossPatternKind.AttackAndDodgeBelief:
                activeBelief = BossBeliefKind.AttackAndDodge;
                yield return Stare(settings.StareDuration);
                while (token == generation && CanContinue)
                {
                    if (ChoosePattern(true, out BossPatternKind followup))
                    { yield return attacks.Regular(followup); break; }
                    SetStatus("응시 완료 · 실행 가능한 일반 공격 대기");
                    yield return SelectionRetry;
                }
                break;
            case BossPatternKind.NoAttackBelief:
                activeBelief = BossBeliefKind.NoAttack;
                BeginSignalWindow();
                yield return StareWithMeleeWarning();
                break;
            case BossPatternKind.ShockwaveBelief:
                activeBelief = BossBeliefKind.Shockwave;
                yield return attacks.Melee(0, settings.Windup, settings.MeleeDamage, BossBeliefKind.None);
                if (token == generation && CanContinue) yield return attacks.Shockwave();
                break;
        }
        if (token != generation || !CanContinue) yield break;
        activeBelief = BossBeliefKind.None;
        EndSignalWindow();
        conditionEndsAt = 0f;
        SetActionCue("패턴 종료 · 반격 기회", "다음 공격의 예고를 확인하세요.", BossPresentationState.Recovery, settings.PatternRecovery / SpeedMultiplier);
        yield return new WaitForSeconds(settings.PatternRecovery / SpeedMultiplier);
        if (token != generation) yield break;
        patternRunning = false;
        patternRoutine = null;
        SetActionCue("다음 패턴 대기", "공격 예고를 보고 대응하세요.", BossPresentationState.Idle);
        Notify();
    }
    private IEnumerator Stare(float duration)
    {
        SetConditionTimer(duration);
        SetActionCue(ActivePatternName, activeBelief == BossBeliefKind.AttackIntent ?
            $"공격하지 않고 기다리면 믿음 -{settings.BeliefReduction:0}" :
            $"응시 뒤 이어지는 공격을 회피하면 믿음 -{settings.BeliefReduction:0}", BossPresentationState.Stare, duration);
        while (CanContinue && ConditionRemaining > 0f) { FacePlayer(); yield return null; }
        conditionEndsAt = 0f;
    }
    private IEnumerator StareWithMeleeWarning()
    {
        int token = generation;
        SetConditionTimer(settings.StareDuration);
        SetActionCue("공격 금지 예언 · 응시", $"지금 공격하면 믿음 -{settings.BeliefReduction:0} · 응시 직후 베기", BossPresentationState.Stare, settings.StareDuration);
        BossAttackVisual preview = attacks.CreateWarning(attacks.MeleeGeometry(), followOwner: true);
        while (CanContinue && ConditionRemaining > 0f && token == generation)
        {
            FacePlayer();
            preview.Show(attacks.MeleeGeometry(), settings.WarningOrder, followTarget: transform);
            preview.SetProgress(ConditionProgress);
            preview.SetCue("응시 후 베기", ConditionRemaining, ConditionProgress, this);
            yield return null;
        }
        DestroyEffect(preview != null ? preview.gameObject : null);
        EndSignalWindow();
        conditionEndsAt = 0f;
        if (token == generation && CanContinue && !playerActed)
            ShowFeedback("응시 중 공격하지 않았습니다 · 믿음 유지", new Color(1f, .8f, .45f));
        if (token == generation && CanContinue)
            yield return attacks.Melee(0, settings.SurpriseWindup, settings.MeleeDamage * 1.15f, BossBeliefKind.None, false);
    }
    private void BeginSignalWindow()
    { windowAttackIds.Clear(); playerActed = false; signalWindow = true; windowStartedAt = Time.time; }
    private void EndSignalWindow() { signalWindow = false; windowAttackIds.Clear(); }
    public void SetConditionTimer(float duration) { conditionDuration = duration; conditionEndsAt = Time.time + duration; Notify(); }
    private void HandlePlayerSignal(PlayerCombatSignal signal)
    {
        if (!EncounterStarted || IsDead || !TargetValid || signal.Actor != playerHealth.gameObject || signal.Target != gameObject ||
            signal.AttackId <= 0 || signal.OccurredAt < encounterStartedAt) return;
        if (signal.Type == PlayerCombatSignalType.AttackEvaded)
        {
            if (allowManualEvadeInjection) SetStatus("수동 회피 신호 #" + signal.AttackId + " · 실제 결과/믿음에 미반영");
            return;
        }
        if (!signalWindow || signal.OccurredAt < windowStartedAt || ConditionRemaining <= 0f) return;
        bool eligible = settings.AttackSignalMode == BossAttackSignalMode.Either ||
            settings.AttackSignalMode == BossAttackSignalMode.Attempt && signal.Type == PlayerCombatSignalType.AttackAttempt ||
            settings.AttackSignalMode == BossAttackSignalMode.HitConfirmed && signal.Type == PlayerCombatSignalType.HitConfirmed;
        if (!eligible || !windowAttackIds.Add(signal.AttackId)) return;
        playerActed = true;
        if (activeBelief == BossBeliefKind.NoAttack && windowAttackIds.Count == 1)
            ReduceBelief(settings.BeliefReduction, "응시 중 공격함");
    }
    private bool ChoosePattern(bool regularOnly, out BossPatternKind pattern) => BossPatternSelection.TryChoose(
        Phase == BossPhase.One ? settings.PhaseOnePool : settings.PhaseTwoPool, patternUses,
        regularOnly ? eligibleRegular : eligibleAny,
        UnityEngine.Random.value, out pattern, candidateWeights);
    public bool IsPatternEligible(BossPatternKind candidate)
    {
        if (!TargetValid || !BossPatternSelection.AllowedInPhase(candidate, Phase)) return false;
        if (candidate == BossPatternKind.CloudBelief || candidate == BossPatternKind.ShockwaveBelief)
            return BossCombatPhysics.FindGround(candidate == BossPatternKind.CloudBelief ? (Vector2)PlayerCollider.bounds.min : (Vector2)bodyCollider.bounds.center,
                0.1f, 8f, this, out _);
        return true;
    }
    public void RecordRegularUse(BossPatternKind pattern) { if (BossPatternSelection.IsRegular(pattern)) RecordUse(pattern); }
    private void RecordUse(BossPatternKind pattern) { patternUses[pattern] = PatternUseCount(pattern) + 1; }
    public int PatternUseCount(BossPatternKind pattern) => patternUses.TryGetValue(pattern, out int count) ? count : 0;
    public BossAttackRecord CreateAttack(BossAttackKind kind, int hitIndex, AttackGeometry geometry, float damage,
        BossBeliefKind attackBelief, bool burn)
    {
        var record = new BossAttackRecord(generation, patternId, nextAttackId++, hitIndex, kind, attackBelief, geometry, damage * DamageMultiplier, burn);
        activeAttacks.Add(record.Id, record);
        Observe(record, geometry);
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
        if (!IsAttackValid(record) || !record.TryContact()) return false;
        var damage = new DamageInfo(record.Damage, DamageSource.Enemy);
        bool isComboHit = record.Kind == BossAttackKind.Melee && record.HitIndex > 0;
        bool accepted = isComboHit && record.HitIndex > 1 && lastAcceptedComboPatternId == record.PatternId
            ? playerHealth.TakeDamageFromBossCombo(damage)
            : playerHealth.TakeDamage(damage);
        if (!accepted) return false;
        if (isComboHit) lastAcceptedComboPatternId = record.PatternId;
        record.AcceptDamage();
        ShowFeedback($"피격 -{record.Damage:0}" + (record.AllowsBurn && IsBurning ? " · 화상 발생" : ""), new Color(1f, .5f, .42f));
        attacks.Impact(PlayerCollider.bounds.center, false);
        if (record.AllowsBurn && IsBurning)
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
        if (record.Outcome == AttackOutcome.EvadedByPlayer &&
            (record.Belief == BossBeliefKind.Burn || record.Belief == BossBeliefKind.AttackAndDodge ||
             record.Belief == BossBeliefKind.Shockwave && record.Kind == BossAttackKind.Shockwave))
            ReduceBelief(settings.BeliefReduction, AttackName(record.Kind) + " 회피 성공");
        else if (record.Outcome == AttackOutcome.EvadedByPlayer)
            ShowFeedback(AttackName(record.Kind) + " 회피 성공", new Color(.6f, .85f, 1f));
        AttackResolved?.Invoke(record);
        Notify();
    }
    public BossBeliefKind RegularAttackBelief => activeBelief == BossBeliefKind.Burn || activeBelief == BossBeliefKind.AttackAndDodge ? activeBelief : BossBeliefKind.None;
    public void OnCloudInteraction(bool white, Vector2 feet, int cloudGeneration, int cloudPattern)
    {
        if (!CanContinue || activeBelief != BossBeliefKind.Cloud || cloudGeneration != generation || cloudPattern != patternId) return;
        cloudTouched = true;
        if (white) ShowFeedback("흰 구름 착지 · 발판에서 다시 점프할 수 있습니다", new Color(.65f, .95f, 1f));
        else if (hazards.ScheduleSpike(feet))
            ShowFeedback($"먹구름 통과 · {settings.SpikeWarning:0.0}초 후 발밑 가시", new Color(1f, .68f, .45f));
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
    public void DestroyEffect(GameObject effect)
    {
        if (effect == null) return;
        effects.Remove(effect);
        effect.SetActive(false);
        Destroy(effect);
    }
    private void CancelCombat(bool removeBurn)
    {
        if (patternRoutine != null) StopCoroutine(patternRoutine);
        patternRoutine = null;
        patternRunning = false;
        EndSignalWindow();
        activeBelief = BossBeliefKind.None;
        burnEndsAt = conditionEndsAt = 0f;
        cueDuration = cueEndsAt = 0f;
        cueTitle = "전투 대기"; cueHint = "공격 예고를 보고 대응하세요.";
        CueState = BossPresentationState.Idle;
        foreach (var record in new List<BossAttackRecord>(activeAttacks.Values)) CompleteAttack(record, BossAttackEndReason.Cancelled);
        activeAttacks.Clear();
        generation++;
        nextPatternSearchAt = 0f;
        if (hazards != null) hazards.Cancel();
        if (attacks != null) attacks.ClearAfterimages();
        if (effectRoot != null) { effectRoot.gameObject.SetActive(false); Destroy(effectRoot.gameObject); effectRoot = null; }
        effects.Clear();
        if (body != null) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        if (removeBurn && playerHealth != null) playerHealth.GetComponent<BossBurnEffect>()?.Clear(this);
    }
    private void Die()
    {
        IsDead = true;
        EncounterStarted = false;
        SetContactDamageSuppressed(true);
        CancelCombat(true);
        body.linearVelocity = Vector2.zero;
        bodyCollider.enabled = false;
        SetPresentation(BossPresentationState.Dead);
        SetStatus("보스 격파");
    }
    public void FacePlayer() { if (TargetValid) Face(PlayerCollider.bounds.center.x >= transform.position.x ? 1 : -1); }
    private void Face(int direction)
    {
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
            case BossAttackKind.Spike: return "가시";
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
            case BossPatternKind.AttackIntentBelief: return "공격 예언 · 응시";
            case BossPatternKind.CloudBelief: return "구름 시험";
            case BossPatternKind.BurnBelief: return "화상 강화";
            case BossPatternKind.AttackAndDodgeBelief: return "응시 후 공격";
            case BossPatternKind.NoAttackBelief: return "공격 금지 예언";
            case BossPatternKind.ShockwaveBelief: return "베기 후 충격파";
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
        CancelCombat(true);
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
            case BossBeliefKind.Cloud: return "너는 구름 위에 올라갈 것이다.";
            case BossBeliefKind.Burn: return "너는 불타게 될 것이다.";
            case BossBeliefKind.NoAttack: return "너는 날 공격하지 않을 것이다.";
            case BossBeliefKind.Shockwave: return "너는 피하지 못할 것이다.";
            default: return "";
        }
    }
}
