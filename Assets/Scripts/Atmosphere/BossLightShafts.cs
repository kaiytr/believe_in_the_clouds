using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Boss stage mood: pale light falling through the broken roof, pools of light on the floor where it lands,
/// and dust drifting through. Layout matches the 90-unit ruined sanctuary.
/// Outside the arena the light is a handful of slanted shafts. Inside the arena the roof is hidden by the
/// storm clouds: the arena is split into light columns, and a column pours down from the sky whenever no
/// cloud is above it and draws back up when a cloud slides over it. Light never slides sideways.
/// </summary>
public sealed class BossLightShafts : MonoBehaviour
{
    private const float MinX = -38f, MaxX = 46f, MinGap = 3.5f, MaxGap = 13f;
    // Fallback arena span (centre 10.5, 30 wide); the real span is read from the arena trigger at load.
    private const float DefaultArenaMinX = -4.5f, DefaultArenaMaxX = 25.5f;
    private const float ColumnSpacing = 1.6f, ColumnWidth = 2.3f, ColumnMinLean = 4f, ColumnMaxLean = 16f, ColumnMargin = .2f;
    private const float PourSeconds = .9f, DrawBackSeconds = .5f;
    private const float FieldSearchInterval = .5f;
    private const float FloorY = -3.35f, TopY = 13f;
    private const float MinLean = 6f, MaxLean = 22f;
    private const int ShaftSortingOrder = -2;
    private static readonly Color Beam = new Color(1f, .93f, .78f);

    private struct Shaft
    {
        public SpriteRenderer Visual;
        public Light2D Pool;
        public Transform VisualTransform, PoolTransform;
        public float Phase, Speed, BaseAlpha, BaseIntensity;
        public float Open;     // 0 hidden .. 1 fully poured down
        public float X, Lean, Width; // arena columns only
    }

    private Shaft[] shafts;
    private Shaft[] columns;
    private BossCloudField field;
    private float nextFieldSearch;
    private float arenaMinX = DefaultArenaMinX, arenaMaxX = DefaultArenaMaxX;
    private Texture2D beamTexture, moteTexture;
    private Sprite beamSprite;
    private Material moteMaterial;

    private void Awake()
    {
        FindArenaSpan();
        beamSprite = BuildBeamSprite();
        var random = new System.Random();
        BuildShafts(random);
        BuildColumns(random);
        BuildMotes();
    }

    // The arena can be resized or moved in the scene, so follow its entry trigger instead of fixed numbers.
    private void FindArenaSpan()
    {
        BossEncounterTrigger arena = FindFirstObjectByType<BossEncounterTrigger>();
        BoxCollider2D box = arena != null ? arena.GetComponent<BoxCollider2D>() : null;
        if (box == null || box.bounds.size.x < 1f) return;
        arenaMinX = box.bounds.min.x;
        arenaMaxX = box.bounds.max.x;
    }

    private void BuildShafts(System.Random random)
    {
        var built = new List<Shaft>();
        // Irregular gaps: some shafts crowd together, others leave wide dark stretches.
        float x = MinX + Range(random, 0f, 4f);
        while (x < MaxX)
        {
            if (x >= arenaMinX && x <= arenaMaxX) { x = arenaMaxX + Range(random, 1f, 4f); continue; }
            // Either side, with its own tilt, so the rays cross the room instead of running in parallel.
            float lean = Range(random, MinLean, MaxLean) * (random.NextDouble() < .5 ? -1f : 1f);
            float width = Range(random, 1.2f, 4f);
            built.Add(CreateBeam(built.Count, x, lean, width, random));
            x += Range(random, MinGap, MaxGap);
        }
        shafts = built.ToArray();
    }

