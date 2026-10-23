using UnityEngine;

[CreateAssetMenu(menuName = "Belief/Belief Profile Catalog", fileName = "BeliefProfileCatalog")]
public sealed class BeliefProfileCatalog : ScriptableObject
{
    [SerializeField] private BeliefProfile rearPositionProfile;
    [SerializeField] private BeliefProfile dodgeAttackProfile;

    public BeliefProfile RearPositionProfile => rearPositionProfile;
    public BeliefProfile DodgeAttackProfile => dodgeAttackProfile;
    public BeliefProfile DefaultProfile => rearPositionProfile;

    public BeliefProfile Resolve(BeliefType type)
    {
        switch (type)
        {
            case BeliefType.RearPosition:
                return rearPositionProfile;
            case BeliefType.DodgeAttack:
                return dodgeAttackProfile;
            case BeliefType.Unassigned:
                return rearPositionProfile;
            default:
                return null;
        }
    }

    public bool IsValid(out string error)
    {
        if (rearPositionProfile == null || dodgeAttackProfile == null)
        {
            error = "Belief profile catalog requires both V1 profiles.";
            return false;
        }

        if (rearPositionProfile.Type != BeliefType.RearPosition ||
            dodgeAttackProfile.Type != BeliefType.DodgeAttack)
        {
            error = "Belief profile catalog contains profiles in the wrong slots.";
            return false;
        }

        if (!rearPositionProfile.IsValid(out error) ||
            !dodgeAttackProfile.IsValid(out error))
        {
            return false;
        }

        error = null;
        return true;
    }
}
