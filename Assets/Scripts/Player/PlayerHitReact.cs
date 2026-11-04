using System.Collections;
using UnityEngine;

// 생존 중 피격에만 몸을 웅크린다. 사망 자세는 스프라이트 애니메이션이 맡는다.
// 실제 스케일 변경은 PlayerBody에 pose 요청으로 위임하므로
// 대시 중 피격처럼 연출이 겹쳐도 깨지지 않는다.
[RequireComponent(typeof(PlayerBody))]
public class PlayerHitReact : MonoBehaviour
{
    [Header("피격: 몸 웅크리기")]
    [Tooltip("피격 순간 몸 높이 배율(1 = 평소).")]
    [SerializeField, Range(0.2f, 1f)] private float hitCrouchScaleY = 0.75f;
    [Tooltip("웅크린 채로 있는 시간(초).")]
    [SerializeField, Min(0f)] private float hitCrouchDuration = 0.18f;

    private const string HitKey = "hit";

    private PlayerBody body;
    private PlayerHealth health;
    private Coroutine hitRoutine;

    private void Awake()
    {
        body = GetComponent<PlayerBody>();
        health = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        DoctrineEventBus.EventRaised += HandleDoctrineEvent;
    }

    private void OnDisable()
    {
        DoctrineEventBus.EventRaised -= HandleDoctrineEvent;
        ClearHitCrouch();
    }

    private void HandleDoctrineEvent(DoctrineEventData evt)
    {
        if (evt.SubjectObject != gameObject || evt.Condition != ConditionType.OnHit)
            return;

        ClearHitCrouch();
        if (health != null && health.IsDead)
            return;

        body.PushPose(HitKey, hitCrouchScaleY);
        hitRoutine = StartCoroutine(HitCrouchRoutine());
    }

    private IEnumerator HitCrouchRoutine()
    {
        if (hitCrouchDuration > 0f)
            yield return new WaitForSeconds(hitCrouchDuration);
        else
            yield return null;

        body.PopPose(HitKey);
        hitRoutine = null;
    }

    private void ClearHitCrouch()
    {
        if (hitRoutine != null)
        {
            StopCoroutine(hitRoutine);
            hitRoutine = null;
        }
        body.PopPose(HitKey);
    }
}
