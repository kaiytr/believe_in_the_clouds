using UnityEngine;
using System.Collections.Generic;

public static class BossCombatPhysics
{
    public static bool IsObstacle(Collider2D collider, BossCombatController boss)
    {
        return collider != null && !collider.isTrigger && collider != boss.PlayerCollider &&
            !collider.transform.IsChildOf(boss.transform) && collider.GetComponentInParent<BossCloudPlatform>() == null &&
            (boss.Settings.ObstacleMask.value & (1 << collider.gameObject.layer)) != 0;
    }

    public static bool FindGround(Vector2 point, float above, float below, BossCombatController boss, out Vector2 ground)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(point + Vector2.up * above, Vector2.down, above + below, boss.Settings.GroundMask);
        foreach (RaycastHit2D hit in hits)
        {
            if (!IsObstacle(hit.collider, boss) || hit.normal.y < 0.65f) continue;
            ground = hit.point;
            return true;
        }
        ground = default;
        return false;
    }

    public static float ClearDistance(Vector2 center, Vector2 size, Vector2 direction, float distance,
        BossCombatController boss, bool circle, out bool blocked)
    {
        blocked = false;
        float clear = distance;
        RaycastHit2D[] hits = circle ? Physics2D.CircleCastAll(center, size.x * 0.5f, direction, distance, boss.Settings.ObstacleMask) :
            Physics2D.BoxCastAll(center, size, 0f, direction, distance, boss.Settings.ObstacleMask);
        foreach (RaycastHit2D hit in hits)
        {
            if (!IsObstacle(hit.collider, boss)) continue;
            // A horizontal body cast touching its supporting floor is not a wall.
            if (!circle && Mathf.Abs(direction.y) < 0.01f && hit.collider.bounds.max.y <= center.y - size.y * 0.5f + 0.04f) continue;
            if (Vector2.Dot(hit.normal, direction) >= -0.05f) continue;
            if (hit.distance < clear) { clear = Mathf.Max(0f, hit.distance - 0.01f); blocked = true; }
        }
        return clear;
    }

    public static bool SweptTarget(Vector2 center, Vector2 size, Vector2 direction, float distance,
        BossCombatController boss, bool circle)
    {
        Collider2D target = boss.PlayerCollider;
        if (target == null) return false;
        var shape = new BossAttackGeometry(center, size, 0f, circle);
        if (shape.Overlaps(target)) return true;
        RaycastHit2D[] hits = circle ? Physics2D.CircleCastAll(center, size.x * 0.5f, direction, distance) :
            Physics2D.BoxCastAll(center, size, 0f, direction, distance);
        foreach (var hit in hits) if (hit.collider == target) return true;
        return false;
    }

    public static BossWavePath TraceGroundPath(Vector2 origin, int direction, float maxDistance, BossCombatController boss)
    {
        bool blocked = false;
        var points = new List<Vector2> { origin };
        float travelled = 0f;
        Vector2 ground = origin;
        while (travelled < maxDistance)
        {
            float step = Mathf.Min(0.2f, maxDistance - travelled);
            Vector2 point = ground + Vector2.right * direction * step;
            if (!FindGround(point, boss.Settings.MaximumGroundStep, boss.Settings.MaximumGroundStep, boss, out Vector2 next) ||
                Mathf.Abs(next.y - ground.y) > boss.Settings.MaximumGroundStep)
            { blocked = true; break; }
            Vector2 size = boss.Settings.WaveSize;
            float clear = ClearDistance(ground + Vector2.up * (size.y * 0.5f + 0.02f), size,
                Vector2.right * direction, step, boss, false, out bool wall);
            if (wall)
            {
                blocked = true;
                if (clear > .001f) points.Add(ground + Vector2.right * direction * clear);
                break;
            }
            travelled += step;
            ground = next;
            points.Add(ground);
        }
        return new BossWavePath(points, direction, boss.Settings.WaveSize, blocked);
    }
}
