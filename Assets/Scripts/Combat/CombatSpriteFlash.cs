using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns every sprite of an actor flat white for a moment (hit flash) or for as long as a hold is requested
/// (the player's invulnerability). It swaps the sprite materials and puts the originals back afterwards.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatSpriteFlash : MonoBehaviour
{
    private const string ShaderResource = "Shaders/CombatSpriteFlash";
    private static Material flashMaterial;

    private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    private readonly List<Material> originals = new List<Material>();
    private float flashUntil;
    private int holds;
    private bool applied;

    public static CombatSpriteFlash Of(GameObject actor)
    {
        if (actor == null) return null;
        CombatSpriteFlash flash = actor.GetComponent<CombatSpriteFlash>();
        return flash != null ? flash : actor.AddComponent<CombatSpriteFlash>();
    }

    /// <summary>White for a short moment, e.g. the frames right after a hit.</summary>
    public void Flash(float seconds)
    {
        flashUntil = Mathf.Max(flashUntil, Time.time + Mathf.Max(0f, seconds));
        Refresh();
    }

    /// <summary>White until the matching <see cref="EndHold"/>. Holds stack.</summary>
    public void BeginHold() { holds++; Refresh(); }
    public void EndHold() { holds = Mathf.Max(0, holds - 1); Refresh(); }
    public void ClearAll() { holds = 0; flashUntil = 0f; Restore(); }

    private bool Wanted => holds > 0 || Time.time < flashUntil;

    private void LateUpdate() => Refresh();
    private void OnDisable() => Restore();
    private void OnDestroy() => Restore();

    private void Refresh()
    {
        if (Wanted && !applied) Apply();
        else if (!Wanted && applied) Restore();
    }

    private void Apply()
    {
        Material material = FlashMaterial();
        if (material == null) return;
        renderers.Clear();
        originals.Clear();
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer == null || renderer.sharedMaterial == material) continue;
            renderers.Add(renderer);
            originals.Add(renderer.sharedMaterial);
            renderer.sharedMaterial = material;
        }
        applied = true;
    }

    private void Restore()
    {
        if (!applied) return;
        for (int i = 0; i < renderers.Count; i++)
            if (renderers[i] != null) renderers[i].sharedMaterial = originals[i];
        renderers.Clear();
        originals.Clear();
        applied = false;
    }

    private static Material FlashMaterial()
    {
        if (flashMaterial != null) return flashMaterial;
        Shader shader = Resources.Load<Shader>(ShaderResource);
        if (shader == null || !shader.isSupported) return null;
        flashMaterial = new Material(shader) { name = "CombatSpriteFlash", hideFlags = HideFlags.DontSave };
        return flashMaterial;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => flashMaterial = null;
}
