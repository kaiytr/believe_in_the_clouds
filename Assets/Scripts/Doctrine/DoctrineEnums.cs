// 교리(Doctrine) 시스템의 타입 안전한 식별자 모음.
// 문자열 비교 대신 enum으로 주어/조건/결과를 관리한다.
// None = 0 값은 "미지정" 검출용이다.
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
    LowHealth = 4,
    OnDodge = 5
}

public enum ResultType
{
    None = 0,
    Heal = 1,
    Explode = 2,
    Slow = 3,
    IncreaseSpeed = 4,
    IncreaseAttack = 5,
    TakeDamage = 6
}
