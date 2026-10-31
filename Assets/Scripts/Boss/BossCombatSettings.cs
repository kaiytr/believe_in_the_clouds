using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Boss settings")]
public sealed class BossCombatSettings : ScriptableObject
{
    [Header("Health / shared belief")]
    public float MaxHp = 500f;
    [Range(0.1f, 0.9f)] public float PhaseTwoHealthRatio = 0.5f;
    public float MaxBelief = 100f;
    public float BeliefReduction = 20f;
    public float VulnerableDuration = 5f;
    [Range(0.01f, 1f)] public float BeliefRecoveryRatio = 1f;
    public float MaintainedDamageMultiplier = 0.6f;
    public float VulnerableDamageMultiplier = 2f;
    public BossAttackSignalMode AttackSignalMode = BossAttackSignalMode.Attempt;

    [Header("Selection / timing")]
    public BossPatternWeight[] PhaseOnePool = DefaultPool(false);
    public BossPatternWeight[] PhaseTwoPool = DefaultPool(true);
    public float PatternRecovery = 0.65f;
    public float Windup = 0.65f;
    public float Recovery = 0.45f;
    public float PhaseTwoSpeedMultiplier = 1f;
    public float PhaseTwoDamageMultiplier = 1f;
    public float StareDuration = 3f;
    public float SurpriseWindup = 0.18f;

    [Header("Melee / dash / projectile")]
    public Vector2 MeleeSize = new Vector2(2.6f, 3f);
    public float MeleeActiveTime = 0.12f;
    public float MeleeDamage = 14f;
    public float[] MeleeComboDamage = { 6f, 7f, 8f, 14f };
    public float MeleeComboFirstWarning = 0.65f;
    public float MeleeComboFollowupWarning = 0.35f;
    public float DashDistance = 4.5f;
    public float DashSpeed = 10f;
    public float DashDamage = 18f;
    public float ProjectileDiameter = 0.38f;
    public float ProjectileSpeed = 8f;
    public float ProjectileDistance = 24f;
    public float ProjectileLifetime = 3f;
    public float ProjectileDamage = 12f;

    [Header("Cloud / spike / ground waves")]
    public float CloudWarning = 0.5f;
    public float CloudDuration = 4f;
    public float CloudTopHeight = 1.5f;
    public float CloudSpacing = 2f;
    public Vector2 CloudSize = new Vector2(2.8f, 0.7f);
    public float SpikeWarning = 3f;
    public float SpikeDuration = 2f;
    public Vector2 SpikeSize = new Vector2(1.25f, 0.8f);
    public float SpikeDamage = 18f;
    public float WaveDistance = 7f;
    public float WaveSpeed = 6f;
    public Vector2 WaveSize = new Vector2(0.6f, 0.6f);
    public float WaveDamage = 16f;
    public float MaximumGroundStep = 0.3f;
    public LayerMask GroundMask = 1 << 6;
    public LayerMask ObstacleMask = 1 << 6;
    public int PlatformLayer = 6;
    public Vector2 ArenaX = new Vector2(-17f, 17f);

    [Header("Burn")]
    public float BurnPreparation = 5f;
    public float BurnWindow = 10f;
    public float BurnDuration = 5f;
    public float BurnTickInterval = 1f;
    public float BurnTickDamage = 2f;

    [Header("Required presentation assets (world-unit sprites)")]
    public BossAttackVisual WarningPrefab;
    public BossAttackVisual SlashPrefab;
    public CombatAfterimage DashPrefab;
    public BossAttackVisual ImpactPrefab;
    public BossAttackProjectile ProjectilePrefab;
    public BossGroundWave WavePrefab;
    public BossCloudPlatform WhiteCloudPrefab;
    public BossCloudPlatform BlackCloudPrefab;
    public BossTimedDamageHazard SpikePrefab;
    public Sprite BurnSprite;
    public Material EffectMaterial;
    public int WarningOrder = 20;
    public int AttackOrder = 24;

