using UnityEngine;

// 프로젝트 에셋에 저장되는 교리 기본 정의(프리셋).
// 플레이어가 실행 중에 조합한 교리(Doctrine)와 구분된다.
// 런타임 사용 시 반드시 ToRuntime() 복사본을 만들어 등록하고,
// 원본 에셋을 직접 수정하지 않는다.
[CreateAssetMenu(menuName = "Doctrine/Doctrine Definition", fileName = "DoctrineDefinition")]
public class DoctrineDefinition : ScriptableObject
{
    public SubjectType Subject = SubjectType.Player;
    public ConditionType Condition = ConditionType.OnHit;
    public ResultType Result = ResultType.Heal;

    [Tooltip("효과 수치: Heal량, TakeDamage량, Explode 피해량, 버프 배율(Slow는 0~1 사이 속도 배율). 0이면 기본값 사용.")]
    public float Magnitude;

    [Tooltip("지속 시간(초). 0이면 기본값 사용, Slow/Increase 계열에서 사용.")]
    public float Duration;

    [Tooltip("Explode 반경. 0이면 기본값 사용.")]
    public float Radius;

    public Doctrine ToRuntime()
    {
        return new Doctrine(Subject, Condition, Result, Magnitude, Duration, Radius);
    }
}
