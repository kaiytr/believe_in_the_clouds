using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Pixel-art glow around the player, used in every stage including the boss stage.</summary>
[DisallowMultipleComponent]
public sealed class PlayerProximityLight : MonoBehaviour
{
    private static readonly Vector3 Offset = new Vector3(0f, .1f, 0f);

    [SerializeField] private Color lightColor = new Color(1f, .84f, .67f, 1f);
    [SerializeField, Min(0f)] private float intensity = .8f;
    [SerializeField, Min(0f)] private float innerRadius = 1.2f;
    [SerializeField, Min(0f)] private float outerRadius = 4f;
    [SerializeField, Range(0f, 1f)] private float falloffIntensity = .75f;

    private Light2D glow;

    private void Awake()
    {
        float outer = Mathf.Max(innerRadius, outerRadius);
        glow = PixelLight.CreateRadial(transform, "Player proximity light", Offset,
            outer, outer > 0f ? innerRadius / outer : 0f, falloffIntensity, lightColor, intensity);
    }

    // The light follows the player in whole light-texel steps, so its rings move like pixels instead of crawling.
    private void LateUpdate()
    {
        if (glow != null) glow.transform.position = PixelLight.Snap(transform.position + Offset);
    }
}
