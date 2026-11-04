using System.Collections;
using UnityEngine;

// Buff/debuff stat storage for the effect execution layer.
// Holds multipliers for IncreaseSpeed/IncreaseAttack.
// PlayerMove reads SpeedMultiplier via ApplyDoctrineSpeed each move;
// PlayerAttack multiplies step damage by AttackMultiplier.
//
// 이속/피해 증가는 "교체식"이다: 다시 발동하면 중첩하지 않고 배율을 덮어쓰고
// 지속시간만 새로 시작한다. (표 값: 지속 5초 · 쿨 10초)
public class DoctrineStats : MonoBehaviour
{
    [Header("Base Attack")]
    [SerializeField] private float baseAttack = 10f;

    public float BaseAttack => baseAttack;
    public float AttackMultiplier { get; private set; } = 1f;
    public float SpeedMultiplier { get; private set; } = 1f;
    public float EffectiveAttack => baseAttack * AttackMultiplier;

    private Coroutine attackRevert;
    private Coroutine speedRevert;

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

        // 교체식: 이전 배율/만료 타이머를 버리고 새로 건다.
        AttackMultiplier = multiplier;

        if (attackRevert != null)
        {
            StopCoroutine(attackRevert);
            attackRevert = null;
        }

        if (duration > 0f)
        {
            attackRevert = StartCoroutine(RevertAttackBuff(duration));
        }
    }

    public void ApplySpeedBuff(float multiplier, float duration)
    {
        if (multiplier <= 0f)
        {
            return;
        }

        // 교체식: 이전 배율/만료 타이머를 버리고 새로 건다.
        SpeedMultiplier = multiplier;

        if (speedRevert != null)
        {
            StopCoroutine(speedRevert);
            speedRevert = null;
        }

        if (duration > 0f)
        {
            speedRevert = StartCoroutine(RevertSpeedBuff(duration));
        }
    }

    public void ClearSpeedBuff()
    {
        if (speedRevert != null)
        {
            StopCoroutine(speedRevert);
            speedRevert = null;
        }

        SpeedMultiplier = 1f;
    }

    public void ClearAttackBuff()
    {
        if (attackRevert != null)
        {
            StopCoroutine(attackRevert);
            attackRevert = null;
        }

        AttackMultiplier = 1f;
    }

    public void ClearAllBuffs()
    {
        StopAllCoroutines();
        speedRevert = null;
        attackRevert = null;
        AttackMultiplier = 1f;
        SpeedMultiplier = 1f;
    }

    private IEnumerator RevertAttackBuff(float duration)
    {
        yield return new WaitForSeconds(duration);
        attackRevert = null;
        AttackMultiplier = 1f;
    }

    private IEnumerator RevertSpeedBuff(float duration)
    {
        yield return new WaitForSeconds(duration);
        speedRevert = null;
        SpeedMultiplier = 1f;
    }
}
