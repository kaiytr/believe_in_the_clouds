using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sunset backdrop of the ending, drawn entirely from small point-filtered textures so it matches the
/// pixel art: banded sky, a sun, drifting clouds and two layers of hill/ruin silhouettes that scroll
/// at different speeds while the ground moves. Everything is unlit so the sunset colours stay as drawn.
/// </summary>
public sealed class EndingBackdrop : MonoBehaviour
{
    private const float PixelsPerUnit = 16f;
    private const float Period = 32f;              // world width of a seamless silhouette texture
    private const float CloudWrap = 44f;

    private sealed class Layer
    {
        public Transform[] Copies;
        public float Factor, Offset;
    }

    private sealed class Cloud
    {
        public Transform Transform;
        public float X, Drift;
    }

    private readonly List<Object> owned = new List<Object>();
    private readonly List<Layer> layers = new List<Layer>();
    private readonly List<Cloud> clouds = new List<Cloud>();

    /// <param name="horizonY">World height of the ground line (where the sky meets the land).</param>
    public static EndingBackdrop Build(Transform parent, float horizonY, float viewTop)
    {
        var root = new GameObject("Sunset backdrop");
        root.transform.SetParent(parent, false);
        var backdrop = root.AddComponent<EndingBackdrop>();
        backdrop.CreateSky(horizonY, viewTop);
        backdrop.CreateSun(horizonY);
        backdrop.CreateClouds(horizonY, viewTop);
        backdrop.CreateSilhouette("Far hills", 0.10f, new Color32(0x8a, 0x3f, 0x6c, 255), horizonY - 1f, -90, 3.4f, 1.1f, false, 11);
        backdrop.CreateSilhouette("Near ruins", 0.22f, new Color32(0x3f, 0x21, 0x4a, 255), horizonY - 1f, -80, 2.0f, 0.8f, true, 29);
        return backdrop;
    }

    /// <summary>Moves the layers by the distance the ground travelled this frame.</summary>
    public void Scroll(float distance, float deltaTime)
    {
        foreach (Layer layer in layers)
        {
            layer.Offset = Mathf.Repeat(layer.Offset + distance * layer.Factor, Period);
            for (int i = 0; i < layer.Copies.Length; i++)
            {
                Vector3 p = layer.Copies[i].localPosition;
                p.x = -layer.Offset + (i - 1) * Period;
                layer.Copies[i].localPosition = p;
            }
        }
        foreach (Cloud cloud in clouds)
        {
            cloud.X -= (cloud.Drift * deltaTime) + distance * 0.04f;
            if (cloud.X < -CloudWrap * .5f) cloud.X += CloudWrap;
            Vector3 p = cloud.Transform.localPosition;
            p.x = cloud.X;
            cloud.Transform.localPosition = p;
        }
    }

