using System.Collections.Generic;
using UnityEngine;

/// <summary>Main-thread queries share growing lists, so crowded scenes never silently overflow a fixed buffer.</summary>
public static class CombatPhysics
{
    private static readonly List<Collider2D> overlaps = new List<Collider2D>(32);
    private static readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(32);
    private static ContactFilter2D Filter(int mask, bool triggers = true) =>
        new ContactFilter2D { useLayerMask = true, layerMask = mask, useTriggers = triggers };

    public static bool IsBodyValid(Collider2D body) => body != null && body.enabled && body.gameObject.activeInHierarchy;

    public static bool OverlapsBox(Collider2D target, Vector2 center, Vector2 size, float angle = 0f)
    {
        if (!IsBodyValid(target)) return false;
        overlaps.Clear();
        Physics2D.OverlapBox(center, size, angle, Filter(1 << target.gameObject.layer), overlaps);
        return overlaps.Contains(target);
    }

    public static bool OverlapsCircle(Collider2D target, Vector2 center, float radius)
    {
        if (!IsBodyValid(target)) return false;
        return (target.ClosestPoint(center) - center).sqrMagnitude <= radius * radius;
    }

    // Results are borrowed until the next cast. Callers finish inspecting them before another query.
    public static List<RaycastHit2D> Cast(Vector2 center, Vector2 size, Vector2 direction, float distance, int mask, bool circle)
    {
        hits.Clear();
        if (circle) Physics2D.CircleCast(center, size.x * .5f, direction, Filter(mask), hits, distance);
        else Physics2D.BoxCast(center, size, 0f, direction, Filter(mask), hits, distance);
        return hits;
    }

    public static List<RaycastHit2D> Raycast(Vector2 origin, Vector2 direction, float distance, int mask)
    {
        hits.Clear();
        Physics2D.Raycast(origin, direction, Filter(mask, false), hits, distance);
        return hits;
    }

    public static bool SweptTarget(Collider2D target, Vector2 center, Vector2 size, Vector2 direction, float distance, bool circle)
    {
        if (!IsBodyValid(target)) return false;
        if (circle ? OverlapsCircle(target, center, size.x * .5f) : OverlapsBox(target, center, size)) return true;
        if (distance <= 0f) return false;
        var results = Cast(center, size, direction, distance, 1 << target.gameObject.layer, circle);
        for (int i = 0; i < results.Count; i++) if (results[i].collider == target) return true;
        return false;
    }
}
