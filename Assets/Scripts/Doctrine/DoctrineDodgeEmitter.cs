using UnityEngine;

// 회피 행위 감지용 이미터. 회피가 가능한 캐릭터에 붙인다.
// 실제 회피 이동/무적 판정은 별도 기능이며, 여기서는
// OnDodge 이벤트 발생(감지)만 담당한다.
public class DoctrineDodgeEmitter : MonoBehaviour
{
    public void PerformDodge()
    {
        DoctrineEventBus.Raise(ConditionType.OnDodge, gameObject, null);
    }
}
