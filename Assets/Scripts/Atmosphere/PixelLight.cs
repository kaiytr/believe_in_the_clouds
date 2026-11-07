using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Pixel-art style light: instead of the smooth built-in point-light falloff, the glow is a low-resolution
/// radial sprite (point filtered) with a few hard brightness bands, so lit areas are chunky stair-stepped
/// rings like the art. There is deliberately no dithering: a dither pattern crawls over the world whenever
/// the light or camera moves and reads as flicker. Used as a Sprite-type Light2D.
/// </summary>
public static class PixelLight
{
    public const int Bands = 4;
    /// <summary>Light texels per world unit (the art is drawn at roughly 12-16 pixels per unit).</summary>
    public const float TexelsPerUnit = 8f;
    public const float TexelSize = 1f / TexelsPerUnit;

    /// <summary>Snaps a world position to the light-texel grid so moving lights step like pixels instead of crawling.</summary>
    public static Vector3 Snap(Vector3 position)
    {
        position.x = Mathf.Round(position.x * TexelsPerUnit) * TexelSize;
        position.y = Mathf.Round(position.y * TexelsPerUnit) * TexelSize;
        return position;
    }

    private static readonly Dictionary<long, Sprite> Cache = new Dictionary<long, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Cache.Clear();

    /// <summary>Creates a pixel-style radial light of the given world radius under `parent`.</summary>
    public static Light2D CreateRadial(Transform parent, string name, Vector3 localPosition,
        float radius, float inner01, float falloff01, Color color, float intensity)
    {
        var holder = new GameObject(name);
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = localPosition;
        Light2D light = holder.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Sprite;
        light.lightCookieSprite = Radial(radius, inner01, falloff01);
        light.color = color;
        light.intensity = intensity;
        light.shadowsEnabled = false;
        SetRadius(light, radius);
        return light;
    }

    /// <summary>The radial sprite is one unit wide, so the light's world scale is its diameter (parent scale cancelled).</summary>
    public static void SetRadius(Light2D light, float radius)
    {
        Vector3 parent = light.transform.parent != null ? light.transform.parent.lossyScale : Vector3.one;
        float diameter = radius * 2f;
        light.transform.localScale = new Vector3(
            diameter / Mathf.Max(.0001f, Mathf.Abs(parent.x)), diameter / Mathf.Max(.0001f, Mathf.Abs(parent.y)), 1f);
    }

    /// <summary>Radial glow: full brightness inside `inner01` (fraction of the radius), then falling off.</summary>
    public static Sprite Radial(float referenceRadius, float inner01, float falloff01)
    {
        int size = Mathf.Clamp(Mathf.RoundToInt(referenceRadius * 2f * TexelsPerUnit), 24, 256);
        float inner = Mathf.Clamp01(inner01);
        float exponent = Mathf.Lerp(1f, 2.2f, Mathf.Clamp01(falloff01));
        long key = ((long)size << 32) | ((long)Mathf.RoundToInt(inner * 100f) << 16) | (long)Mathf.RoundToInt(exponent * 100f);
        if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Pixel light", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave
        };
        var pixels = new Color32[size * size];
        float half = size * .5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(half, half)) / half;
                float t = Mathf.Clamp01((d - inner) / Mathf.Max(.0001f, 1f - inner));
                float v = d >= 1f ? 0f : Mathf.Pow(1f - t, exponent);
                byte level = Quantize(v);
                pixels[y * size + x] = new Color32(level, level, level, 255);
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size, 0, SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.DontSave;
        Cache[key] = sprite;
        return sprite;
    }

    /// <summary>Snaps a 0..1 value to `Bands` hard steps.</summary>
    public static byte Quantize(float value, int bands = Bands)
    {
        if (value <= 0f) return 0;
        float step = Mathf.Floor(Mathf.Clamp01(value) * bands + .5f);
        return (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(step / bands));
    }
}
