using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class BeliefController : MonoBehaviour
{
    [SerializeField] private BeliefType beliefType = BeliefType.Unassigned;
    [SerializeField] private List<BeliefType> beliefTypeCandidates = new List<BeliefType>();
    [SerializeField] private BeliefProfileCatalog profileCatalog;
    [SerializeField] private bool logStateChanges;

    private BeliefProfile profile;
    private BeliefType selectedBeliefType = BeliefType.Unassigned;
    private EnemyUnit owner;
    private Coroutine vulnerabilityRoutine;
    private float currentBelief;
    private BeliefState currentState = BeliefState.Maintained;
    private bool stopped;
    private float vulnerabilityRemaining;

    public event Action<BeliefController, float, float> BeliefChanged;
    public event Action<BeliefController, BeliefState> StateChanged;

    // 믿음 붕괴는 회복 타이머가 없다. 컨트롤러가 멈추거나 적 주인이 죽어도 유지된다.
    public bool IsCollapsed => currentState == BeliefState.Collapsed;

    public BeliefType Type => profile != null ? profile.Type : selectedBeliefType;
    public bool UsesRandomCandidate => beliefTypeCandidates != null && beliefTypeCandidates.Count > 0;
    public IReadOnlyList<BeliefType> CandidateTypes => beliefTypeCandidates;
    public BeliefProfile Profile => profile;
    public float CurrentBelief => currentBelief;
    public float MaxBelief => profile != null ? profile.MaxBelief : 0f;
    public float NormalizedBelief => MaxBelief <= 0f ? 0f : Mathf.Clamp01(currentBelief / MaxBelief);
    public BeliefState CurrentState => currentState;
    public bool IsVulnerable => currentState == BeliefState.Vulnerable;
    // 방어력 저하 피해 배율이 적용되는 상태 (취약 5초 + 붕괴 영구).
    public bool IsDefenseReduced => currentState == BeliefState.Vulnerable || currentState == BeliefState.Collapsed;
    public bool IsStopped => stopped;
    public bool IsConfigured => profile != null;

    private void Awake()
    {
        owner = GetComponent<EnemyUnit>();
        ConfigureProfile();
    }

    private void OnEnable()
    {
        if (profile == null || owner != null && owner.IsDead) return;
        // 붕괴된 믿음은 다시 켜도 회복하지 않는다.
        if (IsCollapsed)
            return;
        stopped = false;
        if (IsVulnerable && vulnerabilityRoutine == null)
            vulnerabilityRoutine = StartCoroutine(VulnerabilityRoutine());
    }

    private void ConfigureProfile()
    {
        if (profileCatalog == null)
        {
            Debug.LogError($"{name}: BeliefProfileCatalog is required.", this);
            return;
        }

        if (!profileCatalog.IsValid(out string catalogError))
        {
            Debug.LogError($"{name}: Invalid belief profile catalog. {catalogError}", this);
            return;
        }

        selectedBeliefType = SelectBeliefType();
        if (UsesRandomCandidate && selectedBeliefType == BeliefType.Unassigned)
            return;

        if (selectedBeliefType == BeliefType.Unassigned)
        {
            Debug.LogWarning($"{name}: Belief type is Unassigned; using the rear-position default profile.", this);
        }

        profile = profileCatalog.Resolve(selectedBeliefType);
        if (profile == null)
        {
            Debug.LogError($"{name}: No profile for belief type {selectedBeliefType}.", this);
            return;
        }

        if (!profile.IsValid(out string profileError))
        {
            Debug.LogError($"{name}: Invalid profile for belief type {beliefType}. {profileError}", this);
            profile = null;
            return;
        }

        if (selectedBeliefType != BeliefType.Unassigned && profile.Type != selectedBeliefType)
        {
            Debug.LogError($"{name}: Resolved profile type does not match {selectedBeliefType}.", this);
            profile = null;
            return;
        }

        if (!BeliefRules.IsCompatible(profile.Type, owner))
        {
            Debug.LogError($"{name}: Belief {profile.Type} belongs to a different enemy kind.", this);
            profile = null;
            return;
        }

        currentBelief = profile.MaxBelief;
        currentState = BeliefState.Maintained; // 붕괴는런타임 전환으로만 진입한다. ConfigureProfile는 풀로 다시 켜질 때 호출될 수 있어 조기 상태 리셋용으로 Maintained를 유지한다.
        vulnerabilityRemaining = 0f;
        vulnerabilityRoutine = null;
    }

    private BeliefType SelectBeliefType()
    {
        if (beliefTypeCandidates == null || beliefTypeCandidates.Count == 0)
            return beliefType;

        List<BeliefType> validCandidates = new List<BeliefType>();
        foreach (BeliefType candidate in beliefTypeCandidates)
        {
            if (!BeliefRules.IsCompatible(candidate, owner) || profileCatalog.Resolve(candidate) == null)
            {
                Debug.LogError($"{name}: Invalid or incompatible random belief candidate {candidate}.", this);
                return BeliefType.Unassigned;
            }
            if (candidate == BeliefType.Unassigned ||
                validCandidates.Contains(candidate))
            {
                continue;
            }

            validCandidates.Add(candidate);
        }

        if (validCandidates.Count == 0)
        {
            Debug.LogError($"{name}: Random belief candidates contain no valid profile types.", this);
            return BeliefType.Unassigned;
        }

        return validCandidates[UnityEngine.Random.Range(0, validCandidates.Count)];
    }

    public bool ReduceBelief(float amount)
    {
        if (stopped || IsVulnerable || IsCollapsed || profile == null || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            return false;

        float previousBelief = currentBelief;
        currentBelief = Mathf.Max(0f, currentBelief - amount);
        if (Mathf.Approximately(previousBelief, currentBelief))
            return false;

        RaiseBeliefChanged();
        if (currentBelief <= 0f)
            Collapse();
        return true;
    }

    public float ApplyIncomingDamageModifier(DamageInfo damageInfo)
    {
        if (damageInfo.Amount <= 0f)
            return 0f;

        if (damageInfo.Source != DamageSource.PlayerOwned || stopped || profile == null)
            return damageInfo.Amount;

        // 방어력 저하 피해 배율이 적용되는 상태 (취약 5초 + 붕괴 영구).
        float multiplier = IsDefenseReduced
            ? profile.VulnerablePlayerDamageMultiplier
            : profile.MaintainedPlayerDamageMultiplier;
        return damageInfo.Amount * multiplier;
    }

    public void StopBeliefSystem()
    {
        if (stopped)
            return;

        stopped = true;
        // 붕괴된 믿음은 비활성화해도 유지된다. (다시 켜도 게이지 회복 없음)
        if (vulnerabilityRoutine != null)
        {
            StopCoroutine(vulnerabilityRoutine);
            vulnerabilityRoutine = null;
        }
    }

    private void StartVulnerability()
    {
        if (stopped || IsVulnerable || IsCollapsed || profile == null)
            return;

        currentBelief = 0f;
        currentState = BeliefState.Vulnerable;
        LogState("Vulnerable started");
        StateChanged?.Invoke(this, currentState);
        RaiseBeliefChanged();
        vulnerabilityRemaining = profile.VulnerableDuration;
        vulnerabilityRoutine = StartCoroutine(VulnerabilityRoutine());
    }

    // 믿음 붕괴: 단일 플레이어 타격(또는 소진)으로 한 번에 진입하며,
    // 회복 타이머·게이지 회복 없이 방어력 저하 피해 배율만 영구 유지한다.
    private void Collapse()
    {
        if (stopped || IsCollapsed || profile == null)
            return;

        currentBelief = 0f;
        currentState = BeliefState.Collapsed;
        LogState("Belief collapsed");
        StateChanged?.Invoke(this, currentState);
        RaiseBeliefChanged();
    }

    private IEnumerator VulnerabilityRoutine()
    {
        while (vulnerabilityRemaining > 0f)
        {
            yield return null;
            vulnerabilityRemaining = Mathf.Max(0f, vulnerabilityRemaining - Time.deltaTime);
        }
        vulnerabilityRoutine = null;

        if (stopped || owner != null && owner.IsDead)
            yield break;

        currentBelief = profile.MaxBelief * profile.RecoveryRatio;
        currentState = BeliefState.Maintained;
        LogState("Vulnerable ended; belief recovered");
        StateChanged?.Invoke(this, currentState);
        RaiseBeliefChanged();
    }

    private void RaiseBeliefChanged()
    {
        BeliefChanged?.Invoke(this, currentBelief, MaxBelief);
    }

    // UI가 믿음 게이지를 숨긴 뒤에도, 붕괴된 말풍선을 띄우지 않게 문구를 비운다.
    public string DisplayPhrase => IsCollapsed ? string.Empty : profile != null ? profile.Phrase : string.Empty;

    private void LogState(string message)
    {
        if (logStateChanges)
            Debug.Log($"[{name}] {message}: {Type} ({currentBelief}/{MaxBelief})", this);
    }

    private void OnDisable()
    {
        StopBeliefSystem();
    }
}
