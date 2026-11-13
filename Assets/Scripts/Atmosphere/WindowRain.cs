using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Rain behind the bars of every stone window, in every scene, looking like the boss stage rain.
/// Two kinds of windows exist:
/// - Interior hall bay windows: a flat glass quad ("... Window Glass") behind arch/jamb/sill frame pieces.
///   The glass is fitted to the see-through opening of the frame (scenes override its size per instance),
///   and the rain is drawn one sorting order above the glass, behind the frame.
/// - Plain "Window" sprites (the story room): a dark pane is added behind the opening so the brick wall
///   does not show through, and the rain is drawn between the pane and the window sprite.
/// </summary>
public static class WindowRain
{
    private const string WindowSpriteName = "Window";
    private const string GlassNameSuffix = "Window Glass";
    private const string HolderName = "Window Rain";
    private static readonly Color PaneColor = new Color(.25f, .31f, .34f);

    // The see-through opening inside the 32x48 px window sprite, in pixels (x from left, y from top).
    private const float SpriteWidthPx = 32f, SpriteHeightPx = 48f;
    private const float OpeningLeftPx = 6f, OpeningRightPx = 26f, OpeningTopPx = 17f, OpeningBottomPx = 41.5f;
    // The same opening when the window is built from separate 32 px wide pieces (arch on top, sill below):
    // it starts just under the arch and reaches into the sill.
    private const float PieceWidthPx = 32f, PieceOpeningHalfWidthPx = 10f, PieceBelowArchPx = 1f, PieceIntoSillPx = 9.5f;

    private static Sprite squareSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => squareSprite = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply(scene);

