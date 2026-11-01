using System.Collections.Generic;
using UnityEngine;

/// <summary>A single deadline and strike. No trigger callbacks or delayed damage.</summary>
public sealed class BossCloudLightning : MonoBehaviour
{
    private BossCombatController owner;
    private BossAttackRecord record;
    private readonly List<BossAttackVisual> warnings = new List<BossAttackVisual>();
    private bool struck;
    public AttackGeometry Shelter { get; private set; }
    public bool HasStruck => struck;

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
    public bool ContainsPlayer() => owner != null && Contains(Shelter, owner.PlayerCollider);

    public void Initialize(BossCombatController boss, BossAttackRecord attack, AttackGeometry shelter)
    {
        owner = boss; record = attack; Shelter = shelter;
        Vector2 min = attack.Geometry.Center - attack.Geometry.Size * .5f;
        Vector2 max = attack.Geometry.Center + attack.Geometry.Size * .5f;
        Vector2 safeMin = shelter.Center - shelter.Size * .5f;
        Vector2 safeMax = shelter.Center + shelter.Size * .5f;
        AddDanger(new Vector2(min.x, min.y), new Vector2(safeMin.x, max.y));
        AddDanger(new Vector2(safeMax.x, min.y), max);
        AddDanger(new Vector2(safeMin.x, safeMax.y), new Vector2(safeMax.x, max.y));
    }
    private void AddDanger(Vector2 min, Vector2 max)
    {
        if (max.x <= min.x || max.y <= min.y) return;
        var warning = owner.GetComponent<BossAttackExecutor>().CreateWarning(new AttackGeometry((min + max) * .5f, max - min));
        warnings.Add(warning);
    }
    private GameObject Child(string label)
    {
        var child = new GameObject(label); child.transform.SetParent(transform, false); return child;
    }
    private LineRenderer Line(string label, Color color, float width)
    {
        var line = Child(label).AddComponent<LineRenderer>();
        line.useWorldSpace = true; line.sharedMaterial = owner.Settings.EffectMaterial;
        line.startWidth = line.endWidth = width; line.startColor = line.endColor = color;
        line.sortingOrder = owner.Settings.AttackOrder + 1;
        return line;
    }
    public void UpdateWarning(float remaining)
    {
        if (struck) return;
        float progress = 1f - Mathf.Clamp01(remaining / (owner.Settings.CloudWarning + owner.Settings.CloudDuration));
        foreach (var warning in warnings) warning.SetProgress(progress);
    }
    public void Strike()
    {
        if (struck || owner == null || !owner.IsAttackValid(record)) return;
        // DestroyEffect disables immediately: no red warning remains on the strike frame.
        ClearWarnings();
        struck = true;
        var data = owner.Settings;
        Vector2 min = record.Geometry.Center - record.Geometry.Size * .5f;
        Vector2 max = record.Geometry.Center + record.Geometry.Size * .5f;
        float safeLeft = Shelter.Center.x - Shelter.Size.x * .5f;
        float safeRight = Shelter.Center.x + Shelter.Size.x * .5f;
        int count = Mathf.Max(2, Mathf.CeilToInt(record.Geometry.Size.x / data.LightningSpacing));
        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(min.x + .08f, max.x - .08f, (i + .5f) / count);
            bool shieldedColumn = x >= safeLeft && x <= safeRight;
            float endY = shieldedColumn ? Shelter.Center.y + Shelter.Size.y * .5f + data.CloudSize.y : min.y;
            var line = Line("Downward lightning", new Color(1f,.94f,.55f), data.LightningWidth);
            line.positionCount = 9;
            for (int j = 0; j < 9; j++)
            {
                float jitter = j == 0 || j == 8 ? 0f : (j % 2 == 0 ? -.12f : .12f);
                float boltX = Mathf.Clamp(x + jitter, min.x, max.x);
                if (!shieldedColumn) boltX = x < safeLeft ? Mathf.Min(boltX, safeLeft - .01f) : Mathf.Max(boltX, safeRight + .01f);
                line.SetPosition(j, new Vector3(boltX, Mathf.Lerp(max.y, endY, j / 8f), 0f));
            }
        }
    }
    private void OnDisable() => ClearWarnings();
    private void ClearWarnings()
    {
        foreach (var warning in warnings) if (warning != null && owner != null) owner.DestroyEffect(warning.gameObject);
        warnings.Clear();
    }
}
