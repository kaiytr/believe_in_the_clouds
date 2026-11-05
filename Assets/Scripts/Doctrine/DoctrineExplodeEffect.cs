using UnityEngine;
using UnityEngine.SceneManagement;

// 폭발 교리 발동 시 터지는 순간 연출. A세트:
//   1) 코어 플래시 (0.1초): 흰색 원이 확 켜졌다 꺼진다
//   2) 충격파 링 (0.35초): 흰빛 링이 0.3배 → 1.0배로 퍼지며 페이드 (최종 크기 = 실제 데미지 반경)
//   3) 빛 글로우 (0.4초): 바닥에 깔리는 옅은 흰빛 광원
//   4) 스파크 (20~30개): 중심에서 방사형으로 튀고 중력 낙하, 흰색→옅은 회백색
//   5) 연기 (8~10개): 밝은 회색 원이 천천히 위로 오르며 0.8초간 사라진다
//
// 대상의 자식으로 두지 않고 씬 루트에 둔다.
// 플레이어는 좌우 반전을 localScale.x 부호로 하므로 자식으로 두면
// 이펙트까지 거울처럼 뒤집히고 몸 스케일로 찌그러진다.
//
// 폭발 쿨이 5초라 자주 터지므로 짧게(0.6~0.7초) 끊는다.
// EditMode 테스트에서 오브젝트가 남지 않도록 플레이 중에만 생성한다.
public sealed class DoctrineExplodeEffect : MonoBehaviour
{
    private static readonly Color CoreColor = new Color(1f, 1f, 1f, 1f);
    private static readonly Color RingColor = new Color(1f, 0.97f, 0.92f, 1f);
    private static readonly Color GlowColor = new Color(1f, 0.95f, 0.88f, 1f);
    private static readonly Color SparkHotColor = new Color(1f, 1f, 1f, 1f);
    private static readonly Color SparkMidColor = new Color(1f, 0.97f, 0.91f, 1f);
    private static readonly Color SparkColdColor = new Color(0.82f, 0.80f, 0.78f, 1f);
    private static readonly Color SmokeColor = new Color(0.80f, 0.79f, 0.78f, 1f);

    private const float TotalDuration = 0.7f;
    private const float FlashDuration = 0.1f;
    private const float RingDuration = 0.35f;
    private const float GlowDuration = 0.4f;
    private const float SmokeLifetimeMax = 0.8f;

    private const int SparkCount = 26;
    private const int SmokeCount = 9;

    private static Sprite coreSprite;
    private static Sprite ringSprite;
    private static Sprite glowSprite;
    private static Sprite sparkSprite;
    private static Sprite smokeSprite;

    private SpriteRenderer flash;
    private SpriteRenderer ring;
    private SpriteRenderer glow;
    private Vector3 ringBaseScale;
    private float blastRadius;

    private SpriteRenderer[] sparks;
    private Vector3[] sparkBase;
    private Vector2[] sparkVelocity;
    private float[] sparkBirth;
    private float[] sparkLifetime;
    private float[] sparkSeed;
    private float[] sparkLength;

    private SpriteRenderer[] smokes;
    private Vector3[] smokeBase;
    private Vector2[] smokeVelocity;
    private float[] smokeBirth;
    private float[] smokeLifetime;
    private float[] smokeScale;

    private Vector3 blastCenter;
    private float bornAt;

    /// <summary>폭발 지점에 연출을 재생한다. radius는 실제 데미지 반경.</summary>
    public static void Play(GameObject center, float radius)
    {
        if (center == null || !Application.isPlaying || radius <= 0f)
        {
            return;
        }

        PlayAt(center.transform.position, radius, center.scene,
            center.GetComponent<SpriteRenderer>() ?? center.GetComponentInChildren<SpriteRenderer>());
    }

    /// <summary>사망 시점(적 파괴 직전) 등 위치가 이미 확정된 경우 그 자리에서 재생한다.</summary>
    public static void PlayAt(Vector3 position, float radius, UnityEngine.SceneManagement.Scene scene)
    {
        if (!Application.isPlaying || radius <= 0f)
        {
            return;
        }

        PlayAt(position, radius, scene, null);
    }

    private static void PlayAt(
        Vector3 position,
        float radius,
        UnityEngine.SceneManagement.Scene scene,
        SpriteRenderer bodyHint)
    {
        GameObject root = new GameObject("Doctrine explode effect");
        SceneManager.MoveGameObjectToScene(root, scene);
        DoctrineExplodeEffect effect = root.AddComponent<DoctrineExplodeEffect>();
        effect.Init(position, Mathf.Max(0.5f, radius), bodyHint);
    }

