using UnityEngine;

/// <summary>
/// The boss fire look: soft blobs that rise, shrink and cool from yellow to red, plus a few sparks. Shared by the
/// player's burning effect and the burning projectile so both read as the same fire. Presentation only.
/// </summary>
public static class BossFireParticles
{
    private static Texture2D softTexture;
    private static Material particleMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { softTexture = null; particleMaterial = null; }

    /// <summary>
    /// Builds the flame and spark systems under <paramref name="parent"/>, stopped. <paramref name="scale"/> sizes the
    /// particles (1 is the player's size). The caller positions and sizes the emission shape.
    /// </summary>
    public static void Build(Transform parent, int sortingLayer, Material fallbackMaterial, float scale,
        out ParticleSystem flames, out ParticleSystem embers)
    {
        Material material = ParticleMaterial(fallbackMaterial);

        flames = CreateSystem(parent, "Burn flames", material, sortingLayer);
        ParticleSystem.MainModule main = flames.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .85f);
        main.startSize = new ParticleSystem.MinMaxCurve(.24f * scale, .44f * scale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 90;
        ParticleSystem.EmissionModule emission = flames.emission;
        emission.rateOverTime = 38f;
        ParticleSystem.VelocityOverLifetimeModule velocity = flames.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-.18f * scale, .18f * scale);
        velocity.y = new ParticleSystem.MinMaxCurve(.7f * scale, 1.35f * scale);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        ParticleSystem.NoiseModule noise = flames.noise;
        noise.enabled = true;
        noise.strength = .28f * scale;
        noise.frequency = .9f;
        noise.scrollSpeed = .7f;
        noise.damping = true;
        ParticleSystem.ColorOverLifetimeModule color = flames.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(Fire(
            new Color(1f, .96f, .55f), new Color(1f, .74f, .2f), new Color(1f, .36f, .06f), new Color(.55f, .1f, .04f), .8f));
        ParticleSystem.SizeOverLifetimeModule size = flames.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, .7f), new Keyframe(.25f, 1f), new Keyframe(1f, .12f)));

        embers = CreateSystem(parent, "Burn sparks", material, sortingLayer);
        ParticleSystem.MainModule sparks = embers.main;
        sparks.startLifetime = new ParticleSystem.MinMaxCurve(.5f, 1f);
        sparks.startSize = new ParticleSystem.MinMaxCurve(.05f * scale, .1f * scale);
        sparks.maxParticles = 30;
        ParticleSystem.EmissionModule sparkEmission = embers.emission;
        sparkEmission.rateOverTime = 11f;
        ParticleSystem.VelocityOverLifetimeModule sparkVelocity = embers.velocityOverLifetime;
        sparkVelocity.enabled = true;
        sparkVelocity.space = ParticleSystemSimulationSpace.World;
        sparkVelocity.x = new ParticleSystem.MinMaxCurve(-.5f * scale, .5f * scale);
        sparkVelocity.y = new ParticleSystem.MinMaxCurve(1.2f * scale, 2.3f * scale);
        sparkVelocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        ParticleSystem.ColorOverLifetimeModule sparkColor = embers.colorOverLifetime;
        sparkColor.enabled = true;
        sparkColor.color = new ParticleSystem.MinMaxGradient(Fire(
            new Color(1f, 1f, .75f), new Color(1f, .85f, .3f), new Color(1f, .45f, .1f), new Color(.8f, .2f, .05f), 1f));
    }

    private static ParticleSystem CreateSystem(Transform parent, string childName, Material material, int sortingLayer)
    {
        var child = new GameObject(childName);
        child.transform.SetParent(parent, false);
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
        var renderer = child.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.sortingLayerID = sortingLayer;
        return system;
    }

    // Hot pale-yellow core fading through orange to dark red and out.
    private static Gradient Fire(Color hot, Color warm, Color orange, Color ember, float peakAlpha)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(hot, 0f), new GradientColorKey(warm, .2f),
                new GradientColorKey(orange, .55f), new GradientColorKey(ember, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, .12f),
                new GradientAlphaKey(peakAlpha * .7f, .55f), new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static Material ParticleMaterial(Material fallback)
    {
        if (particleMaterial != null) return particleMaterial;
        if (softTexture == null)
        {
            const int size = 64;
            softTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave, name = "BurnSoftBlob"
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f;
                    float fade = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 1.6f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * fade));
                }
            softTexture.SetPixels32(pixels);
            softTexture.Apply(false, true);
        }
        Shader shader = Shader.Find("Sprites/Default");
        particleMaterial = shader != null ? new Material(shader) : new Material(fallback);
        particleMaterial.mainTexture = softTexture;
        particleMaterial.hideFlags = HideFlags.DontSave;
        return particleMaterial;
    }
}
