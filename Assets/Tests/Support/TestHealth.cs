using System;
using UnityEngine;

// 테스트 전용 최소 체력. IHealth를 만족하고,
// 피해/사망/저체력 상황에서 DoctrineEventBus로 이벤트를 발생시켜 교리를 구동한다.
// 실제 게임 체력은 친구의 PlayerHealth를 사용할 예정이며,
// 머지 시점에 어댑터(또는 이벤트 연결)로 교체한다.
public class TestHealth : MonoBehaviour, IHealth
{
    [Header("체력")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] [Range(0f, 0.95f)] private float lowHealthRatio = 0.3f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0f;
    public bool IsLowHealth => !IsDead && currentHealth <= maxHealth * lowHealthRatio;

    public event Action<float, GameObject> Damaged;
    public event Action<float> Healed;
    public event Action<GameObject> Died;

    private float currentHealth;
    private bool lowHealthNotified;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    // 테스트/셋업용. 최대 체력을 바꾸고 가득 채운다.
    public void SetMaxHealth(float value)
    {
        maxHealth = Mathf.Max(1f, value);
        currentHealth = maxHealth;
        lowHealthNotified = false;
    }

    // 테스트/셋업용. 체력을 강제로 지정한다.
    public void ForceHealth(float value)
    {
        currentHealth = Mathf.Clamp(value, 0f, maxHealth);
        lowHealthNotified = IsLowHealth;
    }

    public void TakeDamage(float amount, GameObject source = null)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        if (Damaged != null)
        {
            Damaged(amount, source);
        }

        // 구독자가 없으면 transform 접근/버스 호출을 생략 (핫패스 절약).
        if (DoctrineEventBus.HasListeners)
        {
            DoctrineEventBus.Raise(ConditionType.OnHit, gameObject, source);
        }

        if (!lowHealthNotified && IsLowHealth)
        {
            lowHealthNotified = true;
            if (DoctrineEventBus.HasListeners)
            {
                DoctrineEventBus.Raise(ConditionType.LowHealth, gameObject, source);
            }
        }

        if (IsDead)
        {
            if (Died != null)
            {
                Died(source);
            }

            if (DoctrineEventBus.HasListeners)
            {
                DoctrineEventBus.Raise(ConditionType.OnDeath, gameObject, source);
            }
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

        if (Healed != null)
        {
            Healed(amount);
        }

        if (!IsLowHealth)
        {
            lowHealthNotified = false;
        }
    }
}
