using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Attack lifecycles and presentation, independent of the pattern selection/state machine.</summary>
public sealed class BossAttackExecutor : MonoBehaviour
{
    private static readonly WaitForFixedUpdate PhysicsTick = new WaitForFixedUpdate();
    private BossCombatController owner;
    private CombatAfterimageEmitter afterimages;
    private DownFanImpactEffect bladeEffect;
    private BossCombatSettings Data => owner.Settings;
    public void Initialize(BossCombatController boss)
    {
        owner = boss;
        afterimages = GetComponent<CombatAfterimageEmitter>();
        if (afterimages == null) afterimages = gameObject.AddComponent<CombatAfterimageEmitter>();
        bladeEffect = GetComponent<DownFanImpactEffect>();
        if (bladeEffect == null) bladeEffect = gameObject.AddComponent<DownFanImpactEffect>();
        bladeEffect.SetSortingLayer(owner.BodyRenderer.sortingLayerID, owner.BodyRenderer.sortingOrder);
    }

    /// <summary>Number of shockwave branches still travelling after the body action ended.</summary>
    public int ShockwaveTracking { get; private set; }

    public void ClearPresentationEffects()
    {
        // Only executor-owned coroutines (the travelling shockwave tracker) live here; the lanes run on the controller.
        StopAllCoroutines();
        ShockwaveTracking = 0;
        afterimages?.Clear();
        bladeEffect?.Clear();
    }

    public bool TryGetMeleeGeometry(out AttackGeometry geometry)
    {
        geometry = default;
        if (owner.PlayerAttack == null || !owner.PlayerAttack.TryGetComboStep(0, out PlayerAttack.ComboStep step))
            return false;
        geometry = ComboGeometry(step, owner.Facing);
        return true;
    }

    public BossAttackVisual CreateWarning(AttackGeometry geometry, bool followOwner = false)
    {
        var warning = owner.SpawnEffect(Data.WarningPrefab);
        warning.Show(geometry, Data.WarningOrder, followTarget: followOwner ? owner.transform : null);
        warning.SetSortingBehind(owner.BodyRenderer);
        return warning;
    }
    public void Impact(Vector2 position, bool blocked)
    {
        var effect = owner.SpawnEffect(Data.ImpactPrefab);
        effect.Show(new AttackGeometry(position, Vector2.one * (blocked ? 0.6f : 0.4f)), Data.AttackOrder + 2);
        Fade(effect, 0.18f);
    }
    private void Fade(BossAttackVisual effect, float duration)
    { effect.Fade(duration); Destroy(effect.gameObject, duration + 0.02f); }

    private IEnumerator Windup(BossAttackRecord record, float duration)
    {
        var warning = CreateWarning(record.Geometry, followOwner: true);
        string label = AttackLabel(record);
        owner.SetActionCue(label, AttackHint(record), BossPresentationState.Windup, duration);
        Vector2 previousBossPosition = owner.transform.position;
        float elapsed = 0f;
        while (elapsed < duration && owner.IsAttackValid(record) && owner.CanStartNextHit)
        {
            Vector2 currentBossPosition = owner.transform.position;
            record.TranslateWarning(currentBossPosition - previousBossPosition);
            previousBossPosition = currentBossPosition;
            owner.Observe(record, record.Geometry);
            warning.SetProgress(elapsed / duration);
            warning.SetCue(label, duration - elapsed, elapsed / duration, owner);
            yield return PhysicsTick;
            elapsed += Time.fixedDeltaTime;
        }
        if (owner.IsAttackValid(record))
        {
            Vector2 currentBossPosition = owner.transform.position;
            record.TranslateWarning(currentBossPosition - previousBossPosition);
            owner.Observe(record, record.Geometry);
        }
        owner.DestroyEffect(warning != null ? warning.gameObject : null);
        if (owner.IsAttackValid(record) && !owner.CanStartNextHit)
            owner.CompleteAttack(record, BossAttackEndReason.Cancelled);
    }
    private IEnumerator Recovery()
    {
        if (!owner.CanContinue) yield break;
        owner.SetActionCue("공격 후 빈틈", "다음 예고를 확인하며 반격하세요.", BossPresentationState.Recovery, Data.Recovery / owner.SpeedMultiplier);
        yield return new WaitForSeconds(Data.Recovery / owner.SpeedMultiplier);
    }

