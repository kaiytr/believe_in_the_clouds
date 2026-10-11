using System;
using UnityEngine;

// 교리 유효성 검증 계층.
// 유효하지 않은 교리는 활성화되지 않으며, 실패 이유를 문자열로 반환한다.
// 상반 효과의 충돌 해결(우선순위 등)은 이번 단계에서 다루지 않고,
// 금지/우선순위 확정도 하지 않는다.
public static class DoctrineValidator
{
    public static bool TryValidate(Doctrine doctrine, out string reason)
    {
        return TryValidate(doctrine, null, out reason);
    }

    // target이 null이면 규칙 수준(지정 여부, 지원 여부, 수치 범위)만 검사한다.
    // target이 지정되면(Owner 바인딩 등록) 효과 실행에 필요한 컴포넌트도 검사한다.
    public static bool TryValidate(Doctrine doctrine, GameObject target, out string reason)
    {
        if (doctrine == null)
        {
            reason = "교리가 null입니다.";
            return false;
        }

        if (doctrine.Subject == SubjectType.None ||
            !Enum.IsDefined(typeof(SubjectType), doctrine.Subject))
        {
            reason = $"Subject가 지정되지 않았거나 지원하지 않는 값입니다: {doctrine.Subject}.";
            return false;
        }

        if (doctrine.Condition == ConditionType.None ||
            !Enum.IsDefined(typeof(ConditionType), doctrine.Condition))
        {
            reason = $"Condition이 지정되지 않았거나 지원하지 않는 값입니다: {doctrine.Condition}.";
            return false;
        }

        if (doctrine.Result == ResultType.None ||
            !Enum.IsDefined(typeof(ResultType), doctrine.Result))
        {
            reason = $"Result가 지정되지 않았거나 지원하지 않는 값입니다: {doctrine.Result}.";
            return false;
        }

        if (!DoctrineEffectExecutor.IsSupported(doctrine.Result))
        {
            reason = $"구현되지 않은 Result입니다: {doctrine.Result}.";
            return false;
        }

        if (doctrine.Magnitude < 0f)
        {
            reason = "Magnitude(효과 수치)는 0 이상이어야 합니다.";
            return false;
        }

        if (doctrine.Duration < 0f)
        {
            reason = "Duration(지속 시간)은 0 이상이어야 합니다.";
            return false;
        }

        if (doctrine.Radius < 0f)
        {
            reason = "Radius(반경)는 0 이상이어야 합니다.";
            return false;
        }

        if (target != null)
        {
            if (doctrine.Subject != SubjectType.All)
            {
                DoctrineSubject subject = target.GetComponent<DoctrineSubject>();
                if (subject == null)
                {
                    reason = $"대상({target.name})에 DoctrineSubject가 없어 " +
                             $"Subject({doctrine.Subject}) 일치 여부를 확인할 수 없습니다.";
                    return false;
                }

                if (subject.Identity == SubjectType.None)
                {
                    reason = $"대상({target.name})의 Subject Identity가 None이라 교리를 실행할 수 없습니다.";
                    return false;
                }

                if (subject.Identity != doctrine.Subject)
                {
                    reason = $"대상의 Subject({subject.Identity})가 교리의 Subject({doctrine.Subject})와 달라 " +
                             "결코 발동할 수 없는 교리입니다.";
                    return false;
                }
            }

            switch (doctrine.Result)
            {
                case ResultType.Heal:
                case ResultType.TakeDamage:
                    if (target.GetComponent<IHealth>() == null)
                    {
                        reason = $"Result({doctrine.Result}) 실행에 IHealth가 필요하지만 " +
                                 $"대상({target.name})에 없습니다.";
                        return false;
                    }

                    break;

                case ResultType.Slow:
                case ResultType.IncreaseSpeed:
                case ResultType.IncreaseAttack:
                    if (target.GetComponent<DoctrineStats>() == null)
                    {
                        reason = $"Result({doctrine.Result}) 실행에 DoctrineStats가 필요하지만 " +
                                 $"대상({target.name})에 없습니다.";
                        return false;
                    }

                    break;

                case ResultType.Explode:
                    break;

                default:
                    reason = $"지원하지 않는 Result입니다: {doctrine.Result}.";
                    return false;
            }
        }

        reason = null;
        return true;
    }
}
