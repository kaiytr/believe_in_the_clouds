using UnityEngine;
using UnityEngine.UI;

public sealed class SquareHoldProgressGraphic : MaskableGraphic
{
    private float progress;
    public float Thickness = 3f;
    public float Progress
    {
        get => progress;
        set { float next = Mathf.Clamp01(value); if (next == progress) return; progress = next; SetVerticesDirty(); }
    }

    public static Vector2 PointAt(float progress, float side)
    {
        float length = Mathf.Clamp01(progress) * side * 4;
        Vector2[] points = Path(side);
        for (int i = 0; i < points.Length - 1; i++)
        {
            float segment = Vector2.Distance(points[i], points[i + 1]);
            if (length <= segment) return Vector2.Lerp(points[i], points[i + 1], length / segment);
            length -= segment;
        }
        return points[points.Length - 1];
    }

    private static Vector2[] Path(float side)
    {
        float h = side * .5f;
        return new[] { new Vector2(0, h), new Vector2(h, h), new Vector2(h, -h),
            new Vector2(-h, -h), new Vector2(-h, h), new Vector2(0, h) };
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect rect = GetPixelAdjustedRect();
        float side = Mathf.Max(0, Mathf.Min(rect.width, rect.height) - Thickness);
        if (side <= 0 || progress <= 0) return;
        float remaining = side * 4 * progress;
        Vector2[] points = Path(side);
        for (int i = 0; i < points.Length - 1 && remaining > 0; i++)
        {
            float segment = Vector2.Distance(points[i], points[i + 1]);
            Vector2 end = Vector2.Lerp(points[i], points[i + 1], Mathf.Min(remaining / segment, 1));
            AddLine(helper, points[i] + rect.center, end + rect.center);
            remaining -= segment;
        }
    }

    private void AddLine(VertexHelper helper, Vector2 from, Vector2 to)
    {
        Vector2 direction = (to - from).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * Thickness * .5f;
        // Extend both ends by half a line width to close the square corners.
        from -= direction * Thickness * .5f; to += direction * Thickness * .5f;
        int start = helper.currentVertCount;
        helper.AddVert(from - normal, color, Vector2.zero);
        helper.AddVert(from + normal, color, Vector2.zero);
        helper.AddVert(to + normal, color, Vector2.zero);
        helper.AddVert(to - normal, color, Vector2.zero);
        helper.AddTriangle(start, start + 1, start + 2); helper.AddTriangle(start, start + 2, start + 3);
    }
}
