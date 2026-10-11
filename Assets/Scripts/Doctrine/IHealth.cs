using UnityEngine;

// 교리 시스템이 의존하는 최소 체력 계약.
// 친구의 PlayerHealth와 중복 구현을 피하기 위해
// 구체 클래스(구 Health.cs) 대신 이 인터페이스만 참조한다.
// 실제 체력 구현은 이 인터페이스를 구현하거나 어댑터로 연결하면 된다.
public interface IHealth
{
    float CurrentHealth { get; }
    float MaxHealth { get; }
    bool IsDead { get; }
    void TakeDamage(float amount, GameObject source = null);
    void Heal(float amount);
}