    private void Init(Vector3 position, float radius, SpriteRenderer bodyHint)
    {
        blastCenter = position;
        blastRadius = radius;

        int layer = bodyHint != null ? bodyHint.sortingLayerID : 0;
        int order = bodyHint != null ? bodyHint.sortingOrder : 0;

        // 1) 코어 플래시: 흰색 원, radius*0.8. 캐릭터 앞(+5).
        flash = CreateRenderer("Flash", CoreSprite, layer, order + 5);
        flash.transform.position = blastCenter;
        flash.transform.localScale = Vector3.one * (blastRadius * 1.6f);
        flash.color = CoreColor;

        // 2) 충격파 링: 최종 크기가 실제 데미지 반경과 일치. 캐릭터 앞(+5).
        ring = CreateRenderer("Shockwave", RingSprite, layer, order + 5);
        ring.transform.position = blastCenter;
        ringBaseScale = Vector3.one * (blastRadius * 2f);
        ring.transform.localScale = ringBaseScale * 0.3f;
        ring.color = WithAlpha(RingColor, 0.9f);

        // 3) 빛 글로우: 바닥에 깔리는 타원. 캐릭터 뒤(-1).
        glow = CreateRenderer("FireGlow", GlowSprite, layer, order - 1);
        glow.transform.position = blastCenter + Vector3.down * (blastRadius * 0.15f);
        glow.transform.localScale = new Vector3(blastRadius * 1.8f, blastRadius * 0.9f, 1f);
        glow.color = WithAlpha(GlowColor, 0.7f);

        CreateSparks(layer, order);
        CreateSmoke(layer, order);

        bornAt = Time.time;
    }

