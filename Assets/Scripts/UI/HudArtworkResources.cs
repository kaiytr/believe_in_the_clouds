using System.Collections.Generic;
using UnityEngine;

public readonly struct HudArtworkSprite
{
    public readonly Sprite Sprite;
    public readonly RectInt PixelBounds;

    public bool IsValid => Sprite != null && PixelBounds.width > 0 && PixelBounds.height > 0;

    public HudArtworkSprite(Sprite sprite, RectInt pixelBounds)
    {
        Sprite = sprite;
        PixelBounds = pixelBounds;
    }
}

public static class HudArtworkResources
{
    private const string ResourceFolder = "UI/Hud/";
    private static readonly Dictionary<string, HudArtworkSprite> cached = new Dictionary<string, HudArtworkSprite>();
    private static readonly HashSet<Sprite> generatedSprites = new HashSet<Sprite>();
    // Alpha bounds in Unity's bottom-left pixel coordinates for the 1920x1080 source canvases.
    private static readonly Dictionary<string, RectInt> pixelBounds = new Dictionary<string, RectInt>
    {
        { "PlayerStrengthBackground", new RectInt(34, 33, 1001, 206) },
        { "PlayerStrengthGauge", new RectInt(118, 47, 904, 53) },
        { "PlayerPortrait", new RectInt(34, 33, 206, 206) },
        { "PlayerCoinBadge", new RectInt(1585, 42, 271, 79) },
        { "EnemyStrengthBackground", new RectInt(1169, 283, 465, 38) },
        { "EnemyStrengthGauge", new RectInt(1174, 288, 453, 28) },
        { "EnemyBeliefBackground", new RectInt(1234, 880, 318, 22) },
        { "EnemyBeliefGauge", new RectInt(1237, 883, 312, 16) },
        { "EnemyBeliefSpeechBubble", new RectInt(1234, 744, 318, 120) }
    };

    public static HudArtworkSprite Load(string assetName)
    {
        if (cached.TryGetValue(assetName, out HudArtworkSprite result))
            return result;

        Sprite source = Resources.Load<Sprite>(ResourceFolder + assetName);
        if (source == null || source.texture == null)
        {
            Debug.LogError($"Missing HUD artwork at Resources/{ResourceFolder}{assetName}.png.");
            return default;
        }

        Texture2D texture = source.texture;
        if (!pixelBounds.TryGetValue(assetName, out RectInt bounds))
        {
            Debug.LogError($"Missing transparent-padding bounds for HUD artwork: {assetName}.");
            return default;
        }

        if (texture.width != 1920 || texture.height != 1080)
        {
            Debug.LogError($"HUD artwork must retain its 1920x1080 source canvas: {assetName}.");
            return default;
        }

        Sprite trimmed = Sprite.Create(
            texture,
            new Rect(bounds.x, bounds.y, bounds.width, bounds.height),
            Vector2.zero,
            source.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect);
        trimmed.name = assetName + " (Trimmed)";

        result = new HudArtworkSprite(trimmed, bounds);
        cached.Add(assetName, result);
        generatedSprites.Add(trimmed);
        return result;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        foreach (Sprite sprite in generatedSprites)
        {
            if (sprite == null)
                continue;

            if (Application.isPlaying) Object.Destroy(sprite);
            else Object.DestroyImmediate(sprite);
        }
        generatedSprites.Clear();
        cached.Clear();
    }
}
