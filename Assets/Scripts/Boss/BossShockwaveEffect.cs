using UnityEngine;

// 밝은 선두는 실제 파동 크기에 맞추고, 무해한 잔상과 지면 스파크는 짧고 옅게 남긴다.
// 모든 시각 오브젝트는 공격의 자식으로 두어 기존 취소/씬 전환 정리를 함께 따른다.
[DisallowMultipleComponent]
public sealed class BossShockwaveEffect : MonoBehaviour
{
    private const int TileSize = 64;
    private const int EchoCount = 6;
    private const int SparkCount = 18;
    private const float BladeWidth = 1.15f;
    private const float BladeHeight = 2.5f;
    private const float ShadowHeight = 2.62f;
    private const float GlowHeight = 2.8f;

    private struct Echo
    {
        public SpriteRenderer Renderer;
        public Vector3 Position;
        public float Age;
    }

    private struct Spark
    {
        public SpriteRenderer Renderer;
        public Vector3 Position;
        public Vector2 Velocity, Size;
        public float Age, Lifetime;
    }

    private BossGroundWave wave;
    private SpriteRenderer core, blade, shadow, glow, groundLight, launchRing, launchFlash;
    private Transform worldDetails;
    private Texture2D atlas;
    private Texture2D quadTexture;
    private Sprite quadSprite;
    private readonly Sprite[] sprites = new Sprite[4];
    private readonly Echo[] echoes = new Echo[EchoCount];
    private readonly Spark[] sparks = new Spark[SparkCount];
    private Vector2 size;
    private Vector3 previousCenter, launchPosition;
    private int direction, nextEcho, nextSpark;
    private float age, trailDistance;

    public void Initialize(BossGroundWave source, SpriteRenderer renderer, Vector2 waveSize, int sign,
        Material detailMaterial)
    {
        wave = source; core = renderer; size = waveSize; direction = sign < 0 ? -1 : 1;
        BuildSprites();
        core.flipX = direction < 0;
        core.color = Color.white;
        core.enabled = false;

        blade = Create("Animated ground blade", quadSprite, 0, transform);
        blade.transform.localScale = new Vector3(BladeWidth, BladeHeight, 1f);
        blade.transform.localPosition = Vector3.down * ((1f - BladeHeight) * .5f);
        blade.flipX = core.flipX;

        shadow = Create("Wave contrast edge", sprites[0], -1, transform, detailMaterial);
        shadow.flipX = core.flipX;
        shadow.transform.localScale = new Vector3(BladeWidth + .13f, ShadowHeight, 1f);
        shadow.transform.localPosition = Vector3.up * (-.5f + .485f * ShadowHeight) +
            Vector3.left * (direction * .018f);
        glow = Create("Soft white corona", sprites[1], -2, transform, detailMaterial);
        glow.flipX = core.flipX;
        glow.transform.localScale = new Vector3(BladeWidth + .37f, GlowHeight, 1f);
        glow.transform.localPosition = Vector3.up * (-.5f + .485f * GlowHeight);
        groundLight = Create("Ground light streak", sprites[2], -1, transform, detailMaterial);
        groundLight.transform.localPosition = new Vector3(-direction * .25f, -.43f, 0f);
        groundLight.transform.localScale = new Vector3(1.5f, .14f, 1f);

        worldDetails = new GameObject("World-space wave wake").transform;
        worldDetails.SetParent(transform, false);
        Vector3 scale = transform.lossyScale;
        worldDetails.localScale = new Vector3(1f / Mathf.Max(.001f, Mathf.Abs(scale.x)),
            1f / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1f);
        for (int i = 0; i < EchoCount; i++)
            echoes[i].Renderer = Create("Fading wave echo", sprites[0], -2, worldDetails, detailMaterial);
        for (int i = 0; i < SparkCount; i++)
            sparks[i].Renderer = Create("Ground spark", sprites[2], 1, worldDetails, detailMaterial);

        previousCenter = transform.position;
        launchPosition = previousCenter - Vector3.up * (size.y * .5f - .035f);
        // 양쪽 파동 중 한쪽에서만 중심 타격광을 만든다.
        if (direction > 0)
        {
            launchRing = Create("Ground impact ring", sprites[3], -1, worldDetails, detailMaterial);
            launchFlash = Create("Ground impact flash", sprites[2], 1, worldDetails, detailMaterial);
        }
        EmitSparks(previousCenter, 8);
        Present(0f);
    }

