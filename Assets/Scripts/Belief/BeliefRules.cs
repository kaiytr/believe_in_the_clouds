public enum BeliefRule
{
    Unassigned,
    LegacyRearPosition,
    LegacyDodgeAttack,
    Counterattack,
    NoHpDamage,
    RearHit,
    ConsecutiveHits,
    RetreatPunish,
    EscapeCircle,
    RearDwell,
    PrayerHit
}

/// <summary>Content identity is separate from the rule used to evaluate it.</summary>
public static class BeliefRules
{
    public static BeliefRule For(BeliefType type) => type switch
    {
        BeliefType.RearPosition => BeliefRule.LegacyRearPosition,
        BeliefType.DodgeAttack => BeliefRule.LegacyDodgeAttack,
        BeliefType.DogCounterattack => BeliefRule.Counterattack,
        BeliefType.DogUnerringAttack or BeliefType.BelieverUnerringStone => BeliefRule.NoHpDamage,
        BeliefType.DogRearAttack => BeliefRule.RearHit,
        BeliefType.BelieverConsecutiveHits => BeliefRule.ConsecutiveHits,
        BeliefType.BelieverSafeDistance => BeliefRule.RetreatPunish,
        BeliefType.ExecutiveEscapeCircle => BeliefRule.EscapeCircle,
        BeliefType.ExecutiveRearPosition => BeliefRule.RearDwell,
        BeliefType.ExecutivePrayer => BeliefRule.PrayerHit,
        _ => BeliefRule.Unassigned
    };

    public static bool IsExpanded(BeliefType type) => type >= BeliefType.DogCounterattack && type <= BeliefType.ExecutivePrayer;

    public static BeliefType[] CandidatesFor(EnemyUnit enemy)
    {
        if (enemy is DogEnemy)
            return new[] { BeliefType.DogCounterattack, BeliefType.DogUnerringAttack, BeliefType.DogRearAttack };
        if (enemy is BelieverEnemy)
            return new[] { BeliefType.BelieverConsecutiveHits, BeliefType.BelieverUnerringStone, BeliefType.BelieverSafeDistance };
        if (enemy is ExecutiveEnemy)
            return new[] { BeliefType.ExecutiveEscapeCircle, BeliefType.ExecutiveRearPosition, BeliefType.ExecutivePrayer };
        return System.Array.Empty<BeliefType>();
    }

    public static bool IsCompatible(BeliefType type, EnemyUnit enemy)
    {
        if (!IsExpanded(type) || enemy == null) return true;
        foreach (BeliefType candidate in CandidatesFor(enemy))
            if (candidate == type) return true;
        return false;
    }
}
