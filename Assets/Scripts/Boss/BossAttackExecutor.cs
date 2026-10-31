using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Attack lifecycles and presentation, independent of the pattern selection/state machine.</summary>
public sealed class BossAttackExecutor : MonoBehaviour
{
    private static readonly WaitForFixedUpdate PhysicsTick = new WaitForFixedUpdate();
    private BossCombatController owner;
    private CombatAfterimageEmitter afterimages;
    private BossCombatSettings Data => owner.Settings;
    public void Initialize(BossCombatController boss)
    {
        owner = boss;
        afterimages = GetComponent<CombatAfterimageEmitter>();
        if (afterimages == null) afterimages = gameObject.AddComponent<CombatAfterimageEmitter>();
    }
    public void ClearAfterimages() => afterimages?.Clear();

    public AttackGeometry MeleeGeometry() => new AttackGeometry(
        owner.AttackOrigin + Vector2.right * owner.Facing * Data.MeleeSize.x * 0.5f, Data.MeleeSize);

    public BossAttackVisual CreateWarning(AttackGeometry geometry, bool followOwner = false)
    {
        var warning = owner.SpawnEffect(Data.WarningPrefab);
        warning.Show(geometry, Data.WarningOrder, followTarget: followOwner ? owner.transform : null);
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
        if (record.Kind == BossAttackKind.Dash) warning.SetDirection(owner.Facing);
        else if (record.Kind == BossAttackKind.Projectile) warning.SetDirection();
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

    public IEnumerator Regular(BossPatternKind kind)
    {
        if (!owner.CanStartNextHit) yield break;
        owner.RecordRegularUse(kind);
        if (kind == BossPatternKind.MeleeCombo)
        {
            yield return MeleeCombo();
        }
        else if (kind == BossPatternKind.Dash) yield return Dash();
        else if (kind == BossPatternKind.Projectile) yield return Projectile();
    }

    private IEnumerator MeleeCombo()
    {
        if (owner.PlayerAttack == null || owner.PlayerAttack.ComboCount < 4)
        {
            owner.ShowFeedback("연속 베기 중지 · 플레이어 4타 콤보 데이터 없음", new Color(1f, .65f, .45f));
            yield break;
        }

        owner.FacePlayer();
        int direction = owner.Facing;
        for (int index = 0; index < 4 && owner.CanStartNextHit; index++)
        {
            if (!owner.PlayerAttack.TryGetComboStep(index, out PlayerAttack.ComboStep step))
                yield break;

            float warning = index == 0 ? Data.MeleeComboFirstWarning : Data.MeleeComboFollowupWarning;
            // The player's comboWindow accepts a buffered click; this sequence advances automatically after recovery.
            yield return MeleeComboStep(index + 1, step, direction, Data.MeleeComboDamage[index],
                warning / owner.SpeedMultiplier, owner.RegularAttackBelief);
        }
    }

    private IEnumerator MeleeComboStep(int hitIndex, PlayerAttack.ComboStep step, int direction,
        float damage, float warningDuration, BossBeliefKind belief)
    {
        if (!owner.CanStartNextHit) yield break;

        Vector2 attackDirection = Vector2.right * direction;
        var geometry = ComboGeometry(step, direction);
        var record = owner.CreateAttack(BossAttackKind.Melee, hitIndex, geometry, damage, belief, true);
        var warning = CreateWarning(geometry, followOwner: true);
        string label = $"연속 베기 {hitIndex}/4타";
        string hint = hitIndex == 4
            ? "올려베기 피니시 · 적중 시 넉백과 띄우기"
            : "플레이어의 4타 연계 · 표시된 범위에서 벗어나세요.";
        if (owner.IsBurning) hint += " · 피격 시 화상";
        else if (belief == BossBeliefKind.AttackAndDodge) hint += $" · 회피하면 믿음 -{Data.BeliefReduction:0}";

        owner.SetActionCue(label, hint, BossPresentationState.Windup, warningDuration);
        float elapsed = 0f;
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

        while (elapsed < warningDuration && owner.IsAttackValid(record) && owner.CanStartNextHit)
        {
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
                    Vector2 offset = attackDirection * clear;
                    owner.Body.MovePosition(owner.Body.position + offset);
                    record.TranslateWarning(offset);
                    lungeDistance -= clear;
                }
                lungeBlocked = blocked || clear + 0.001f < requested;
            }

            owner.Observe(record, record.Geometry);
            warning.SetProgress(elapsed / warningDuration);
            warning.SetCue(label, warningDuration - elapsed, elapsed / warningDuration, owner);
            yield return PhysicsTick;
            elapsed += Time.fixedDeltaTime;
        }

