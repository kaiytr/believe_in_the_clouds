// Doctrine system identifier enums.
// Subject/Condition/Result are managed as enums instead of strings.
// None = 0 is for "unspecified" detection.
// Fixed by table: Subjects 4 / Conditions 4 / Results 4.
// (Conditions: OnHit/OnAttack/OnDeath/LowHealth, Results: Heal/Explode/IncreaseSpeed/IncreaseAttack)
// Numeric values keep serialized asset compatibility (removed Slow=3, OnDodge=5, TakeDamage=6).
public enum SubjectType
{
    None = 0,
    Player = 1,
    Enemy = 2,
    Boss = 3,
    All = 4
}

public enum ConditionType
{
    None = 0,
    OnHit = 1,
    OnAttack = 2,
    OnDeath = 3,
    LowHealth = 4
}

public enum ResultType
{
    None = 0,
    Heal = 1,
    Explode = 2,
    IncreaseSpeed = 4,
    IncreaseAttack = 5
}
