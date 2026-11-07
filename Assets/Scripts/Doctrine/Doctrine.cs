// Doctrine runtime data: [Subject] + [Condition] + [Result].
// e.g. Player + OnHit + Heal = "Player is healed when hit".
// CooldownSec: result cooldown (seconds). 0 = no cooldown.
// Magnitude/Duration/Radius are kept for tuning but new doctrines use
// fixed table values via DoctrineTable (magnitude is an override when > 0).
// This is a runtime copy, distinct from the DoctrineDefinition asset.
[System.Serializable]
public sealed class Doctrine
{
    public SubjectType Subject;
    public ConditionType Condition;
    public ResultType Result;
    public float Magnitude;
    public float Duration;
    public float Radius;
    public float CooldownSec;

    public Doctrine()
    {
    }

    public Doctrine(
        SubjectType subject,
        ConditionType condition,
        ResultType result,
        float magnitude = 0f,
        float duration = 0f,
        float radius = 0f,
        float cooldownSec = 0f)
    {
        Subject = subject;
        Condition = condition;
        Result = result;
        Magnitude = magnitude;
        Duration = duration;
        Radius = radius;
        CooldownSec = cooldownSec;
    }

    public bool IsFullySpecified =>
        Subject != SubjectType.None &&
        Condition != ConditionType.None &&
        Result != ResultType.None;

    // Copy on registration to protect against external mutation.
    public Doctrine Clone()
    {
        return new Doctrine(Subject, Condition, Result, Magnitude, Duration, Radius, CooldownSec);
    }

    // Rule equality check for Deactivate.
    public bool HasSameRule(Doctrine other)
    {
        if (other == null)
        {
            return false;
        }

        return Subject == other.Subject &&
               Condition == other.Condition &&
               Result == other.Result &&
               Magnitude == other.Magnitude &&
               Duration == other.Duration &&
               Radius == other.Radius &&
               CooldownSec == other.CooldownSec;
    }

    public override string ToString()
    {
        return $"{Subject} + {Condition} + {Result} " +
               $"(mag:{Magnitude}, dur:{Duration}, rad:{Radius}, cd:{CooldownSec})";
    }
}
