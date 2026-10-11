using UnityEngine;

public class EnemyTestDamageReceiver :
    MonoBehaviour,
    IDamageable
{
    [SerializeField]
    private float maxHp = 100f;

    private float currentHp;

    private void Awake()
    {
        currentHp = maxHp;
    }

    public void TakeDamage(float damage)
    {
        currentHp -= damage;

        currentHp =
            Mathf.Max(
                currentHp,
                0f
            );

        Debug.Log(
            $"[Test Player] Damage: {damage} / HP: {currentHp}/{maxHp}"
        );
    }
}