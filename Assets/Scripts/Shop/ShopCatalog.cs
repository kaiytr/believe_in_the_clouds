using System.Collections.Generic;

// 상점 카드 한 장 = 교리의 구성 요소 하나.
// 표(주어 4 / 조건 4 / 결과 4)를 그대로 3열 × 4행으로 보여주기 위한 데이터다.
public enum ShopColumn
{
    Subject = 0,
    Condition = 1,
    Result = 2
}

public enum ShopRarity { Common, Uncommon, Rare }

public sealed class ShopEntry
{
    public string Id { get; internal set; }
    public ShopColumn Column { get; internal set; }
    public string Label { get; internal set; }
    public string Detail { get; internal set; }
    public ShopRarity Rarity { get; internal set; }
    public int Price => Rarity == ShopRarity.Rare ? 95 : Rarity == ShopRarity.Uncommon ? 55 : 30;

    public SubjectType Subject { get; internal set; } = SubjectType.None;
    public ConditionType Condition { get; internal set; } = ConditionType.None;
    public ResultType Result { get; internal set; } = ResultType.None;
}

public static class ShopCatalog
{
    private static readonly ShopEntry[] SubjectEntries =
    {
        new ShopEntry
        {
            Column = ShopColumn.Subject,
            Subject = SubjectType.Player,
            Id = "subject.player", Rarity = ShopRarity.Common,
            Label = "플레이어",
            Detail = "플레이어는"
        },
        new ShopEntry
        {
            Column = ShopColumn.Subject,
            Subject = SubjectType.Enemy,
            Id = "subject.enemy", Rarity = ShopRarity.Common,
            Label = "일반 적",
            Detail = "일반 적은"
        },
        new ShopEntry
        {
            Column = ShopColumn.Subject,
            Subject = SubjectType.Boss,
            Id = "subject.boss", Rarity = ShopRarity.Uncommon,
            Label = "보스",
            Detail = "보스는"
        },
        new ShopEntry
        {
            Column = ShopColumn.Subject,
            Subject = SubjectType.All,
            Id = "subject.all", Rarity = ShopRarity.Rare,
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
            Id = "condition.hit", Rarity = ShopRarity.Uncommon,
            Label = "피격",
            Detail = "피격당하면"
        },
        new ShopEntry
        {
            Column = ShopColumn.Condition,
            Condition = ConditionType.OnAttack,
            Id = "condition.attack", Rarity = ShopRarity.Uncommon,
            Label = "공격",
            Detail = "공격하면"
        },
        new ShopEntry
        {
            Column = ShopColumn.Condition,
            Condition = ConditionType.OnDeath,
            Id = "condition.death", Rarity = ShopRarity.Common,
            Label = "사망",
            Detail = "사망하면"
        },
        new ShopEntry
        {
            Column = ShopColumn.Condition,
            Condition = ConditionType.LowHealth,
            Id = "condition.low-health", Rarity = ShopRarity.Common,
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
            Id = "result.heal", Rarity = ShopRarity.Uncommon,
            Label = "회복",
            Detail = "회복한다 · 최대체력 10%"
        },
        new ShopEntry
        {
            Column = ShopColumn.Result,
            Result = ResultType.Explode,
            Id = "result.explode", Rarity = ShopRarity.Rare,
            Label = "폭발",
            Detail = "폭발한다 · 최대체력 30%"
        },
        new ShopEntry
        {
            Column = ShopColumn.Result,
            Result = ResultType.IncreaseSpeed,
            Id = "result.speed", Rarity = ShopRarity.Common,
            Label = "이속 증가",
            Detail = "이동속도가 증가한다 · +40%, 5초"
        },
        new ShopEntry
        {
            Column = ShopColumn.Result,
            Result = ResultType.IncreaseAttack,
            Id = "result.attack", Rarity = ShopRarity.Uncommon,
            Label = "피해 증가",
            Detail = "피해가 증가한다 · +20%, 5초"
        }
    };

    public static IReadOnlyList<ShopEntry> GetColumn(ShopColumn column)
    {
        switch (column)
        {
            case ShopColumn.Subject: return System.Array.AsReadOnly(SubjectEntries);
            case ShopColumn.Condition: return System.Array.AsReadOnly(ConditionEntries);
            case ShopColumn.Result: return System.Array.AsReadOnly(ResultEntries);
            default: throw new System.ArgumentOutOfRangeException(nameof(column));
        }
    }

    public static bool TryGet(string id, out ShopEntry entry)
    {
        foreach (ShopColumn column in System.Enum.GetValues(typeof(ShopColumn)))
            foreach (ShopEntry item in GetColumn(column))
                if (item.Id == id) { entry = item; return true; }
        entry = null;
        return false;
    }

    public static string IdFor(SubjectType value) => Find(ShopColumn.Subject, item => item.Subject == value);
    public static string IdFor(ConditionType value) => Find(ShopColumn.Condition, item => item.Condition == value);
    public static string IdFor(ResultType value) => Find(ShopColumn.Result, item => item.Result == value);
    private static string Find(ShopColumn column, System.Predicate<ShopEntry> matches)
    {
        foreach (ShopEntry entry in GetColumn(column)) if (matches(entry)) return entry.Id;
        return null;
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
