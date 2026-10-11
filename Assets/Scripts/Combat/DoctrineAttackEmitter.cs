using UnityEngine;

// 공격 행위 감지용 이미터. 공격자의 게임 객체에 붙인다.
// PerformAttack이 호출되면 OnAttack 이벤트를 먼저 발생시키고,
// 그 다음 대상의 IHealth에 피해를 적용한다(피해 적용 시 대상 측 OnHit 발생).
// 이렇게 하면 "적은 공격하면 ..." 교리와 "플레이어는 피격당하면 ..." 교리가
// 각각 독립적으로 발동한다.
public class DoctrineAttackEmitter : MonoBehaviour
{
    public void PerformAttack(GameObject target, float damage)
    {
        if (target == null)
        {
            return;
        }

        DoctrineEventBus.Raise(ConditionType.OnAttack, gameObject, target);

        IHealth health = target.GetComponent<IHealth>();
        if (health != null)
        {
            health.TakeDamage(damage, gameObject);
        }
    }
}