    private void CreateSky(float horizonY, float viewTop)
    {
        // Warm horizon, purple zenith, in hard bands like a palette-limited pixel sky.
        Color32[] stops =
        {
            new Color32(0xff, 0xd9, 0x84, 255),
            new Color32(0xff, 0xa2, 0x57, 255),
            new Color32(0xe5, 0x5f, 0x62, 255),
            new Color32(0x9c, 0x3f, 0x77, 255),
            new Color32(0x4a, 0x2a, 0x6a, 255),
            new Color32(0x24, 0x1a, 0x4a, 255)
        };
        float bottom = horizonY - 3f, top = viewTop + 1f;
        int height = Mathf.RoundToInt((top - bottom) * PixelsPerUnit);
        const int width = 4, bands = 16;
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            float worldY = bottom + y / PixelsPerUnit;
            float t = Mathf.Clamp01((worldY - horizonY) / (top - horizonY));
            t = Mathf.Floor(t * bands) / bands;
            float scaled = Mathf.Pow(t, .8f) * (stops.Length - 1);
            int a = Mathf.Min(stops.Length - 2, Mathf.FloorToInt(scaled));
            Color32 c = Color32.Lerp(stops[a], stops[a + 1], scaled - a);
            for (int x = 0; x < width; x++) pixels[y * width + x] = c;
        }
        Sprite sky = MakeSprite("Sunset sky", width, height, pixels, new Vector2(.5f, 0f));
        SpriteRenderer renderer = NewRenderer("Sky", sky, -100);
        renderer.transform.localPosition = new Vector3(0f, bottom, 0f);
        renderer.transform.localScale = new Vector3(60f / (width / PixelsPerUnit), 1f, 1f);
    }

    private void CreateSun(float horizonY)
    {
        const int size = 192;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                Color32 c = r < .24f ? new Color32(255, 247, 196, 255)
                    : r < .32f ? new Color32(255, 222, 138, 255)
                    : r < .5f ? new Color32(255, 170, 96, 130)
                    : r < .72f ? new Color32(255, 128, 96, 62)
                    : r < 1f ? new Color32(255, 104, 110, 26)
                    : new Color32(0, 0, 0, 0);
                pixels[y * size + x] = c;
            }
        SpriteRenderer sun = NewRenderer("Sun", MakeSprite("Sunset sun", size, size, pixels, new Vector2(.5f, .5f)), -99);
        sun.transform.localPosition = new Vector3(3.6f, horizonY + .2f, 0f);
    }

    private void CreateClouds(float horizonY, float viewTop)
    {
        var random = new System.Random(7);
        var sprites = new Sprite[3];
        for (int i = 0; i < sprites.Length; i++) sprites[i] = BuildCloud(random, i);
        for (int i = 0; i < 8; i++)
        {
            SpriteRenderer renderer = NewRenderer("Cloud " + i, sprites[i % sprites.Length], -95);
            float height = horizonY + 1.4f + (float)random.NextDouble() * (viewTop - horizonY - 2f);
            float scale = 1f + (float)random.NextDouble() * 1.2f;
            renderer.transform.localScale = new Vector3(scale * ((i & 1) == 0 ? 1f : -1f), scale, 1f);
            var cloud = new Cloud
            {
                Transform = renderer.transform,
                X = -CloudWrap * .5f + CloudWrap * i / 8f,
                Drift = .12f + (float)random.NextDouble() * .18f
            };
            renderer.transform.localPosition = new Vector3(cloud.X, height, 0f);
            clouds.Add(cloud);
        }
    }

    private Sprite BuildCloud(System.Random random, int seed)
    {
        const int width = 112, height = 28;
        var blobs = new List<Vector4>();
        for (int i = 0; i < 6; i++)
            blobs.Add(new Vector4(14f + (float)random.NextDouble() * (width - 28f), 9f + (float)random.NextDouble() * 6f,
                10f + (float)random.NextDouble() * 16f, 3.5f + (float)random.NextDouble() * 4f));
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int hits = 0; bool top = false;
                foreach (Vector4 b in blobs)
                {
                    float dx = (x + .5f - b.x) / b.z, dy = (y + .5f - b.y) / b.w;
                    if (dx * dx + dy * dy <= 1f) { hits++; if (dy > .25f) top = true; }
                }
                if (hits == 0) continue;
                // Sunlit upper edge, shaded belly.
                pixels[y * width + x] = top ? new Color32(255, 190, 140, 235) : new Color32(196, 104, 128, 235);
            }
        return MakeSprite("Sunset cloud " + seed, width, height, pixels, new Vector2(.5f, .5f));
    }

    private void CreateSilhouette(string name, float factor, Color32 color, float baseY, int order,
        float maxHeight, float roughness, bool ruins, int seed)
    {
        int width = Mathf.RoundToInt(Period * PixelsPerUnit), height = Mathf.RoundToInt((maxHeight + 3f) * PixelsPerUnit);
        var random = new System.Random(seed);
        // Integer harmonics of the period keep the left and right edges identical, so the texture tiles seamlessly.
        float[] phase = { (float)random.NextDouble() * 6.28f, (float)random.NextDouble() * 6.28f, (float)random.NextDouble() * 6.28f };
        int[] harmonic = { 2, 5, 11 };
        float[] amplitude = { .45f, .3f * roughness, .15f * roughness };
        var heights = new int[width];
        for (int x = 0; x < width; x++)
        {
            float u = x / (float)width, h = .5f;
            for (int k = 0; k < 3; k++) h += amplitude[k] * Mathf.Sin(6.2831853f * harmonic[k] * u + phase[k]) * .5f;
            heights[x] = Mathf.Clamp(Mathf.RoundToInt(h * maxHeight * PixelsPerUnit), 4, height - 1);
        }
        if (ruins)
        {
            // Broken towers and walls standing on the ridge.
            for (int i = 0; i < 9; i++)
            {
                int w = random.Next(5, 11), x0 = random.Next(8, width - 24);
                int top = heights[x0] + random.Next(12, 34);
                for (int x = x0; x < x0 + w; x++)
                {
                    int broken = top - random.Next(0, 3) * ((x - x0) % 3);
                    heights[x] = Mathf.Min(height - 1, Mathf.Max(heights[x], broken));
                }
            }
        }
        var pixels = new Color32[width * height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < heights[x]; y++) pixels[y * width + x] = color;
        Sprite sprite = MakeSprite(name, width, height, pixels, new Vector2(.5f, 0f));

        var layer = new Layer { Factor = factor, Copies = new Transform[3] };
        for (int i = 0; i < 3; i++)
        {
            SpriteRenderer renderer = NewRenderer(name + " " + i, sprite, order);
            renderer.transform.localPosition = new Vector3((i - 1) * Period, baseY, 0f);
            layer.Copies[i] = renderer.transform;
        }
        layers.Add(layer);
    }

    private SpriteRenderer NewRenderer(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        AtmosphereMaterials.ApplyUnlit(renderer);
        return renderer;
    }

    private Sprite MakeSprite(string name, int width, int height, Color32[] pixels, Vector2 pivot)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.DontSave;
        owned.Add(texture);
        owned.Add(sprite);
        return sprite;
    }

    private void OnDestroy()
    {
        foreach (Object item in owned) if (item != null) Destroy(item);
        owned.Clear();
    }
}
