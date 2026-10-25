using UnityEngine;

[CreateAssetMenu(menuName = "Belief/Belief Profile", fileName = "BeliefProfile")]
public sealed class BeliefProfile : ScriptableObject
{
    [SerializeField] private BeliefType type = BeliefType.Unassigned;
    [SerializeField, TextArea] private string phrase;
    [SerializeField, Min(1f)] private float maxBelief = 100f;
    [SerializeField, Min(0f)] private float maintainedPlayerDamageMultiplier = 0.6f;
    [SerializeField, Min(0f)] private float vulnerablePlayerDamageMultiplier = 2f;
    [SerializeField, Min(0f)] private float vulnerableDuration = 5f;
    [SerializeField, Range(0f, 1f)] private float recoveryRatio = 1f;
    [SerializeField, Min(0f)] private float reductionPerSecond;
    [SerializeField, Min(0f)] private float reductionPerEvade;
    [SerializeField, Min(0f)] private float reductionPerSuccess;
    [SerializeField, Min(0.01f)] private float timeWindow = 2f;
    [SerializeField, Min(0.01f)] private float rearInterval = 2f;

    public BeliefType Type => type;
    public string Phrase => phrase;
    public float MaxBelief => maxBelief;
    public float MaintainedPlayerDamageMultiplier => maintainedPlayerDamageMultiplier;
    public float VulnerablePlayerDamageMultiplier => vulnerablePlayerDamageMultiplier;
    public float VulnerableDuration => vulnerableDuration;
    public float RecoveryRatio => recoveryRatio;
    public float ReductionPerSecond => reductionPerSecond;
    public float ReductionPerEvade => reductionPerEvade;
    public BeliefRule Rule => BeliefRules.For(type);
    public float ReductionPerSuccess => reductionPerSuccess;
    public float TimeWindow => timeWindow;
    public float RearInterval => rearInterval;

    public bool IsValid(out string error)
    {
        if (Rule == BeliefRule.Unassigned)
        {
            error = "Belief profile type is Unassigned.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(phrase))
        {
            error = "Belief profile phrase is empty.";
            return false;
        }

        if (maxBelief <= 0f || vulnerableDuration <= 0f)
        {
            error = "Belief profile requires positive maximum belief and vulnerable duration.";
            return false;
        }

        if (maintainedPlayerDamageMultiplier < 0f || vulnerablePlayerDamageMultiplier < 0f)
        {
            error = "Belief profile damage multipliers cannot be negative.";
            return false;
        }

        if (recoveryRatio < 0f || recoveryRatio > 1f)
        {
            error = "Belief profile recovery ratio must be between 0 and 1.";
            return false;
        }

        bool hasReduction = Rule switch
        {
            BeliefRule.LegacyRearPosition => reductionPerSecond > 0f,
            BeliefRule.LegacyDodgeAttack => reductionPerEvade > 0f,
            _ => reductionPerSuccess > 0f && timeWindow > 0f && rearInterval > 0f
        };
        if (!hasReduction)
        {
            error = "Belief profile is missing the reduction value for its type.";
            return false;
        }

        error = null;
        return true;
    }
}
