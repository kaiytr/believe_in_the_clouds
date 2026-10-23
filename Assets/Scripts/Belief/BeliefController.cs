using System;
using System.Collections;
using UnityEngine;

public sealed class BeliefController : MonoBehaviour
{
    [SerializeField] private BeliefType beliefType = BeliefType.Unassigned;
    [SerializeField] private BeliefProfileCatalog profileCatalog;
    [SerializeField] private bool logStateChanges;

    private BeliefProfile profile;
    private EnemyUnit owner;
    private Coroutine vulnerabilityRoutine;
    private float currentBelief;
    private BeliefState currentState = BeliefState.Maintained;
    private bool stopped;

    public event Action<BeliefController, float, float> BeliefChanged;
    public event Action<BeliefController, BeliefState> StateChanged;

    public BeliefType Type => beliefType == BeliefType.Unassigned && profile != null
        ? profile.Type
        : beliefType;
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

        if (beliefType == BeliefType.Unassigned)
        {
            Debug.LogWarning($"{name}: Belief type is Unassigned; using the rear-position default profile.", this);
        }

        profile = profileCatalog.Resolve(beliefType);
        if (profile == null)
        {
            Debug.LogError($"{name}: No profile for belief type {beliefType}.", this);
            return;
        }

        if (!profile.IsValid(out string profileError))
        {
            Debug.LogError($"{name}: Invalid profile for belief type {beliefType}. {profileError}", this);
            profile = null;
            return;
        }

        if (beliefType != BeliefType.Unassigned && profile.Type != beliefType)
        {
            Debug.LogError($"{name}: Resolved profile type does not match {beliefType}.", this);
            profile = null;
            return;
        }

        currentBelief = profile.MaxBelief;
        currentState = BeliefState.Maintained;
    }

    public bool ReduceBelief(float amount)
    {
        if (stopped || IsVulnerable || profile == null || amount <= 0f)
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
        vulnerabilityRoutine = StartCoroutine(VulnerabilityRoutine());
    }

    private IEnumerator VulnerabilityRoutine()
    {
        yield return new WaitForSeconds(profile.VulnerableDuration);
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
