using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Warm torch light. Normal stages use the pixel-art look (hard bands, constant brightness, the rim only
/// shifts by a texel now and then). The boss stage keeps the original smooth, flickering point light.
/// </summary>
public sealed class TorchLight : MonoBehaviour
{
    private const float Intensity = 1.15f;
    private const float Radius = 7.5f;
    private const float MinHold = .3f, MaxHold = .9f;   // seconds between rim changes
    // The flame sits in the upper part of the 1x2 torch sprite.
    private static readonly Vector3 FlameOffset = new Vector3(0f, .6f, 0f);
    private static readonly Color Warm = new Color(1f, .6f, .27f);

    // Smooth look used in the boss stage.
    private const float SmoothIntensity = 1.35f;

    private Light2D glow;
    private bool smooth;
    private bool wide = true;
    private float nextChange;
    private float seed;

    private void Awake()
    {
        smooth = SceneManager.GetActiveScene().name == LobbyFlowRuntime.BossTestScene;
        seed = Random.value * 100f;
        if (smooth)
        {
            var holder = new GameObject("Torch light");
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = FlameOffset;
            glow = holder.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.color = Warm;
            glow.intensity = SmoothIntensity;
            glow.pointLightInnerRadius = .6f;
            glow.pointLightOuterRadius = Radius;
            glow.falloffIntensity = .7f;
            glow.shadowsEnabled = false;
            return;
        }
        glow = PixelLight.CreateRadial(transform, "Torch light", FlameOffset,
            Radius, .6f / Radius, .7f, Warm, Intensity);
        glow.transform.position = PixelLight.Snap(glow.transform.position);
        nextChange = Time.time + Random.Range(MinHold, MaxHold);
    }

    private void Update()
    {
        if (glow == null) return;
        if (smooth)
        {
            float t = Time.time;
            float noise = Mathf.PerlinNoise(t * 7f + seed, seed) * .6f + Mathf.PerlinNoise(t * 2.1f, seed + 9f) * .4f;
            glow.intensity = SmoothIntensity * (.82f + noise * .36f);
            glow.pointLightOuterRadius = Radius * (.94f + noise * .1f);
            return;
        }
        if (Time.time < nextChange) return;
        nextChange = Time.time + Random.Range(MinHold, MaxHold);
        wide = !wide;
        // One texel smaller or larger: the outer ring grows/shrinks, the core never changes.
        PixelLight.SetRadius(glow, Radius - (wide ? 0f : PixelLight.TexelSize * 2f));
    }
}
