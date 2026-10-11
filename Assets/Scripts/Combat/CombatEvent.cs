using System;
using UnityEngine;

// 게임에서 발생한 사건을 교리 엔진에 전달하는 데이터.
// SubjectObject: 교리 주어 판정의 기준이 되는 객체(예: 피격당한 플레이어).
// SourceObject: 사건의 원인 객체(예: 공격자). 없을 수 있다.
[Serializable]
public struct DoctrineEventData
{
    public ConditionType Condition;
    public GameObject SubjectObject;
    public GameObject SourceObject;
    public Vector3 Position;

    public DoctrineEventData(ConditionType condition, GameObject subject, GameObject source = null)
    {
        Condition = condition;
        SubjectObject = subject;
        SourceObject = source;
        Position = subject != null ? subject.transform.position : Vector3.zero;
    }

    public override string ToString()
    {
        string subjectName = SubjectObject != null ? SubjectObject.name : "null";
        return $"Event({Condition}, subject:{subjectName})";
    }
}

// 이벤트 감지와 교리 판정을 분리하는 정적 버스.
// 감지 측(Health, Emitter)은 Raise만 호출하고,
// 판정 측(DoctrineManager)이 구독한다.
public static class DoctrineEventBus
{
    public static event Action<DoctrineEventData> EventRaised;

    public static bool HasListeners => EventRaised != null;

    public static void Raise(DoctrineEventData evt)
    {
        try
        {
            if (EventRaised != null)
            {
                EventRaised(evt);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Doctrine] 이벤트 처리 중 예외: {ex}");
        }
    }

    public static void Raise(ConditionType condition, GameObject subject, GameObject source = null)
    {
        Raise(new DoctrineEventData(condition, subject, source));
    }

    // 테스트 격리용. 런타임 게임 코드에서는 호출하지 않는다.
    public static void RemoveAllListeners()
    {
        EventRaised = null;
    }
}
