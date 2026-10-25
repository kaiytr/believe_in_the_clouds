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

    public BeliefType Type => profile != null ? profile.Type : selectedBeliefType;
    public bool UsesRandomCandidate => beliefTypeCandidates != null && beliefTypeCandidates.Count > 0;
    public IReadOnlyList<BeliefType> CandidateTypes => beliefTypeCandidates;
    public BeliefProfile Profile => profile;
    public float CurrentBelief => currentBelief;
    public float MaxBelief => profile != null ? profile.MaxBelief : 0f;
    public float NormalizedBelief => MaxBelief <= 0f ? 0f : Mathf.Clamp01(currentBelief / MaxBelief);
    public BeliefState CurrentState => currentState;
    public bool IsVulnerable => currentState == BeliefState.Vulnerable;
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
        currentState = BeliefState.Maintained;
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
        if (stopped || IsVulnerable || profile == null || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            return false;

        float previousBelief = currentBelief;
        currentBelief = Mathf.Max(0f, currentBelief - amount);
        if (Mathf.Approximately(previousBelief, currentBelief))
            return false;

        RaiseBeliefChanged();
        if (currentBelief <= 0f)
            StartVulnerability();
        return true;
    }

    public float ApplyIncomingDamageModifier(DamageInfo damageInfo)
    {
        if (damageInfo.Amount <= 0f)
            return 0f;

        if (damageInfo.Source != DamageSource.PlayerOwned || stopped || profile == null)
            return damageInfo.Amount;

        float multiplier = IsVulnerable
            ? profile.VulnerablePlayerDamageMultiplier
            : profile.MaintainedPlayerDamageMultiplier;
        return damageInfo.Amount * multiplier;
    }

    public void StopBeliefSystem()
    {
        if (stopped)
            return;

        stopped = true;
        if (vulnerabilityRoutine != null)
        {
            StopCoroutine(vulnerabilityRoutine);
            vulnerabilityRoutine = null;
        }
    }

    private void StartVulnerability()
    {
        if (stopped || IsVulnerable || profile == null)
            return;

        currentBelief = 0f;
        currentState = BeliefState.Vulnerable;
        LogState("Vulnerable started");
        StateChanged?.Invoke(this, currentState);
        RaiseBeliefChanged();
        vulnerabilityRemaining = profile.VulnerableDuration;
        vulnerabilityRoutine = StartCoroutine(VulnerabilityRoutine());
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
