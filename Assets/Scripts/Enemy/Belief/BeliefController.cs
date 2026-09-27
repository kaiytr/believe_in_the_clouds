using System;
using System.Collections;
using UnityEngine;

public class BeliefController : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string beliefId = "belief";
    [SerializeField] private string beliefName = "Belief";

    [TextArea]
    [SerializeField] private string beliefDescription;

    [Header("Belief")]
    [SerializeField] private float maxBelief = 100f;
    [SerializeField] private float currentBelief = 100f;
    [SerializeField] private float defaultBeliefLossAmount = 25f;

    [Header("Damage")]
    [Range(0f, 1f)]
    [SerializeField] private float beliefDamageReduction = 0.4f;
    [SerializeField] private float vulnerableDamageMultiplier = 2f;

    [Header("Vulnerability")]
    [SerializeField] private float vulnerableDuration = 5f;

    [Range(0f, 1f)]
    [SerializeField] private float recoveryRatio = 1f;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    private EnemyUnit owner;
    private Coroutine vulnerabilityRoutine;
    private BeliefState currentState = BeliefState.Active;
    private bool stopped;

    public event Action<BeliefController, float, float> OnBeliefChanged;
    public event Action<BeliefController> OnBeliefBroken;
    public event Action<BeliefController> OnVulnerableStarted;
    public event Action<BeliefController> OnVulnerableEnded;
    public event Action<BeliefController> OnBeliefRecovered;

    public string BeliefId => beliefId;
    public string BeliefName => beliefName;
    public string BeliefDescription => beliefDescription;
    public float CurrentBelief => currentBelief;
    public float MaxBelief => maxBelief;
    public float NormalizedBelief =>
        maxBelief <= 0f
            ? 0f
            : Mathf.Clamp01(currentBelief / maxBelief);
    public float DefaultBeliefLossAmount => defaultBeliefLossAmount;
    public float BeliefDamageReduction => beliefDamageReduction;
    public float VulnerableDamageMultiplier => vulnerableDamageMultiplier;
    public float VulnerableDuration => vulnerableDuration;
    public float RecoveryRatio => recoveryRatio;
    public BeliefState CurrentState => currentState;
    public bool IsVulnerable =>
        currentState == BeliefState.Vulnerable;
    public bool IsStopped => stopped;

    private void Awake()
    {
        owner = GetComponent<EnemyUnit>();

        SanitizeValues();

        currentBelief = maxBelief;
    }

    private void OnValidate()
    {
        SanitizeValues();
    }

    public void ReduceBelief()
    {
        ReduceBelief(defaultBeliefLossAmount);
    }

    public void ReduceBelief(float amount)
    {
        if (stopped)
            return;

        if (owner != null && owner.IsDead)
            return;

        if (currentState != BeliefState.Active)
            return;

        if (amount <= 0f)
            return;

        float previousBelief = currentBelief;

        currentBelief =
            Mathf.Max(
                currentBelief - amount,
                0f
            );

        if (!Mathf.Approximately(
                previousBelief,
                currentBelief))
        {
            RaiseBeliefChanged();
        }

        if (currentBelief <= 0f)
        {
            BreakBelief();
        }
    }

    public void ResetBelief()
    {
        if (stopped)
            return;

        if (vulnerabilityRoutine != null)
        {
            StopCoroutine(vulnerabilityRoutine);
            vulnerabilityRoutine = null;
        }

        bool wasVulnerable = IsVulnerable;
        currentState = BeliefState.Active;
        currentBelief = maxBelief;
        if (wasVulnerable)
            OnVulnerableEnded?.Invoke(this);
        RaiseBeliefChanged();
    }

    public float ApplyIncomingDamageModifier(float rawDamage)
    {
        if (rawDamage <= 0f)
            return 0f;

        if (stopped)
            return rawDamage;

        if (currentState == BeliefState.Vulnerable)
        {
            return rawDamage *
                   Mathf.Max(
                       0f,
                       vulnerableDamageMultiplier
                   );
        }

        float safeReduction =
            Mathf.Clamp01(
                beliefDamageReduction
            );

        return rawDamage *
               (1f - safeReduction);
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

    [ContextMenu("Debug/Reduce Belief By Default Amount")]
    private void DebugReduceBelief()
    {
        ReduceBelief();
    }

    [ContextMenu("Debug/Break Belief")]
    private void DebugBreakBelief()
    {
        ReduceBelief(maxBelief);
    }

    private void BreakBelief()
    {
        if (stopped)
            return;

        if (currentState != BeliefState.Active)
            return;

        currentBelief = 0f;

        LogState(
            "Belief Broken"
        );

        OnBeliefBroken?.Invoke(this);

        StartVulnerability();
    }

    private void StartVulnerability()
    {
        if (stopped)
            return;

        if (vulnerabilityRoutine != null)
            return;

        currentState = BeliefState.Vulnerable;

        LogState(
            "Vulnerable Started"
        );

        OnVulnerableStarted?.Invoke(this);

        vulnerabilityRoutine =
            StartCoroutine(
                VulnerabilityRoutine()
            );
    }

    private IEnumerator VulnerabilityRoutine()
    {
        float duration =
            Mathf.Max(
                0f,
                vulnerableDuration
            );

        if (duration > 0f)
        {
            yield return new WaitForSeconds(
                duration
            );
        }

        vulnerabilityRoutine = null;

        if (stopped)
            yield break;

        if (owner != null && owner.IsDead)
            yield break;

        EndVulnerabilityAndRecover();
    }

    private void EndVulnerabilityAndRecover()
    {
        if (currentState != BeliefState.Vulnerable)
            return;

        currentState = BeliefState.Active;

        LogState(
            "Vulnerable Ended"
        );

        OnVulnerableEnded?.Invoke(this);

        float recoveredBelief =
            maxBelief *
            Mathf.Clamp01(
                recoveryRatio
            );

        currentBelief =
            Mathf.Clamp(
                recoveredBelief,
                0f,
                maxBelief
            );

        RaiseBeliefChanged();

        LogState(
            "Belief Recovered"
        );

        OnBeliefRecovered?.Invoke(this);
    }

    private void RaiseBeliefChanged()
    {
        OnBeliefChanged?.Invoke(
            this,
            currentBelief,
            maxBelief
        );
    }

    private void SanitizeValues()
    {
        maxBelief =
            Mathf.Max(
                1f,
                maxBelief
            );

        currentBelief =
            Mathf.Clamp(
                currentBelief,
                0f,
                maxBelief
            );

        defaultBeliefLossAmount =
            Mathf.Max(
                0f,
                defaultBeliefLossAmount
            );

        beliefDamageReduction =
            Mathf.Clamp01(
                beliefDamageReduction
            );

        vulnerableDamageMultiplier =
            Mathf.Max(
                0f,
                vulnerableDamageMultiplier
            );

        vulnerableDuration =
            Mathf.Max(
                0f,
                vulnerableDuration
            );

        recoveryRatio =
            Mathf.Clamp01(
                recoveryRatio
            );
    }

    private void LogState(string message)
    {
        if (!logStateChanges)
            return;

        Debug.Log(
            $"[{name}] {message}: {beliefName} ({currentBelief}/{maxBelief})",
            this
        );
    }

    private void OnDisable()
    {
        StopBeliefSystem();
    }
}