    public static BossPatternWeight[] DefaultPool(bool phaseTwo)
    {
        return new[] {
            new BossPatternWeight(BossPatternKind.MeleeCombo, 1f, 0.5f),
            new BossPatternWeight(BossPatternKind.Dash, 1f, 0.5f),
            new BossPatternWeight(BossPatternKind.Projectile, 1f, 0.5f),
            new BossPatternWeight(phaseTwo ? BossPatternKind.AttackAndDodgeBelief : BossPatternKind.AttackIntentBelief, 1f, 0.5f),
            new BossPatternWeight(phaseTwo ? BossPatternKind.NoAttackBelief : BossPatternKind.CloudBelief, 1f, 0.5f),
            new BossPatternWeight(phaseTwo ? BossPatternKind.ShockwaveBelief : BossPatternKind.BurnBelief, 1f, 0.5f)
        };
    }

    public List<string> ValidateSettings()
    {
        var errors = new List<string>();
        if (MaxHp <= 0f || MaxBelief <= 0f || BeliefReduction <= 0f || VulnerableDuration <= 0f || BeliefRecoveryRatio <= 0f)
            errors.Add("Health/shared belief: positive HP, belief, reduction, vulnerability and recovery are required.");
        if (PhaseTwoHealthRatio <= 0f || PhaseTwoHealthRatio >= 1f || MaintainedDamageMultiplier < 0f || VulnerableDamageMultiplier < 0f)
            errors.Add("Health/phase: invalid phase threshold or incoming damage multiplier.");
        if (Windup <= 0f || Recovery < 0f || PatternRecovery < 0f || MeleeActiveTime <= 0f || SurpriseWindup <= 0f || StareDuration <= 0f ||
            PhaseTwoSpeedMultiplier <= 0f || PhaseTwoDamageMultiplier < 0f ||
            MeleeComboFirstWarning <= 0f || MeleeComboFollowupWarning <= 0f)
            errors.Add("Attack timing: windup/active/speed must be positive and recovery/damage multipliers non-negative.");
        if (MeleeComboDamage == null || MeleeComboDamage.Length != 4)
            errors.Add("Melee combo: exactly four damage values are required.");
        else
            for (int i = 0; i < MeleeComboDamage.Length; i++)
                if (MeleeComboDamage[i] < 0f)
                    errors.Add("Melee combo: damage values must be non-negative.");
        if (!Positive(MeleeSize) || !Positive(CloudSize) || !Positive(SpikeSize) || !Positive(WaveSize) ||
            DashDistance <= 0f || DashSpeed <= 0f || ProjectileDiameter <= 0f || ProjectileSpeed <= 0f ||
            ProjectileDistance <= 0f || ProjectileLifetime <= 0f || WaveSpeed <= 0f || WaveDistance <= 0f)
            errors.Add("Attack geometry/travel: every size, speed, distance and projectile lifetime must be positive.");
        if (CloudWarning <= 0f || CloudDuration <= 0f || CloudTopHeight <= 0f || CloudSpacing <= 0f || SpikeWarning <= 0f || SpikeDuration <= 0f ||
            BurnPreparation <= 0f || BurnWindow <= 0f || BurnDuration <= 0f || BurnTickInterval <= 0f || BurnTickDamage < 0f ||
            MeleeDamage < 0f || DashDamage < 0f || ProjectileDamage < 0f || SpikeDamage < 0f || WaveDamage < 0f)
            errors.Add("Belief hazards: invalid duration, placement or damage.");
        if (GroundMask.value == 0 || ObstacleMask.value == 0 || PlatformLayer < 0 || PlatformLayer > 31 ||
            (GroundMask.value & (1 << PlatformLayer)) == 0 || ArenaX.x >= ArenaX.y || MaximumGroundStep <= 0f)
            errors.Add("Ground/platform: masks, platform layer, step and arena bounds must be configured.");
        Require(WarningPrefab, nameof(WarningPrefab), errors);
        Require(SlashPrefab, nameof(SlashPrefab), errors);
        Require(DashPrefab, nameof(DashPrefab), errors);
        Require(ImpactPrefab, nameof(ImpactPrefab), errors);
        Require(ProjectilePrefab, nameof(ProjectilePrefab), errors);
        Require(WavePrefab, nameof(WavePrefab), errors);
        Require(WhiteCloudPrefab, nameof(WhiteCloudPrefab), errors);
        Require(BlackCloudPrefab, nameof(BlackCloudPrefab), errors);
        Require(SpikePrefab, nameof(SpikePrefab), errors);
        Require(BurnSprite, nameof(BurnSprite), errors);
        Require(EffectMaterial, nameof(EffectMaterial), errors);
        foreach (Component effect in new Component[] { WarningPrefab, SlashPrefab, DashPrefab, ImpactPrefab, ProjectilePrefab,
            WavePrefab, SpikePrefab, WhiteCloudPrefab, BlackCloudPrefab })
        {
            if (effect == null) continue;
            SpriteRenderer renderer = effect.GetComponent<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null || renderer.sharedMaterial == null)
                errors.Add("Missing sprite/material: " + effect.name);
            else if (Vector2.Distance(renderer.sprite.bounds.size, Vector2.one) > .005f || effect.transform.localScale != Vector3.one)
                errors.Add("Effect requires a unit sprite and unit root scale: " + effect.name);
        }
        if (WarningPrefab != null && WarningPrefab.GetComponent<DangerIndicator>() == null)
            errors.Add("WarningPrefab requires the shared DangerIndicator component.");
        if (WhiteCloudPrefab != null)
        {
            var surface = WhiteCloudPrefab.GetComponent<BoxCollider2D>();
            var effector = WhiteCloudPrefab.GetComponent<PlatformEffector2D>();
            if (surface == null || surface.isTrigger || !surface.usedByEffector || effector == null || !effector.useOneWay)
                errors.Add("WhiteCloudPrefab requires a one-way PlatformEffector2D surface.");
        }
        if (BlackCloudPrefab != null)
        {
            var surface = BlackCloudPrefab.GetComponent<BoxCollider2D>();
            if (surface == null || !surface.isTrigger) errors.Add("BlackCloudPrefab requires a pass-through trigger.");
        }
        ValidatePool(PhaseOnePool, BossPhase.One, "PhaseOnePool", errors);
        ValidatePool(PhaseTwoPool, BossPhase.Two, "PhaseTwoPool", errors);
        return errors;
    }

    private static bool Positive(Vector2 value) => value.x > 0f && value.y > 0f;
    private static void Require(Object value, string field, List<string> errors)
    {
        if (value == null) errors.Add("Missing effect reference: " + field);
    }

    private static void ValidatePool(BossPatternWeight[] pool, BossPhase phase, string name, List<string> errors)
    {
        bool hasRegular = false, hasAny = false;
        var seen = new HashSet<BossPatternKind>();
        if (pool != null)
            foreach (BossPatternWeight entry in pool)
            {
                if (entry == null || entry.BaseWeight <= 0f) continue;
                if (!BossPatternSelection.AllowedInPhase(entry.Pattern, phase) || !seen.Add(entry.Pattern))
                    errors.Add(name + ": invalid phase or duplicate entry " + entry.Pattern);
                hasAny = true;
                hasRegular |= BossPatternSelection.IsRegular(entry.Pattern);
            }
        if (!hasAny || !hasRegular) errors.Add(name + ": requires usable patterns and at least one regular attack.");
    }
}

