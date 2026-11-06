using UnityEngine;

/// <summary>
/// Floor zone laid by the executive: it stays for a while and slows and hurts whoever stands in it.
/// The zone is a box on the ground (circle width x zone height), the same box the warning column shows.
/// </summary>
public class MagicCircle : MonoBehaviour
{
    // Sheet: 6 frames of 50x56 px. The sigil's ellipse is 36 px wide, centered at (23.5, 7.5) from the frame's bottom-left.
    private const string SheetResource = "Effects/Executive/MagicCircleFloor";
    private const int FrameCount = 6, FrameWidth = 50, FrameHeight = 56, EllipseWidthPixels = 36;
    private const float FramesPerSecond = 14f, FadeOutSeconds = .5f, BaseRadius = 1.5f, FloorVisualLift = .18f;
    private static readonly Vector2 FramePivot = new Vector2(23.5f / FrameWidth, 7.5f / FrameHeight);
    private static Sprite[] frames;

    private SpriteRenderer sprite;
    private Collider2D target;
    private GameObject attacker;
    private Vector2 center, size;
    private float damage, slowMultiplier, slowLinger, damageInterval;
    private float endsAt, nextDamage, burstStartedAt;

    public Vector2 ZoneCenter => center;
    public Vector2 ZoneSize => size;
    public bool IsActive => Time.time < endsAt;

    /// <summary>
    /// Lays the zone with its floor point at the current position and applies the first tick immediately.
    /// Returns whether that first tick hurt or slowed the target.
    /// </summary>
    public bool Activate(
        float radius,
        float height,
        float damage,
        float slowMultiplier,
        float slowLinger,
        float damageInterval,
        float duration,
        Collider2D targetBodyCollider,
        out bool hpDamageApplied, GameObject attacker = null)
    {
        this.attacker = attacker;
        this.target = targetBodyCollider;
        this.damage = damage;
        this.slowMultiplier = slowMultiplier;
        this.slowLinger = slowLinger;
        this.damageInterval = Mathf.Max(.05f, damageInterval);
        Vector2 floor = transform.position;
        size = new Vector2(radius * 2f, height);
        center = floor + new Vector2(0f, height * .5f);
        endsAt = Time.time + Mathf.Max(.1f, duration);
        nextDamage = Time.time + this.damageInterval;

        sprite = GetComponent<SpriteRenderer>();
        if (sprite == null) sprite = gameObject.AddComponent<SpriteRenderer>();
        AtmosphereMaterials.ApplyUnlit(sprite);
        float scale = radius / BaseRadius;
        transform.localScale = new Vector3(scale, scale, 1f);
        // The sigil's ellipse sits just on the floor line; the hit box above still starts at the floor point.
        transform.position = floor + Vector2.up * FloorVisualLift;
        ShowBurst();
        Update();

        hpDamageApplied = false;
        return ApplyTick(true, out hpDamageApplied);
    }

    private void FixedUpdate()
    {
        if (Time.time >= endsAt) { Destroy(gameObject); return; }
        if (target == null || !IsTargetInside()) return;
        bool damageDue = Time.time >= nextDamage;
        ApplyTick(damageDue, out _);
        if (damageDue)
        {
            nextDamage = Time.time + damageInterval;
            ShowBurst();
        }
    }

    private void Update()
    {
        if (sprite == null) return;
        Sprite[] sheet = Frames();
        if (sheet == null || sheet.Length == 0) return;
        // A short flash plays when the zone appears and on every damage tick, then it rests on the base sigil.
        int frame = Mathf.FloorToInt((Time.time - burstStartedAt) * FramesPerSecond);
        sprite.sprite = frame >= 0 && frame < sheet.Length ? sheet[frame] : sheet[0];
        Color color = sprite.color;
        color.a = Mathf.Clamp01((endsAt - Time.time) / FadeOutSeconds);
        sprite.color = color;
    }

    private void ShowBurst() => burstStartedAt = Time.time;

    private bool IsTargetInside() => CombatPhysics.OverlapsBox(target, center, size);

    private bool ApplyTick(bool allowDamage, out bool hpDamageApplied)
    {
        hpDamageApplied = false;
        if (target == null || !target.enabled || !IsTargetInside()) return false;

        Transform playerRoot = target.attachedRigidbody != null ? target.attachedRigidbody.transform : target.transform.root;
        if (playerRoot == null) return false;

        bool harmful = false;
        if (allowDamage && damage > 0f)
        {
            IDamageable damageable = playerRoot.GetComponent<IDamageable>();
            if (damageable == null) damageable = playerRoot.GetComponentInChildren<IDamageable>();
            if (damageable != null)
            {
                hpDamageApplied = damageable.TakeDamage(new DamageInfo(damage, DamageSource.Enemy, attacker, "마법진"));
                harmful |= hpDamageApplied;
            }
        }

        ISlowable slowable = playerRoot.GetComponent<ISlowable>();
        if (slowable == null) slowable = playerRoot.GetComponentInChildren<ISlowable>();
        if (slowable != null && slowLinger > 0f && slowMultiplier < 1f)
        {
            // Refreshed every physics step while standing in the zone; it wears off shortly after leaving.
            slowable.ApplySlow(slowMultiplier, slowLinger);
            harmful = true;
        }
        return harmful;
    }

    private static Sprite[] Frames()
    {
        if (frames != null && frames.Length > 0 && frames[0] != null) return frames;
        Sprite sheet = Resources.Load<Sprite>(SheetResource);
        if (sheet == null) return null;
        Texture2D texture = sheet.texture;
        // 36 px of ellipse span 2 * BaseRadius, so the sprite scales with the circle radius.
        float pixelsPerUnit = EllipseWidthPixels / (BaseRadius * 2f);
        frames = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            frames[i] = Sprite.Create(texture, new Rect(i * FrameWidth, 0f, FrameWidth, FrameHeight), FramePivot,
                pixelsPerUnit, 0, SpriteMeshType.FullRect);
            frames[i].name = "MagicCircleFloor_" + i;
            frames[i].hideFlags = HideFlags.DontSave;
        }
        return frames;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => frames = null;

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireCube(center, size);
    }
}