    public IEnumerator Regular(BossPatternKind kind, BossRegularAttackContext context)
    {
        if (!owner.CanStartNextHit) yield break;
        if (kind == BossPatternKind.MeleeCombo) yield return MeleeCombo(context);
        else if (kind == BossPatternKind.Dash) yield return Dash(context);
        else if (kind == BossPatternKind.Projectile) yield return Projectile(context);
    }

    private IEnumerator MeleeCombo(BossRegularAttackContext context)
    {
        int stepCount = owner.PlayerAttack != null ? owner.PlayerAttack.ComboCount : 0;
        if (owner.PlayerAttack == null || stepCount < 2)
        {
            owner.ShowFeedback("연속 베기 중지 · 플레이어 콤보 데이터 부족", new Color(1f, .65f, .45f));
            yield break;
        }

        owner.FacePlayer();
        int direction = owner.Facing;
        int token = owner.Generation;
        for (int index = 0; index < stepCount && token == owner.Generation && owner.CanStartNextHit; index++)
        {
            if (!owner.PlayerAttack.TryGetComboStep(index, out PlayerAttack.ComboStep step))
                yield break;

            float warning = index == 0 ? Data.MeleeComboFirstWarning : Data.MeleeComboFollowupWarning;
            float damage = index < Data.MeleeComboDamage.Length
                ? Data.MeleeComboDamage[index]
                : Data.MeleeComboDamage[Data.MeleeComboDamage.Length - 1];
            // The player's comboWindow accepts a buffered click; this sequence advances automatically after recovery.
            yield return PlayerMeleeStep(index + 1, index == stepCount - 1, step, direction, damage,
                warning / owner.SpeedMultiplier, context);
        }
    }

