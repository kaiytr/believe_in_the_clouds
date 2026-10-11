using System;

// 교리 하나를 표현하는 런타임 데이터.
// [주어 Subject] + [조건 Condition] + [결과 Result] 조합.
// 예: Player + OnHit + Heal = "플레이어는 피격당하면 회복한다".
// Magnitude/Duration/Radius는 효과의 수치 파라미터다.
// (Heal량, TakeDamage량, 버프 배율, Explode 피해/반경 등)
// 원본 에셋(DoctrineDefinition)과 구분되는 런타임 복사본으로 사용한다.
[Serializable]
public sealed class Doctrine
{
    public SubjectType Subject;
    public ConditionType Condition;
    public ResultType Result;
    public float Magnitude;
    public float Duration;
    public float Radius;

    public Doctrine()
    {
    }

    public Doctrine(
        SubjectType subject,
        ConditionType condition,
        ResultType result,
        float magnitude = 0f,
        float duration = 0f,
        float radius = 0f)
    {
        Subject = subject;
        Condition = condition;
        Result = result;
        Magnitude = magnitude;
        Duration = duration;
        Radius = radius;
    }

    public bool IsFullySpecified =>
        Subject != SubjectType.None &&
        Condition != ConditionType.None &&
        Result != ResultType.None;

    // 등록 시 외부 변경으로부터 보호하기 위한 복사.
    public Doctrine Clone()
    {
        return new Doctrine(Subject, Condition, Result, Magnitude, Duration, Radius);
    }

    // 해제(Deactivate) 시 규칙 동등성 비교용.
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
               Radius == other.Radius;
    }

    public override string ToString()
    {
        return $"{Subject} + {Condition} + {Result} " +
               $"(mag:{Magnitude}, dur:{Duration}, rad:{Radius})";
    }
}
