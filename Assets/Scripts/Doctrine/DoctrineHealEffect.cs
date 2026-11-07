using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 교리 발동 피드백용 주변 연출. 회복(초록)이 원본이며 피해증가(연보라)도 같은 틀을 쓴다.
//
// 캐릭터 스프라이트를 직접 물들이지 않는다(몸 색이 변하지 않음).
// 대신 대상 둘레에 다음 요소가 생겼다가 천천히 사라진다:
//   1) 발밑에서 퍼지는 링 + 바닥 광원
//   2) 위로 솟는 얇은 빛줄기
//   3) 떠오르는 십자(+) 문양  ← 기준 이미지의 핵심
//   4) 작게 반짝이며 떠오르는 빛 알갱이
//
// 대상의 자식으로 두지 않고 씬 루트에 둔다.
// 플레이어는 좌우 반전을 localScale.x 부호로 하므로 자식으로 두면
// 이펙트까지 거울처럼 뒤집히고 몸 스케일로 찌그러진다.
//
// 같은 대상에는 하나만 유지한다(교체식 버프와 동일: 재발동하면 기존 연출을 끊고 새로 시작).
// EditMode 테스트에서 오브젝트가 남지 않도록 플레이 중에만 생성한다.
public sealed class DoctrineHealEffect : MonoBehaviour
{
    private static readonly Color DefaultTint = new Color(0.36f, 1f, 0.52f, 1f);
    private static readonly Color AttackBuffTint = new Color(0.78f, 0.62f, 1f, 1f);
    private static readonly Color SpeedBuffTint = new Color(0.48f, 0.84f, 1f, 1f);

    private enum EffectKind
    {
        Heal = 0,
        AttackBuff = 1,
        SpeedBuff = 2
    }

    // 대상 + 종류별로 하나만 유지한다. 이속과 피해증가가 동시에 걸려도
    // 서로의 연출을 지우지 않는다.
    private readonly struct EffectKey : System.IEquatable<EffectKey>
    {
        private readonly int targetId;
        private readonly EffectKind kind;

        public EffectKey(int targetId, EffectKind kind)
        {
            this.targetId = targetId;
            this.kind = kind;
        }

        public bool Equals(EffectKey other) => targetId == other.targetId && kind == other.kind;
        public override bool Equals(object obj) => obj is EffectKey other && Equals(other);
        public override int GetHashCode() => (targetId * 397) ^ (int)kind;
    }

    private static readonly Dictionary<EffectKey, DoctrineHealEffect> ActiveByTarget =
        new Dictionary<EffectKey, DoctrineHealEffect>();

    private const float TotalDuration = 1.5f;
    private const float RingDuration = 0.8f;
    private const float GroundGlowDuration = 1.05f;

    private const int CrossCount = 11;
    private const int OrbCount = 9;
    private const int BeamCount = 6;

    private static Sprite crossSprite;
    private static Sprite orbSprite;
    private static Sprite ringSprite;
    private static Sprite beamSprite;
    private static Sprite glowSprite;

    private Transform target;
    private SpriteRenderer sourceBody;

    private SpriteRenderer ring;
    private SpriteRenderer groundGlow;
    private Vector3 ringBaseScale;

    private SpriteRenderer[] crosses;
    private Vector3[] crossBase;
    private Vector2[] crossVelocity;
    private float[] crossBirth;
    private float[] crossLifetime;
    private float[] crossScale;
    private float[] crossSpin;
    private float[] crossSeed;

    private SpriteRenderer[] orbs;
    private Vector3[] orbBase;
    private Vector2[] orbVelocity;
    private float[] orbBirth;
    private float[] orbLifetime;
    private float[] orbScale;
    private float[] orbSeed;

    private SpriteRenderer[] beams;
    private Vector3[] beamBase;
    private float[] beamBirth;
    private float[] beamLifetime;
    private float[] beamWidth;
    private float[] beamHeight;
    private float[] beamSeed;
    private float[] beamSway;

    private Color tint;
    private Vector3 spawnCenter;
    private Vector3 feetPoint;
    private float bornAt;

    /// <summary>대상 주변에 회복 이펙트를 재생한다. tint를 생략하면 기본 초록.</summary>
    public static void Play(GameObject target, Color? tintOverride = null)
    {
        PlayInternal(target, tintOverride ?? DefaultTint, EffectKind.Heal, "Doctrine heal effect");
    }