    private IEnumerator PlayerMeleeStep(int hitIndex, bool isFinisher, PlayerAttack.ComboStep step, int direction,
        float damage, float warningDuration, BossRegularAttackContext context)
    {
        if (!owner.CanStartNextHit) yield break;

        Vector2 attackDirection = Vector2.right * direction;
        var geometry = ComboGeometry(step, direction);
        var record = owner.CreateAttack(BossAttackKind.Melee, hitIndex, geometry, damage, context.DisplayBelief, true, context);
        BossAttackVisual slash = null;
        BossFanWarning warning = null;
        bool completed = false;
        try
        {
            // The telegraph is the same fan the player's blade slash draws (see DownFanImpactEffect.SwingFan).
            warning = CreateFanWarning();
            ShowFan(warning, record, direction);
            bool combo = hitIndex > 0;
            string label = combo ? $"연속 베기 {hitIndex}/{OwnerComboCount}" : AttackLabel(record);
            string finisherHint = step.launchUp > 0f ? "피니시 · 적중 시 넉백과 띄우기" : "피니시 · 적중 시 넉백";
            string hint = combo
                ? (isFinisher ? finisherHint : "플레이어의 연계 · 표시된 범위에서 벗어나세요.")
                : AttackHint(record);
            if (combo && record.BurnEnhanced) hint += " · 피격 시 화상";
            else if (combo && context.Dodge != null) hint += $" · 회피하면 믿음 -{Data.BeliefReduction:0}";

            warningDuration = Mathf.Max(.001f, warningDuration);
            owner.SetActionCue(label, hint, BossPresentationState.Windup, warningDuration);
            int animationIndex = Mathf.Max(0, hitIndex - 1);
            owner.Presentation.ShowMelee(animationIndex);
            float warningStartedAt = Time.time;
            float warningEndsAt = warningStartedAt + warningDuration;
            float lungeDistance = step.windupTime > 0f
                ? step.lungeDistance * step.windupTime / (step.windupTime + 0.05f)
                : 0f;
            float lungeDuration = Mathf.Min(warningDuration, step.windupTime / owner.SpeedMultiplier);
            float angleRadians = step.hitboxAngle * Mathf.Deg2Rad;
            float hitboxForwardExtent = 0.5f * (Mathf.Abs(Mathf.Cos(angleRadians)) * step.hitboxSize.x +
                Mathf.Abs(Mathf.Sin(angleRadians)) * step.hitboxSize.y);
            float playerForwardDistance = Vector2.Dot((Vector2)owner.PlayerCollider.bounds.center - (Vector2)owner.transform.position,
                attackDirection);
            float maxLungeBeforeTargetLeavesHitbox = playerForwardDistance + owner.PlayerCollider.bounds.extents.x +
                hitboxForwardExtent - step.hitboxOffset.x - 0.02f;
            lungeDistance = Mathf.Min(lungeDistance, Mathf.Max(0f, maxLungeBeforeTargetLeavesHitbox));
            float lungeSpeed = lungeDuration > 0f ? lungeDistance / lungeDuration : 0f;
            bool lungeBlocked = false;

            while (Time.time < warningEndsAt && owner.IsAttackValid(record) && owner.CanStartNextHit)
            {
                float elapsed = Time.time - warningStartedAt;
                if (!lungeBlocked && lungeDistance > 0f && elapsed < lungeDuration)
                {
                    float requested = Mathf.Min(lungeDistance, lungeSpeed * Time.fixedDeltaTime);
                    Vector2 center = owner.BodyCollider.bounds.center;
                    Vector2 size = owner.BodyCollider.bounds.size;
                    float clear = BossCombatPhysics.ClearDistance(center, size, attackDirection, requested,
                        owner, false, out bool blocked);
                    if (clear > 0f)
                    {
                        owner.Body.linearVelocity = new Vector2(0f, owner.Body.linearVelocity.y);
                        owner.Body.MovePosition(owner.Body.position + attackDirection * clear);
                        lungeDistance -= clear;
                    }
                    lungeBlocked = blocked || clear + 0.001f < requested;
                }

                SyncMeleeWarning(record, step, direction);
                owner.Observe(record, record.Geometry);
                ShowFan(warning, record, direction);
                warning.SetProgress(elapsed / warningDuration);
                yield return PhysicsTick;
            }

            if (owner.IsAttackValid(record))
            {
                SyncMeleeWarning(record, step, direction);
                owner.Observe(record, record.Geometry);
            }
            owner.DestroyEffect(warning != null ? warning.gameObject : null);
            warning = null;
            if (owner.IsAttackValid(record) && !owner.CanStartNextHit)
                owner.CompleteAttack(record, BossAttackEndReason.Cancelled);
            if (!owner.IsAttackValid(record)) yield break;

            record.Activate();
            float activeDuration = Data.MeleeActiveTime / owner.SpeedMultiplier;
            float activeEndsAt = Time.time + activeDuration;
            owner.SetActionCue(label, hint, BossPresentationState.Active, activeDuration);
            if (owner.Presentation.ShowMelee(animationIndex, activeDuration * .5f))
                owner.PlayerAttack.PlaySwingEffect(bladeEffect, owner.transform.position,
                    record.Geometry.Center, record.Geometry.Size, direction);
            else
            {
                slash = owner.SpawnEffect(Data.SlashPrefab);
                slash.Show(record.Geometry, Data.AttackOrder, record.BurnEnhanced);
            }
            while (Time.time < activeEndsAt && owner.IsAttackValid(record))
            {
                if (owner.Observe(record, record.Geometry) && owner.TryHit(record) && isFinisher)
                    ApplyFinisherKnockback(step, direction);
                yield return PhysicsTick;
            }

            owner.CompleteAttack(record, BossAttackEndReason.Completed);
            completed = true;
            if (slash != null) Fade(slash, 0.12f);
            slash = null;

            float recovery = step.recoveryTime / owner.SpeedMultiplier;
            if (combo && recovery > 0f && owner.CanContinue)
            {
                owner.SetActionCue("연속 베기 연결", "다음 타격을 준비합니다.", BossPresentationState.Recovery, recovery);
                yield return new WaitForSeconds(recovery);
            }
        }
        finally
        {
            owner.DestroyEffect(warning != null ? warning.gameObject : null);
            if (!completed)
            {
                owner.DestroyEffect(slash != null ? slash.gameObject : null);
                owner.CompleteAttack(record, BossAttackEndReason.Cancelled);
            }
        }
    }

