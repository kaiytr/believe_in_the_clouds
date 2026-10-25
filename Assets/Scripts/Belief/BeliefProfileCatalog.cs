using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Belief/Belief Profile Catalog", fileName = "BeliefProfileCatalog")]
public sealed class BeliefProfileCatalog : ScriptableObject
{
    [SerializeField] private List<BeliefProfile> profiles = new List<BeliefProfile>();
    // Read old catalogs without changing their GUIDs or serialized enum values.
    [SerializeField, HideInInspector] private BeliefProfile rearPositionProfile;
    [SerializeField, HideInInspector] private BeliefProfile dodgeAttackProfile;

    public IReadOnlyList<BeliefProfile> Profiles => profiles;
    public BeliefProfile RearPositionProfile => Resolve(BeliefType.RearPosition);
    public BeliefProfile DodgeAttackProfile => Resolve(BeliefType.DodgeAttack);
    public BeliefProfile DefaultProfile => RearPositionProfile;

    public BeliefProfile Resolve(BeliefType type)
    {
        if (type == BeliefType.Unassigned) type = BeliefType.RearPosition;
        if (profiles != null && profiles.Count > 0)
        {
            foreach (BeliefProfile profile in profiles)
                if (profile != null && profile.Type == type) return profile;
            return null;
        }
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
        if (profiles != null && profiles.Count > 0)
        {
            var types = new HashSet<BeliefType>();
            foreach (BeliefProfile profile in profiles)
            {
                if (profile == null || !types.Add(profile.Type))
                {
                    error = "Belief catalog contains a missing profile or a duplicate content type.";
                    return false;
                }
                if (!profile.IsValid(out error)) return false;
            }
            if (!types.Contains(BeliefType.RearPosition))
            {
                error = "Belief catalog requires its explicit rear-position default.";
                return false;
            }
            error = null;
            return true;
        }
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
