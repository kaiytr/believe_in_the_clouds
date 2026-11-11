using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ground checks for enemies that must not walk off a platform while keeping their distance.
/// Same probing rules as BelieverEnemy: ground under the body, then ground just ahead at roughly the same height.
/// </summary>
public static class EnemyPlatformProbe
{
    public const float DefaultMaximumStep = 0.45f;
    public const float DefaultEdgeProbeDistance = 0.18f;
    public const float DefaultGroundProbeLift = 0.25f;
    public const float DefaultCurrentGroundDrop = 0.7f;
    private const float WalkableNormalMinimum = 0.65f;

    /// <summary>True when there is still walkable ground ahead of <paramref name="body"/> in <paramref name="direction"/> (1 right, -1 left).</summary>
    public static bool CanMoveSafely(Collider2D body, float direction, LayerMask platformMask,
        float maximumStep = DefaultMaximumStep, float edgeProbeDistance = DefaultEdgeProbeDistance,
        float groundProbeLift = DefaultGroundProbeLift)
    {
        if (body == null || Mathf.Abs(direction) < 0.01f)
            return false;

        Bounds bounds = body.bounds;
        Vector2 origin = new Vector2(bounds.center.x, bounds.min.y + groundProbeLift);
        if (!TryFindWalkableGround(origin, groundProbeLift + DefaultCurrentGroundDrop, platformMask, out Vector2 currentGround))
            return false;

        float probeX = bounds.center.x + Mathf.Sign(direction) * (bounds.extents.x + edgeProbeDistance);
        Vector2 aheadOrigin = new Vector2(probeX, currentGround.y + maximumStep + 0.02f);
        return TryFindWalkableGround(aheadOrigin, maximumStep * 2f + 0.04f, platformMask, out Vector2 groundAhead) &&
               Mathf.Abs(groundAhead.y - currentGround.y) <= maximumStep;
    }

    private static bool TryFindWalkableGround(Vector2 origin, float distance, LayerMask platformMask, out Vector2 groundPoint)
    {
        List<RaycastHit2D> hits = CombatPhysics.Raycast(origin, Vector2.down, distance, platformMask);
        float closestDistance = float.MaxValue;
        groundPoint = default;
        bool found = false;
        for (int i = 0; i < hits.Count; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider == null || hit.normal.y < WalkableNormalMinimum || hit.distance >= closestDistance)
                continue;

            closestDistance = hit.distance;
            groundPoint = hit.point;
            found = true;
        }

        return found;
    }
}
