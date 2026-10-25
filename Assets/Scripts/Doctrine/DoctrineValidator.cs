using System;
using UnityEngine;

// Doctrine validation layer.
// Invalid doctrines are not activated; the failure reason is returned as string.
// Per-result cooldown / priority rules are not decided here.
public static class DoctrineValidator
{
    public static bool TryValidate(Doctrine doctrine, out string reason)
    {
        return TryValidate(doctrine, null, out reason);
    }

    // target == null: rule-level check only (specified/supported/value ranges).
    // target != null (Owner-bound registration): also checks effect prerequisites.
    public static bool TryValidate(Doctrine doctrine, GameObject target, out string reason)
    {
        if (doctrine == null)
        {
            reason = "Doctrine is null.";
            return false;
        }

        if (doctrine.Subject == SubjectType.None ||
            !Enum.IsDefined(typeof(SubjectType), doctrine.Subject))
        {
            reason = $"Subject is unspecified or unsupported: {doctrine.Subject}.";
            return false;
        }

        if (doctrine.Condition == ConditionType.None ||
            !Enum.IsDefined(typeof(ConditionType), doctrine.Condition))
        {
            reason = $"Condition is unspecified or unsupported: {doctrine.Condition}.";
            return false;
        }

        if (doctrine.Result == ResultType.None ||
            !Enum.IsDefined(typeof(ResultType), doctrine.Result))
        {
            reason = $"Result is unspecified or unsupported: {doctrine.Result}.";
            return false;
        }

        if (!DoctrineEffectExecutor.IsSupported(doctrine.Result))
        {
            reason = $"Unimplemented Result: {doctrine.Result}.";
            return false;
        }

        if (doctrine.Magnitude < 0f)
        {
            reason = "Magnitude must be 0 or above.";
            return false;
        }

        if (doctrine.Duration < 0f)
        {
            reason = "Duration must be 0 or above.";
            return false;
        }

        if (doctrine.Radius < 0f)
        {
            reason = "Radius must be 0 or above.";
            return false;
        }

        if (doctrine.CooldownSec < 0f)
        {
            reason = "CooldownSec must be 0 or above.";
            return false;
        }

        if (target != null)
        {
            if (doctrine.Subject != SubjectType.All)
            {
                DoctrineSubject subject = target.GetComponent<DoctrineSubject>();
                if (subject == null)
                {
                    reason = $"Target ({target.name}) has no DoctrineSubject; " +
                             $"cannot match Subject ({doctrine.Subject}).";
                    return false;
                }

                if (subject.Identity == SubjectType.None)
                {
                    reason = $"Target ({target.name}) Subject Identity is None.";
                    return false;
                }

                if (subject.Identity != doctrine.Subject)
                {
                    reason = $"Target Subject ({subject.Identity}) differs from " +
                             $"doctrine Subject ({doctrine.Subject}); never triggers.";
                    return false;
                }
            }

            switch (doctrine.Result)
            {
                case ResultType.Heal:
                    if (target.GetComponent<IHealth>() == null)
                    {
                        reason = $"Result ({doctrine.Result}) needs IHealth but " +
                                  $"target ({target.name}) has none.";
                        return false;
                    }

                    break;

                case ResultType.IncreaseSpeed:
                case ResultType.IncreaseAttack:
                    if (target.GetComponent<DoctrineStats>() == null)
                    {
                        reason = $"Result ({doctrine.Result}) needs DoctrineStats but " +
                                  $"target ({target.name}) has none.";
                        return false;
                    }

                    break;

                case ResultType.Explode:
                    break;

                default:
                    reason = $"Unsupported Result: {doctrine.Result}.";
                    return false;
            }
        }

        reason = null;
        return true;
    }
}
