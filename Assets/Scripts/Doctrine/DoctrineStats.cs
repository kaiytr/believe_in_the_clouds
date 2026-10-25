using System.Collections;
using UnityEngine;

// Buff/debuff stat storage for the effect execution layer.
// Holds multipliers for IncreaseSpeed/IncreaseAttack.
// PlayerMove reads SpeedMultiplier via ApplyDoctrineSpeed each move;
// PlayerAttack multiplies step damage by AttackMultiplier.
public class DoctrineStats : MonoBehaviour
{
    [Header("Base Attack")]
    [SerializeField] private float baseAttack = 10f;

    public float BaseAttack => baseAttack;
    public float AttackMultiplier { get; private set; } = 1f;
    public float SpeedMultiplier { get; private set; } = 1f;
    public float EffectiveAttack => baseAttack * AttackMultiplier;

    // Persistent (duration <= 0) speed buffs re-applied while the doctrine
    // stays equipped: e.g. table IncreaseSpeed (+40%, no expiry).
    private float persistentSpeedBonus = 0f;
    private float timedSpeedMultiplier = 1f;

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

        if (duration <= 0f)
        {
            // Persistent buff: fixed table value, re-asserted each use.
            persistentSpeedBonus = Mathf.Max(persistentSpeedBonus, multiplier);
            RefreshSpeedMultiplier();
            return;
        }

        timedSpeedMultiplier *= multiplier;
        RefreshSpeedMultiplier();
        StartCoroutine(RevertSpeedBuff(multiplier, duration));
    }

    public void ClearPersistentSpeedBuff()
    {
        persistentSpeedBonus = 0f;
        RefreshSpeedMultiplier();
    }

    private void RefreshSpeedMultiplier()
    {
        float persistent = persistentSpeedBonus > 0f ? persistentSpeedBonus : 1f;
        SpeedMultiplier = persistent * timedSpeedMultiplier;
    }

    private IEnumerator RevertAttackBuff(float multiplier, float duration)
    {
        yield return new WaitForSeconds(duration);
        AttackMultiplier = Mathf.Max(0.01f, AttackMultiplier / multiplier);
    }

    private IEnumerator RevertSpeedBuff(float multiplier, float duration)
    {
        yield return new WaitForSeconds(duration);
        timedSpeedMultiplier = Mathf.Max(0.01f, timedSpeedMultiplier / multiplier);
        RefreshSpeedMultiplier();
    }
}