    /// <summary>대상 주변에 피해증가 이펙트를 재생한다. 회복과 같은 모양, 연보라색.</summary>
    public static void PlayAttackBuff(GameObject target)
    {
        PlayInternal(target, AttackBuffTint, EffectKind.AttackBuff, "Doctrine attack buff effect");
    }

    /// <summary>대상 주변에 이속증가 이펙트를 재생한다. 회복과 같은 모양, 하늘색.</summary>
    public static void PlaySpeedBuff(GameObject target)
    {
        PlayInternal(target, SpeedBuffTint, EffectKind.SpeedBuff, "Doctrine speed buff effect");
    }

    private static void PlayInternal(GameObject target, Color tint, EffectKind kind, string rootName)
    {
        if (target == null || !Application.isPlaying)
        {
            return;
        }

        // 같은 대상·같은 종류의 이전 연출은 끊고 새로 시작한다.
        // 10초 쿨/5초 지속이라 겹치면 번잡하다.
        EffectKey key = new EffectKey(target.GetInstanceID(), kind);
        if (ActiveByTarget.TryGetValue(key, out DoctrineHealEffect previous) && previous != null)
        {
            ActiveByTarget.Remove(key);
            Destroy(previous.gameObject);
        }

        GameObject root = new GameObject(rootName);
        SceneManager.MoveGameObjectToScene(root, target.scene);
        DoctrineHealEffect effect = root.AddComponent<DoctrineHealEffect>();
        ActiveByTarget[key] = effect;
        effect.Init(target, tint, key);
    }

    private EffectKey boundKey;
    private bool hasBoundKey;

    private void OnDestroy()
    {
        if (hasBoundKey && ActiveByTarget.TryGetValue(boundKey, out DoctrineHealEffect current) && current == this)
        {
            ActiveByTarget.Remove(boundKey);
        }
    }

    private void Init(GameObject targetObject, Color tintColor, EffectKey key)
    {
        target = targetObject.transform;
        boundKey = key;
        hasBoundKey = true;
        tint = tintColor;

        sourceBody = FindBodyRenderer(targetObject);

        Bounds bounds = ResolveBounds(targetObject, sourceBody);
        spawnCenter = bounds.center;
        feetPoint = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

        Vector3 size = bounds.size;
        if (size.x <= 0.01f || size.y <= 0.01f)
        {
            size = new Vector3(1f, 1f, 1f);
        }

        int layer = sourceBody != null ? sourceBody.sortingLayerID : 0;
        int order = sourceBody != null ? sourceBody.sortingOrder : 0;

        // 1) 바닥: 퍼지는 링 + 은은한 광원. 캐릭터 뒤에 깔린다.
        groundGlow = CreateRenderer("GroundGlow", GlowSprite, layer, order - 2);
        groundGlow.transform.position = feetPoint + Vector3.up * 0.02f;
        groundGlow.transform.localScale = new Vector3(size.x * 2.6f, size.x * 0.9f, 1f);
        groundGlow.color = WithAlpha(tint, 0.5f);

        ring = CreateRenderer("Ring", RingSprite, layer, order - 1);
        ring.transform.position = feetPoint + Vector3.up * 0.03f;
        ringBaseScale = new Vector3(size.x * 3.1f, size.x * 1.05f, 1f);
        ring.transform.localScale = ringBaseScale * 0.7f;
        ring.color = WithAlpha(tint, 0.8f);

        CreateBeams(size, layer, order);
        CreateCrosses(size, layer, order);
        CreateOrbs(size, layer, order);

        bornAt = Time.time;
    }