    // 중심에서 방사형으로 튀는 불꽃. 흰색→옅은 회백색으로 식는다.
    private void CreateSparks(int layer, int order)
    {
        sparks = new SpriteRenderer[SparkCount];
        sparkBase = new Vector3[SparkCount];
        sparkVelocity = new Vector2[SparkCount];
        sparkBirth = new float[SparkCount];
        sparkLifetime = new float[SparkCount];
        sparkSeed = new float[SparkCount];
        sparkLength = new float[SparkCount];

        for (int i = 0; i < SparkCount; i++)
        {
            SpriteRenderer spark = CreateRenderer("Spark" + i, SparkSprite, layer, order + 5);
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float startRadius = Random.Range(0.05f, 0.35f) * blastRadius;
            sparkBase[i] = blastCenter + new Vector3(
                Mathf.Cos(angle) * startRadius,
                Mathf.Sin(angle) * startRadius * 0.7f,
                0f);

            float speed = Random.Range(2.5f, 7f) * blastRadius;
            sparkVelocity[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            sparkBirth[i] = Random.Range(0f, 0.06f);
            sparkLifetime[i] = Random.Range(0.25f, 0.5f);
            sparkSeed[i] = Random.Range(0f, 10f);

            float length = Random.Range(0.25f, 0.6f);
            spark.transform.position = sparkBase[i];
            spark.transform.localScale = new Vector3(0.12f, length, 1f);
            spark.transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
            spark.color = ClearAlpha(SparkHotColor);
            sparks[i] = spark;
            sparkLength[i] = length;
        }
    }

    // 천천히 위로 오르는 연기.
    private void CreateSmoke(int layer, int order)
    {
        smokes = new SpriteRenderer[SmokeCount];
        smokeBase = new Vector3[SmokeCount];
        smokeVelocity = new Vector2[SmokeCount];
        smokeBirth = new float[SmokeCount];
        smokeLifetime = new float[SmokeCount];
        smokeScale = new float[SmokeCount];

        for (int i = 0; i < SmokeCount; i++)
        {
            SpriteRenderer smoke = CreateRenderer("Smoke" + i, SmokeSprite, layer, order + 4);
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float startRadius = Random.Range(0f, 0.4f) * blastRadius;
            smokeBase[i] = blastCenter + new Vector3(
                Mathf.Cos(angle) * startRadius,
                Mathf.Sin(angle) * startRadius * 0.5f,
                0f);

            smokeVelocity[i] = new Vector2(
                Random.Range(-0.4f, 0.4f),
                Random.Range(0.8f, 1.8f));
            smokeBirth[i] = Random.Range(0.05f, 0.2f);
            smokeLifetime[i] = Random.Range(0.5f, SmokeLifetimeMax);
            smokeScale[i] = Random.Range(0.4f, 0.8f);

            smoke.transform.position = smokeBase[i];
            smoke.transform.localScale = Vector3.one * smokeScale[i];
            smoke.color = ClearAlpha(SmokeColor);
            smokes[i] = smoke;
        }
    }

    private void LateUpdate()
    {
        float elapsed = Time.time - bornAt;

        UpdateFlash(elapsed);
        UpdateRing(elapsed);
        UpdateGlow(elapsed);
        UpdateSparks(elapsed);
        UpdateSmoke(elapsed);

        if (elapsed >= TotalDuration)
        {
            Destroy(gameObject);
        }
    }

    private void UpdateFlash(float elapsed)
    {
        if (flash == null)
        {
            return;
        }

        float t = Mathf.Clamp01(elapsed / FlashDuration);
        // 첫 프레임에 확 켜졌다가 0.1초 만에 사라진다.
        float alpha = Mathf.Pow(1f - t, 1.2f);
        flash.color = WithAlpha(CoreColor, alpha);

        float shrink = Mathf.Lerp(1f, 0.85f, t);
        flash.transform.localScale = Vector3.one * (blastRadius * 1.6f * shrink);
    }

    private void UpdateRing(float elapsed)
    {
        if (ring == null)
        {
            return;
        }

        float t = Mathf.Clamp01(elapsed / RingDuration);
        float alpha = 0.9f * Mathf.Pow(1f - t, 1.4f);
        ring.color = WithAlpha(RingColor, alpha);

        // 0.3배에서 시작해 데미지 반경(1.0배)까지 퍼진다.
        float grow = Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0f, 1f, t));
        ring.transform.localScale = ringBaseScale * grow;
    }

    private void UpdateGlow(float elapsed)
    {
        if (glow == null)
        {
            return;
        }

        float t = Mathf.Clamp01(elapsed / GlowDuration);
        float alpha = 0.7f * Mathf.Pow(1f - t, 1.5f);
        glow.color = WithAlpha(GlowColor, alpha);
    }

    private void UpdateSparks(float elapsed)
    {
        if (sparks == null)
        {
            return;
        }

        for (int i = 0; i < sparks.Length; i++)
        {
            SpriteRenderer spark = sparks[i];
            if (spark == null)
            {
                continue;
            }

            float local = elapsed - sparkBirth[i];
            if (local < 0f)
            {
                spark.color = ClearAlpha(SparkHotColor);
                continue;
            }

            float t = local / Mathf.Max(0.01f, sparkLifetime[i]);
            if (t >= 1f)
            {
                spark.color = ClearAlpha(SparkHotColor);
                continue;
            }

            // 바깥으로 튀며 중력으로 떨어진다.
            Vector2 velocity = sparkVelocity[i];
            velocity.y -= 9f * local;
            Vector3 position = sparkBase[i]
                + (Vector3)(sparkVelocity[i] * local)
                + Vector3.down * (4.5f * local * local);
            position.x += Mathf.Sin((local * 8f) + sparkSeed[i]) * 0.05f;
            spark.transform.position = position;

            // 흰색→옅은 회백색으로 식으며 사라진다.
            Color color = t < 0.3f
                ? Color.Lerp(SparkHotColor, SparkMidColor, t / 0.3f)
                : Color.Lerp(SparkMidColor, SparkColdColor, (t - 0.3f) / 0.7f);
            spark.color = WithAlpha(color, 1f - t);

            // 진행 방향으로 눕히고 점점 짧아진다.
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f;
            spark.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            float shrink = Mathf.Lerp(1f, 0.3f, t);
            spark.transform.localScale = new Vector3(
                0.12f * shrink + 0.03f,
                sparkLength[i] * shrink,
                1f);
        }
    }

    private void UpdateSmoke(float elapsed)
    {
        if (smokes == null)
        {
            return;
        }

        for (int i = 0; i < smokes.Length; i++)
        {
            SpriteRenderer smoke = smokes[i];
            if (smoke == null)
            {
                continue;
            }

            float local = elapsed - smokeBirth[i];
            if (local < 0f)
            {
                smoke.color = ClearAlpha(SmokeColor);
                continue;
            }

            float t = local / Mathf.Max(0.01f, smokeLifetime[i]);
            if (t >= 1f)
            {
                smoke.color = ClearAlpha(SmokeColor);
                continue;
            }

            Vector3 position = smokeBase[i]
                + (Vector3)(smokeVelocity[i] * local)
                + Vector3.up * (0.3f * local * local);
            smoke.transform.position = position;

            // 서서히 부풀며 옅어진다.
            float alpha = 0.45f * Mathf.Sin(t * Mathf.PI);
            smoke.color = WithAlpha(SmokeColor, alpha);

            float grow = Mathf.Lerp(0.7f, 1.6f, t);
            smoke.transform.localScale = Vector3.one * (smokeScale[i] * grow);
        }
    }

    private SpriteRenderer CreateRenderer(string objectName, Sprite sprite, int layer, int order)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(transform, false);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerID = layer;
        renderer.sortingOrder = order;

        // 스프라이트 기본 머티리얼을 한 번만 공유해 드로우콜/할당을 줄인다.
        // 셰이더를 못 찾으면 기본 머티리얼을 그대로 써서(보이지 않게 되는 일 없이) 그린다.
        Material material = SharedSpriteMaterial;
        if (material != null)
        {
            renderer.sharedMaterial = material;
        }

        return renderer;
    }

    private static Material sharedSpriteMaterial;

    private static Material SharedSpriteMaterial
    {
        get
        {
            if (sharedSpriteMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    return null;
                }

                sharedSpriteMaterial = new Material(shader);
            }

            return sharedSpriteMaterial;
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private static Color ClearAlpha(Color color)
    {
        color.a = 0f;
        return color;
    }

    // ---------------------------------------------------------------- 스프라이트

    // 코어 플래시: 가운데가 가장 밝은 원.
    private static Sprite CoreSprite
    {
        get
        {
            if (coreSprite == null)
            {
                coreSprite = CreateRadialSprite(64, 1.4f, "DoctrineExplodeCore");
            }

            return coreSprite;
        }
    }

    // 충격파 링: 얇은 원형 테두리.
    private static Sprite RingSprite
    {
        get
        {
            if (ringSprite == null)
            {
                ringSprite = CreateRingSprite(128, "DoctrineExplodeRing");
            }

            return ringSprite;
        }
    }

    // 바닥 빛 광원.
    private static Sprite GlowSprite
    {
        get
        {
            if (glowSprite == null)
            {
                glowSprite = CreateRadialSprite(64, 2.2f, "DoctrineExplodeGlow");
            }

            return glowSprite;
        }
    }

    // 불꽃: 뾰족한 바늘 모양.
    private static Sprite SparkSprite
    {
        get
        {
            if (sparkSprite == null)
            {
                sparkSprite = CreateSparkSprite(8, 64, "DoctrineExplodeSpark");
            }

            return sparkSprite;
        }
    }

    // 연기: 부드러운 뭉게구름.
    private static Sprite SmokeSprite
    {
        get
        {
            if (smokeSprite == null)
            {
                smokeSprite = CreateRadialSprite(48, 1.1f, "DoctrineExplodeSmoke");
            }

            return smokeSprite;
        }
    }

    private static Sprite CreateRadialSprite(int size, float falloff, string textureName)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = textureName;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / radius;
                float dy = (y - center) / radius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = Mathf.Pow(alpha, falloff);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        return Bake(texture, pixels, size, textureName);
    }

    // 가운데가 비어 있는 얇은 원 링.
    private static Sprite CreateRingSprite(int size, string textureName)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = textureName;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = size * 0.5f;

        const float ringRadius = 0.82f;
        const float ringWidth = 0.085f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / radius;
                float dy = (y - center) / radius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                float ringDistance = Mathf.Abs(distance - ringRadius);
                float ringAlpha = Mathf.Clamp01(1f - ringDistance / ringWidth);
                ringAlpha = Mathf.Pow(ringAlpha, 1.5f);

                float inner = Mathf.Clamp01(1f - distance / ringRadius);
                float innerAlpha = Mathf.Pow(inner, 3.5f) * 0.2f;

                float alpha = Mathf.Clamp01(ringAlpha + innerAlpha);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        return Bake(texture, pixels, size, textureName);
    }

    // 불꽃용 뾰족한 바늘. 양 끝이 뾰족하고 가운데가 가장 밝다.
    private static Sprite CreateSparkSprite(int width, int height, string textureName)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = textureName;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            // 0 = 아래 끝, 1 = 위 끝
            float v = (float)y / (height - 1);
            // 양 끝을 뾰족하게: 가운데가 1, 끝으로 갈수록 0.
            float taper = Mathf.Pow(Mathf.Sin(v * Mathf.PI), 0.55f);
            float intensity = Mathf.Lerp(0.55f, 1f, v);

            for (int x = 0; x < width; x++)
            {
                float h = (x - (width - 1) * 0.5f) / (width * 0.5f);
                float across = Mathf.Clamp01(1f - Mathf.Abs(h));
                across = Mathf.Pow(across, 1.6f);

                float alpha = Mathf.Clamp01(taper * across * intensity);
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        return Bake(texture, pixels, width, height, textureName);
    }

    private static Sprite Bake(Texture2D texture, Color32[] pixels, int size, string textureName)
    {
        return Bake(texture, pixels, size, size, textureName);
    }

    private static Sprite Bake(Texture2D texture, Color32[] pixels, int width, int height, string textureName)
    {
        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            Mathf.Max(width, height));
        sprite.name = textureName;
        return sprite;
    }
}
