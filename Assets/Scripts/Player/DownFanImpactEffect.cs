using System.Collections.Generic;
using UnityEngine;

// 공중 아래 부채꼴 공격의 칼날 궤적 효과.
// 둥근 연기/불꽃 대신, 판정 부채꼴을 따라 얇은 초승달 아크가 그어지고
// 그 위로 날카로운 칼자국 스파크가 바깥으로 튄다.
//
// 오브젝트는 플레이어 자식이 아니라 씬 루트에 둔다.
// 플레이어는 좌우 반전을 localScale.x 부호로 하므로, 자식으로 두면
// 궤적 모양까지 거울처럼 뒤집히고 몸 스케일로 찌그러진다.
//
// 풀링: 파티클/아크 오브젝트를 재사용하므로 공격마다 Instantiate 하지 않는다.
public sealed class DownFanImpactEffect : MonoBehaviour
{
    [Header("칼날 아크")]
    [Tooltip("부채꼴을 따라 그어지는 초승달 궤적.")]
    [SerializeField] private bool spawnArc = true;
    [Tooltip("아크가 보이는 시간(초). 짧을수록 날카롭다.")]
    [SerializeField, Min(0.02f)] private float arcLifetime = 0.16f;
    [Tooltip("아크 띠의 안쪽 반경 비율. 0.55면 바깥쪽 45%만 칼날처럼 남는다.")]
    [SerializeField, Range(0.1f, 0.95f)] private float arcInnerRatio = 0.55f;
    [Tooltip("아크 띠 두께를 부채꼴 각도보다 좁게 잡아 끝을 뾰족하게 만든다.")]
    [SerializeField, Range(0.5f, 1f)] private float arcAngleScale = 0.96f;
    [SerializeField] private Color arcCoreColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private Color arcEdgeColor = new Color(1f, 0.72f, 0.28f, 0.75f);

    [Header("칼자국 스파크")]
    [SerializeField, Min(1)] private int particleCount = 22;
    [Tooltip("바깥으로 튀는 속도.")]
    [SerializeField, Min(0f)] private float speedMin = 6f;
    [SerializeField, Min(0f)] private float speedMax = 13f;
    [SerializeField, Min(0.02f)] private float lifetimeMin = 0.10f;
    [SerializeField, Min(0.02f)] private float lifetimeMax = 0.26f;
    [Tooltip("스파크 두께.")]
    [SerializeField, Min(0.01f)] private float widthMin = 0.045f;
    [SerializeField, Min(0.01f)] private float widthMax = 0.10f;
    [Tooltip("스파크 길이(칼날 방향으로 늘어난 길이).")]
    [SerializeField, Min(0.05f)] private float lengthMin = 0.35f;
    [SerializeField, Min(0.05f)] private float lengthMax = 0.95f;

    [Header("움직임")]
    [Tooltip("퍼지는 속도 감쇠. 낮을수록 곧게 뻗는다.")]
    [SerializeField, Min(0f)] private float drag = 1.2f;
    [Tooltip("아래로 떨어지는 가속도.")]
    [SerializeField, Min(0f)] private float gravity = 4f;

    [Header("모양")]
    [Tooltip("부채꼴 안쪽(중심에서 가까운) 비율. 0.25면 반경의 25%부터 시작.")]
    [SerializeField, Range(0f, 0.9f)] private float arcInnerRatioForParticles = 0.3f;
    [Tooltip("퍼짐 각도를 부채꼴보다 조금 좁게 잡아 테두리 밖으로 튀지 않게 한다.")]
    [SerializeField, Range(0.3f, 1f)] private float angleScale = 0.94f;

    [Header("색")]
    [SerializeField] private Color coreColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color edgeColor = new Color(1f, 0.62f, 0.18f, 1f);

    [Header("렌더")]
    [Tooltip("플레이어(0)보다 앞, 판정 박스(10)보다 뒤에 그린다.")]
    [SerializeField] private int sortingOrder = 6;

    private sealed class Particle
    {
        public Transform Transform;
        public SpriteRenderer Renderer;
        public Vector2 Velocity;
        public Color BaseColor;
        public float BornAt;
        public float Lifetime;
        public float StartWidth;
        public float StartLength;
    }

    private readonly List<Particle> pool = new List<Particle>();
    private readonly List<Particle> active = new List<Particle>();

