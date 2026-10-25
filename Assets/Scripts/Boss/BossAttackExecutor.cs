using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Attack lifecycles and presentation, independent of the pattern selection/state machine.</summary>
public sealed class BossAttackExecutor : MonoBehaviour
{
    private static readonly WaitForFixedUpdate PhysicsTick = new WaitForFixedUpdate();
    private BossCombatController owner;
    private int comboHits;
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
            int count = owner.ForcedComboHits == 2 || owner.ForcedComboHits == 3 ? owner.ForcedComboHits : Random.Range(2, 4);
            comboHits = count;
            for (int i = 0; i < count && owner.CanStartNextHit; i++)
                yield return Melee(i + 1, Data.Windup, Data.MeleeDamage, owner.RegularAttackBelief);
        }
        else if (kind == BossPatternKind.Dash) yield return Dash();
        else if (kind == BossPatternKind.Projectile) yield return Projectile();
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
        Vector2 direction = ((Vector2)owner.PlayerCollider.bounds.center - origin).normalized;
        if (direction.sqrMagnitude < 0.01f) direction = Vector2.right * owner.Facing;
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
        $"연속 베기 {record.HitIndex}/{comboHits}타" : BossCombatController.AttackName(record.Kind);

    private string AttackHint(BossAttackRecord record)
    {
        if (owner.IsBurning) return $"피격 시 화상 · 이 공격을 회피하면 믿음 -{Data.BeliefReduction:0}";
        if (record.Belief == BossBeliefKind.AttackAndDodge) return $"연결 공격을 회피하면 믿음 -{Data.BeliefReduction:0}";
        if (record.Kind == BossAttackKind.Projectile) return "조준 고정 · 표시된 직선에서 벗어나세요.";
        if (record.Kind == BossAttackKind.Dash) return "돌진 방향 고정 · 붉은 경로에서 벗어나세요.";
        if (owner.ActivePattern == BossPatternKind.ShockwaveBelief) return "베기를 피한 뒤, 지면 충격파를 점프로 넘으세요.";
        if (owner.ActivePattern == BossPatternKind.NoAttackBelief) return "응시 종료 · 곧 베기! 붉은 범위에서 벗어나세요.";
        return "붉은 범위에서 벗어나세요 · 콤보는 매 타격 전 다시 조준합니다.";
    }
}
