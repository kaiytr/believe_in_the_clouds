using System.Collections.Generic;
using UnityEngine;

// 도트 구름에 맞춘 계단형 낙뢰. 충돌은 생성하지 않고 빛/잔광만 그린다.
public sealed class BossLightningStrikeVisual : MonoBehaviour
{
    private Mesh mesh;
    private Color32[] sourceColors, fadedColors;
    private float startedAt, duration;
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<int> triangles = new List<int>();
    private readonly List<Color32> colors = new List<Color32>();

    public void Initialize(Vector2 start, Vector2 end, Material material, int sortingOrder, float width, float lifetime)
    {
        startedAt = Time.time;
        duration = Mathf.Max(.05f, lifetime);
        transform.position = Vector3.zero;
        var filter = gameObject.AddComponent<MeshFilter>();
        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        float pixel = Mathf.Max(.04f, width * .5f);
        int steps = Mathf.Max(3, Mathf.CeilToInt((start.y - end.y) / .35f));
        Vector2 previous = start;
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            float x = i == steps ? end.x : start.x + Mathf.Round(Random.Range(-.22f, .22f) / pixel) * pixel;
            Vector2 next = new Vector2(x, Mathf.Lerp(start.y, end.y, t));
            Vector2 corner = new Vector2(next.x, previous.y);
            Stroke(previous, corner, width);
            Stroke(corner, next, width);
            if (i == Mathf.Max(1, steps / 4))
            {
                float side = Random.value < .5f ? -1f : 1f;
                Vector2 branchCorner = previous + new Vector2(side * .4f, 0f);
                Stroke(previous, branchCorner, width * .55f);
                Stroke(branchCorner, branchCorner + new Vector2(0f, -.45f), width * .55f);
            }
            previous = next;
        }

        // 착지점에는 작은 사각 빛 조각만 남겨 화면 전체를 덮지 않는다.
        AddQuad(end, new Vector2(.7f, .07f), new Color(.95f, .75f, .2f, .45f));
        for (int i = 0; i < 5; i++)
            AddQuad(end + new Vector2(Random.Range(-.35f, .35f), Random.Range(.06f, .3f)),
                Vector2.one * pixel, new Color(1f, .93f, .6f, .8f));

        mesh = new Mesh { name = "Pixel lightning mesh" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetColors(colors);
        mesh.SetUVs(0, vertices.ConvertAll(_ => new Vector2(.5f, .5f)));
        mesh.RecalculateBounds();
        filter.sharedMesh = mesh;
        sourceColors = mesh.colors32;
        fadedColors = new Color32[sourceColors.Length];
    }

    private void Stroke(Vector2 start, Vector2 end, float width)
    {
        Vector2 size = new Vector2(Mathf.Abs(end.x - start.x) + width, Mathf.Abs(end.y - start.y) + width);
        Vector2 center = (start + end) * .5f;
        // 금색 낙뢰: 옅은 금빛 외곽 → 진한 금색 본체 → 거의 흰 금빛 코어.
        AddQuad(center, size + Vector2.one * width, new Color(.62f, .42f, .08f, .16f));
        AddQuad(center, size, new Color(.95f, .75f, .2f, .85f));
        AddQuad(center, new Vector2(Mathf.Max(width * .36f, size.x - width * .64f),
            Mathf.Max(width * .36f, size.y - width * .64f)), new Color(1f, .95f, .72f, 1f));
    }

    private void AddQuad(Vector2 center, Vector2 size, Color color)
    {
        int first = vertices.Count;
        Vector2 half = size * .5f;
        vertices.Add(center + new Vector2(-half.x, -half.y));
        vertices.Add(center + new Vector2(-half.x, half.y));
        vertices.Add(center + new Vector2(half.x, half.y));
        vertices.Add(center + new Vector2(half.x, -half.y));
        triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
        triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
        for (int i = 0; i < 4; i++) colors.Add(color);
    }

    private void Update()
    {
        if (mesh == null || sourceColors == null) return;
        float progress = Mathf.Clamp01((Time.time - startedAt) / duration);
        float alpha = progress < .15f ? 1f : progress < .28f ? .2f :
            progress < .43f ? .85f : Mathf.Lerp(.42f, 0f, (progress - .43f) / .57f);
        for (int i = 0; i < sourceColors.Length; i++)
        {
            Color32 color = sourceColors[i];
            color.a = (byte)(color.a * alpha);
            fadedColors[i] = color;
        }
        mesh.colors32 = fadedColors;
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