    // 위로 솟는 얇은 빛줄기. 몸 뒤에서 올라와 실루엣을 가리지 않는다.
    private void CreateBeams(Vector3 size, int layer, int order)
    {
        beams = new SpriteRenderer[BeamCount];
        beamBase = new Vector3[BeamCount];
        beamBirth = new float[BeamCount];
        beamLifetime = new float[BeamCount];
        beamWidth = new float[BeamCount];
        beamHeight = new float[BeamCount];
        beamSeed = new float[BeamCount];
        beamSway = new float[BeamCount];

        for (int i = 0; i < BeamCount; i++)
        {
            // 몸 뒤(-2): 캐릭터를 초록으로 덮지 않고 뒤에서 빛만 비친다.
            SpriteRenderer beam = CreateRenderer("Beam" + i, BeamSprite, layer, order - 2);
            float x = Random.Range(-0.55f, 0.55f) * size.x;
            float z = Random.Range(-0.1f, 0.1f);
            beamBase[i] = new Vector3(spawnCenter.x + x, feetPoint.y, spawnCenter.z + z);
            beamBirth[i] = Random.Range(0f, 0.4f);
            beamLifetime[i] = Random.Range(0.55f, 0.95f);
            beamWidth[i] = size.x * Random.Range(0.12f, 0.24f);
            beamHeight[i] = size.y * Random.Range(1.8f, 3.2f);
            beamSeed[i] = Random.Range(0f, 10f);
            beamSway[i] = Random.Range(0.02f, 0.08f);

            beam.transform.position = beamBase[i];
            beam.transform.localScale = new Vector3(beamWidth[i], 0.01f, 1f);
            beam.color = ClearAlpha(tint);
            beams[i] = beam;
        }
    }

    // 떠오르는 초록 십자 문양. 기준 이미지의 주된 요소.
    // 캐릭터를 가리지 않도록 몸 바깥쪽에 배치한다.
    private void CreateCrosses(Vector3 size, int layer, int order)
    {
        crosses = new SpriteRenderer[CrossCount];
        crossBase = new Vector3[CrossCount];
        crossVelocity = new Vector2[CrossCount];
        crossBirth = new float[CrossCount];
        crossLifetime = new float[CrossCount];
        crossScale = new float[CrossCount];
        crossSpin = new float[CrossCount];
        crossSeed = new float[CrossCount];

        float bodyHalfWidth = Mathf.Max(0.12f, size.x * 0.5f);

        for (int i = 0; i < CrossCount; i++)
        {
            SpriteRenderer cross = CreateRenderer("Cross" + i, CrossSprite, layer, order + 4);

            // 60%는 몸 바깥 좌우에, 40%는 머리 위쪽에 배치해 실루엣을 비운다.
            bool side = i % 5 != 0;
            float x;
            if (side)
            {
                float sideSign = (i % 2 == 0) ? 1f : -1f;
                x = sideSign * (bodyHalfWidth + Random.Range(0.22f, 0.95f) * size.x);
            }
            else
            {
                x = Random.Range(-0.7f, 0.7f) * size.x;
            }

            float y = side
                ? Random.Range(-0.15f, 0.7f) * size.y
                : Random.Range(0.55f, 1.15f) * size.y;

            crossBase[i] = new Vector3(
                spawnCenter.x + x,
                feetPoint.y + y,
                spawnCenter.z);

            crossVelocity[i] = new Vector2(
                Random.Range(-0.18f, 0.18f),
                Random.Range(1.0f, 2.0f) * Mathf.Max(0.5f, size.y));
            crossBirth[i] = Random.Range(0f, 0.4f);
            crossLifetime[i] = Random.Range(0.6f, 1.05f);
            crossScale[i] = size.magnitude * Random.Range(0.09f, 0.18f);
            crossSpin[i] = Random.Range(-30f, 30f);
            crossSeed[i] = Random.Range(0f, 10f);

            cross.transform.position = crossBase[i];
            cross.transform.localScale = Vector3.one * crossScale[i];
            cross.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            cross.color = ClearAlpha(tint);
            crosses[i] = cross;
        }
    }

    // 작게 반짝이는 빛 알갱이.
    private void CreateOrbs(Vector3 size, int layer, int order)
    {
        orbs = new SpriteRenderer[OrbCount];
        orbBase = new Vector3[OrbCount];
        orbVelocity = new Vector2[OrbCount];
        orbBirth = new float[OrbCount];
        orbLifetime = new float[OrbCount];
        orbScale = new float[OrbCount];
        orbSeed = new float[OrbCount];

        float bodyHalfWidth = Mathf.Max(0.12f, size.x * 0.5f);

        for (int i = 0; i < OrbCount; i++)
        {
            SpriteRenderer orb = CreateRenderer("Orb" + i, OrbSprite, layer, order + 3);
            float sideSign = (i % 2 == 0) ? 1f : -1f;
            float x = sideSign * (bodyHalfWidth + Random.Range(0.05f, 0.8f) * size.x);
            float y = Random.Range(0.05f, 0.8f) * size.y;

            orbBase[i] = new Vector3(
                spawnCenter.x + x,
                feetPoint.y + y,
                spawnCenter.z);
            orbVelocity[i] = new Vector2(
                Random.Range(-0.3f, 0.3f),
                Random.Range(0.7f, 1.5f) * Mathf.Max(0.5f, size.y));
            orbBirth[i] = Random.Range(0f, 0.5f);
            orbLifetime[i] = Random.Range(0.45f, 0.9f);
            orbScale[i] = size.magnitude * Random.Range(0.03f, 0.065f);
            orbSeed[i] = Random.Range(0f, 10f);

            orb.transform.position = orbBase[i];
            orb.transform.localScale = Vector3.one * orbScale[i];
            orb.color = ClearAlpha(tint);
            orbs[i] = orb;
        }
    }

