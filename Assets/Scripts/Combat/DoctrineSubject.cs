using System;
using UnityEngine;

// 게임 객체의 교리 주어(Subject) 정체성.
// DoctrineManager가 이벤트의 SubjectObject에서 이 컴포넌트를 읽어
// 교리의 SubjectType과 비교한다.
// 컴포넌트가 없는 객체는 SubjectType.None으로 취급되어
// Subject == All인 교리에만 매칭된다.
public class DoctrineSubject : MonoBehaviour
{
    public SubjectType Identity = SubjectType.Enemy;

    public static SubjectType GetIdentity(GameObject go)
    {
        if (go == null)
        {
            return SubjectType.None;
        }

        if (!go.TryGetComponent(out DoctrineSubject subject) || subject == null)
        {
            return SubjectType.None;
        }

        return subject.Identity;
    }
}
