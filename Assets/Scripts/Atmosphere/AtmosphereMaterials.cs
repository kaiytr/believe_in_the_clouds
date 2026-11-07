using UnityEngine;

/// <summary>
/// World sprites are lit by the 2D lights. Gameplay telegraphs and light shafts must not be dimmed
/// by the stage darkness, so they use this unlit material instead.
/// </summary>
public static class AtmosphereMaterials
{
    private static Material unlit;

    public static Material Unlit
    {
        get
        {
            if (unlit == null) unlit = Resources.Load<Material>("Materials/SpriteUnlit");
            return unlit;
        }
    }

    public static void ApplyUnlit(SpriteRenderer renderer)
    {
        Material material = Unlit;
        if (renderer != null && material != null) renderer.sharedMaterial = material;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => unlit = null;
}