    private BossFanWarning CreateFanWarning()
    {
        BossFanWarning fan = BossFanWarning.Create(owner.transform, Data.WarningPrefab, owner.BodyRenderer);
        owner.AdoptEffect(fan.gameObject);
        return fan;
    }

    private void ShowFan(BossFanWarning fan, BossAttackRecord record, int direction)
    {
        if (fan == null) return;
        Vector2 origin = owner.transform.position;
        if (DownFanImpactEffect.SwingFan(origin, record.Geometry.Center, record.Geometry.Size, direction,
                out float centerAngle, out float radius, out float half))
            fan.Show(origin, centerAngle, radius, half);
    }

    private void SyncMeleeWarning(BossAttackRecord record, PlayerAttack.ComboStep step, int direction)
    {
        // Follow actual physics movement, including knockback, instead of assuming every requested lunge was completed.
        record.TranslateWarning(ComboGeometry(step, direction).Center - record.Geometry.Center);
    }

    private AttackGeometry ComboGeometry(PlayerAttack.ComboStep step, int direction)
    {
        Vector2 center = (Vector2)owner.transform.position +
            new Vector2(step.hitboxOffset.x * direction, step.hitboxOffset.y);
        float angle = direction >= 0 ? step.hitboxAngle : -step.hitboxAngle;
        return new AttackGeometry(center, step.hitboxSize, angle);
    }

    private void ApplyFinisherKnockback(PlayerAttack.ComboStep step, int direction)
    {
        Rigidbody2D playerBody = owner.PlayerHealth != null
            ? owner.PlayerHealth.GetComponent<Rigidbody2D>()
            : null;
        if (playerBody == null || (step.knockback <= 0f && step.launchUp <= 0f)) return;

        playerBody.AddForce(new Vector2(direction * step.knockback, step.launchUp), ForceMode2D.Impulse);
    }

    private IEnumerator Dash(BossRegularAttackContext context)
    {
        owner.FacePlayer();
        Vector2 direction = Vector2.right * owner.Facing;
        Vector2 size = owner.BodyCollider.bounds.size;
        Vector2 start = owner.BodyCollider.bounds.center;
        float warningDistance = BossCombatPhysics.ClearDistance(start, size, direction,
            Data.DashDistance, owner, false, out _);
        // The warning starts at the body's leading edge: nothing is drawn behind or under the boss.
        var corridor = new AttackGeometry(start + direction * (size.x * 0.5f + warningDistance * 0.5f),
            new Vector2(warningDistance, size.y));
        var record = owner.CreateAttack(BossAttackKind.Dash, 0, corridor, Data.DashDamage, context.DisplayBelief, true, context);
        float speed = Data.DashSpeed * owner.SpeedMultiplier;
        yield return Windup(record, Data.Windup / owner.SpeedMultiplier);
        if (!owner.IsAttackValid(record)) yield break;
        record.Activate();
        owner.SetActionCue("돌진", AttackHint(record), BossPresentationState.Active, Data.DashDistance / speed);
        owner.Presentation.ShowDash();
        float remaining = Data.DashDistance;
        bool blocked = false;
        while (remaining > 0.001f && owner.IsAttackValid(record))
        {
            Vector2 center = owner.BodyCollider.bounds.center;
            float step = Mathf.Min(remaining, speed * Time.fixedDeltaTime);
            step = BossCombatPhysics.ClearDistance(center, size, direction, step, owner, false, out blocked);
            var ahead = new AttackGeometry(center + direction * (size.x * 0.5f + remaining * 0.5f), new Vector2(remaining, size.y));
            owner.Observe(record, ahead);
            if (BossCombatPhysics.SweptTarget(center, size, direction, step, owner, false)) owner.TryHit(record);
            Color? tint = record.BurnEnhanced ? (Color?)new Color(1f, 0.43f, 0.12f, 0.9f) : null;
            afterimages.Emit(Data.DashPrefab, owner.BodyRenderer, Data.AttackOrder, tint);
            owner.Body.linearVelocity = new Vector2(0f, owner.Body.linearVelocity.y);
            owner.Body.MovePosition(owner.Body.position + direction * step);
            remaining -= step;
            yield return PhysicsTick;
            if (blocked) { Impact(owner.BodyCollider.bounds.center + (Vector3)(direction * size.x * 0.5f), true); break; }
        }
        owner.Body.linearVelocity = new Vector2(0f, owner.Body.linearVelocity.y);
        owner.CompleteAttack(record, blocked ? BossAttackEndReason.Wall : BossAttackEndReason.Completed);
        yield return Recovery();
    }

