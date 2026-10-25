using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 플레이어 근접 콤보 공격. 마우스 좌클릭으로 지상 4연타가 나간다.
// 1타: 뒤로 짧게 사선 베기
// 2타: 반대 앞 방향으로 길게 베기
// 3타: 전진 찌르기
// 4타: 올려베기 피니쉬
// 공중(점프 중) 공격: 뒤로 길게 사선 베기 1회. 착지 전까지 1회만 나간다.
//
// 판정: 각 타수마다 전방 OverlapBox 1회로 적중을 처리한다.
// 피해: IDamageable.TakeDamage(DamageInfo(PlayerOwned)) 공통 경로를 사용하므로
//       EnemyUnit의 신앙치 피해 보정(BeliefController)이 그대로 적용된다.
// 교리: 적중 직전 DoctrineEventBus에 OnAttack을 Raise한다 (주체=플레이어).
//       "플레이어는 공격하면 ..." 계열 교리가 별도 분기 없이 발동한다.
// 이동: 공격 중에는 PlayerMove.ControlLocked를 잠가 이동 덮어쓰기와
//       방향 전환을 막고, 타수별 전진(lunge) 거리만큼 Rigidbody로 밀어준다.
// 애니메이터가 있으면 Attack1~4 / AirAttack 트리거, IsAttacking / Combo 파라미터를 쏘므로
// 나중에 스프라이트 애니메이션만 연결하면 된다.
[RequireComponent(typeof(PlayerMove))]
public class PlayerAttack : MonoBehaviour
{
    [Serializable]
    public struct ComboStep
    {
        [Tooltip("에디터/Gizmos용 이름.")]
        public string name;

        [Tooltip("기본 피해량. DoctrineStats가 있으면 AttackMultiplier가 곱해진다.")]
        [Min(0f)] public float damage;

        [Tooltip("판정 박스 크기.")]
        public Vector2 hitboxSize;

        [Tooltip("플레이어 중심 기준 판정 박스 오프셋. x는 바라보는 방향으로 뒤집힌다.")]
        public Vector2 hitboxOffset;

        [Tooltip("판정 박스 회전(도). x 뒤집힘에 맞춰 부호가 반전된다.")]
        public float hitboxAngle;

        [Tooltip("타격 발생까지 걸리는 시간(선딜). 이 동안 전진이 나온다.")]
        [Min(0f)] public float windupTime;

        [Tooltip("타격 후 다음 입력/종료를 기다리는 경직 시간.")]
        [Min(0f)] public float recoveryTime;

        [Tooltip("recovery 이후 다음 타 입력을 받아주는 유예 시간. 4타는 피니쉬라 0 권장.")]
        [Min(0f)] public float comboWindow;

        [Tooltip("이번 타에 앞으로 미끄러지듯 나가는 거리.")]
        [Min(0f)] public float lungeDistance;

        [Tooltip("적중 시 적을 미는 힘(충격량).")]
        [Min(0f)] public float knockback;

        [Tooltip("적중 시 적을 띄우는 힘(충격량). 4타 피니쉬용.")]
        [Min(0f)] public float launchUp;
    }

    [Header("입력")]
    [Tooltip("공격에 쓸 마우스 버튼. 0 = 좌클릭.")]
    [SerializeField] private int attackMouseButton = 0;

    [Header("콤보 (1~4타)")]
    [SerializeField] private ComboStep[] comboSteps = new ComboStep[]
    {
        new ComboStep
        {
            name = "1타: 짧은 사선 베기",
            damage = 10f,
            hitboxSize = new Vector2(1.5f, 1.0f),
            hitboxOffset = new Vector2(0.8f, 0.2f),
            hitboxAngle = -30f,
            windupTime = 0.07f,
            recoveryTime = 0.16f,
            comboWindow = 0.35f,
            lungeDistance = 0.5f,
            knockback = 2f,
            launchUp = 0f,
        },
        new ComboStep
        {
            name = "2타: 반대로 길게 베기",
            damage = 12f,
            hitboxSize = new Vector2(2.1f, 1.1f),
            hitboxOffset = new Vector2(1.0f, 0.2f),
            hitboxAngle = 30f,
            windupTime = 0.09f,
            recoveryTime = 0.2f,
            comboWindow = 0.35f,
            lungeDistance = 0.8f,
            knockback = 3f,
            launchUp = 0f,
        },
        new ComboStep
        {
            name = "3타: 전진 찌르기",
            damage = 14f,
            hitboxSize = new Vector2(2.4f, 0.6f),
            hitboxOffset = new Vector2(1.4f, 0f),
            hitboxAngle = 0f,
            windupTime = 0.1f,
            recoveryTime = 0.24f,
            comboWindow = 0.4f,
            lungeDistance = 1.8f,
            knockback = 4f,
            launchUp = 0f,
        },
        new ComboStep
        {
            name = "4타: 올려베기 피니쉬",
            damage = 24f,
            hitboxSize = new Vector2(2.0f, 1.9f),
            hitboxOffset = new Vector2(0.9f, 0.6f),
            hitboxAngle = 60f,
            windupTime = 0.16f,
            recoveryTime = 0.38f,
            comboWindow = 0f,
            lungeDistance = 0.9f,
            knockback = 6f,
            launchUp = 5f,
        },
    };

