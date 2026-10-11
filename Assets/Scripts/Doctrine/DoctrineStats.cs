using System.Collections;
using UnityEngine;

// 효과 실행 계층이 버프/디버프를 적용하는 대상 스탯.
// Slow/IncreaseSpeed/IncreaseAttack의 배율을 보관한다.
// NOTE: 기존 PlayerMove는 이 배율을 참조하지 않는다.
// PlayerMove에 교리별 분기를 추가하지 않기 위해 별도 컴포넌트로 분리했으며,
// 실제 이동 연동은 PlayerMove 수정 단계에서
// moveSpeed * DoctrineStats.SpeedMultiplier 형태로 연결하면 된다.
public class DoctrineStats : MonoBehaviour
{
    [Header("기본 공격력")]
    [SerializeField] private float baseAttack = 10f;

    public float BaseAttack => baseAttack;
    public float AttackMultiplier { get; private set; } = 1f;
    public float SpeedMultiplier { get; private set; } = 1f;
    public float EffectiveAttack => baseAttack * AttackMultiplier;

    public void SetBaseAttack(float value)
    {
        baseAttack = Mathf.Max(0f, value);
    }

    public void ApplyAttackBuff(float multiplier, float duration)
    {
        if (multiplier <= 0f)
        {
            return;
        }

        AttackMultiplier *= multiplier;
        if (duration > 0f)
        {
            StartCoroutine(RevertAttackBuff(multiplier, duration));
        }
    }

    public void ApplySpeedBuff(float multiplier, float duration)
    {
        if (multiplier <= 0f)
        {
            return;
        }

        SpeedMultiplier *= multiplier;
        if (duration > 0f)
        {
            StartCoroutine(RevertSpeedBuff(multiplier, duration));
        }
    }

    private IEnumerator RevertAttackBuff(float multiplier, float duration)
    {
        yield return new WaitForSeconds(duration);
        AttackMultiplier = Mathf.Max(0.01f, AttackMultiplier / multiplier);
    }

    private IEnumerator RevertSpeedBuff(float multiplier, float duration)
    {
        yield return new WaitForSeconds(duration);
        SpeedMultiplier = Mathf.Max(0.01f, SpeedMultiplier / multiplier);
    }
}
