using UnityEngine;

/// <summary>
/// Red warning shaped like the player's blade slash (same fan values as <see cref="DownFanImpactEffect.SwingFan"/>).
/// Used by the boss melee combo so the telegraph matches the slash that will be drawn.
/// </summary>
public sealed class BossFanWarning : MonoBehaviour
{
    private const int Segments = 28;
    private static Material sharedMaterial;
    private MeshFilter filter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;
    private Color fullColor = new Color(1f, .15f, .1f, .3f);
    private float initialAlpha = .12f;

    public static BossFanWarning Create(Transform parent, BossAttackVisual template, SpriteRenderer sortingReference)
    {
        var go = new GameObject("Melee fan warning");
        go.transform.SetParent(parent, false);
        var fan = go.AddComponent<BossFanWarning>();
        SpriteRenderer source = template != null ? template.GetComponent<SpriteRenderer>() : null;
        if (source != null) fan.fullColor = source.color;
        fan.fullColor.a = Mathf.Max(.05f, fan.fullColor.a);
        fan.meshRenderer.sortingLayerID = sortingReference != null ? sortingReference.sortingLayerID : 0;
        fan.meshRenderer.sortingOrder = sortingReference != null ? sortingReference.sortingOrder - 2 : 18;
        return fan;
    }

    private void Awake()
    {
        filter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        if (sharedMaterial == null) sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        meshRenderer.sharedMaterial = sharedMaterial;
        mesh = new Mesh { name = "Melee fan warning" };
        filter.sharedMesh = mesh;
        SetProgress(0f);
    }

    /// <summary>Pie slice from the attacker outward: origin, direction angle, radius and half angle in degrees.</summary>
    public void Show(Vector2 origin, float centerAngleDeg, float radius, float halfAngleDeg)
    {
        transform.SetPositionAndRotation(origin, Quaternion.identity);
        var vertices = new Vector3[Segments + 2];
        var triangles = new int[Segments * 3];
        vertices[0] = Vector3.zero;
        for (int i = 0; i <= Segments; i++)
        {
            float angle = (centerAngleDeg - halfAngleDeg + 2f * halfAngleDeg * i / Segments) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            if (i < Segments)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
        }
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    public void SetProgress(float progress)
    {
        if (meshRenderer == null) return;
        Color color = fullColor;
        color.a = Mathf.Lerp(Mathf.Min(initialAlpha, fullColor.a), fullColor.a, Mathf.Clamp01(progress));
        // Sprites/Default tints through the renderer's material color via a property block.
        var block = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(block);
        block.SetColor("_RendererColor", color);
        block.SetColor("_Color", color);
        meshRenderer.SetPropertyBlock(block);
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