    private SpriteRenderer Create(string label, Sprite sprite, int orderOffset, Transform parent,
        Material material = null)
    {
        var child = new GameObject(label);
        child.layer = gameObject.layer;
        child.transform.SetParent(parent, false);
        var renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material != null ? material : core.sharedMaterial;
        renderer.sortingLayerID = core.sortingLayerID;
        renderer.sortingOrder = core.sortingOrder + orderOffset;
        renderer.enabled = false;
        return renderer;
    }

    private void LateUpdate()
    {
        if (core == null || wave == null) return;
        float dt = Time.deltaTime;
        age += dt;
        Vector3 center = transform.position;
        trailDistance += Vector3.Distance(previousCenter, center);
        previousCenter = center;
        if (!wave.Finished && trailDistance >= size.x * .45f)
        {
            trailDistance = 0f;
            ref Echo echo = ref echoes[nextEcho];
            echo.Position = center + Vector3.up * (size.y * (BladeHeight * .485f - 1f) * .5f); echo.Age = 0f;
            echo.Renderer.flipX = direction < 0;
            echo.Renderer.enabled = true;
            nextEcho = (nextEcho + 1) % EchoCount;
            EmitSparks(center, 2);
        }
        Present(dt);
    }

    private void Present(float dt)
    {
        float alpha = core.color.a;
        Show(blade, new Color(1f, 1f, 1f, alpha));
        Show(shadow, new Color(.07f, .12f, .19f, .72f * alpha));
        float shimmer = .92f + .08f * Mathf.Sin(age * 35f);
        Show(glow, new Color(.83f, .93f, 1f, .24f * shimmer * alpha));
        Show(groundLight, new Color(.94f, .98f, 1f, .42f * alpha));

        if (launchRing != null)
        {
            float t = Mathf.Clamp01(age / .22f);
            Place(launchRing, launchPosition, new Vector2(Mathf.Lerp(.25f, 1.45f, t), Mathf.Lerp(.08f, .32f, t)));
            Show(launchRing, new Color(.9f, .96f, 1f, (1f - t) * (1f - t) * .65f * alpha));
            Place(launchFlash, launchPosition, new Vector2(Mathf.Lerp(1.1f, .15f, t), .065f));
            Show(launchFlash, new Color(1f, 1f, 1f, (1f - Mathf.Clamp01(age / .10f)) * alpha));
        }
        for (int i = 0; i < EchoCount; i++)
        {
            ref Echo echo = ref echoes[i];
            if (!echo.Renderer.enabled) continue;
            echo.Age += dt;
            float t = Mathf.Clamp01(echo.Age / .13f);
            Place(echo.Renderer, echo.Position,
                new Vector2(size.x * BladeWidth, size.y * BladeHeight) * Mathf.Lerp(1f, .88f, t));
            Show(echo.Renderer, new Color(.87f, .94f, 1f, .23f * (1f - t) * (1f - t) * alpha));
        }
        for (int i = 0; i < SparkCount; i++)
        {
            ref Spark spark = ref sparks[i];
            if (!spark.Renderer.enabled) continue;
            spark.Age += dt;
            float t = Mathf.Clamp01(spark.Age / spark.Lifetime);
            spark.Velocity *= Mathf.Exp(-5f * dt);
            spark.Velocity.y -= 3f * dt;
            spark.Position += (Vector3)(spark.Velocity * dt);
            float angle = Mathf.Atan2(spark.Velocity.y, spark.Velocity.x) * Mathf.Rad2Deg;
            Place(spark.Renderer, spark.Position, spark.Size * Mathf.Lerp(1f, .25f, t), angle);
            Show(spark.Renderer, new Color(.95f, .98f, 1f, .8f * (1f - t) * (1f - t) * alpha));
        }
    }

    private void EmitSparks(Vector3 center, int count)
    {
        for (int i = 0; i < count; i++)
        {
            ref Spark spark = ref sparks[nextSpark];
            spark.Position = center + new Vector3(-direction * size.x * Random.Range(.1f, .5f),
                size.y * Random.Range(-.43f, -.25f), 0f);
            spark.Velocity = new Vector2(-direction * Random.Range(1.2f, 3.6f), Random.Range(.2f, 1.4f));
            spark.Size = new Vector2(size.x * Random.Range(.25f, .55f), size.y * Random.Range(.025f, .065f));
            spark.Age = 0f; spark.Lifetime = Random.Range(.12f, .23f);
            spark.Renderer.enabled = true;
            nextSpark = (nextSpark + 1) % SparkCount;
        }
    }

