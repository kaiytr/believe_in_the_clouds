using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One lightning activation over the persistent clouds: a warning that follows the moving clouds, a single
/// deadline strike, then a short bolt visual. The warning and the damage test share the field's danger
/// rectangles, and one record guarantees at most one hit / one belief outcome per strike.
/// </summary>
public sealed class BossCloudLightning : MonoBehaviour
{
    private BossCombatController owner;
    private BossCloudField field;
    private BossAttackRecord record;
    private readonly List<BossAttackVisual> warnings = new List<BossAttackVisual>();
    private readonly List<AttackGeometry> rects = new List<AttackGeometry>();
    private float startedAt, deadline, visualEndsAt;
    private bool struck;

    public bool HasStruck => struck;
    public float Remaining => struck ? 0f : Mathf.Max(0f, deadline - Time.time);
    public BossAttackRecord Record => record;
    public IReadOnlyList<AttackGeometry> DangerRects => rects;
    /// <summary>Compatibility: shelter under the first white cloud (all shelters: <see cref="BossCloudField.WhiteClouds"/>).</summary>
    public AttackGeometry Shelter =>
        field != null && field.WhiteClouds.Count > 0 ? field.ShelterOf(field.WhiteClouds[0]) : default;

    // The entire real body must fit beneath the white cloud;
    // tolerance only absorbs floating point round-off, not a gameplay margin.
    public static bool Contains(AttackGeometry shelter, Collider2D body)
    {
        if (body == null || !body.enabled || !body.gameObject.activeInHierarchy) return false;
        Bounds bounds = body.bounds;
        Vector2 min = shelter.Center - shelter.Size * .5f;
        Vector2 max = shelter.Center + shelter.Size * .5f;
        const float epsilon = .0001f;
        return bounds.min.x >= min.x - epsilon && bounds.max.x <= max.x + epsilon &&
            bounds.min.y >= min.y - epsilon && bounds.max.y <= max.y + epsilon;
    }
    public bool ContainsPlayer() => owner != null && field != null && field.IsInsideShelter(owner.PlayerCollider);

    public bool Initialize(BossCombatController boss, BossCloudField cloudField)
    {
        owner = boss; field = cloudField;
        field.ComputeDangerRects(rects);
        if (rects.Count == 0 && field.StormClouds.Count == 0) return false;
        startedAt = Time.time;
        deadline = startedAt + boss.Settings.CloudDuration;
        // The record carries a single bounding shape for diagnostics; hit tests use the live rectangles.
        record = boss.CreateAttack(BossAttackKind.Lightning, 0, Bounding(rects), boss.Settings.LightningDamage, BossBeliefKind.Cloud, false, null, observeNow: false);
        SyncWarnings();
        return true;
    }

    private static AttackGeometry Bounding(List<AttackGeometry> shapes)
    {
        if (shapes.Count == 0) return new AttackGeometry(Vector2.zero, Vector2.one);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (AttackGeometry shape in shapes)
        {
            min = Vector2.Min(min, shape.Center - shape.Size * .5f);
            max = Vector2.Max(max, shape.Center + shape.Size * .5f);
        }
        return new AttackGeometry((min + max) * .5f, max - min);
    }

    private bool OverlapsDanger(Collider2D body)
    {
        if (body == null) return false;
        foreach (AttackGeometry rect in rects) if (rect.Overlaps(body)) return true;
        return false;
    }

    private void FixedUpdate()
    {
        if (owner == null || field == null) return;
        if (struck)
        {
            if (Time.time >= visualEndsAt) End();
            return;
        }
        if (!owner.IsAttackValid(record)) { End(); return; }
        field.ComputeDangerRects(rects);
        Collider2D body = owner.PlayerCollider;
        record.Observe(OverlapsDanger(body), body.bounds.center);
        if (Time.time >= deadline) Strike(body);
    }

    private void LateUpdate()
    {
        if (struck || owner == null || field == null) return;
        field.ComputeDangerRects(rects);
        SyncWarnings();
    }

    private void SyncWarnings()
    {
        BossAttackExecutor attacks = owner.GetComponent<BossAttackExecutor>();
        while (warnings.Count < rects.Count) warnings.Add(attacks.CreateWarning(rects[warnings.Count]));
        float progress = 1f - Mathf.Clamp01(Remaining / Mathf.Max(.0001f, owner.Settings.CloudDuration));
        for (int i = 0; i < warnings.Count; i++)
        {
            if (i >= rects.Count) { if (warnings[i] != null) warnings[i].gameObject.SetActive(false); continue; }
            if (warnings[i] == null) warnings[i] = attacks.CreateWarning(rects[i]);
            warnings[i].gameObject.SetActive(true);
            warnings[i].Show(rects[i], owner.Settings.WarningOrder);
            warnings[i].SetProgress(progress);
        }
    }

    private void Strike(Collider2D body)
    {
        // Sync at the deadline: a cloud moved this step must use its actual current collider.
        Physics2D.SyncTransforms();
        field.ComputeDangerRects(rects);
        bool exposed = OverlapsDanger(body);
        bool sheltered = !exposed && field.OverlapsStormRange(body) && field.IsInsideShelter(body);
        struck = true;
        visualEndsAt = Time.time + owner.Settings.LightningDuration;
        record.Activate();
        // DestroyEffect disables immediately: no red warning remains on the strike frame.
        ClearWarnings();
        DrawBolts();
        if (exposed) owner.TryHit(record);
        else if (sheltered) record.ProtectFromLightning();
        owner.CompleteAttack(record, BossAttackEndReason.Completed);
    }

    private void DrawBolts()
    {
        BossCombatSettings data = owner.Settings;
        foreach (BossCloudPlatform storm in field.StormClouds)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(storm.Size.x / data.LightningSpacing));
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(storm.Left, storm.Right, (i + .5f) / count);
                BossCloudPlatform cover = CoveringWhite(x);
                float endY = cover != null ? cover.Top : field.FloorY;
                var strike = new GameObject("Pixel lightning strike").AddComponent<BossLightningStrikeVisual>();
                strike.transform.SetParent(transform, false);
                strike.Initialize(new Vector2(x, storm.Bottom), new Vector2(x, endY),
                    data.EffectMaterial, data.AttackOrder + 1, data.LightningWidth, data.LightningDuration);
            }
        }
    }

    private BossCloudPlatform CoveringWhite(float x)
    {
        // 낙뢰의 폭과 계단 모양까지 고려해 보호 구름 아래로 시각 효과가 새지 않게 한다.
        foreach (BossCloudPlatform white in field.WhiteClouds)
            if (x >= white.Left - .65f && x <= white.Right + .65f) return white;
        return null;
    }
    private void End()
    {
        ClearWarnings();
        if (field != null) field.LightningEnded(this);
        Destroy(gameObject);
    }

    private void OnDisable() => ClearWarnings();
    private void OnDestroy() { if (field != null) field.LightningEnded(this); }

    private void ClearWarnings()
    {
        foreach (var warning in warnings) if (warning != null && owner != null) owner.DestroyEffect(warning.gameObject);
        warnings.Clear();
    }
}
