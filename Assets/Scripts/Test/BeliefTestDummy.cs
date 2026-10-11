using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BeliefTestDummy : EnemyUnit
{
    [SerializeField] private float testDamage = 10f;

    public float CurrentHpForTest => currentHp;

    protected override void Awake()
    {
        maxHp = 10000f;
        moveSpeed = 0f;
        base.Awake();
    }

    protected override void UpdateAI()
    {
        StopMovement();
    }

    public void ReceiveTestHit(int side)
    {
        if (IsDead)
            return;

        if (Target == null ||
            Mathf.Sign(Target.position.x - transform.position.x) != side)
            return;

        TakeDamage(testDamage);
    }

    public void ResetTestHealth()
    {
        if (!IsDead)
            currentHp = maxHp;
    }
}
