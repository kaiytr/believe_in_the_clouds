using System.Collections.Generic;
using UnityEngine;

// Design/Tip 원본(1920x1080 통캔버스)을 런타임에 잘라 쓰는 로더.
// SettingsScreen.LoadArt와 같은 패턴: Resources/Tip 아래 복사본에서
// Sprite 또는 Texture2D로 읽어 지정 rect만 잘라 Sprite를 만든다.
public static class TipArtworkLoader
{
    private static readonly Dictionary<string, Sprite> cached = new Dictionary<string, Sprite>();
    private static readonly List<Sprite> generated = new List<Sprite>();

    public static Sprite Load(string assetName, RectInt pixelRect)
    {
        string key = assetName + "@" + pixelRect.x + "," + pixelRect.y + ","
            + pixelRect.width + "x" + pixelRect.height;
        if (cached.TryGetValue(key, out Sprite found))
            return found;

        string resourcePath = "Tip/" + assetName;
        Sprite source = Resources.Load<Sprite>(resourcePath);
        Texture2D texture = source != null ? source.texture : null;
        float pixelsPerUnit = source != null ? source.pixelsPerUnit : 100f;

        if (texture == null)
            texture = Resources.Load<Texture2D>(resourcePath);

        if (texture == null)
        {
            Debug.LogError($"Missing tip artwork at Resources/{resourcePath}.png.");
            return null;
        }

        Sprite trimmed = Sprite.Create(
            texture,
            new Rect(pixelRect.x, pixelRect.y, pixelRect.width, pixelRect.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit,
            0,
            SpriteMeshType.FullRect);
        trimmed.name = assetName + " (Tip)";
        cached.Add(key, trimmed);
        generated.Add(trimmed);
        return trimmed;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        foreach (Sprite sprite in generated)
        {
            if (sprite == null)
                continue;
            if (Application.isPlaying) Object.Destroy(sprite);
            else Object.DestroyImmediate(sprite);
        }
        generated.Clear();
        cached.Clear();
    }
}
