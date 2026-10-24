using System.Collections;
using UnityEngine;

public sealed class BossBurnEffect : MonoBehaviour
{
    private PlayerHealth playerHealth;
    private float damagePerTick;
    private float tickInterval;
    private float endsAt;
    private Coroutine burnRoutine;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
    }

    public void Apply(float damage, float duration, float interval)
    {
        damagePerTick = Mathf.Max(0f, damage);
        tickInterval = Mathf.Max(0.1f, interval);
        endsAt = Mathf.Max(endsAt, Time.time + Mathf.Max(0f, duration));
        if (burnRoutine == null)
            burnRoutine = StartCoroutine(BurnRoutine());
    }

    private IEnumerator BurnRoutine()
    {
        while (Time.time < endsAt && playerHealth != null && !playerHealth.IsDead)
        {
            yield return new WaitForSeconds(tickInterval);
            if (Time.time <= endsAt && playerHealth != null && !playerHealth.IsDead)
                playerHealth.TakeDamage(new DamageInfo(damagePerTick, DamageSource.Enemy));
        }

        burnRoutine = null;
    }

    private void OnDisable()
    {
        if (burnRoutine != null)
        {
            StopCoroutine(burnRoutine);
            burnRoutine = null;
        }
    }
}