    private static void Apply(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;

        var glasses = new List<SpriteRenderer>();
        var windows = new List<SpriteRenderer>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null || renderer.transform.Find(HolderName) != null) continue;
                if (renderer.gameObject.name.EndsWith(GlassNameSuffix)) glasses.Add(renderer);
                else if (renderer.sprite.name == WindowSpriteName) windows.Add(renderer);
            }

        foreach (SpriteRenderer glass in glasses) AddGlassRain(glass);
        // Scenes with real glass keep their plain window sprites as they are; elsewhere they get a pane.
        bool needsPane = glasses.Count == 0;
        foreach (SpriteRenderer window in windows) AddWindowRain(window, needsPane);
    }

    // ---- hall bay windows ---------------------------------------------------------------------------

    private static bool AddGlassRain(SpriteRenderer glass)
    {
        FitGlassToFrame(glass);
        Bounds bounds = glass.sprite.bounds;
        // One order above the glass, still behind the frame (arch, jamb and sill) drawn over it.
        return Attach(glass, bounds.min.x, bounds.max.x, bounds.max.y, bounds.min.y, glass.sortingOrder + 1, null);
    }

    // "Left Window Glass" sits next to "Left Window Arch" / "Left Window Sill". The glass quad is bigger than
    // the frame's opening, so resize it to the opening.
    private static void FitGlassToFrame(SpriteRenderer glass)
    {
        Transform parent = glass.transform.parent;
        if (parent == null) return;
        string name = glass.gameObject.name;
        string side = name.Substring(0, name.Length - GlassNameSuffix.Length);
        SpriteRenderer arch = FindSibling(parent, side + "Window Arch");
        SpriteRenderer sill = FindSibling(parent, side + "Window Sill");
        if (arch == null || sill == null) return;

        Bounds archBounds = arch.bounds, sillBounds = sill.bounds;
        float pixel = archBounds.size.x / PieceWidthPx;
        if (pixel <= 0f) return;
        float centerX = archBounds.center.x;
        float left = centerX - PieceOpeningHalfWidthPx * pixel, right = centerX + PieceOpeningHalfWidthPx * pixel;
        float top = archBounds.min.y - PieceBelowArchPx * pixel;
        float bottom = sillBounds.max.y - PieceIntoSillPx * pixel;
        if (right <= left || top <= bottom) return;

        Bounds local = glass.sprite.bounds;
        Vector3 parentScale = parent.lossyScale;
        if (local.size.x <= 0f || local.size.y <= 0f
            || Mathf.Approximately(parentScale.x, 0f) || Mathf.Approximately(parentScale.y, 0f)) return;

        // Desired world size over sprite size, expressed in the parent's space.
        Vector3 scale = glass.transform.localScale;
        scale.x = (right - left) / (local.size.x * Mathf.Abs(parentScale.x));
        scale.y = (top - bottom) / (local.size.y * Mathf.Abs(parentScale.y));
        glass.transform.localScale = scale;

        // Move the sprite's bounds centre (not necessarily its pivot) to the opening's centre.
        Vector3 lossy = glass.transform.lossyScale;
        Vector3 position = glass.transform.position;
        position.x = (left + right) * .5f - local.center.x * lossy.x;
        position.y = (top + bottom) * .5f - local.center.y * lossy.y;
        glass.transform.position = position;
    }

    private static SpriteRenderer FindSibling(Transform parent, string name)
    {
        Transform sibling = parent.Find(name);
        return sibling != null ? sibling.GetComponent<SpriteRenderer>() : null;
    }

    // ---- plain window sprites -----------------------------------------------------------------------

    private static bool AddWindowRain(SpriteRenderer window, bool addPane)
    {
        Bounds bounds = window.sprite.bounds; // local to the window transform, before its scale
        float left = bounds.min.x + bounds.size.x * OpeningLeftPx / SpriteWidthPx;
        float right = bounds.min.x + bounds.size.x * OpeningRightPx / SpriteWidthPx;
        float top = bounds.max.y - bounds.size.y * OpeningTopPx / SpriteHeightPx;
        float bottom = bounds.max.y - bounds.size.y * OpeningBottomPx / SpriteHeightPx;
        // Pane two orders behind the window sprite, rain one order behind it.
        int? paneOrder = addPane ? window.sortingOrder - 2 : (int?)null;
        return Attach(window, left, right, top, bottom, window.sortingOrder - 1, paneOrder);
    }

    // ---- shared -------------------------------------------------------------------------------------

    // left/right/top/bottom: the rain rectangle in the target's local space.
    private static bool Attach(SpriteRenderer target, float left, float right, float top, float bottom,
        int rainOrder, int? paneOrder)
    {
        Vector3 lossy = target.transform.lossyScale;
        if (Mathf.Approximately(lossy.x, 0f) || Mathf.Approximately(lossy.y, 0f)) return false;
        Sprite square = GetSquareSprite();

        // The holder cancels the target's scale so everything inside it is in plain world units.
        var holder = new GameObject(HolderName);
        holder.transform.SetParent(target.transform, false);
        holder.transform.localPosition = new Vector3((left + right) * .5f, (top + bottom) * .5f, 0f);
        holder.transform.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f);
        float width = (right - left) * Mathf.Abs(lossy.x);
        float height = (top - bottom) * Mathf.Abs(lossy.y);

        if (paneOrder.HasValue)
        {
            var pane = new GameObject("Window pane");
            pane.transform.SetParent(holder.transform, false);
            pane.transform.localScale = new Vector3(width, height, 1f);
            var paneRenderer = pane.AddComponent<SpriteRenderer>();
            paneRenderer.sprite = square;
            paneRenderer.color = PaneColor;
            paneRenderer.sharedMaterial = target.sharedMaterial;
            paneRenderer.sortingLayerID = target.sortingLayerID;
            paneRenderer.sortingOrder = paneOrder.Value;
        }

        // Same material as the window, like the boss stage rain uses the default sprite material.
        holder.AddComponent<WindowRainDrops>().Build(
            square, target.sharedMaterial, width, height, target.sortingLayerID, rainOrder);
        return true;
    }

    // 1x1 unit white square: a pane is this sprite scaled up, a rain streak is it stretched thin.
    private static Sprite GetSquareSprite()
    {
        if (squareSprite != null) return squareSprite;
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
        {
            name = "Window rain square", filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
        };
        var pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        squareSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4f);
        squareSprite.hideFlags = HideFlags.HideAndDontSave;
        return squareSprite;
    }
}
