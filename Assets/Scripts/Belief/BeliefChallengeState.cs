using System;

/// <summary>Per-enemy challenge memory, using explicit game time for deterministic boundaries.</summary>
public sealed class BeliefChallengeState
{
    private readonly BeliefRule rule;
    private readonly float reduction, window, rearInterval;
    private float counterStartedAt, lastHitAt, retreatStartedAt;
    private double rearElapsed;
    private bool counterOpen, hasLastHit, retreatOpen, wasRetreating, wasInside, retreatConsumed;

    public BeliefChallengeState(BeliefRule rule, float reduction, float window = 2f, float rearInterval = 2f)
    {
        if (reduction <= 0f || window <= 0f || rearInterval <= 0f)
            throw new ArgumentOutOfRangeException(nameof(reduction));
        this.rule = rule;
        this.reduction = reduction;
        this.window = window;
        this.rearInterval = rearInterval;
    }

    public void Reset()
    {
        counterOpen = hasLastHit = retreatOpen = wasRetreating = wasInside = retreatConsumed = false;
        rearElapsed = 0d;
    }

    public float ResolveAttack(EnemyAttackResult result)
    {
        if (!result.WasLaunched || !result.CompletedNormally) return 0f;
        if (rule == BeliefRule.Counterattack && result.Outcome == AttackOutcome.EvadedByPlayer)
        {
            counterOpen = true;
            counterStartedAt = result.OccurredAt;
        }
        if (rule == BeliefRule.NoHpDamage && !result.PlayerHpDamaged &&
            result.Outcome != AttackOutcome.Blocked && result.Outcome != AttackOutcome.Cancelled)
            return reduction;
        return rule == BeliefRule.EscapeCircle && result.Outcome == AttackOutcome.EvadedByPlayer ? reduction : 0f;
    }

    public float Hit(float now, bool behind, bool preparingCircle)
    {
        switch (rule)
        {
            case BeliefRule.Counterattack:
                if (!counterOpen || !Within(now, counterStartedAt)) return 0f;
                counterOpen = false;
                return reduction;
            case BeliefRule.RearHit:
                return behind ? reduction : 0f;
            case BeliefRule.ConsecutiveHits:
                bool consecutive = hasLastHit && Within(now, lastHitAt);
                lastHitAt = now;
                hasLastHit = true;
                return consecutive ? reduction : 0f;
            case BeliefRule.RetreatPunish:
                if (!retreatOpen || retreatConsumed || !Within(now, retreatStartedAt)) return 0f;
                retreatConsumed = true;
                retreatOpen = false;
                return reduction;
            case BeliefRule.PrayerHit:
                return preparingCircle ? reduction : 0f;
            default:
                return 0f;
        }
    }

    public void ObserveRetreat(bool retreating, bool inside, float now)
    {
        if (rule != BeliefRule.RetreatPunish) return;
        if (!retreating)
        {
            wasRetreating = wasInside = retreatOpen = retreatConsumed = false;
            return;
        }
        if (!wasRetreating) wasInside = false;
        if (!retreatConsumed && inside && !wasInside)
        {
            retreatOpen = true;
            retreatStartedAt = now;
        }
        wasRetreating = true;
        wasInside = inside;
    }

    public float TickRear(bool behind, float deltaTime)
    {
        if (rule != BeliefRule.RearDwell) return 0f;
        if (!behind)
        {
            rearElapsed = 0d;
            return 0f;
        }
        if (deltaTime <= 0f) return 0f;
        rearElapsed += deltaTime;
        int intervals = (int)Math.Floor(rearElapsed / rearInterval);
        rearElapsed -= intervals * (double)rearInterval;
        return intervals * reduction;
    }

    private bool Within(float now, float start) => now >= start && now - start <= window;
}
