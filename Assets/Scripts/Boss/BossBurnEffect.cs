using UnityEngine;

public sealed class BossBurnEffect : MonoBehaviour
{
    private PlayerHealth health;
    private BossCombatController source;
    private BossCombatSettings data;
    private float damage, interval, endsAt, nextTick;
    public float Remaining => Mathf.Max(0f, endsAt - Time.time);
    public bool IsBurning => Remaining > 0f;
    public int TickAttempts { get; private set; }
    public int TicksAccepted { get; private set; }

    // Fire as particles: soft round blobs rise from the body, shrink and cool from yellow to red, plus a few sparks.
    // Presentation only; the player's own sprite color and hit blink are never touched.
    private static Texture2D softTexture;
    private static Material particleMaterial;
    private ParticleSystem flames, embers;
    private SpriteRenderer bodySprite;
    private bool fxBuilt;

    private void Awake() { health = GetComponent<PlayerHealth>(); }
    public void Apply(BossCombatController owner, BossCombatSettings data)
    {
        if (health == null || health.IsDead) return;
        bool fresh = !IsBurning;
        this.data = data;
        source = owner; damage = data.BurnTickDamage; interval = data.BurnTickInterval;
        endsAt = Time.time + data.BurnDuration;
        if (fresh) { nextTick = Time.time + interval; TickAttempts = TicksAccepted = 0; }
        BuildFx();
        PlayFx();
    }
    private void FixedUpdate()
    {
        if (endsAt <= 0f) return;
        if (health == null || health.IsDead) { Clear(); return; }
        // Include a tick exactly at the end; never run ticks beyond the saved deadline.
        while (Time.time + 0.0001f >= nextTick && nextTick <= endsAt + 0.0001f)
        {
            TickAttempts++;
            if (health.TakeDamage(new DamageInfo(damage, DamageSource.Enemy, source != null ? source.gameObject : null, "화상"))) TicksAccepted++;
            nextTick += interval;
        }
        if (Time.time >= endsAt) Clear();
    }
    private void LateUpdate()
    {
        if (!fxBuilt || !IsBurning || health == null || health.BodyCollider == null) return;
        // Follow the body: flames fill it from the feet to just above the head. World simulation lets them trail behind movement.
        Bounds bounds = health.BodyCollider.bounds;
        Vector2 root = new Vector2(Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.x)), Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.y)));
        Vector3 center = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * .45f, transform.position.z);
        ParticleSystem.ShapeModule flameShape = flames.shape;
        flameShape.position = transform.InverseTransformPoint(center);
        flameShape.scale = new Vector3(bounds.size.x * .8f / root.x, bounds.size.y * .8f / root.y, .05f);
        ParticleSystem.ShapeModule emberShape = embers.shape;
        emberShape.position = flameShape.position;
        emberShape.scale = flameShape.scale;
        int order = bodySprite != null ? bodySprite.sortingOrder : data.AttackOrder + 3;
        flames.GetComponent<ParticleSystemRenderer>().sortingOrder = order + 1;
        embers.GetComponent<ParticleSystemRenderer>().sortingOrder = order + 2;
    }
    public void Clear(BossCombatController owner = null)
    {
        if (owner != null && owner != source) return;
        endsAt = nextTick = 0f;
        if (!fxBuilt) return;
        // Stop emitting; what is already in the air fades out on its own.
        if (flames != null) flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (embers != null) embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
    private void OnDisable() { Clear(); }

    private void PlayFx()
    {
        if (flames != null && !flames.isEmitting) flames.Play(true);
        if (embers != null && !embers.isEmitting) embers.Play(true);
    }

    private void BuildFx()
    {
        if (fxBuilt) return;
        fxBuilt = true;
        bodySprite = GetComponentInChildren<SpriteRenderer>();
        Material material = ParticleMaterial();
        int layer = bodySprite != null ? bodySprite.sortingLayerID : 0;

        flames = CreateSystem("Burn flames", material, layer);
        ParticleSystem.MainModule main = flames.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .85f);
        main.startSize = new ParticleSystem.MinMaxCurve(.24f, .44f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 90;
        ParticleSystem.EmissionModule emission = flames.emission;
        emission.rateOverTime = 38f;
        ParticleSystem.VelocityOverLifetimeModule velocity = flames.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-.18f, .18f);
        velocity.y = new ParticleSystem.MinMaxCurve(.7f, 1.35f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        ParticleSystem.NoiseModule noise = flames.noise;
        noise.enabled = true;
        noise.strength = .28f;
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

        embers = CreateSystem("Burn sparks", material, layer);
        ParticleSystem.MainModule sparks = embers.main;
        sparks.startLifetime = new ParticleSystem.MinMaxCurve(.5f, 1f);
        sparks.startSize = new ParticleSystem.MinMaxCurve(.05f, .1f);
        sparks.maxParticles = 30;
        ParticleSystem.EmissionModule sparkEmission = embers.emission;
        sparkEmission.rateOverTime = 11f;
        ParticleSystem.VelocityOverLifetimeModule sparkVelocity = embers.velocityOverLifetime;
        sparkVelocity.enabled = true;
        sparkVelocity.space = ParticleSystemSimulationSpace.World;
        sparkVelocity.x = new ParticleSystem.MinMaxCurve(-.5f, .5f);
        sparkVelocity.y = new ParticleSystem.MinMaxCurve(1.2f, 2.3f);
        sparkVelocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        ParticleSystem.ColorOverLifetimeModule sparkColor = embers.colorOverLifetime;
        sparkColor.enabled = true;
        sparkColor.color = new ParticleSystem.MinMaxGradient(Fire(
            new Color(1f, 1f, .75f), new Color(1f, .85f, .3f), new Color(1f, .45f, .1f), new Color(.8f, .2f, .05f), 1f));
    }

    private ParticleSystem CreateSystem(string childName, Material material, int sortingLayer)
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

    private Material ParticleMaterial()
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
        particleMaterial = shader != null ? new Material(shader) : new Material(data.EffectMaterial);
        particleMaterial.mainTexture = softTexture;
        particleMaterial.hideFlags = HideFlags.DontSave;
        return particleMaterial;
    }
}
