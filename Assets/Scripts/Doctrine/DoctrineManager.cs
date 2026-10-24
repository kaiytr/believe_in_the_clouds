using System;
using System.Collections.Generic;
using UnityEngine;

// 교리 실행 엔진.
// 책임: 활성 교리 목록 관리, 등록/해제, 이벤트 수신, 조건 판정, 효과 실행 요청.
// 처리 흐름: 게임 이벤트 발생(DoctrineEventBus) -> NotifyEvent ->
//            활성 교리 순회 판정 -> 조건 일치 교리의 효과 실행(DoctrineEffectExecutor).
// 교리별 if문 없이 공통 판정(MatchesRule)과 공통 실행 경로를 사용한다.
// 하나의 교리 실행이 나머지 교리 판정을 중단시키지 않는다.
public class DoctrineManager : MonoBehaviour
{
    public static DoctrineManager Instance { get; private set; }

    [Header("안전 장치")]
    [Tooltip("TakeDamage 교리가 OnHit을 재발생시키는 등의 연쇄를 끊기 위한 최대 중첩 깊이.")]
    [SerializeField] private int maxNestedDepth = 8;

    [Header("로그")]
    [Tooltip("켜면 발동/활성화 로그를 출력한다. 콘솔 스팸은 에디터 렉의 주범이라 기본 OFF.")]
    [SerializeField] private bool verboseLogs = false;

    [Serializable]
    public sealed class ActiveDoctrine
    {
        public Doctrine Doctrine;
        public GameObject Owner;

        public ActiveDoctrine(Doctrine doctrine, GameObject owner)
        {
            Doctrine = doctrine;
            Owner = owner;
        }
    }

    private readonly List<ActiveDoctrine> activeDoctrines = new List<ActiveDoctrine>();
    private int notifyDepth;

    // UI 표시 전용 이벤트. 판정/실행 로직은 이 이벤트에 의존하지 않는다.
    // (BeliefView와 같은 원칙: View는 표시만 한다)
    public event Action<ActiveDoctrine> Activated;
    public event Action<ActiveDoctrine> Deactivated;
    public event Action<ActiveDoctrine, GameObject> Triggered;
    public event Action Cleared;

