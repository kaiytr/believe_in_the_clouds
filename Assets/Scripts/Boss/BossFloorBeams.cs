using UnityEngine;

/// <summary>
/// Phase two arena floor: golden light beams rise out of the ground across the whole arena, like the light of a huge
/// magic circle under it (figuratively; there is no actual circle), with a faint glow haze lying on the floor.
/// Presentation only. Fades in when started and is removed with the arena visuals.
/// </summary>
public sealed class BossFloorBeams : MonoBehaviour
{
    private const float FadeInSeconds = 2.5f;
    // Emitted beams per second per world unit of floor, and the same for the low haze.
    private const float BeamDensity = .22f, HazeDensity = .08f;

    private static Texture2D beamTexture;
    private static Material beamMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { beamTexture = null; beamMaterial = null; }

    private ParticleSystem beams, haze;
    private float beamRate, hazeRate, ramp;

    /// <summary>Builds the emitters along the floor line from left to right (world x) at floorY.</summary>
    public void Build(float left, float right, float floorY, int sortingLayerId, int sortingOrder, Material fallbackMaterial)
    {
        float width = Mathf.Max(1f, right - left);
        transform.position = new Vector3((left + right) * .5f, floorY, 0f);
        Material material = BeamMaterial(fallbackMaterial);

        beams = CreateSystem("Light beams", material, sortingLayerId, sortingOrder, width);
        ParticleSystem.MainModule main = beams.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(.25f, .7f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(1.2f, 3.2f);
        main.startSizeZ = 1f;
        main.maxParticles = 160;
        // Gold, from deep to pale, never one flat colour.
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .8f, .3f), new Color(1f, .93f, .62f));
        beamRate = width * BeamDensity;
        ParticleSystem.SizeOverLifetimeModule size = beams.sizeOverLifetime;
        size.enabled = true;
        size.separateAxes = true;
        // The beam shoots up quickly, then thins out as it fades.
        size.x = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, .5f), new Keyframe(.25f, 1f), new Keyframe(1f, .35f)));
        size.y = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, .05f), new Keyframe(.22f, 1f), new Keyframe(1f, 1.1f)));
        size.z = new ParticleSystem.MinMaxCurve(1f, 1f);
        SetFade(beams, .55f);

        haze = CreateSystem("Floor glow", material, sortingLayerId, sortingOrder, width);
        ParticleSystem.MainModule hazeMain = haze.main;
        hazeMain.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
        hazeMain.startSize3D = true;
        hazeMain.startSizeX = new ParticleSystem.MinMaxCurve(1.8f, 3.6f);
        hazeMain.startSizeY = new ParticleSystem.MinMaxCurve(.4f, .8f);
        hazeMain.startSizeZ = 1f;
        hazeMain.maxParticles = 80;
        hazeMain.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .76f, .26f), new Color(1f, .9f, .55f));
        hazeRate = width * HazeDensity;
        SetFade(haze, .3f);

        ramp = 0f;
        Apply();
        beams.Play(true);
        haze.Play(true);
    }

    private void Update()
    {
        if (beams == null) return;
        if (ramp < 1f) ramp = Mathf.Min(1f, ramp + Time.deltaTime / FadeInSeconds);
        Apply();
    }

    // The emission grows from nothing so the floor lights up gradually.
    private void Apply()
    {
        float k = Mathf.SmoothStep(0f, 1f, ramp);
        ParticleSystem.EmissionModule emission = beams.emission;
        emission.rateOverTime = beamRate * k;
        ParticleSystem.EmissionModule hazeEmission = haze.emission;
        hazeEmission.rateOverTime = hazeRate * k;
    }

    private ParticleSystem CreateSystem(string childName, Material material, int sortingLayerId, int sortingOrder, float width)
    {
        var child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        var system = child.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width, .05f, .05f);
        var renderer = child.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        // A particle's bottom edge sits on the floor point instead of its middle.
        renderer.pivot = new Vector3(0f, .5f, 0f);
        renderer.sharedMaterial = material;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
        return system;
    }

    private static void SetFade(ParticleSystem system, float peakAlpha)
    {
        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, .2f),
                new GradientAlphaKey(peakAlpha * .7f, .6f), new GradientAlphaKey(0f, 1f)
            });
        color.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    // Soft vertical strip: narrow bright core, fading sideways and towards the top; strongest at the bottom edge.
    private static Material BeamMaterial(Material fallback)
    {
        if (beamMaterial != null) return beamMaterial;
        if (beamTexture == null)
        {
            const int width = 32, height = 128;
            beamTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave, name = "FloorBeam"
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float dx = (x + .5f) / width * 2f - 1f;
                    float v = (y + .5f) / height;               // 0 = floor end, 1 = tip
                    float side = Mathf.Exp(-dx * dx * 5f);
                    float along = Mathf.Pow(1f - v, 1.4f) * Mathf.SmoothStep(0f, 1f, v / .04f);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * side * along));
                }
            beamTexture.SetPixels32(pixels);
            beamTexture.Apply(false, true);
        }
        Shader shader = Shader.Find("Sprites/Default");
        beamMaterial = shader != null ? new Material(shader) : new Material(fallback);
        beamMaterial.mainTexture = beamTexture;
        beamMaterial.hideFlags = HideFlags.DontSave;
        return beamMaterial;
    }
}