    private static void Show(SpriteRenderer renderer, Color color)
    {
        renderer.color = color;
        renderer.enabled = color.a > .002f;
    }

    private static void Place(SpriteRenderer renderer, Vector3 position, Vector2 scale, float angle = 0f)
    {
        renderer.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));
        renderer.transform.localScale = new Vector3(scale.x, scale.y, 1f);
    }

    private void BuildSprites()
    {
        atlas = new Texture2D(TileSize * sprites.Length, TileSize, TextureFormat.RGBA32, false);
        atlas.name = "White shockwave shapes";
        atlas.filterMode = FilterMode.Point;
        atlas.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[atlas.width * atlas.height];
        for (int tile = 0; tile < sprites.Length; tile++)
            for (int y = 0; y < TileSize; y++)
                for (int x = 0; x < TileSize; x++)
                {
                    float u = (x + .5f) / TileSize - .5f;
                    float v = (y + .5f) / TileSize - .5f;
                    pixels[y * atlas.width + tile * TileSize + x] = SampleShape(tile, u, v);
                }
        atlas.SetPixels32(pixels);
        atlas.Apply(false, true);
        for (int i = 0; i < sprites.Length; i++)
        {
            sprites[i] = Sprite.Create(atlas, new Rect(i * TileSize, 0, TileSize, TileSize),
                Vector2.one * .5f, TileSize, 0, SpriteMeshType.FullRect);
            sprites[i].name = "White shockwave shape " + i;
        }

        quadTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        quadTexture.name = "Shockwave shader quad";
        quadTexture.filterMode = FilterMode.Point;
        quadTexture.wrapMode = TextureWrapMode.Clamp;
        quadTexture.SetPixel(0, 0, Color.white);
        quadTexture.Apply(false, true);
        quadSprite = Sprite.Create(quadTexture, new Rect(0, 0, 1, 1),
            Vector2.one * .5f, 1f, 0, SpriteMeshType.FullRect);
        quadSprite.name = "Shockwave shader quad";
    }

    private static Color SampleShape(int shape, float x, float y)
    {
        float alpha;
        float along = Mathf.Clamp01(x + .5f);
        float crest = Mathf.Lerp(-.3f, .49f, Mathf.Pow(along, 1.65f)) +
            .0325f * Mathf.Sin(along * Mathf.PI);
        float bottom = -.485f;
        float ends = SmoothRange(-.5f, -.42f, x) * (1f - SmoothRange(.41f, .5f, x));
        if (shape == 0)
        {
            alpha = SmoothRange(bottom - .018f, bottom + .008f, y) *
                (1f - SmoothRange(crest - .012f, crest + .025f, y)) * ends;
            float ridge = 1f - SmoothRange(.008f, .026f, Mathf.Abs(y - (crest - .032f)));
            alpha = Mathf.Max(alpha, ridge * ends * .75f);
            float shade = Mathf.Lerp(.74f, 1f, Mathf.Clamp01((y - bottom) / Mathf.Max(.05f, crest - bottom)));
            return new Color(shade, Mathf.Lerp(.84f, 1f, shade), 1f, alpha);
        }
        if (shape == 1)
        {
            float outside = Mathf.Max(Mathf.Max(bottom - y, y - crest), 0f);
            alpha = Mathf.Exp(-Mathf.Pow(outside / .075f, 2f)) * ends;
        }
        else if (shape == 2)
        {
            float taper = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(x * 2f)), .65f);
            alpha = taper * Mathf.Clamp01((taper - Mathf.Abs(y * 2f)) * 3f);
        }
        else
        {
            float radius = new Vector2(x, y).magnitude;
            alpha = (1f - SmoothRange(.018f, .05f, Mathf.Abs(radius - .42f))) *
                Mathf.Lerp(.3f, 1f, Mathf.Abs(x * 2f));
        }
        return new Color(1f, 1f, 1f, alpha);
    }

    private static float SmoothRange(float min, float max, float value) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(min, max, value));

    private void OnDestroy()
    {
        foreach (Sprite sprite in sprites) if (sprite != null) Destroy(sprite);
        if (quadSprite != null) Destroy(quadSprite);
        if (quadTexture != null) Destroy(quadTexture);
        if (atlas != null) Destroy(atlas);
    }
}