    [Header("공중 공격 (점프 중)")]
    [Tooltip("공중에 있을 때 좌클릭하면 나가는 단타.")]
    [SerializeField] private ComboStep airAttack = new ComboStep
    {
        name = "공중: 뒤로 길게 사선 베기",
        damage = 14f,
        hitboxSize = new Vector2(2.2f, 1.2f),
        hitboxOffset = new Vector2(-1.1f, 0.3f),
        hitboxAngle = -40f,
        windupTime = 0.05f,
        recoveryTime = 0.22f,
        comboWindow = 0f,
        lungeDistance = 0f,
        knockback = 3f,
        launchUp = 1f,
    };

    [Tooltip("공격 후 착지 전까지 공중 공격을 다시 쓸 수 있게 하는지.")]
    [SerializeField] private bool airAttackResetOnLand = true;

    [Header("판정")]
    [Tooltip("공격이 맞을 레이어. 비워두면(0) 전부 판정한다.")]
    [SerializeField] private LayerMask targetLayers = Physics2D.AllLayers;

    [Header("히트박스 비주얼")]
    [Tooltip("자식으로 붙는 PlayerAttackHitbox. 비워두면 Awake에서 자동 탐색/생성한다.")]
    [SerializeField] private PlayerAttackHitbox hitboxVisual;

    [Tooltip("타격 이펙트처럼 박스가 남아있는 시간.")]
    [SerializeField, Min(0f)] private float hitboxVisualDuration = 0.12f;

    [Header("피니쉬 연출")]
    [Tooltip("4타가 적중했을 때 시간을 잠깐 멈추는 타격감 연출(초). 0이면 끄기.")]
    [SerializeField, Min(0f)] private float finishHitStop = 0.06f;

    private Rigidbody2D rb;
    private PlayerMove playerMove;
    private Animator animator;
    private DoctrineStats stats;
    private PlayerHealth health;

    private int currentCombo; // 다음에 나갈 타수 (0~3)
    private bool isAttacking;
    private bool isAirAttacking;
    private bool airAttackUsed; // 이번 점프에서 공중 공격을 썼는지
    private bool wasGrounded = true;
    private bool inputBuffered; // 이번 타 도중 다음 타 입력이 들어왔는지
    private Coroutine attackRoutine;
    private Vector3 baseScale;
    private static readonly HashSet<IDamageable> OverlapSeen = new HashSet<IDamageable>();