    /// <summary>
    /// Plans a projectile from the muzzle toward the player's body center, whatever the height difference.
    /// False when an obstacle ends the path before it can reach the player.
    /// </summary>
    public bool TryPlanProjectile(int facing, out Vector2 origin, out Vector2 direction, out float length, out bool endBlocked, out AttackGeometry geometry)
    {
        origin = owner.ProjectileOriginFor(facing);
        direction = ProjectileAimDirection(origin, facing);
        float speed = Data.ProjectileSpeed * owner.SpeedMultiplier;
        float lifetime = Data.ProjectileLifetime / owner.SpeedMultiplier;
        float maxDistance = Mathf.Min(Data.ProjectileDistance, speed * lifetime);
        length = BossCombatPhysics.ClearDistance(origin, Vector2.one * Data.ProjectileDiameter, direction,
            maxDistance, owner, true, out endBlocked);
        // Include the projectile's cap so the visible warning encloses all swept damage.
        geometry = AttackGeometry.Corridor(origin - direction * Data.ProjectileDiameter * 0.5f,
            direction, length + Data.ProjectileDiameter, Data.ProjectileDiameter);
        return geometry.Overlaps(owner.PlayerCollider);
    }

    private IEnumerator Projectile(BossRegularAttackContext context)
    {
        owner.FacePlayer();
        float speed = Data.ProjectileSpeed * owner.SpeedMultiplier;
        float lifetime = Data.ProjectileLifetime / owner.SpeedMultiplier;
        if (!TryPlanProjectile(owner.Facing, out Vector2 origin, out Vector2 direction, out float length, out bool endBlocked, out AttackGeometry geometry))
        {
            yield return Recovery();
            yield break;
        }

        var record = owner.CreateAttack(BossAttackKind.Projectile, 0, geometry, Data.ProjectileDamage, context.DisplayBelief, true, context);
        yield return Windup(record, Data.Windup / owner.SpeedMultiplier);
        if (!owner.IsAttackValid(record)) yield break;
        record.Activate();
        owner.SetActionCue("투사체", AttackHint(record), BossPresentationState.Active, lifetime);
        var projectile = owner.SpawnEffect(Data.ProjectilePrefab);
        projectile.Initialize(owner, record, origin, direction, speed, length, lifetime, endBlocked);
        while (owner.IsAttackValid(record)) yield return PhysicsTick;
        yield return Recovery();
    }

    private Vector2 ProjectileAimDirection(Vector2 origin, int facing)
    {
        // The aim line is locked at fire time and may be diagonal; the warning, the flight and the sweep share it.
        Vector2 direction = ((Vector2)owner.PlayerCollider.bounds.center - origin).normalized;
        return direction.sqrMagnitude < 0.01f ? Vector2.right * facing : direction;
    }