        if (owner.IsAttackValid(record)) owner.Observe(record, record.Geometry);
        owner.DestroyEffect(warning != null ? warning.gameObject : null);
        if (owner.IsAttackValid(record) && !owner.CanStartNextHit)
            owner.CompleteAttack(record, BossAttackEndReason.Cancelled);
        if (!owner.IsAttackValid(record)) yield break;

        record.Activate();
        float activeDuration = Data.MeleeActiveTime / owner.SpeedMultiplier;
        owner.SetActionCue(label, hint, BossPresentationState.Active, activeDuration);
        var slash = owner.SpawnEffect(Data.SlashPrefab);
        slash.Show(record.Geometry, Data.AttackOrder, owner.IsBurning);
        elapsed = 0f;
        while (elapsed < activeDuration && owner.IsAttackValid(record))
        {
            if (owner.Observe(record, record.Geometry) && owner.TryHit(record) && hitIndex == 4)
                ApplyFinisherKnockback(step, direction);
            yield return PhysicsTick;
            elapsed += Time.fixedDeltaTime;
        }

        owner.CompleteAttack(record, BossAttackEndReason.Completed);
        if (slash != null) Fade(slash, 0.12f);

        float recovery = step.recoveryTime / owner.SpeedMultiplier;
        if (recovery > 0f && owner.CanContinue)
        {
            owner.SetActionCue("연속 베기 연결", "다음 타격을 준비합니다.", BossPresentationState.Recovery, recovery);
            yield return new WaitForSeconds(recovery);
        }
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

    public IEnumerator Melee(int hitIndex, float windup, float damage, BossBeliefKind belief, bool facePlayer = true)
    {
        if (!owner.CanStartNextHit) yield break;
        if (facePlayer) owner.FacePlayer();
        var record = owner.CreateAttack(BossAttackKind.Melee, hitIndex, MeleeGeometry(), damage, belief, true);
        yield return Windup(record, windup / owner.SpeedMultiplier);
        if (!owner.IsAttackValid(record)) yield break;
        record.Activate();
        owner.SetActionCue(AttackLabel(record), AttackHint(record), BossPresentationState.Active, Data.MeleeActiveTime / owner.SpeedMultiplier);
        var slash = owner.SpawnEffect(Data.SlashPrefab);
        slash.Show(record.Geometry, Data.AttackOrder, owner.IsBurning);
        float elapsed = 0f;
        while (elapsed < Data.MeleeActiveTime / owner.SpeedMultiplier && owner.IsAttackValid(record))
        {
            if (owner.Observe(record, record.Geometry)) owner.TryHit(record);
            yield return PhysicsTick;
            elapsed += Time.fixedDeltaTime;
        }
        // Disable logical damage before leaving a harmless afterimage.
        owner.CompleteAttack(record, BossAttackEndReason.Completed);
        if (slash != null) Fade(slash, 0.12f);
        yield return Recovery();
    }

