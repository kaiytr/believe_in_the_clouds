using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class PlayerProximityLight : MonoBehaviour
{
    [SerializeField] private Color lightColor = new Color(1f, .84f, .67f, 1f);
    [SerializeField, Min(0f)] private float intensity = .8f;
    [SerializeField, Min(0f)] private float innerRadius = 1.2f;
    [SerializeField, Min(0f)] private float outerRadius = 4f;
    [SerializeField, Range(0f, 1f)] private float falloffIntensity = .75f;

    private void Awake()
    {
        GameObject lightObject = new GameObject("Player proximity light");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0f, .1f, 0f);

        Light2D glow = lightObject.AddComponent<Light2D>();
        glow.lightType = Light2D.LightType.Point;
        glow.color = lightColor;
        glow.intensity = intensity;
        glow.pointLightInnerRadius = innerRadius;
        glow.pointLightOuterRadius = Mathf.Max(innerRadius, outerRadius);
        glow.falloffIntensity = falloffIntensity;
        glow.shadowsEnabled = false;
    }
}