public static class BossPatternSelection
{
    public static bool IsRegular(BossPatternKind pattern) => pattern >= BossPatternKind.MeleeCombo && pattern <= BossPatternKind.Projectile;
    public static bool AllowedInPhase(BossPatternKind pattern, BossPhase phase) => IsRegular(pattern) ||
        (phase == BossPhase.One ? pattern >= BossPatternKind.AttackIntentBelief && pattern <= BossPatternKind.BurnBelief :
        pattern >= BossPatternKind.AttackAndDodgeBelief && pattern <= BossPatternKind.ShockwaveBelief);

    public static bool TryChoose(BossPatternWeight[] pool, IDictionary<BossPatternKind, int> uses,
        System.Predicate<BossPatternKind> eligible, float roll, out BossPatternKind result, List<float> weights = null)
    {
        result = default;
        if (pool == null) return false;
        if (weights == null) weights = new List<float>(pool.Length);
        weights.Clear();
        float total = 0f;
        foreach (var entry in pool)
        {
            float weight = entry != null && entry.BaseWeight > 0f && eligible(entry.Pattern) ?
                entry.EffectiveWeight(uses.TryGetValue(entry.Pattern, out int count) ? count : 0) : 0f;
            weights.Add(weight); total += weight;
        }
        if (total <= 0f) return false;
        float cursor = Mathf.Clamp01(roll) * total;
        for (int i = 0; i < pool.Length; i++)
        {
            if (weights[i] <= 0f) continue;
            result = pool[i].Pattern;
            cursor -= weights[i];
            if (cursor < 0f) return true;
        }
        return true;
    }
}