    private IEnumerator Dash()
    {
        owner.FacePlayer();
        Vector2 direction = Vector2.right * owner.Facing;
        Vector2 size = owner.BodyCollider.bounds.size;
        Vector2 start = owner.BodyCollider.bounds.center;
        var corridor = new AttackGeometry(start + direction * Data.DashDistance * 0.5f,
            new Vector2(Data.DashDistance + size.x, size.y));
        var record = owner.CreateAttack(BossAttackKind.Dash, 0, corridor, Data.DashDamage, owner.RegularAttackBelief, true);
        float speed = Data.DashSpeed * owner.SpeedMultiplier;
        yield return Windup(record, Data.Windup / owner.SpeedMultiplier);
        if (!owner.IsAttackValid(record)) yield break;
        record.Activate();
        owner.SetActionCue("돌진", AttackHint(record), BossPresentationState.Active, Data.DashDistance / speed);
        float remaining = Data.DashDistance;
        bool blocked = false;
        while (remaining > 0.001f && owner.IsAttackValid(record))
        {
            Vector2 center = owner.BodyCollider.bounds.center;
            float step = Mathf.Min(remaining, speed * Time.fixedDeltaTime);
            step = BossCombatPhysics.ClearDistance(center, size, direction, step, owner, false, out blocked);
            var ahead = new AttackGeometry(center + direction * remaining * 0.5f, new Vector2(remaining + size.x, size.y));
            owner.Observe(record, ahead);
            if (BossCombatPhysics.SweptTarget(center, size, direction, step, owner, false)) owner.TryHit(record);
            Color? tint = owner.IsBurning ? (Color?)new Color(1f, 0.43f, 0.12f, 0.9f) : null;
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

    private IEnumerator Projectile()
    {
        owner.FacePlayer();
        Vector2 origin = owner.ProjectileOrigin;
        Vector2 direction = ProjectileAimDirection(origin);
        float speed = Data.ProjectileSpeed * owner.SpeedMultiplier;
        float lifetime = Data.ProjectileLifetime / owner.SpeedMultiplier;
        float maxDistance = Mathf.Min(Data.ProjectileDistance, speed * lifetime);
        float length = BossCombatPhysics.ClearDistance(origin, Vector2.one * Data.ProjectileDiameter, direction,
            maxDistance, owner, true, out bool endBlocked);
        // Include the projectile's cap so the visible warning encloses all swept damage.
        var geometry = AttackGeometry.Corridor(origin - direction * Data.ProjectileDiameter * 0.5f,
            direction, length + Data.ProjectileDiameter, Data.ProjectileDiameter);
        var record = owner.CreateAttack(BossAttackKind.Projectile, 0, geometry, Data.ProjectileDamage, owner.RegularAttackBelief, true);
        yield return Windup(record, Data.Windup / owner.SpeedMultiplier);
        if (!owner.IsAttackValid(record)) yield break;
        origin = owner.ProjectileOrigin;
        length = BossCombatPhysics.ClearDistance(origin, Vector2.one * Data.ProjectileDiameter, direction,
            maxDistance, owner, true, out endBlocked);
        record.Activate();
        owner.SetActionCue("투사체", AttackHint(record), BossPresentationState.Active, lifetime);
        var projectile = owner.SpawnEffect(Data.ProjectilePrefab);
        projectile.Initialize(owner, record, origin, direction, speed, length, lifetime, endBlocked);
        while (owner.IsAttackValid(record)) yield return PhysicsTick;
        yield return Recovery();
    }

    private Vector2 ProjectileAimDirection(Vector2 origin)
    {
        Bounds target = owner.PlayerCollider.bounds;
        // Boss and player occupy the same combat plane. Aim at the player's horizontal
        // position while preserving the muzzle height so distance cannot change the slope.
        Vector2 aimPoint = new Vector2(target.center.x, origin.y);
        Vector2 direction = (aimPoint - origin).normalized;
        return direction.sqrMagnitude < 0.01f ? Vector2.right * owner.Facing : direction;
    }

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
        var first = owner.SpawnEffect(Data.WavePrefab);
        var second = owner.SpawnEffect(Data.WavePrefab);
        first.Initialize(owner, record, left);
        second.Initialize(owner, record, right);
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
        yield return Recovery();
    }

    private string AttackLabel(BossAttackRecord record) => record.Kind == BossAttackKind.Melee && record.HitIndex > 0 ?
        $"연속 베기 {record.HitIndex}/4타" : BossCombatController.AttackName(record.Kind);

    private string AttackHint(BossAttackRecord record)
    {
        if (owner.IsBurning) return $"피격 시 화상 · 이 공격을 회피하면 믿음 -{Data.BeliefReduction:0}";
        if (record.Belief == BossBeliefKind.AttackAndDodge) return $"연결 공격을 회피하면 믿음 -{Data.BeliefReduction:0}";
        if (record.Kind == BossAttackKind.Projectile) return "조준 고정 · 표시된 직선에서 벗어나세요.";
        if (record.Kind == BossAttackKind.Dash) return "돌진 방향 고정 · 붉은 경로에서 벗어나세요.";
        if (owner.ActivePattern == BossPatternKind.ShockwaveBelief) return "베기를 피한 뒤, 지면 충격파를 점프로 넘으세요.";
        if (owner.ActivePattern == BossPatternKind.NoAttackBelief) return "응시 종료 · 곧 베기! 붉은 범위에서 벗어나세요.";
        return "붉은 예고 범위에서 벗어나세요.";
    }
}
