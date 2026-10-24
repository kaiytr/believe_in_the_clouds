using System.Collections;
using UnityEngine;

// 피격/사망 리액션. 피격 = 몸 웅크리기, 사망 = 무릎 꿇기.
// PlayerHealth를 직접 고치지 않고 DoctrineEventBus의 OnHit/OnDeath을 구독한다.
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

    [Header("사망: 무릎 꿇기")]
    [Tooltip("사망 시 몸 높이 배율(1 = 평소).")]
    [SerializeField, Range(0.2f, 1f)] private float kneelScaleY = 0.55f;

    private const string HitKey = "hit";
    private const string DeathKey = "death";

    private PlayerBody body;
    private PlayerMove playerMove;
    private PlayerAttack attack;
    private PlayerDash dash;
    private PlayerHealth health;
    private Rigidbody2D rb;
    private Coroutine hitRoutine;

    private void Awake()
    {
        body = GetComponent<PlayerBody>();
        playerMove = GetComponent<PlayerMove>();
        attack = GetComponent<PlayerAttack>();
        dash = GetComponent<PlayerDash>();
        health = GetComponent<PlayerHealth>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        DoctrineEventBus.EventRaised += HandleDoctrineEvent;
    }

    private void OnDisable()
    {
        DoctrineEventBus.EventRaised -= HandleDoctrineEvent;
        if (hitRoutine != null)
        {
            StopCoroutine(hitRoutine);
            hitRoutine = null;
        }
    }

    private void Update()
    {
        // 리스폰 등으로 체력이 돌아오면 사망 연출을 푼다.
        if (body.HasPose(DeathKey) && health != null && !health.IsDead)
        {
            body.PopPose(DeathKey);
            if (playerMove != null)
                playerMove.ControlLocked = false;
            if (dash != null)
                dash.enabled = true;
        }
    }

    private void HandleDoctrineEvent(DoctrineEventData evt)
    {
        if (evt.SubjectObject != gameObject)
            return;

        if (evt.Condition == ConditionType.OnHit)
        {
            // 사망 시에는 OnHit 뒤에 OnDeath이 오므로 무릎 꿇기가 이긴다.
            PlayHitCrouch();
        }
        else if (evt.Condition == ConditionType.OnDeath)
        {
            PlayDeathKneel();
        }
    }

    private void PlayHitCrouch()
    {
        if (body.HasPose(DeathKey))
            return;

        if (hitRoutine != null)
        {
            StopCoroutine(hitRoutine);
            body.PopPose(HitKey);
        }

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

    private void PlayDeathKneel()
    {
        if (hitRoutine != null)
        {
            StopCoroutine(hitRoutine);
            hitRoutine = null;
        }
        body.PopPose(HitKey);
        body.PushPose(DeathKey, kneelScaleY);

        if (attack != null)
            attack.CancelAttack();
        if (dash != null)
            dash.enabled = false;
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
        if (playerMove != null)
            playerMove.ControlLocked = true;
    }
}
