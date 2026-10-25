using System.Collections.Generic;

// 상점 카드 한 장 = 교리의 구성 요소 하나.
// 표(주어 4 / 조건 4 / 결과 4)를 그대로 3열 × 4행으로 보여주기 위한 데이터다.
public enum ShopColumn
{
    Subject = 0,
    Condition = 1,
    Result = 2
}

public sealed class ShopEntry
{
    public ShopColumn Column;
    public string Label;
    public string Detail;

    public SubjectType Subject = SubjectType.None;
    public ConditionType Condition = ConditionType.None;
    public ResultType Result = ResultType.None;
}

public static class ShopCatalog
{
    private static readonly ShopEntry[] SubjectEntries =
    {
        new ShopEntry
        {
            Column = ShopColumn.Subject,
            Subject = SubjectType.Player,
            Label = "플레이어",
            Detail = "플레이어는"
        },
        new ShopEntry
        {
            Column = ShopColumn.Subject,
            Subject = SubjectType.Enemy,
            Label = "일반 적",
            Detail = "일반 적은"
        },
        new ShopEntry
        {
            Column = ShopColumn.Subject,
            Subject = SubjectType.Boss,
            Label = "보스",
            Detail = "보스는"
        },
        new ShopEntry
        {
            Column = ShopColumn.Subject,
            Subject = SubjectType.All,
            Label = "모든 존재",
            Detail = "모든 존재는"
        }
    };

    private static readonly ShopEntry[] ConditionEntries =
    {
        new ShopEntry
        {
            Column = ShopColumn.Condition,
            Condition = ConditionType.OnHit,
            Label = "피격",
            Detail = "피격당하면"
        },
        new ShopEntry
        {
            Column = ShopColumn.Condition,
            Condition = ConditionType.OnAttack,
            Label = "공격",
            Detail = "공격하면"
        },
        new ShopEntry
        {
            Column = ShopColumn.Condition,
            Condition = ConditionType.OnDeath,
            Label = "사망",
            Detail = "사망하면"
        },
        new ShopEntry
        {
            Column = ShopColumn.Condition,
            Condition = ConditionType.LowHealth,
            Label = "체력 30% 이하",
            Detail = "체력이 30% 이하가 되면"
        }
    };

    private static readonly ShopEntry[] ResultEntries =
    {
        new ShopEntry
        {
            Column = ShopColumn.Result,
            Result = ResultType.Heal,
            Label = "회복",
            Detail = "회복한다 · 최대체력 10%"
        },
        new ShopEntry
        {
            Column = ShopColumn.Result,
            Result = ResultType.Explode,
            Label = "폭발",
            Detail = "폭발한다 · 최대체력 30%"
        },
        new ShopEntry
        {
            Column = ShopColumn.Result,
            Result = ResultType.IncreaseSpeed,
            Label = "이속 증가",
            Detail = "이동속도가 증가한다 · +40%"
        },
        new ShopEntry
        {
            Column = ShopColumn.Result,
            Result = ResultType.IncreaseAttack,
            Label = "피해 증가",
            Detail = "피해가 증가한다 · +20%, 5초"
        }
    };

    public static IReadOnlyList<ShopEntry> GetColumn(ShopColumn column)
    {
        switch (column)
        {
            case ShopColumn.Subject: return SubjectEntries;
            case ShopColumn.Condition: return ConditionEntries;
            case ShopColumn.Result: return ResultEntries;
            default: return SubjectEntries;
        }
    }

    /// <summary>세 열에서 하나씩 고르면 완성된 교리가 된다. 하나라도 비면 null.</summary>
    public static Doctrine Compose(ShopEntry subject, ShopEntry condition, ShopEntry result)
    {
        if (subject == null || condition == null || result == null)
        {
            return null;
        }

        return new Doctrine(subject.Subject, condition.Condition, result.Result);
    }
}