    /// <summary>
    /// Belief body action: windup, downward swing and recovery. The waves keep travelling in an executor-owned
    /// tracker after this returns, so releasing the body does not cut the belief's own hazard.
    /// </summary>
    public IEnumerator Shockwave()
    {
        Vector2 foot = new Vector2(owner.BodyCollider.bounds.center.x, owner.BodyCollider.bounds.min.y);
        if (!BossCombatPhysics.FindGround(foot, 0.15f, 2f, owner, out Vector2 ground)) yield break;
        var left = BossCombatPhysics.TraceGroundPath(ground, -1, Data.WaveDistance, owner);
        var right = BossCombatPhysics.TraceGroundPath(ground, 1, Data.WaveDistance, owner);
        var shapes = new List<AttackGeometry>(left.Warnings);
        shapes.AddRange(right.Warnings);
        var record = owner.CreateAttack(BossAttackKind.Shockwave, 0, shapes[0], Data.WaveDamage, BossBeliefKind.Shockwave, false);
        var warnings = new List<BossAttackVisual>();
        for (int i = 0; i < shapes.Count; i++)
        {
            if (i > 0) record.AddWarningGeometry(shapes[i]);
            warnings.Add(CreateWarning(shapes[i]));
        }
        owner.SetActionCue("지면 충격파", $"점프로 충격파를 넘으면 믿음 -{Data.BeliefReduction:0}", BossPresentationState.Windup, Data.Windup / owner.SpeedMultiplier);
        owner.Presentation.ShowDownAttack();
        float elapsed = 0f, duration = Data.Windup / owner.SpeedMultiplier;
        while (elapsed < duration && owner.IsAttackValid(record))
        {
            record.Observe(left.Threatens(owner.PlayerCollider) || right.Threatens(owner.PlayerCollider), owner.PlayerCollider.bounds.center);
            foreach (var warning in warnings) warning.SetProgress(elapsed / duration);
            yield return PhysicsTick;
            elapsed += Time.fixedDeltaTime;
        }
        foreach (var warning in warnings) owner.DestroyEffect(warning != null ? warning.gameObject : null);
        if (!owner.IsAttackValid(record)) yield break;
        record.Activate();
        owner.SetActionCue("지면 충격파", $"낮은 파동을 점프로 넘으면 믿음 -{Data.BeliefReduction:0}", BossPresentationState.Active, Data.WaveDistance / (Data.WaveSpeed * owner.SpeedMultiplier));
        if (owner.Presentation.ShowDownAttack(1f / (12f * owner.SpeedMultiplier)))
            owner.PlayerAttack.PlayDownAttackEffect(bladeEffect, owner.transform.position, owner.Facing);
        var first = owner.SpawnEffect(Data.WavePrefab);
        var second = owner.SpawnEffect(Data.WavePrefab);
        first.Initialize(owner, record, left);
        second.Initialize(owner, record, right);
        ShockwaveTracking++;
        StartCoroutine(TrackShockwave(record, first, second));
        yield return Recovery();
    }

    private IEnumerator TrackShockwave(BossAttackRecord record, BossGroundWave first, BossGroundWave second)
    {
        while (owner.IsAttackValid(record) && (!first.Finished || !second.Finished))
        {
            // A union observation prevents one branch from erasing the other's threat.
            bool threatened = first.Threatens(owner.PlayerCollider) || second.Threatens(owner.PlayerCollider);
            record.Observe(threatened, owner.PlayerCollider.bounds.center);
            yield return PhysicsTick;
        }
        owner.CompleteAttack(record, first.Blocked || second.Blocked ? BossAttackEndReason.Wall : BossAttackEndReason.Completed);
        owner.DestroyEffect(first != null ? first.gameObject : null);
        owner.DestroyEffect(second != null ? second.gameObject : null);
        ShockwaveTracking = Mathf.Max(0, ShockwaveTracking - 1);
    }

    private int OwnerComboCount => owner.PlayerAttack != null ? Mathf.Max(1, owner.PlayerAttack.ComboCount) : 1;

    private string AttackLabel(BossAttackRecord record) => record.Kind == BossAttackKind.Melee && record.HitIndex > 0 ?
        $"연속 베기 {record.HitIndex}/{OwnerComboCount}" : BossCombatController.AttackName(record.Kind);

    private string AttackHint(BossAttackRecord record)
    {
        if (record.BurnEnhanced) return $"피격 시 화상 · 이 공격을 회피하면 믿음 -{Data.BeliefReduction:0}";
        if (record.Context != null && record.Context.Dodge != null) return $"연결 공격을 회피하면 믿음 -{Data.BeliefReduction:0}";
        if (record.Kind == BossAttackKind.Projectile) return "조준 고정 · 표시된 직선에서 벗어나세요.";
        if (record.Kind == BossAttackKind.Dash) return "돌진 방향 고정 · 붉은 경로에서 벗어나세요.";
        return "붉은 예고 범위에서 벗어나세요.";
    }
}