    private void BuildColumns(System.Random random)
    {
        // Every column leans its own way, like the shafts outside the arena. Slanted beams land away from
        // where they start, so columns start a little beyond both walls to keep the whole floor lit.
        float pad = (TopY - FloorY) * Mathf.Tan(ColumnMaxLean * Mathf.Deg2Rad);
        float start = arenaMinX - pad;
        int count = Mathf.CeilToInt((arenaMaxX + pad - start) / ColumnSpacing);
        columns = new Shaft[count];
        for (int i = 0; i < count; i++)
        {
            float x = start + (i + .5f) * ColumnSpacing;
            float lean = Range(random, ColumnMinLean, ColumnMaxLean) * (random.NextDouble() < .5 ? -1f : 1f);
            columns[i] = CreateBeam(100 + i, x, lean, ColumnWidth, random);
            columns[i].Open = 0f;
            columns[i].X = x; columns[i].Lean = lean; columns[i].Width = ColumnWidth;
            columns[i].BaseAlpha = Range(random, .05f, .075f);
            columns[i].BaseIntensity = Range(random, .5f, .65f);
        }
    }

    private Shaft CreateBeam(int index, float x, float lean, float width, System.Random random)
    {
        var holder = new GameObject("Light shaft " + index);
        holder.transform.SetParent(transform, false);
        var visual = holder.AddComponent<SpriteRenderer>();
        visual.sprite = beamSprite;
        visual.sortingOrder = ShaftSortingOrder;
        AtmosphereMaterials.ApplyUnlit(visual);

        var poolObject = new GameObject("Light pool");
        poolObject.transform.SetParent(transform, false);
        var pool = poolObject.AddComponent<Light2D>();
        pool.lightType = Light2D.LightType.Point;
        pool.color = Beam;
        pool.pointLightInnerRadius = 0f;
        pool.falloffIntensity = .55f;
        pool.shadowsEnabled = false;

        var shaft = new Shaft
        {
            Visual = visual, Pool = pool,
            VisualTransform = holder.transform, PoolTransform = poolObject.transform,
            Phase = Range(random, 0f, 6.28f),
            Speed = Range(random, .25f, .55f),
            BaseAlpha = Range(random, .08f, .18f),
            BaseIntensity = Range(random, .7f, 1.1f),
            Open = 1f
        };
        Place(shaft, x, lean, width, 1f);
        return shaft;
    }

    // `x` is where the beam starts at the roof line; the beam leans by `lean` degrees and is `width` wide.
    // `reach` (0..1) is how far down it has poured: the top stays at the roof and the lower end advances.
    private static void Place(Shaft shaft, float x, float lean, float width, float reach)
    {
        float fullLength = (TopY - FloorY) / Mathf.Cos(lean * Mathf.Deg2Rad);
        float length = fullLength * Mathf.Max(.001f, reach);
        shaft.VisualTransform.position = new Vector3(x, TopY, 0f);
        shaft.VisualTransform.rotation = Quaternion.Euler(0f, 0f, lean);
        // The sprite is a quarter unit wide per unit of height.
        shaft.VisualTransform.localScale = new Vector3(width * 4f, length, 1f);

        shaft.PoolTransform.position = new Vector3(x, TopY, 0f);
        shaft.PoolTransform.rotation = Quaternion.Euler(0f, 0f, 180f + lean);
        float spread = 2f * Mathf.Atan(width * .5f / fullLength) * Mathf.Rad2Deg;
        shaft.Pool.pointLightInnerAngle = Mathf.Max(2f, spread * .8f);
        shaft.Pool.pointLightOuterAngle = Mathf.Max(6f, spread * 1.6f);
        shaft.Pool.pointLightOuterRadius = length + 2f;
    }

    private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

    private void Update()
    {
        if (shafts == null) return;
        for (int i = 0; i < shafts.Length; i++) Shine(shafts[i], 1f);
        UpdateColumns();
    }

    private void Shine(Shaft s, float open)
    {
        if (s.Visual == null) return;
        float eased = Mathf.SmoothStep(0f, 1f, open);
        float breathe = .5f + .5f * Mathf.Sin(Time.time * s.Speed + s.Phase);
        float k = (.65f + breathe * .35f) * eased;
        Color color = Beam;
        color.a = s.BaseAlpha * k;
        s.Visual.color = color;
        s.Visual.enabled = eased > .005f;
        if (s.Pool != null) s.Pool.intensity = s.BaseIntensity * k;
    }