    private Transform root;
    private Transform arcTransform;
    private MeshFilter arcFilter;
    private MeshRenderer arcRenderer;
    private Material arcMaterial;
    private float arcBornAt = -999f;
    private float arcLifetimeRuntime = 1f;
    private int sortingLayerId;
    private int sortingOrderOffset;

    private static Sprite sharpStreak;
    private static Material sharedSpriteMaterial;

    private void Awake()
    {
        EnsureRoot();
    }

    private void EnsureRoot()
    {
        if (root != null)
            return;

        GameObject holder = new GameObject(name + " slash");
        holder.transform.SetParent(null, false);
        // 씬 전환 시 이펙트만 남지 않게, 이 컴포넌트가 있는 씬에 둔다.
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder, gameObject.scene);
        root = holder.transform;
    }

    private void EnsureArc()
    {
        if (!spawnArc || arcTransform != null)
            return;

        GameObject go = new GameObject(name + " arc");
        go.transform.SetParent(root, false);
        arcFilter = go.AddComponent<MeshFilter>();
        arcRenderer = go.AddComponent<MeshRenderer>();

        if (sharedSpriteMaterial == null)
            sharedSpriteMaterial = new Material(Shader.Find("Sprites/Default"));
        arcMaterial = new Material(sharedSpriteMaterial);
        arcRenderer.material = arcMaterial;
        arcRenderer.sortingLayerID = sortingLayerId;
        arcRenderer.sortingOrder = sortingOrder + sortingOrderOffset - 1;

        arcTransform = go.transform;
        go.SetActive(false);
    }

    /// <summary>
    /// 아래 부채꼴 판정이 터진 지점에서 칼날 궤적을 그린다.
    /// origin은 월드 좌표, radius/halfAngle은 PlayerAttack 판정과 같은 값을 넘긴다.
    /// </summary>
    public void Play(Vector2 origin, float radius, float halfAngleDeg, float facingSign = 1f)
    {
        if (!isActiveAndEnabled || radius <= 0f)
            return;

        EnsureRoot();
        EnsureArc();

        float now = Time.time;
        float half = Mathf.Abs(halfAngleDeg);

        if (spawnArc && arcTransform != null)
            DrawArc(origin, radius, 270f, half);

        EmitSparks(origin, radius, 270f, half, now, radial: true);
    }

    /// <summary>
    /// 지상 베기 타격 지점에 칼날 궤적을 그린다.
    /// playerPos는 플레이어 위치(아크 중심), center는 판정 박스 중심이다.
    /// 박스 크기에 맞춰 반경/폭을 잡아 아래 공격과 같은 재질로 보인다.
    /// </summary>
    public void PlaySwing(Vector2 playerPos, Vector2 center, Vector2 size, float facingSign = 1f)
    {
        if (!isActiveAndEnabled)
            return;

        EnsureRoot();
        EnsureArc();

        Vector2 toCenter = center - playerPos;
        float distance = toCenter.magnitude;
        if (distance < 0.05f)
        {
            toCenter = new Vector2(facingSign >= 0f ? 1f : -1f, 0.25f);
            distance = toCenter.magnitude;
        }

        float centerAngle = Mathf.Atan2(toCenter.y, toCenter.x) * Mathf.Rad2Deg;
        float radius = distance + size.magnitude * 0.22f;
        if (radius <= 0f)
            return;

        float half = Mathf.Clamp(26f + size.magnitude * 4f, 25f, 45f);

        if (spawnArc && arcTransform != null)
            DrawArc(playerPos, radius, centerAngle, half);

        EmitSparks(playerPos, radius, centerAngle, half, Time.time, radial: false, facingSign);
    }

    // 스파크를 뿌린다. radial이 true면 아래 공격처럼 바깥으로 튀고,
    // false면 지상 베기처럼 궤접선 방향으로 흘러 칼자국처럼 보인다.
    private void EmitSparks(Vector2 origin, float radius, float centerAngleDeg, float halfAngleDeg,
        float now, bool radial, float facingSign = 1f)
    {
        float particleHalf = halfAngleDeg * Mathf.Clamp(angleScale, 0.3f, 1f);

        for (int i = 0; i < particleCount; i++)
        {
            float angle = centerAngleDeg + Random.Range(-particleHalf, particleHalf);
            float rad = angle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            float distance = radius * Random.Range(arcInnerRatioForParticles, 1f);

            Particle particle = Rent();
            if (particle == null)
                return;

            Vector2 spawn = origin + direction * distance;
            particle.Transform.position = spawn;

            float speed = Random.Range(speedMin, speedMax);
            // 중심에서 멀수록 바깥으로 더 세게 (칼끝이 튀는 느낌).
            float outward = Mathf.Clamp01(distance / Mathf.Max(0.01f, radius));
            Vector2 outwardVelocity = direction * speed * (0.65f + 0.7f * outward);
            // 지상 베기는 진행 방향(접선)으로 흘러 칼날이 지나간 자국처럼 보이게 한다.
            Vector2 tangentDir = new Vector2(-direction.y, direction.x) * Mathf.Sign(facingSign);
            float side = radial ? Random.Range(-2.5f, 2.5f) : Random.Range(2f, 7f);
            particle.Velocity = outwardVelocity + tangentDir * side;

            particle.StartWidth = Random.Range(widthMin, widthMax);
            particle.StartLength = Random.Range(lengthMin, lengthMax);
            particle.Transform.localScale = new Vector3(particle.StartWidth, particle.StartLength, 1f);
            // 스프라이트 긴 축이 Y이므로 진행 방향으로 눕힌다.
            particle.Transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(particle.Velocity.y, particle.Velocity.x) * Mathf.Rad2Deg - 90f);

            float t = Random.value;
            particle.BaseColor = Color.Lerp(coreColor, edgeColor, t);
            particle.BornAt = now;
            particle.Lifetime = Random.Range(lifetimeMin, lifetimeMax);

            particle.Renderer.color = particle.BaseColor;
            particle.Renderer.enabled = true;
            particle.Transform.gameObject.SetActive(true);
            active.Add(particle);
        }
    }

    // 판정을 따라가는 얇은 초승달 띠를 만든다.
    // 양 끝은 알파 0, 안쪽은 어둡고 바깥쪽은 밝게 해서 칼날 모서리처럼 보이게 한다.
    private void DrawArc(Vector2 origin, float radius, float centerAngleDeg, float halfAngleDeg)
    {
        const int segments = 28;

        Mesh mesh = arcFilter.mesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            arcFilter.mesh = mesh;
        }
        mesh.Clear();

        float innerRadius = radius * Mathf.Clamp01(arcInnerRatio);
        float half = halfAngleDeg * Mathf.Clamp01(arcAngleScale);
        float start = centerAngleDeg - half;
        float sweep = half * 2f;

        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = (start + sweep * t) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            int inner = i * 2;
            int outer = inner + 1;
            vertices[inner] = dir * innerRadius;
            vertices[outer] = dir * radius;

            // 끝으로 갈수록 투명하게 (뾰족한 칼끝).
            float ends = Mathf.Sin(t * Mathf.PI);
            ends = Mathf.Pow(Mathf.Clamp01(ends), 0.65f);

            Color innerColor = arcEdgeColor;
            innerColor.a *= ends * 0.35f;
            Color outerColor = Color.Lerp(arcEdgeColor, arcCoreColor, 0.75f);
            outerColor.a *= ends;

            colors[inner] = innerColor;
            colors[outer] = outerColor;

            if (i < segments)
            {
                int index = i * 6;
                triangles[index] = inner;
                triangles[index + 1] = outer;
                triangles[index + 2] = inner + 2;

                triangles[index + 3] = outer;
                triangles[index + 4] = outer + 2;
                triangles[index + 5] = inner + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        arcTransform.position = origin;
        arcTransform.rotation = Quaternion.identity;
        arcTransform.localScale = Vector3.one;
        arcMaterial.color = Color.white;
        arcLifetimeRuntime = Mathf.Max(0.02f, arcLifetime);
        arcBornAt = Time.time;
        arcTransform.gameObject.SetActive(true);
    }

    private Particle Rent()
    {
        Particle particle = null;
        if (pool.Count > 0)
        {
            particle = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
        }

        if (particle == null)
        {
            GameObject go = new GameObject("DownFanSlash");
            go.transform.SetParent(root, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();

            if (sharedSpriteMaterial == null)
                sharedSpriteMaterial = new Material(Shader.Find("Sprites/Default"));
            renderer.sharedMaterial = sharedSpriteMaterial;
            renderer.sprite = SharpStreakSprite;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder + sortingOrderOffset;
            renderer.enabled = false;
            go.SetActive(false);

            particle = new Particle
            {
                Transform = go.transform,
                Renderer = renderer
            };
        }

        if (particle.Transform == null || particle.Renderer == null)
            return null;

        particle.Renderer.sortingOrder = sortingOrder + sortingOrderOffset;
        particle.Renderer.sprite = SharpStreakSprite;
        return particle;
    }

    private void Update()
    {
        float now = Time.time;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            Particle particle = active[i];
            if (particle.Transform == null || particle.Renderer == null)
            {
                active.RemoveAt(i);
                continue;
            }

            float elapsed = now - particle.BornAt;
            float t = elapsed / Mathf.Max(0.01f, particle.Lifetime);
            if (t >= 1f)
            {
                Release(particle);
                active.RemoveAt(i);
                continue;
            }

            particle.Velocity *= 1f - Mathf.Clamp01(drag * Time.deltaTime);
            particle.Velocity.y -= gravity * Time.deltaTime;
            particle.Transform.position += (Vector3)(particle.Velocity * Time.deltaTime);

            // 칼날 방향으로 길게 늘어난 채 빠르게 줄어든다 (잔상 느낌).
            float shrink = Mathf.Lerp(1f, 0.15f, t * t);
            particle.Transform.localScale = new Vector3(
                particle.StartWidth * Mathf.Lerp(1f, 0.35f, t),
                particle.StartLength * shrink,
                1f);

            Color color = particle.BaseColor;
            color.a *= 1f - t * t;
            particle.Renderer.color = color;
        }

        if (arcTransform != null && arcTransform.gameObject.activeSelf)
        {
            float t = (now - arcBornAt) / Mathf.Max(0.01f, arcLifetimeRuntime);
            if (t >= 1f)
            {
                arcTransform.gameObject.SetActive(false);
            }
            else
            {
                Color color = Color.white;
                // 뒤쪽 절반에서 빠르게 바랜다.
                color.a = 1f - Mathf.Clamp01(t * 1.15f);
                arcMaterial.color = color;
                // 바깥으로 살짝 퍼지며 사라져 휘두른 궤적처럼 보인다.
                float grow = Mathf.Lerp(0.94f, 1.06f, t);
                arcTransform.localScale = new Vector3(grow, grow, 1f);
            }
        }
    }

    private void Release(Particle particle)
    {
        if (particle.Renderer != null)
            particle.Renderer.enabled = false;
        if (particle.Transform != null)
            particle.Transform.gameObject.SetActive(false);
        pool.Add(particle);
    }

    public void Clear()
    {
        for (int i = active.Count - 1; i >= 0; i--)
            Release(active[i]);
        active.Clear();

        if (arcTransform != null)
            arcTransform.gameObject.SetActive(false);
    }

    private void OnDisable() => Clear();

    private void OnDestroy()
    {
        if (arcMaterial != null)
            Destroy(arcMaterial);
        if (root != null)
            Destroy(root.gameObject);
    }

    // 칼날 스파크용 뾰족한 스프라이트.
    // 위아래로 뾰족하고 가운데가 가장 밝은 얇은 바늘 모양이라
    // 길게 늘리면 칼이 지나간 자국처럼 보인다.
    private static Sprite SharpStreakSprite
    {
        get
        {
            if (sharpStreak != null)
                return sharpStreak;

            const int width = 8;
            const int height = 64;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "DownFanSlashStreak";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                // 0 = 아래 끝, 1 = 위 끝
                float v = (float)y / (height - 1);
                // 양 끝을 뾰족하게: 가운데가 1, 끝으로 갈수록 0.
                float taper = Mathf.Pow(Mathf.Sin(v * Mathf.PI), 0.55f);
                // 세로 밝기: 한쪽 끝(칼끝)이 더 밝다.
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

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sharpStreak = Sprite.Create(texture, new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f), height);
            sharpStreak.name = "DownFanSlashStreak";
            return sharpStreak;
        }
    }

    /// <summary>공격하는 캐릭터의 스프라이트를 기준으로 정렬한다.</summary>
    public void SetSortingLayer(int layerId, int orderOffset = 0)
    {
        sortingLayerId = layerId;
        sortingOrderOffset = orderOffset;
        if (arcRenderer != null)
        {
            arcRenderer.sortingLayerID = layerId;
            arcRenderer.sortingOrder = sortingOrder + sortingOrderOffset - 1;
        }
    }
}
