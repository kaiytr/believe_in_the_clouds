using UnityEngine;

/// <summary>
/// One-shot burst for a landed hit: a spiky star flash, thin golden spark streaks thrown out (the hardest ones along
/// the hit direction) and a few embers that drop. Pure presentation built from particles.
/// </summary>
public static class CombatHitEffect
{
    private static Texture2D softTexture, starTexture;
    private static Material material, starMaterial;

    public static void Spawn(Vector2 position, Vector2 direction, SpriteRenderer sortingReference = null, float scale = 1f)
    {
        if (direction.sqrMagnitude < .0001f) direction = Vector2.right;
        direction.Normalize();
        scale = Mathf.Max(.2f, scale);
        int layer = sortingReference != null ? sortingReference.sortingLayerID : 0;
        int order = (sortingReference != null ? sortingReference.sortingOrder : 0) + 8;
        Material mat = Material();
        Material starMat = StarMaterial();

        var root = new GameObject("Hit effect");
        root.transform.position = new Vector3(position.x, position.y, sortingReference != null ? sortingReference.transform.position.z : 0f);
        Quaternion facing = Quaternion.LookRotation(new Vector3(direction.x, direction.y, 0f), Vector3.forward);

        // Spiky star flash at the contact point: a couple of them, gone in a few frames.
        ParticleSystem star = Create(root, "Star", starMat, layer, order + 3, Quaternion.identity, ParticleSystemShapeType.Circle);
        ParticleSystem.ShapeModule starShape = star.shape;
        starShape.radius = .04f * scale;
        Configure(star, lifetime: new Vector2(.09f, .13f), size: new Vector2(1.3f, 1.8f) * scale, speed: Vector2.zero, count: 2);
        SetColor(star, new Color(1f, 1f, .8f), new Color(1f, .6f, .1f), 1f);
        SetSize(star, .7f, 1.15f);

        // Thin, sharp spark streaks in every direction ...
        ParticleSystem rays = Create(root, "Sparks all around", mat, layer, order + 2, Quaternion.identity, ParticleSystemShapeType.Circle);
        Configure(rays, lifetime: new Vector2(.12f, .24f), size: new Vector2(.03f, .055f) * scale, speed: new Vector2(8f, 17f) * scale, count: 14);
        Stretch(rays, 9f, .06f);
        SetColor(rays, new Color(1f, 1f, .75f), new Color(1f, .5f, .05f), 1f);
        SetSize(rays, 1f, .2f);
        SetSpeedFade(rays);

        // ... and a harder, longer fan thrown along the hit direction.
        ParticleSystem forward = Create(root, "Sparks forward", mat, layer, order + 2, facing, ParticleSystemShapeType.Cone);
        ParticleSystem.ShapeModule cone = forward.shape;
        cone.angle = 24f;
        cone.radius = .02f;
        Configure(forward, lifetime: new Vector2(.14f, .3f), size: new Vector2(.03f, .05f) * scale, speed: new Vector2(14f, 26f) * scale, count: 10);
        Stretch(forward, 11f, .07f);
        SetColor(forward, new Color(1f, 1f, .85f), new Color(1f, .6f, .1f), 1f);
        SetSize(forward, 1f, .15f);
        SetSpeedFade(forward);

        // Embers: tiny bright dots that arc away and drop.
        ParticleSystem dots = Create(root, "Embers", mat, layer, order + 1, Quaternion.identity, ParticleSystemShapeType.Circle);
        Configure(dots, lifetime: new Vector2(.3f, .65f), size: new Vector2(.035f, .07f) * scale, speed: new Vector2(4f, 11f) * scale, count: 12);
        ParticleSystem.MainModule dotMain = dots.main;
        dotMain.gravityModifier = .9f;
        SetColor(dots, new Color(1f, 1f, .8f), new Color(1f, .6f, .15f), 1f);
        SetSize(dots, 1f, .4f);

        foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>()) system.Play();
        Object.Destroy(root, 1.2f);
    }

    private static ParticleSystem Create(GameObject root, string name, Material mat, int layer, int order,
        Quaternion rotation, ParticleSystemShapeType shapeType)
    {
        var child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
        child.transform.rotation = rotation;
        var system = child.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = .1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = shapeType;
        if (shapeType == ParticleSystemShapeType.Circle) { shape.radius = .06f; shape.radiusThickness = 0f; }
        var renderer = child.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = mat;
        renderer.sortingLayerID = layer;
        renderer.sortingOrder = order;
        return system;
    }

    private static void Configure(ParticleSystem system, Vector2 lifetime, Vector2 size, Vector2 speed, int count)
    {
        ParticleSystem.MainModule main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.maxParticles = Mathf.Max(8, count * 2);
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    private static void Stretch(ParticleSystem system, float lengthScale, float velocityScale)
    {
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = lengthScale;
        renderer.velocityScale = velocityScale;
    }

    private static void SetColor(ParticleSystem system, Color start, Color end, float alpha)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, .6f), new GradientColorKey(end, 1f) },
            new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha * .8f, .55f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private static void SetSize(ParticleSystem system, float from, float to)
    {
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, from), new Keyframe(1f, to)));
    }

    // Particles are flung out fast and slow down quickly, like sparks hitting the air.
    // x/y/z와 speedModifier는 모두 같은 커브 모드여야 한다(Curve).
    // x/y/z를 상수로 두고 speedModifier만 커브로 두면
    // "Particle Velocity curves must all be in the same mode" 에러가 난다.
    // 그래서 0인 축들도 값은 항상 0인 커브로 둔다.
    private static readonly AnimationCurve ZeroCurve =
        new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
    private static readonly AnimationCurve SpeedFadeCurve =
        new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, .08f));

    private static void SetSpeedFade(ParticleSystem system)
    {
        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(1f, ZeroCurve);
        velocity.y = new ParticleSystem.MinMaxCurve(1f, ZeroCurve);
        velocity.z = new ParticleSystem.MinMaxCurve(1f, ZeroCurve);
        velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, SpeedFadeCurve);
    }

    // A sharp 8-point star: four long spikes along the axes, four shorter ones on the diagonals.
    private static Material StarMaterial()
    {
        if (starMaterial != null) return starMaterial;
        const int size = 96;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            name = "HitEffectStar"
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float longSpikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * angle)), 14f);
                float shortSpikes = .5f * Mathf.Pow(Mathf.Abs(Mathf.Sin(2f * angle)), 14f);
                float reach = .16f + .84f * Mathf.Max(longSpikes, shortSpikes);
                float fade = Mathf.Clamp01(1f - radius / reach);
                fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fade * 2.2f));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * fade));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        starTexture = texture;
        Shader shader = Shader.Find("Sprites/Default");
        starMaterial = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"))
        {
            mainTexture = texture, hideFlags = HideFlags.DontSave, name = "HitEffectStar"
        };
        return starMaterial;
    }

    private static Material Material()
    {
        if (material != null) return material;
        if (softTexture == null)
        {
            const int size = 64;
            softTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
                name = "HitEffectSoft"
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f;
                    float fade = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    fade = Mathf.SmoothStep(0f, 1f, fade * 1.6f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * fade));
                }
            softTexture.SetPixels32(pixels);
            softTexture.Apply(false, true);
        }
        Shader shader = Shader.Find("Sprites/Default");
        material = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"))
        {
            mainTexture = softTexture, hideFlags = HideFlags.DontSave, name = "HitEffect"
        };
        return material;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { material = starMaterial = null; softTexture = starTexture = null; }
}