    // A column pours down while the sky above it is open and retracts upward once a cloud covers it.
    private void UpdateColumns()
    {
        if (field == null && Time.time >= nextFieldSearch)
        {
            nextFieldSearch = Time.time + FieldSearchInterval;
            field = FindFirstObjectByType<BossCloudField>();
        }
        bool clouds = field != null && field.IsPrepared;
        for (int i = 0; i < columns.Length; i++)
        {
            Shaft s = columns[i];
            bool open = !clouds || !Covered(s.X, Mathf.Tan(s.Lean * Mathf.Deg2Rad));
            float rate = open ? 1f / PourSeconds : -1f / DrawBackSeconds;
            s.Open = Mathf.Clamp01(s.Open + rate * Time.deltaTime);
            columns[i] = s;
            if (s.Open > .001f) Place(s, s.X, s.Lean, s.Width, Mathf.SmoothStep(0f, 1f, s.Open));
            // Faint while it is only a short stub, full once it has travelled a little.
            Shine(s, Mathf.Clamp01(s.Open * 4f));
        }
    }

    // The beam is slanted, so test where it crosses the cloud layer rather than where it starts.
    private bool Covered(float topX, float slope)
    {
        foreach (BossCloudPlatform cloud in field.StormClouds)
        {
            if (cloud == null) continue;
            float crossing = topX + (TopY - cloud.Bottom) * slope;
            if (crossing > cloud.Left - ColumnMargin && crossing < cloud.Right + ColumnMargin) return true;
        }
        return false;
    }

    // Soft-edged shaft: fades at both sides and thins out towards the bottom. Pivot is the top centre.
    private Sprite BuildBeamSprite()
    {
        const int width = 32, height = 128;
        beamTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Boss light shaft", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave
        };
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            float along = y / (height - 1f);            // 0 bottom, 1 top
            float length = Mathf.Pow(Mathf.Lerp(.08f, 1f, along), 1.2f);
            for (int x = 0; x < width; x++)
            {
                float side = Mathf.Abs((x + .5f) / width * 2f - 1f);
                float edge = 1f - Mathf.SmoothStep(.25f, 1f, side);
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * edge * length));
            }
        }
        beamTexture.SetPixels32(pixels);
        beamTexture.Apply(false, true);
        // One world unit tall, a quarter unit wide; scaled per shaft.
        return Sprite.Create(beamTexture, new Rect(0, 0, width, height), new Vector2(.5f, 1f), height, 0, SpriteMeshType.FullRect);
    }

    private void BuildMotes()
    {
        var holder = new GameObject("Dust motes");
        holder.transform.SetParent(transform, false);
        holder.transform.position = new Vector3((MinX + MaxX) * .5f, FloorY + (TopY - FloorY) * .45f, 0f);
        var particles = holder.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 13f);
        main.startSize = new ParticleSystem.MinMaxCurve(.05f, .13f);
        main.startSpeed = 0f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .93f, .78f, .35f), new Color(1f, 1f, 1f, .65f));
        main.maxParticles = 160;
        var emission = particles.emission;
        emission.rateOverTime = 14f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(MaxX - MinX, (TopY - FloorY) * .9f, 1f);
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-.08f, .18f);
        velocity.y = new ParticleSystem.MinMaxCurve(-.12f, .1f);
        velocity.z = 0f;
        var noise = particles.noise;
        noise.enabled = true;
        noise.strength = .25f;
        noise.frequency = .18f;
        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .2f), new GradientAlphaKey(1f, .8f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        moteTexture = BuildSoftDot();
        moteMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = moteTexture, hideFlags = HideFlags.DontSave };
        var renderer = holder.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = moteMaterial;
        renderer.sortingOrder = ShaftSortingOrder + 1;
        particles.Play();
    }

    private static Texture2D BuildSoftDot()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Boss dust mote", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * size * .5f) / (size * .5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f)));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void OnDestroy()
    {
        if (beamSprite != null) Destroy(beamSprite);
        if (beamTexture != null) Destroy(beamTexture);
        if (moteMaterial != null) Destroy(moteMaterial);
        if (moteTexture != null) Destroy(moteTexture);
    }
}
