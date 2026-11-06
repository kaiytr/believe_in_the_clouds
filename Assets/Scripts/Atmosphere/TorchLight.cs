using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Warm flickering point light placed on a wall torch.</summary>
public sealed class TorchLight : MonoBehaviour
{
    private const float BaseIntensity = 1.35f;
    private const float BaseRadius = 7.5f;
    private static readonly Color Warm = new Color(1f, .6f, .27f);

    private Light2D glow;
    private float seed;

    private void Awake()
    {
        seed = Random.value * 100f;
        var holder = new GameObject("Torch light");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = new Vector3(0f, .1f, 0f);
        glow = holder.AddComponent<Light2D>();
        glow.lightType = Light2D.LightType.Point;
        glow.color = Warm;
        glow.intensity = BaseIntensity;
        glow.pointLightInnerRadius = .6f;
        glow.pointLightOuterRadius = BaseRadius;
        glow.falloffIntensity = .7f;
        glow.shadowsEnabled = false;
    }

    private void Update()
    {
        if (glow == null) return;
        float t = Time.time;
        float noise = Mathf.PerlinNoise(t * 7f + seed, seed) * .6f + Mathf.PerlinNoise(t * 2.1f, seed + 9f) * .4f;
        glow.intensity = BaseIntensity * (.82f + noise * .36f);
        glow.pointLightOuterRadius = BaseRadius * (.94f + noise * .1f);
    }
}