    private void LateUpdate()
    {
        float elapsed = Time.time - bornAt;

        UpdateRing(elapsed);
        UpdateGroundGlow(elapsed);
        UpdateBeams(elapsed);
        UpdateCrosses(elapsed);
        UpdateOrbs(elapsed);

        if (elapsed >= TotalDuration)
        {
            Destroy(gameObject);
        }
    }

    private void UpdateRing(float elapsed)
    {
        if (ring == null)
        {
            return;
        }

        float t = Mathf.Clamp01(elapsed / RingDuration);
        // 초반에 확 퍼졌다가 부드럽게 사라진다.
        float alpha = 0.8f * Mathf.Pow(1f - t, 1.3f);
        ring.color = WithAlpha(tint, alpha);

        float grow = Mathf.Lerp(0.7f, 1.45f, Mathf.SmoothStep(0f, 1f, t));
        ring.transform.localScale = new Vector3(
            ringBaseScale.x * grow,
            ringBaseScale.y * grow,
            1f);
    }

    private void UpdateGroundGlow(float elapsed)
    {
        if (groundGlow == null)
        {
            return;
        }

        float t = Mathf.Clamp01(elapsed / GroundGlowDuration);
        // 바닥이 은은하게 밝아졌다가 서서히 꺼진다.
        float pulse = Mathf.Sin(Mathf.Clamp01(elapsed / 0.12f) * Mathf.PI * 0.5f);
        float alpha = 0.5f * pulse * Mathf.Pow(1f - t, 1.5f);
        groundGlow.color = WithAlpha(tint, alpha);

        if (target != null)
        {
            groundGlow.transform.position = feetPoint + Vector3.up * 0.02f;
        }
    }

    private void UpdateBeams(float elapsed)
    {
        if (beams == null)
        {
            return;
        }

        for (int i = 0; i < beams.Length; i++)
        {
            SpriteRenderer beam = beams[i];
            if (beam == null)
            {
                continue;
            }

            float local = elapsed - beamBirth[i];
            if (local < 0f)
            {
                beam.color = ClearAlpha(tint);
                continue;
            }

            float t = local / Mathf.Max(0.01f, beamLifetime[i]);
            if (t >= 1f)
            {
                beam.color = ClearAlpha(tint);
                continue;
            }

            // 아래에서 위로 자라나듯 솟고, 꼭대기는 부드럽게 사라진다.
            float rise = Mathf.Clamp01(local / 0.14f);
            float alpha = 0.55f * rise * Mathf.Pow(1f - t, 1.6f);

            beam.color = WithAlpha(new Color(
                Mathf.Lerp(tint.r, 1f, 0.35f),
                Mathf.Lerp(tint.g, 1f, 0.25f),
                Mathf.Lerp(tint.b, 1f, 0.35f),
                1f), alpha);

            float sway = Mathf.Sin((local * 4f) + beamSeed[i]) * beamSway[i];
            beam.transform.position = beamBase[i] + new Vector3(sway, 0f, 0f);
            beam.transform.localScale = new Vector3(
                beamWidth[i] * Mathf.Lerp(0.7f, 1f, rise),
                beamHeight[i] * Mathf.Lerp(0.6f, 1f, rise),
                1f);
        }
    }