    public IReadOnlyList<ActiveDoctrine> ActiveDoctrines => activeDoctrines;
    public int ActiveCount => activeDoctrines.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[Doctrine] DoctrineManager가 이미 있어 중복 객체를 파기합니다.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        EnsureSubscribed();
    }

    private void OnDisable()
    {
        DoctrineEventBus.EventRaised -= NotifyEvent;
    }

    // 메시지(Awake/OnEnable)가 호출되지 않는 환경에서도
    // 테스트 하네스가 명시적으로 구독을 보장하기 위한 멱등 메서드.
    // 중복 구독되지 않는다.
    public void EnsureSubscribed()
    {
        DoctrineEventBus.EventRaised -= NotifyEvent;
        DoctrineEventBus.EventRaised += NotifyEvent;
    }

    // 향후 상점/슬롯/UI가 연결할 인터페이스: 생성된 Doctrine을 넘기면 검증 후 활성화.
    public bool TryActivate(Doctrine doctrine, out string reason)
    {
        return TryActivate(doctrine, null, out reason);
    }

    // owner 지정 시 해당 객체의 이벤트에만 반응하는 바인딩 등록.
    // (향후 교리 슬롯/장착 개념과 연결 가능)
    public bool TryActivate(Doctrine doctrine, GameObject owner, out string reason)
    {
        if (!DoctrineValidator.TryValidate(doctrine, owner, out reason))
        {
            Debug.LogWarning($"[Doctrine] 활성화 실패: {reason}");
            return false;
        }

        ActiveDoctrine entry = new ActiveDoctrine(doctrine.Clone(), owner);
        activeDoctrines.Add(entry);
        if (verboseLogs)
        {
            Debug.Log($"[Doctrine] 활성화: {doctrine}");
        }
        Activated?.Invoke(entry);
        return true;
    }

    public bool Deactivate(Doctrine doctrine)
    {
        if (doctrine == null)
        {
            return false;
        }

        int index = activeDoctrines.FindIndex(
            a => a.Doctrine != null && a.Doctrine.HasSameRule(doctrine));
        if (index < 0)
        {
            return false;
        }

        ActiveDoctrine removed = activeDoctrines[index];
        activeDoctrines.RemoveAt(index);
        if (verboseLogs)
        {
            Debug.Log($"[Doctrine] 해제: {doctrine}");
        }
        Deactivated?.Invoke(removed);
        return true;
    }

    public void Clear()
    {
        activeDoctrines.Clear();
        if (verboseLogs)
        {
            Debug.Log("[Doctrine] 전체 해제.");
        }
        Cleared?.Invoke();
    }

    public void NotifyEvent(DoctrineEventData evt)
    {
        // 활성 교리가 없으면 GetComponent/스냅샷 할당 없이 즉시 복귀.
        // TakeDamage가 매 타격마다 OnHit을 Raise하므로 핫패스다.
        if (activeDoctrines.Count == 0)
        {
            return;
        }

        if (notifyDepth >= maxNestedDepth)
        {
            Debug.LogWarning("[Doctrine] 중첩 깊이 초과로 이벤트를 무시합니다 (무한 재귀 방지).");
            return;
        }

        notifyDepth++;
        try
        {
            GameObject target = evt.SubjectObject;
            if (target == null)
            {
                return;
            }

            // 루프당 GetComponent 1회로 고정. 기존 Matches(entry, evt)는
            // 교리 수만큼 GetComponent를 호출해서 이벤트 핫패스를 느리게 했다.
            SubjectType identity = DoctrineSubject.GetIdentity(target);

            // 실행 중 등록/해제가 일어나도 안전하도록 스냅샷 순회.
            List<ActiveDoctrine> snapshot = new List<ActiveDoctrine>(activeDoctrines);
            foreach (ActiveDoctrine entry in snapshot)
            {
                if (entry == null || entry.Doctrine == null)
                {
                    continue;
                }

                if (!MatchesRule(entry.Doctrine, identity, evt.Condition))
                {
                    continue;
                }

                // Owner 바인딩이 있으면 해당 객체의 이벤트에만 반응.
                if (entry.Owner != null && entry.Owner != target)
                {
                    continue;
                }

                bool ok = false;
                try
                {
                    ok = DoctrineEffectExecutor.Execute(entry.Doctrine, target, evt.SourceObject);
                    if (verboseLogs)
                    {
                        Debug.Log($"[Doctrine] 발동: {entry.Doctrine} -> {target.name} (성공:{ok}).");
                    }
                }
                catch (Exception ex)
                {
                    // 한 교리의 실패가 다른 교리 판정을 막지 않는다.
                    Debug.LogError($"[Doctrine] 효과 실행 중 예외 (다음 교리 계속 판정): {ex}");
                }

                // 효과 실행 성공 여부와 무관하게 "발동했다"는 사실은 UI에 알린다.
                // UI 예외가 교리 실행 실패로 기록되지 않도록 try 밖에서 호출한다.
                try
                {
                    Triggered?.Invoke(entry, target);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Doctrine] 발동 이벤트 처리 중 예외: {ex}");
                }
            }
        }
        finally
        {
            notifyDepth--;
        }
    }

    public static bool Matches(ActiveDoctrine entry, DoctrineEventData evt)
    {
        if (entry == null || entry.Doctrine == null)
        {
            return false;
        }

        if (!MatchesRule(entry.Doctrine, DoctrineSubject.GetIdentity(evt.SubjectObject), evt.Condition))
        {
            return false;
        }

        // Owner 바인딩이 있으면 해당 객체의 이벤트에만 반응.
        if (entry.Owner != null && entry.Owner != evt.SubjectObject)
        {
            return false;
        }

        return true;
    }

    // 새 교리 조합 추가 시 수정 없이 재사용되는 공통 판정.
    // All은 모든 주어와 일치한다.
    public static bool MatchesRule(Doctrine doctrine, SubjectType actualIdentity, ConditionType condition)
    {
        if (doctrine == null || doctrine.Condition != condition)
        {
            return false;
        }

        if (doctrine.Subject == SubjectType.All)
        {
            return true;
        }

        return actualIdentity != SubjectType.None && doctrine.Subject == actualIdentity;
    }
}
