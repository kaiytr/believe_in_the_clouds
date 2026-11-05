using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 플레이어 근접 콤보 공격. 마우스 좌클릭으로 지상 3연타가 나간다.
// 1타: 뒤로 짧게 사선 베기
// 2타: 반대 앞 방향으로 길게 베기
// 3타: 전진 찌르기 (피니쉬)
// 공중(점프 중) 공격: 아래쪽 부채꼴 1회. 착지 전까지 1회만 나간다.
//
// 판정: 지상은 각 타수마다 전방 OverlapBox 1회로 적중을 처리한다.
//       공중은 플레이어 아래 중심의 부채꼴(원형 범위 + 각도 필터) 1회로 적중을 처리한다.
// 피해: PlayerAttackContext를 일반 적과 보스의 공통 IDamageable 경로에 전달하므로
//       EnemyUnit의 신앙치 피해 보정(BeliefController)이 그대로 적용된다.
// 교리: 피해가 받아들여진 뒤 DoctrineEventBus에 OnAttack을 Raise한다 (주체=플레이어).
//       "플레이어는 공격하면 ..." 계열 교리가 별도 분기 없이 발동한다.
// 이동: 공격 중에는 PlayerMove.ControlLocked를 잠가 이동 덮어쓰기와
//       방향 전환을 막고, 타수별 전진(lunge) 거리만큼 Rigidbody로 밀어준다.
// 애니메이터가 있으면 Attack1~3 / AirAttack 트리거, IsAttacking / Combo 파라미터를 쏘므로
// 나중에 스프라이트 애니메이션만 연결하면 된다.
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerCombatEvents))]
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

        [Tooltip("recovery 이후 다음 타 입력을 받아주는 유예 시간. 마지막 타는 피니쉬라 0 권장.")]
        [Min(0f)] public float comboWindow;

        [Tooltip("이번 타에 앞으로 미끄러지듯 나가는 거리.")]
        [Min(0f)] public float lungeDistance;

        [Tooltip("적중 시 적을 미는 힘(충격량).")]
        [Min(0f)] public float knockback;

        [Tooltip("적중 시 적을 띄우는 힘(충격량). 피니쉬용.")]
        [Min(0f)] public float launchUp;
    }

    [Header("입력")]
    [Tooltip("공격에 쓸 마우스 버튼. 0 = 좌클릭.")]
    [SerializeField] private int attackMouseButton = 0;
    public int AttackMouseButton => attackMouseButton;

    [Header("콤보 (1~3타)")]
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
    };

    [Header("공중 공격 (점프 중 · 아래 부채꼴)")]
    [Tooltip("공중에 있을 때 좌클릭하면 나가는 단타. 판정은 아래쪽 부채꼴(원형 범위 + 각도 필터). 박스 필드(hitboxSize/Offset/Angle)는 공중 공격에서 쓰지 않는다.")]
    [SerializeField] private ComboStep airAttack = new ComboStep
    {
        name = "공중: 아래 부채꼴 내리찍기",
        damage = 14f,
        hitboxSize = new Vector2(2.2f, 1.2f),
        hitboxOffset = new Vector2(0f, 0f),
        hitboxAngle = 0f,
        windupTime = 0.05f,
        recoveryTime = 0.22f,
        comboWindow = 0f,
        lungeDistance = 0f,
        knockback = 0f,
        launchUp = 0f,
    };

    [Header("아래 부채꼴 판정")]
    [Tooltip("부채꼴 반경(월드 단위). 플레이어 아래 중심 원에서 이 거리 안이 후보가 된다.")]
    [SerializeField, Min(0.5f)] private float downFanRadius = 2.6f;
    [Tooltip("아래 방향(270도) 기준 좌우 퍼짐 각도. 65면 총 130도 부채꼴.")]
    [SerializeField, Range(10f, 180f)] private float downFanHalfAngle = 65f;
    [Tooltip("부채꼴 중심(플레이어 기준 오프셋). x는 바라보는 방향으로 뒤집힌다. 기본 0,-0.2.")]
    [SerializeField] private Vector2 downFanOriginOffset = new Vector2(0f, -0.2f);
    [Tooltip("적중 시 적을 아래로 미는 힘(충격량).")]
    [SerializeField, Min(0f)] private float downFanEnemyKnockDown = 4f;
    [Tooltip("적중 시 적을 바깥(좌우)으로 미는 힘. 중심에서 멀어지는 방향.")]
    [SerializeField, Min(0f)] private float downFanEnemyKnockOut = 2f;
    [Tooltip("적중 시 플레이어가 위로 튀어오르는 속도. 0이면 바운스 없음.")]
    [SerializeField, Min(0f)] private float downFanPlayerBounce = 8f;

    [Tooltip("공격 후 착지 전까지 공중 공격을 다시 쓸 수 있게 하는지.")]
    [SerializeField] private bool airAttackResetOnLand = true;

    [Header("아래 부채꼴 파티클")]
    [Tooltip("내리찍기 임팩트 파티클. 비워두면 Awake에서 자동으로 붙인다. 끄려면 enabled를 끄면 된다.")]
    [SerializeField] private DownFanImpactEffect downFanImpact;
    [Tooltip("파티클을 띄울지 여부.")]
    [SerializeField] private bool spawnDownFanImpact = true;

    [Header("판정")]
    [Tooltip("공격이 맞을 레이어. 비워두면(0) 전부 판정한다.")]
    [SerializeField] private LayerMask targetLayers = Physics2D.AllLayers;

    [Header("지상 베기 칼날 파티클")]
    [Tooltip("1/2/3타 타격 지점에서 아래 공격과 같은 재질의 궤적을 그린다.")]
    [SerializeField] private bool spawnSwingSlash = true;

    [Header("피니쉬 연출")]
    [Tooltip("피니쉬(마지막 타)가 적중했을 때 시간을 잠깐 멈추는 타격감 연출(초). 0이면 끄기.")]
    [SerializeField, Min(0f)] private float finishHitStop = 0.06f;

    private Rigidbody2D rb;
    private PlayerMove playerMove;
    private Animator animator;
    private DoctrineStats stats;
    private PlayerHealth health;
    private PlayerDash dash;

    private int currentCombo; // 다음에 나갈 타수 (0~2)
    private bool isAttacking;
    private bool isAirAttacking;
    private bool airAttackUsed; // 이번 점프에서 공중 공격을 썼는지
    private bool wasGrounded = true;
    private bool inputBuffered; // 이번 타 도중 다음 타 입력이 들어왔는지
    private Coroutine attackRoutine;
    private Vector3 baseScale;
    private readonly HashSet<IDamageable> overlapSeen = new HashSet<IDamageable>();
    private readonly List<Collider2D> hitBuffer = new List<Collider2D>(32);
    private readonly List<RaycastHit2D> attemptBuffer = new List<RaycastHit2D>(32);
    private PlayerAttackContext activeSwing;

    public bool IsAttacking => isAttacking || isAirAttacking;
    public bool IsAirAttacking => isAirAttacking;
    public int CurrentCombo => currentCombo;
    public int ComboCount => comboSteps != null ? comboSteps.Length : 0;

    /// <summary>Exposes the ground combo definition so the boss can mirror the player's combo.</summary>
    public bool TryGetComboStep(int index, out ComboStep step)
    {
        if (comboSteps == null || index < 0 || index >= comboSteps.Length)
        {
            step = default;
            return false;
        }

        step = comboSteps[index];
        return true;
    }

    private void Awake()
    {
        // Existing serialized players also need the signal component after this integration.
        if (GetComponent<PlayerCombatEvents>() == null)
            gameObject.AddComponent<PlayerCombatEvents>();
        rb = GetComponent<Rigidbody2D>();
        playerMove = GetComponent<PlayerMove>();
        animator = GetComponentInChildren<Animator>();
        stats = GetComponent<DoctrineStats>();
        health = GetComponent<PlayerHealth>();
        dash = GetComponent<PlayerDash>();
        baseScale = transform.localScale;

        ResolveDownFanImpact();
    }

    // 내리찍기 파티클은 씬 루트에 스스로 풀을 만든다.
    // 플레이어 자식으로 두면 좌우 반전되므로 컴포넌트만 플레이어에 붙이고
    // 파티클 오브젝트는 별도 루트에서 관리한다.
    private void ResolveDownFanImpact()
    {
        if (downFanImpact == null)
            downFanImpact = GetComponent<DownFanImpactEffect>();

        if (downFanImpact == null)
            downFanImpact = gameObject.AddComponent<DownFanImpactEffect>();

        SpriteRenderer body = GetComponent<SpriteRenderer>();
        if (body == null)
            body = GetComponentInChildren<SpriteRenderer>();
        if (body != null)
            downFanImpact.SetSortingLayer(body.sortingLayerID);
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
        if (!isActiveAndEnabled || UiModal.IsOpen || dash != null && dash.IsDashing)
            return;
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

        activeSwing?.Dispose();
        activeSwing = null;
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
            yield return SwingDownFan();
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
        if (animator != null)
            animator.SetTrigger("Attack" + (index + 1));
        return SwingOnce(comboSteps[index], stopSlide: true,
            isFinish: index == comboSteps.Length - 1);
    }

    /// <summary>
    /// 공중 아래 부채꼴 공격. 플레이어 아래 중심 원 + 아래 방향 각도 필터로 판정한다.
    /// </summary>
    private IEnumerator SwingDownFan()
    {
        float facing = transform.localScale.x >= 0f ? 1f : -1f;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(baseScale.x) * facing;
        transform.localScale = scale;

        var attack = new PlayerAttackContext(gameObject);
        activeSwing = attack;
        try
        {
            RegisterDownFanAttempts(attack);
            if (activeSwing != attack || !isActiveAndEnabled)
                yield break;

            if (airAttack.windupTime > 0f)
                yield return new WaitForSeconds(airAttack.windupTime);

            if (activeSwing != attack || !isActiveAndEnabled)
                yield break;

            // 판정과 같은 원점/반경/각도로 칼날 궤적을 그린다.
            PlayDownAttackEffect(downFanImpact, transform.position, facing);

            int hitCount = DoDownFanHit(attack);
            if (hitCount > 0 && rb != null && downFanPlayerBounce > 0f)
            {
                // 아래를 찍고 위로 튀어오른다 (y만 덮어씀).
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, downFanPlayerBounce);
            }

            if (airAttack.recoveryTime > 0f)
                yield return new WaitForSeconds(airAttack.recoveryTime);
        }
        finally
        {
            if (activeSwing == attack) activeSwing = null;
            attack.Dispose();
        }
    }

    internal void PlayDownAttackEffect(DownFanImpactEffect effect, Vector2 position, float facing)
    {
        if (spawnDownFanImpact && effect != null)
            effect.Play(DownFanOrigin(position, facing), downFanRadius, downFanHalfAngle, facing);
    }

    internal void PlaySwingEffect(DownFanImpactEffect effect, Vector2 position, Vector2 center, Vector2 size, float facing)
    {
        if (spawnSwingSlash && effect != null)
            effect.PlaySwing(position, center, size, facing);
    }

    internal void PlaySwingWithinEffect(DownFanImpactEffect effect, Vector2 position, Vector2 center, Vector2 size, float boxAngle, float facing)
    {
        if (spawnSwingSlash && effect != null)
            effect.PlaySwingWithin(position, center, size, boxAngle, facing);
    }

    private Vector2 DownFanOrigin(float facing) => DownFanOrigin(transform.position, facing);

    private Vector2 DownFanOrigin(Vector2 position, float facing) => position +
        new Vector2(downFanOriginOffset.x * facing, downFanOriginOffset.y);

    private bool IsInDownFan(Vector2 origin, Collider2D hit)
    {
        if (!CombatPhysics.IsBodyValid(hit) || hit.transform.IsChildOf(transform))
            return false;
        Vector2 closest = hit.ClosestPoint(origin);
        Vector2 toTarget = closest - origin;
        float distance = toTarget.magnitude;
        if (distance > downFanRadius)
            return false;
        // 원 중심에 겹쳐 있으면 각도 무관하게 적중.
        if (distance < 0.001f)
            return true;
        float angle = Vector2.Angle(Vector2.down, toTarget / distance);
        return angle <= downFanHalfAngle;
    }

    private void RegisterDownFanAttempts(PlayerAttackContext attack)
    {
        Vector2 origin = DownFanOrigin(transform.localScale.x >= 0f ? 1f : -1f);
        CombatPhysics.OverlapCircle(origin, downFanRadius, TargetMask, hitBuffer);
        for (int i = 0; i < hitBuffer.Count; i++)
        {
            if (!IsInDownFan(origin, hitBuffer[i])) continue;
            if (TryGetTarget(hitBuffer[i], out IDamageable target, out _)) attack.Begin(target);
        }
        hitBuffer.Clear();
    }

    private int DoDownFanHit(PlayerAttackContext attack, float _facing = 1f)
    {
        Vector2 origin = DownFanOrigin(transform.localScale.x >= 0f ? 1f : -1f);
        CombatPhysics.OverlapCircle(origin, downFanRadius, TargetMask, hitBuffer);

        overlapSeen.Clear();
        int hitCount = 0;

        float multiplier = stats != null ? stats.AttackMultiplier : 1f;
        float finalDamage = Mathf.Max(0f, airAttack.damage * multiplier);
        if (finalDamage <= 0f)
            return 0;

        for (int i = 0; i < hitBuffer.Count; i++)
        {
            Collider2D hit = hitBuffer[i];
            if (!IsInDownFan(origin, hit)) continue;
            if (!TryGetTarget(hit, out IDamageable damageable, out GameObject targetObject) ||
                !overlapSeen.Add(damageable)) continue;

            bool applied = attack.Begin(damageable, publishAttempt: false) && attack.TryHit(damageable, finalDamage);
            if (applied)
            {
                if (DoctrineEventBus.HasListeners)
                    DoctrineEventBus.Raise(ConditionType.OnAttack, gameObject, targetObject);
                hitCount++;
                ApplyDownFanKnockback(origin, hit);
            }
        }

        overlapSeen.Clear();
        hitBuffer.Clear();
        return hitCount;
    }

    private void ApplyDownFanKnockback(Vector2 origin, Collider2D hit)
    {
        if (hit == null)
            return;

        Rigidbody2D enemyRb = hit.attachedRigidbody;
        if (enemyRb == null)
            enemyRb = hit.GetComponentInParent<Rigidbody2D>();
        if (enemyRb == null || enemyRb == rb)
            return;

        Vector2 targetPos = hit.transform.position;
        Vector2 outward = targetPos - origin;
        if (outward.sqrMagnitude < 0.0001f)
            outward = Vector2.down;
        outward.Normalize();

        Vector2 force = outward * downFanEnemyKnockOut + Vector2.down * downFanEnemyKnockDown;
        enemyRb.AddForce(force, ForceMode2D.Impulse);
    }

    private IEnumerator SwingOnce(ComboStep step, bool stopSlide, bool isFinish = false)
    {
        float facing = transform.localScale.x >= 0f ? 1f : -1f;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(baseScale.x) * facing;
        transform.localScale = scale;

        // 각 타격마다 별도 ID. 시작 범위 밖에서 뒤늦게 진입한 대상은 적중만 알린다.
        var attack = new PlayerAttackContext(gameObject);
        activeSwing = attack;
        try
        {
            RegisterAttackAttempts(attack, step, facing);
            if (activeSwing != attack || !isActiveAndEnabled)
                yield break;

            if (rb != null && step.lungeDistance > 0f)
            {
                float lungeTime = Mathf.Max(0.01f, step.windupTime + 0.05f);
                rb.linearVelocity = new Vector2(
                    facing * (step.lungeDistance / lungeTime), rb.linearVelocity.y);
            }

            if (step.windupTime > 0f)
                yield return new WaitForSeconds(step.windupTime);

            if (activeSwing != attack || !isActiveAndEnabled)
                yield break;

            // 판정 박스를 그리는 대신, 타격 지점에 아래 공격과 같은 칼날 궤적을 그린다.
            PlaySwingEffect(downFanImpact, transform.position, HitCenter(step, facing), step.hitboxSize, facing);

            int hitCount = DoHit(attack, step, facing);
            if (stopSlide) StopMotion();

            if (step.recoveryTime > 0f)
                yield return new WaitForSeconds(step.recoveryTime);
            if (hitCount > 0 && isFinish && finishHitStop > 0f)
                yield return HitStop(finishHitStop);
        }
        finally
        {
            if (activeSwing == attack) activeSwing = null;
            attack.Dispose();
        }
    }

    private void StopMotion()
    {
        // 수평 미끄러짐만 멈춘다 (y는 중력/점프에 맡긴다).
        if (rb != null)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private int TargetMask => targetLayers.value == 0 ? Physics2D.AllLayers : targetLayers.value;

    private Vector2 HitCenter(ComboStep step, float facing) => (Vector2)transform.position +
        new Vector2(step.hitboxOffset.x * facing, step.hitboxOffset.y);

    private bool TryGetTarget(Collider2D hit, out IDamageable damageable, out GameObject targetObject)
    {
        damageable = null;
        targetObject = null;
        if (!CombatPhysics.IsBodyValid(hit) || hit.transform.IsChildOf(transform)) return false;
        damageable = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
        if (damageable == null) return false;
        Component component = damageable as Component;
        if (component != null && (component.GetComponent<PlayerHealth>() != null || component.CompareTag("Player")))
            return false;
        targetObject = component != null ? component.gameObject : hit.gameObject;
        return true;
    }

    private void RegisterAttackAttempts(PlayerAttackContext attack, ComboStep step, float facing)
    {
        Vector2 center = HitCenter(step, facing);
        float angle = facing >= 0f ? step.hitboxAngle : -step.hitboxAngle;
        CombatPhysics.OverlapBox(center, step.hitboxSize, angle, TargetMask, hitBuffer);
        // Include the distance this windup can actually travel, in the locked attack direction.
        float reach = rb != null ? step.lungeDistance * Mathf.Max(0f, step.windupTime) /
            Mathf.Max(0.01f, step.windupTime + 0.05f) : 0f;
        CombatPhysics.SweepBox(center, step.hitboxSize, angle, Vector2.right * facing,
            reach, TargetMask, attemptBuffer);
        for (int i = 0; i < hitBuffer.Count; i++)
            if (TryGetTarget(hitBuffer[i], out IDamageable target, out _)) attack.Begin(target);
        for (int i = 0; i < attemptBuffer.Count; i++)
            if (TryGetTarget(attemptBuffer[i].collider, out IDamageable target, out _)) attack.Begin(target);
        hitBuffer.Clear();
        attemptBuffer.Clear();
    }

    private int DoHit(PlayerAttackContext attack, ComboStep step, float facing)
    {
        Vector2 center = HitCenter(step, facing);
        float angle = facing >= 0f ? step.hitboxAngle : -step.hitboxAngle;

        CombatPhysics.OverlapBox(center, step.hitboxSize, angle, TargetMask, hitBuffer);

        overlapSeen.Clear();
        int hitCount = 0;

        float multiplier = stats != null ? stats.AttackMultiplier : 1f;
        float finalDamage = Mathf.Max(0f, step.damage * multiplier);
        if (finalDamage <= 0f)
            return 0;

        for (int i = 0; i < hitBuffer.Count; i++)
        {
            Collider2D hit = hitBuffer[i];
            if (!TryGetTarget(hit, out IDamageable damageable, out GameObject targetObject) ||
                !overlapSeen.Add(damageable)) continue;

            // Table rule: OnAttack triggers only when the attack motion is done
            // AND the hit lands (TakeDamage applied). Whiffed swings raise nothing.
            // Late entrants may be hit, but never retroactively count as a start-of-swing attempt.
            bool applied = attack.Begin(damageable, publishAttempt: false) && attack.TryHit(damageable, finalDamage);
            if (applied)
            {
                if (DoctrineEventBus.HasListeners)
                    DoctrineEventBus.Raise(ConditionType.OnAttack, gameObject, targetObject);
                hitCount++;
                ApplyKnockback(hit, step, facing);
            }
        }

        overlapSeen.Clear();
        hitBuffer.Clear();
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
        if (PlayerDeathCoordinator.IsResultOpen) yield break;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(duration);
        if (!PlayerDeathCoordinator.IsResultOpen) Time.timeScale = previousScale;
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
        Color[] colors = { Color.cyan, Color.green, Color.yellow };

        for (int i = 0; i < comboSteps.Length; i++)
        {
            DrawStepGizmo(comboSteps[i], colors[i % colors.Length], facing);
        }

        // 공중 공격 Gizmos (아래쪽 부채꼴 범위 표시).
        Vector2 downOrigin = (Vector2)transform.position +
            new Vector2(downFanOriginOffset.x * facing, downFanOriginOffset.y);
        Gizmos.color = new Color(1f, 0.6f, 0.15f, 0.9f);
        Gizmos.DrawWireSphere(downOrigin, downFanRadius);
#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(1f, 0.6f, 0.15f, 0.9f);
        Vector3 downFrom = Vector3.down;
        Vector3 fanFrom = Quaternion.Euler(0f, 0f, downFanHalfAngle) * downFrom;
        Vector3 fanTo = Quaternion.Euler(0f, 0f, -downFanHalfAngle) * downFrom;
        UnityEditor.Handles.DrawLine(downOrigin, downOrigin + (Vector2)(fanFrom * downFanRadius));
        UnityEditor.Handles.DrawLine(downOrigin, downOrigin + (Vector2)(fanTo * downFanRadius));
#endif
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