    private void UpdateCrosses(float elapsed)
    {
        if (crosses == null)
        {
            return;
        }

        for (int i = 0; i < crosses.Length; i++)
        {
            SpriteRenderer cross = crosses[i];
            if (cross == null)
            {
                continue;
            }

            float local = elapsed - crossBirth[i];
            if (local < 0f)
            {
                cross.color = ClearAlpha(tint);
                continue;
            }

            float t = local / Mathf.Max(0.01f, crossLifetime[i]);
            if (t >= 1f)
            {
                cross.color = ClearAlpha(tint);
                continue;
            }

            // 살짝 흔들리며 위로 떠오른다.
            Vector3 position = crossBase[i]
                + (Vector3)(crossVelocity[i] * local)
                + Vector3.up * (0.35f * local * local);
            position.x += Mathf.Sin((local * 2.4f) + crossSeed[i]) * 0.10f;
            cross.transform.position = position;

            // 나타날 때 부드럽게 커지고, 끝에서 천천히 바랜다.
            float appear = Mathf.Clamp01(local / 0.10f);
            float alpha = Mathf.Pow(1f - t, 1.15f) * appear;
            // 중심은 밝은 흰빛, 테두리는 초록으로 번진다.
            Color core = Color.Lerp(tint, Color.white, 0.35f);
            cross.color = WithAlpha(core, alpha);

            float scale = crossScale[i] * Mathf.Lerp(0.65f, 1.05f, appear);
            cross.transform.localScale = new Vector3(scale, scale, 1f);
            cross.transform.rotation = Quaternion.Euler(0f, 0f, crossSpin[i] * local);
        }
    }

    private void UpdateOrbs(float elapsed)
    {
        if (orbs == null)
        {
            return;
        }

        for (int i = 0; i < orbs.Length; i++)
        {
            SpriteRenderer orb = orbs[i];
            if (orb == null)
            {
                continue;
            }

            float local = elapsed - orbBirth[i];
            if (local < 0f)
            {
                orb.color = ClearAlpha(tint);
                continue;
            }

            float t = local / Mathf.Max(0.01f, orbLifetime[i]);
            if (t >= 1f)
            {
                orb.color = ClearAlpha(tint);
                continue;
            }

            Vector3 position = orbBase[i]
                + (Vector3)(orbVelocity[i] * local)
                + Vector3.up * (0.2f * local * local);
            position.x += Mathf.Sin((local * 6f) + orbSeed[i]) * 0.07f;
            orb.transform.position = position;

            float flicker = 0.7f + 0.3f * Mathf.Sin((local * 20f) + orbSeed[i]);
            float alpha = Mathf.Pow(1f - t, 1.1f) * flicker;
            orb.color = WithAlpha(Color.Lerp(tint, Color.white, 0.45f), alpha);

            float scale = orbScale[i] * Mathf.Lerp(0.7f, 1.1f, Mathf.Clamp01(local / 0.08f));
            orb.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private static SpriteRenderer FindBodyRenderer(GameObject target)
    {
        SpriteRenderer fallback = null;
        SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null)
            {
                continue;
            }

            if (renderer.gameObject.name.Contains("Body") ||
                renderer.gameObject.name.Contains("Sprite") ||
                renderer.gameObject.name.Contains("Visual"))
            {
                return renderer;
            }

            if (fallback == null || renderer.sortingOrder > fallback.sortingOrder)
            {
                fallback = renderer;
            }
        }

        return fallback;
    }

