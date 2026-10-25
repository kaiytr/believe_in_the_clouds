using System.Text;
using UnityEngine;

// Doctrine을 사람이 읽는 문장으로 바꾼다.
// 주어 + 조건 + 결과 = 표 고정값 기준.
// UI뿐 아니라 로그/테스트에서도 재사용할 수 있게 순수 함수로 둔다.
public static class DoctrineText
{
    public static string Subject(SubjectType subject)
    {
        switch (subject)
        {
            case SubjectType.Player: return "플레이어는";
            case SubjectType.Enemy: return "일반 적은";
            case SubjectType.Boss: return "보스는";
            case SubjectType.All: return "모든 존재는";
            default: return "?";
        }
    }

    public static string Condition(ConditionType condition)
    {
        switch (condition)
        {
            case ConditionType.OnHit: return "피격당하면";
            case ConditionType.OnAttack: return "공격하면";
            case ConditionType.OnDeath: return "사망하면";
            case ConditionType.LowHealth: return "체력이 30% 이하가 되면";
            default: return "?";
        }
    }

    public static string Result(ResultType result)
    {
        switch (result)
        {
            case ResultType.Heal: return "회복한다";
            case ResultType.Explode: return "폭발한다";
            case ResultType.IncreaseSpeed: return "이동속도가 증가한다";
            case ResultType.IncreaseAttack: return "가하는 피해가 증가한다";
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
            case ConditionType.OnDeath: return "사망";
            case ConditionType.LowHealth: return "체력30%이하";
            default: return "?";
        }
    }

    public static string ResultShort(ResultType result)
    {
        switch (result)
        {
            case ResultType.Heal: return "회복";
            case ResultType.Explode: return "폭발";
            case ResultType.IncreaseSpeed: return "이속증가";
            case ResultType.IncreaseAttack: return "피해증가";
            default: return "?";
        }
    }

    // 표 고정값 안내.
    private static string Explain(Doctrine doctrine)
    {
        StringBuilder sb = new StringBuilder();

        switch (doctrine.Result)
        {
            case ResultType.Heal:
                sb.Append(" (최대체력 10%, 20초 쿨타임)");
                break;
            case ResultType.Explode:
                sb.Append(" (최대체력 30%, 아군·적군 무구분, 5초 쿨타임)");
                break;
            case ResultType.IncreaseSpeed:
                sb.Append(" (기본 이속 +40%)");
                break;
            case ResultType.IncreaseAttack:
                sb.Append(" (직후 5초간 +20%)");
                break;
        }

        if (doctrine.Radius > 0f && doctrine.Result == ResultType.Explode)
            sb.Append($" · 반경 {doctrine.Radius:0.#}");

        return sb.ToString();
    }
}