    public bool IsAttacking => isAttacking || isAirAttacking;
    public int CurrentCombo => currentCombo;
    public int ComboCount => comboSteps != null ? comboSteps.Length : 0;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMove = GetComponent<PlayerMove>();
        animator = GetComponentInChildren<Animator>();
        stats = GetComponent<DoctrineStats>();
        health = GetComponent<PlayerHealth>();
        baseScale = transform.localScale;
        hitboxVisual = GetComponentInChildren<PlayerAttackHitbox>(true);
        if (hitboxVisual == null)
        {
            GameObject go = new GameObject("AttackHitbox");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            // 자식이라 부모 태그를 그대로 물려받지만, 콜라이더가 없어
            // 실제 판정(ContactDamage/OverlapBox)에 간섭하지 않는다.
            go.tag = "Untagged";
            go.layer = gameObject.layer;
            hitboxVisual = go.AddComponent<PlayerAttackHitbox>();
        }
    }

    private void Update()
    {
        if (playerMove != null && airAttackResetOnLand)
        {
            if (!wasGrounded && playerMove.IsGrounded)
                airAttackUsed = false;
            wasGrounded = playerMove.IsGrounded;
        }

        if (!UiModal.IsOpen && Input.GetMouseButtonDown(attackMouseButton))
        {
            OnAttackInput();
        }
    }

    private void OnAttackInput()
    {
        if (health != null && health.IsDead)
            return;

        bool grounded = playerMove == null || playerMove.IsGrounded;

        // 공중에서는 콤보 없이 단타 1회. 착지 전까지 한 번만 나간다.
        if (!grounded)
        {
            if (isAttacking || isAirAttacking || airAttackUsed)
                return;
            airAttackUsed = true;
            isAirAttacking = true;
            attackRoutine = StartCoroutine(AirAttackRoutine());
            return;
        }

        if (!isAttacking)
        {
            if (comboSteps == null || comboSteps.Length == 0)
                return;
            currentCombo = 0;
            attackRoutine = StartCoroutine(ComboRoutine());
        }
        else
        {
            // 타수 도중 입력은 버퍼에 적어두고, 판정 타이밍에 다음 타로 잇는다.
            inputBuffered = true;
        }
    }

    /// <summary>대시 등 외부 연출이 공격 모션을 끊을 때 호출. 공중 공격도 함께 끊는다.</summary>
    public void CancelAttack()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        StopMotion();
        currentCombo = 0;
        inputBuffered = false;
        isAttacking = false;
        isAirAttacking = false;
        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
            animator.SetInteger("Combo", 0);
        }
        if (playerMove != null)
            playerMove.ControlLocked = false;
    }

    private IEnumerator ComboRoutine()
    {
        isAttacking = true;
        if (playerMove != null)
            playerMove.ControlLocked = true;
        if (animator != null)
            animator.SetBool("IsAttacking", true);

        try
        {
            while (true)
            {
                if (currentCombo < 0 || currentCombo >= comboSteps.Length)
                    break;

                inputBuffered = false;
                yield return SwingOnce(currentCombo);

                bool isLast = currentCombo >= comboSteps.Length - 1;
                if (isLast)
                    break;

                // recovery 뒤 콤보 유예 시간 동안 추가 입력을 기다린다.
                float wait = Mathf.Max(0f, comboSteps[currentCombo].comboWindow);
                float elapsed = 0f;
                while (elapsed < wait && !inputBuffered)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (!inputBuffered)
                    break;

                currentCombo++;
                if (animator != null)
                    animator.SetInteger("Combo", currentCombo);
            }
        }
        finally
        {
            StopMotion();
            currentCombo = 0;
            inputBuffered = false;
            isAttacking = false;
            if (animator != null)
            {
                animator.SetBool("IsAttacking", false);
                animator.SetInteger("Combo", 0);
            }
            if (playerMove != null)
                playerMove.ControlLocked = false;
        }
    }

    private IEnumerator AirAttackRoutine()
    {
        if (playerMove != null)
            playerMove.ControlLocked = true;
        if (animator != null)
        {
            animator.SetBool("IsAttacking", true);
            animator.SetTrigger("AirAttack");
        }

        try
        {
            yield return SwingOnce(airAttack, comboIndexForVisual: 0, stopSlide: false);
        }
        finally
        {
            StopMotion();
            isAirAttacking = false;
            if (animator != null)
                animator.SetBool("IsAttacking", false);
            // 공중 공격은 착지 전까지 이동 잠금을 유지하지 않는다.
            if (playerMove != null)
                playerMove.ControlLocked = false;
        }
    }

    private IEnumerator SwingOnce(int index)
    {
        ComboStep step = comboSteps[index];
        bool isFinish = index == comboSteps.Length - 1;

        // 타수 시작 시점의 방향을 고정한다. 도중에 뒤집히지 않는다.
        float facing = transform.localScale.x >= 0f ? 1f : -1f;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(baseScale.x) * facing;
        transform.localScale = scale;

        if (animator != null)
            animator.SetTrigger("Attack" + (index + 1));

        // 전진: 선딜 동안 목표 거리를 속도로 환산해 밀어준다.
        if (rb != null && step.lungeDistance > 0f)
        {
            float lungeTime = Mathf.Max(0.01f, step.windupTime + 0.05f);
            rb.linearVelocity = new Vector2(
                facing * (step.lungeDistance / lungeTime),
                rb.linearVelocity.y);
        }

        if (step.windupTime > 0f)
            yield return new WaitForSeconds(step.windupTime);

        // 실제 판정과 같은 좌표로 비주얼 박스를 띄운다.
        if (hitboxVisual != null)
            hitboxVisual.ShowSwing(index, step.hitboxSize, step.hitboxOffset, step.hitboxAngle, hitboxVisualDuration);

        int hitCount = DoHit(step, facing);

        // 타격 후 미끄러짐을 멈춘다 (y는 중력/점프에 맡긴다).
        StopMotion();

        if (step.recoveryTime > 0f)
            yield return new WaitForSeconds(step.recoveryTime);

        if (hitCount > 0 && isFinish && finishHitStop > 0f)
            yield return HitStop(finishHitStop);
    }

    private IEnumerator SwingOnce(ComboStep step, int comboIndexForVisual, bool stopSlide)
    {
        float facing = transform.localScale.x >= 0f ? 1f : -1f;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(baseScale.x) * facing;
        transform.localScale = scale;

        if (rb != null && step.lungeDistance > 0f)
        {
            float lungeTime = Mathf.Max(0.01f, step.windupTime + 0.05f);
            rb.linearVelocity = new Vector2(
                facing * (step.lungeDistance / lungeTime),
                rb.linearVelocity.y);
        }

        if (step.windupTime > 0f)
            yield return new WaitForSeconds(step.windupTime);

        if (hitboxVisual != null)
            hitboxVisual.ShowSwing(comboIndexForVisual, step.hitboxSize, step.hitboxOffset, step.hitboxAngle, hitboxVisualDuration);

        DoHit(step, facing);

        if (stopSlide && rb != null)
            StopMotion();

        if (step.recoveryTime > 0f)
            yield return new WaitForSeconds(step.recoveryTime);
    }

    private void StopMotion()
    {
        // 수평 미끄러짐만 멈춘다 (y는 중력/점프에 맡긴다).
        if (rb != null)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private int DoHit(ComboStep step, float facing)
    {
        Vector2 center = (Vector2)transform.position +
            new Vector2(step.hitboxOffset.x * facing, step.hitboxOffset.y);
        float angle = facing >= 0f ? step.hitboxAngle : -step.hitboxAngle;

        Collider2D[] hits = targetLayers.value == 0
            ? Physics2D.OverlapBoxAll(center, step.hitboxSize, angle)
            : Physics2D.OverlapBoxAll(center, step.hitboxSize, angle, targetLayers);

        OverlapSeen.Clear();
        int hitCount = 0;

        float multiplier = stats != null ? stats.AttackMultiplier : 1f;
        float finalDamage = Mathf.Max(0f, step.damage * multiplier);
        if (finalDamage <= 0f)
            return 0;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
                continue;

            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable == null)
                damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || !OverlapSeen.Add(damageable))
                continue;

            // 같은 편(플레이어 체력)은 때리지 않는다.
            Component targetComponent = damageable as Component;
            if (targetComponent != null)
            {
                if (targetComponent.GetComponent<PlayerHealth>() != null ||
                    targetComponent.CompareTag("Player"))
                    continue;
            }

            GameObject targetObject = targetComponent != null
                ? targetComponent.gameObject
                : hit.gameObject;

            // Table rule: OnAttack triggers only when the attack motion is done
            // AND the hit lands (TakeDamage applied). Whiffed swings raise nothing.
            bool applied = damageable.TakeDamage(new DamageInfo(finalDamage, DamageSource.PlayerOwned));
            if (applied)
            {
                if (DoctrineEventBus.HasListeners)
                    DoctrineEventBus.Raise(ConditionType.OnAttack, gameObject, targetObject);
                hitCount++;
            }

            ApplyKnockback(hit, step, facing);
        }

        return hitCount;
    }

    private void ApplyKnockback(Collider2D hit, ComboStep step, float facing)
    {
        if ((step.knockback <= 0f && step.launchUp <= 0f) || hit == null)
            return;

        Rigidbody2D enemyRb = hit.attachedRigidbody;
        if (enemyRb == null)
            enemyRb = hit.GetComponentInParent<Rigidbody2D>();
        if (enemyRb == null || enemyRb == rb)
            return;

        enemyRb.AddForce(
            new Vector2(facing * step.knockback, step.launchUp),
            ForceMode2D.Impulse);
    }

    private IEnumerator HitStop(float duration)
    {
        float previousScale = Time.timeScale;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = previousScale;
    }

    private void OnDisable()
    {
        CancelAttack();
    }

    private void OnDrawGizmosSelected()
    {
        if (comboSteps == null)
            return;

        float facing = transform != null && transform.localScale.x < 0f ? -1f : 1f;
        Color[] colors = { Color.cyan, Color.green, Color.yellow, Color.red };

        for (int i = 0; i < comboSteps.Length; i++)
        {
            DrawStepGizmo(comboSteps[i], colors[i % colors.Length], facing);
        }

        // 공중 공격 Gizmos (지상 콤보와 겹쳐 보이지 않게 살짝 위로).
        Gizmos.color = Color.magenta;
        Vector2 airCenter = (Vector2)transform.position +
            new Vector2(airAttack.hitboxOffset.x * facing, airAttack.hitboxOffset.y + 0.05f);
        float airAngle = facing >= 0f ? airAttack.hitboxAngle : -airAttack.hitboxAngle;
        Matrix4x4 airOld = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(airCenter, Quaternion.Euler(0f, 0f, airAngle), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, airAttack.hitboxSize);
        Gizmos.matrix = airOld;
    }

    private void DrawStepGizmo(ComboStep step, Color color, float facing)
    {
        Vector2 center = (Vector2)transform.position +
            new Vector2(step.hitboxOffset.x * facing, step.hitboxOffset.y);
        float angle = facing >= 0f ? step.hitboxAngle : -step.hitboxAngle;

        Gizmos.color = color;
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, step.hitboxSize);
        Gizmos.matrix = old;
    }
}