    private static Bounds ResolveBounds(GameObject target, SpriteRenderer body)
    {
        if (body != null)
        {
            return body.bounds;
        }

        Collider2D collider = target.GetComponentInChildren<Collider2D>();
        if (collider != null)
        {
            return collider.bounds;
        }

        return new Bounds(target.transform.position, Vector3.one);
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

    // 가운데가 밝고 가장자리로 부드럽게 사라지는 원형 그라데이션.
    private static Sprite GlowSprite
    {
        get
        {
            if (glowSprite == null)
            {
                glowSprite = CreateRadialSprite(64, 2.2f, "DoctrineHealGlow");
            }

            return glowSprite;
        }
    }

    // 작은 빛 알갱이.
    private static Sprite OrbSprite
    {
        get
        {
            if (orbSprite == null)
            {
                orbSprite = CreateRadialSprite(24, 1.1f, "DoctrineHealOrb");
            }

            return orbSprite;
        }
    }

    // 기준 이미지의 초록 십자(+) 문양. 가장자리가 부드럽게 번진다.
    private static Sprite CrossSprite
    {
        get
        {
            if (crossSprite == null)
            {
                crossSprite = CreateCrossSprite(96, "DoctrineHealCross");
            }

            return crossSprite;
        }
    }

    // 바닥에 퍼지는 타원 링.
    private static Sprite RingSprite
    {
        get
        {
            if (ringSprite == null)
            {
                ringSprite = CreateRingSprite(128, "DoctrineHealRing");
            }

            return ringSprite;
        }
    }

    // 위로 솟는 빛줄기. 아래가 밝고 위로 갈수록 사라진다.
    private static Sprite BeamSprite
    {
        get
        {
            if (beamSprite == null)
            {
                beamSprite = CreateBeamSprite(16, 96, "DoctrineHealBeam");
            }

            return beamSprite;
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

    // 십자(+) 모양. 둥근 막대 두 개를 합치고 중심에 밝은 코어를 준다.
    private static Sprite CreateCrossSprite(int size, string textureName)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = textureName;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float half = size * 0.5f;

        const float armRadius = 0.26f;   // 막대 두께(정규화)
        const float armReach = 0.66f;    // 막대 길이(중심에서 끝까지)
        const float soft = 0.11f;        // 가장자리 번짐
        const float halo = 6.0f;         // 바깥 번짐 감쇠
        const float haloStrength = 0.30f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - center) / half;
                float ny = (y - center) / half;

                // 가로 막대 / 세로 막대까지의 거리에서 두께를 뺀 값.
                float d1 = DistanceToSegment(nx, ny, -armReach, 0f, armReach, 0f) - armRadius;
                float d2 = DistanceToSegment(nx, ny, 0f, -armReach, 0f, armReach) - armRadius;
                float d = Mathf.Min(d1, d2);

                if (d > 0f)
                {
                    // 형태 바깥: 부드러운 잔광만 남긴다.
                    float glow = Mathf.Exp(-d * halo) * haloStrength;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(glow) * 255f));
                    continue;
                }

                // 형태 안쪽: 꽉 찬 몸통 + 중심 코어 + 안쪽 번짐.
                float body = Mathf.Clamp01(0.5f - d / soft);
                float coreDistance = Mathf.Sqrt(nx * nx + ny * ny);
                float core = Mathf.Clamp01(1f - coreDistance / 1.05f);
                float innerGlow = Mathf.Exp(d * 3.2f) * 0.35f;

                float alpha = Mathf.Clamp01(body * (0.42f + 0.58f * core) + innerGlow);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        return Bake(texture, pixels, size, textureName);
    }

    // 점과 선분 사이의 최단 거리.
    private static float DistanceToSegment(
        float px, float py, float ax, float ay, float bx, float by)
    {
        float dx = bx - ax;
        float dy = by - ay;
        float lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= 0.000001f)
        {
            return Mathf.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));
        }

        float t = ((px - ax) * dx + (py - ay) * dy) / lengthSquared;
        t = Mathf.Clamp01(t);
        float cx = ax + dx * t;
        float cy = ay + dy * t;
        return Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
    }

    // 가운데가 비어 있는 얇은 타원 링.
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

                // 링 본체 + 안쪽의 옅은 채움.
                float ringDistance = Mathf.Abs(distance - ringRadius);
                float ringAlpha = Mathf.Clamp01(1f - ringDistance / ringWidth);
                ringAlpha = Mathf.Pow(ringAlpha, 1.5f);

                float inner = Mathf.Clamp01(1f - distance / ringRadius);
                float innerAlpha = Mathf.Pow(inner, 3.5f) * 0.25f;

                float alpha = Mathf.Clamp01(ringAlpha + innerAlpha);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        return Bake(texture, pixels, size, textureName);
    }

    // 아래가 진하고 위로 갈수록 사라지는 세로 빛줄기.
    private static Sprite CreateBeamSprite(int width, int height, string textureName)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = textureName;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[width * height];
        float center = (width - 1) * 0.5f;

        for (int y = 0; y < height; y++)
        {
            // 0 = 아래, 1 = 위
            float v = (float)y / (height - 1);
            // 위로 갈수록 옅어진다.
            float vertical = Mathf.Pow(1f - v, 1.35f);
            // 맨 아래는 살짝 부드럽게 시작.
            vertical *= Mathf.Clamp01(v * 8f + 0.25f);

            for (int x = 0; x < width; x++)
            {
                float h = (x - center) / (width * 0.5f);
                float across = Mathf.Exp(-(h * h) / 0.14f);
                float alpha = Mathf.Clamp01(vertical * across);
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
