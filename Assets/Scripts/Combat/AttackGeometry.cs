using UnityEngine;

/// <summary>All sizes are world units. The same shape drives warning, threat and active queries.</summary>
public readonly struct AttackGeometry
{
    public Vector2 Center { get; }
    public Vector2 Size { get; }
    public float Angle { get; }
    public bool IsCircle { get; }
    public AttackGeometry(Vector2 center, Vector2 size, float angle = 0f, bool circle = false)
    { Center = center; Size = size; Angle = angle; IsCircle = circle; }

    public static AttackGeometry Corridor(Vector2 origin, Vector2 direction, float distance, float thickness)
    {
        direction.Normalize();
        return new AttackGeometry(origin + direction * distance * 0.5f,
            new Vector2(distance, thickness), Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    public bool Overlaps(Collider2D target)
    {
        return IsCircle ? CombatPhysics.OverlapsCircle(target, Center, Size.x * .5f) :
            CombatPhysics.OverlapsBox(target, Center, Size, Angle);
    }
}
