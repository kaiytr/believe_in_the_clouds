using System.Text;
using UnityEngine;

// Doctrine을 사람이 읽는 문장으로 바꾼다.
// 주어 + 조건 + 결과 = "플레이어는 피격당하면 회복한다 (+30)"
// UI뿐 아니라 로그/테스트에서도 재사용할 수 있게 순수 함수로 둔다.
public static class DoctrineText
{
    public static string Subject(SubjectType subject)
    {
        switch (subject)
        {
            case SubjectType.Player: return "플레이어는";
            case SubjectType.Enemy: return "적은";
            case SubjectType.Boss: return "보스는";
            case SubjectType.All: return "모두";
            default: return "?";
        }
    }

    public static string Condition(ConditionType condition)
    {
        switch (condition)
        {
            case ConditionType.OnHit: return "피격당하면";
            case ConditionType.OnAttack: return "공격하면";
            case ConditionType.OnDeath: return "죽으면";
            case ConditionType.LowHealth: return "체력이 낮아지면";
            case ConditionType.OnDodge: return "회피하면";
            default: return "?";
        }
    }

    public static string Result(ResultType result)
    {
        switch (result)
        {
            case ResultType.Heal: return "회복한다";
            case ResultType.Explode: return "주변을 폭발시킨다";
            case ResultType.Slow: return "느려지게 한다";
            case ResultType.IncreaseSpeed: return "빨라진다";
            case ResultType.IncreaseAttack: return "공격력이 강해진다";
            case ResultType.TakeDamage: return "피해를 입는다";
            default: return "?";
        }
    }

    public static string Describe(Doctrine doctrine)
    {
        if (doctrine == null)
            return "빈 교리";

        StringBuilder sb = new StringBuilder();
        sb.Append(Subject(doctrine.Subject));
        sb.Append(' ');
        sb.Append(Condition(doctrine.Condition));
        sb.Append(' ');
        sb.Append(Result(doctrine.Result));
        sb.Append(Explain(doctrine));
        return sb.ToString();
    }

    // 슬롯 칸에 들어가는 짧은 이름. 이름을 지정하지 않았을 때만 쓴다.
    // 예: Player + OnHit + Heal -> "피격 회복"
    public static string ShortName(Doctrine doctrine)
    {
        if (doctrine == null)
            return "교리";

        return ConditionShort(doctrine.Condition) + " " + ResultShort(doctrine.Result);
    }

    public static string ConditionShort(ConditionType condition)
    {
        switch (condition)
        {
            case ConditionType.OnHit: return "피격";
            case ConditionType.OnAttack: return "공격";
            case ConditionType.OnDeath: return "죽음";
            case ConditionType.LowHealth: return "저체력";
            case ConditionType.OnDodge: return "회피";
            default: return "?";
        }
    }

    public static string ResultShort(ResultType result)
    {
        switch (result)
        {
            case ResultType.Heal: return "회복";
            case ResultType.Explode: return "폭발";
            case ResultType.Slow: return "둔화";
            case ResultType.IncreaseSpeed: return "가속";
            case ResultType.IncreaseAttack: return "강화";
            case ResultType.TakeDamage: return "피해";
            default: return "?";
        }
    }

    // 수치/지속시간/반경을 괄호로 덧붙인다.
    private static string Explain(Doctrine doctrine)
    {
        StringBuilder sb = new StringBuilder();

        if (doctrine.Magnitude > 0f)
        {
            switch (doctrine.Result)
            {
                case ResultType.Heal:
                    sb.Append($" (+{doctrine.Magnitude:0.#})");
                    break;
                case ResultType.TakeDamage:
                case ResultType.Explode:
                    sb.Append($" ({doctrine.Magnitude:0.#} 피해)");
                    break;
                case ResultType.Slow:
                    sb.Append($" (속도 {doctrine.Magnitude * 100f:0}%)");
                    break;
                case ResultType.IncreaseSpeed:
                case ResultType.IncreaseAttack:
                    sb.Append($" (x{doctrine.Magnitude:0.##})");
                    break;
            }
        }

        if (doctrine.Duration > 0f)
            sb.Append($" · {doctrine.Duration:0.#}초");

        if (doctrine.Radius > 0f && doctrine.Result == ResultType.Explode)
            sb.Append($" · 반경 {doctrine.Radius:0.#}");

        return sb.ToString();
    }
}
