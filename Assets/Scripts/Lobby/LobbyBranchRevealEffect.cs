using UnityEngine;
using UnityEngine.SceneManagement;

// A committed reveal only animates a temporary visual. The branch root and its
// interaction collider stay in place, and interruption restores the revealed door.
public sealed class LobbyBranchRevealEffect : MonoBehaviour
{
    private const float RumbleDuration = .12f;
    private const float SinkDuration = .36f;
    private const float BuriedDuration = .12f;
    private const float RiseDuration = .45f;
    private const float SettleDuration = .16f;
    private const float RiseAt = RumbleDuration + SinkDuration + BuriedDuration;
    private const float SettleAt = RiseAt + RiseDuration;
    private const float FinishAt = SettleAt + SettleDuration;
    private const int Capacity = 64;

    private struct Debris
    {
        public Vector2 Position, Velocity, Size;
        public Color Color;
        public float Age, Lifetime, Rotation, Spin;
        public bool Dust, Bounced;
    }

    private SpriteRenderer body, frame, visual;
    private Renderer labelRenderer;
    private TextMesh label;
    private Sprite revealedSprite;
    private Color revealedTint;
    private string revealedLabel;
    private bool bodyWasEnabled, frameWasEnabled, labelWasEnabled, captured, restored;
    private bool sank, emerged, landed;
    private float elapsed, depth, groundWidth;
    private GameObject debrisRoot;
    private Sprite shardSprite, dustSprite;
    private Texture2D shardTexture, dustTexture;
    private readonly Debris[] particles = new Debris[Capacity];
    private readonly SpriteRenderer[] debrisRenderers = new SpriteRenderer[Capacity];
    private int count;

    public bool IsPlaying { get; private set; }

    public bool Play(SpriteRenderer source, SpriteRenderer border, TextMesh roleLabel,
        Sprite targetSprite, Color targetTint, string targetLabel)
    {
        if (captured || source == null || source.sprite == null || targetSprite == null) return false;

        body = source; frame = border; label = roleLabel;
        revealedSprite = targetSprite; revealedTint = targetTint; revealedLabel = targetLabel;
        bodyWasEnabled = body.enabled;
        frameWasEnabled = frame != null && frame.enabled;
        labelRenderer = label != null ? label.GetComponent<Renderer>() : null;
        labelWasEnabled = labelRenderer != null && labelRenderer.enabled;
        Bounds bounds = body.bounds;
        float revealedHeight = targetSprite.bounds.size.y * Mathf.Abs(body.transform.lossyScale.y);
        depth = Mathf.Max(bounds.size.y, revealedHeight) + .15f;
        groundWidth = Mathf.Max(.5f, bounds.size.x * .9f);

        var holder = new GameObject("Branch reveal visual");
        holder.layer = body.gameObject.layer;
        holder.transform.SetParent(body.transform, false);
        visual = holder.AddComponent<SpriteRenderer>();
        visual.sprite = body.sprite;
        visual.sharedMaterial = body.sharedMaterial;
        visual.color = body.color;
        visual.sortingLayerID = body.sortingLayerID;
        visual.sortingOrder = body.sortingOrder;
        visual.flipX = body.flipX; visual.flipY = body.flipY;
        visual.drawMode = body.drawMode; visual.size = body.size;
        visual.maskInteraction = body.maskInteraction;

        captured = true; IsPlaying = true;
        body.enabled = false;
        if (frame != null) frame.enabled = false;
        if (labelRenderer != null) labelRenderer.enabled = false;
        BuildDebris(new Vector3(bounds.center.x, bounds.min.y + .025f, body.transform.position.z));
        Emit(6, 7, .5f);
        return true;
    }

    private void Update()
    {
        if (!captured) return;
        if (body == null) { Destroy(this); return; }
        float dt = Time.deltaTime;
        elapsed += dt;
        if (IsPlaying) AnimateDoor();
        bool debrisAlive = UpdateDebris(dt);
        if (!IsPlaying && !debrisAlive) Destroy(this);
    }

    private void AnimateDoor()
    {
        if (!sank && elapsed >= RumbleDuration)
        {
            sank = true;
            Emit(3, 4, .6f);
        }
        if (!emerged && elapsed >= RiseAt)
        {
            emerged = true;
            visual.sprite = revealedSprite;
            visual.color = revealedTint;
            Emit(16, 12, 1f);
        }
        if (!landed && elapsed >= SettleAt)
        {
            landed = true;
            Emit(4, 8, .45f);
        }
        if (elapsed >= FinishAt)
        {
            RestorePresentation();
            return;
        }

        float x = 0f, y;
        if (elapsed < RumbleDuration)
        {
            x = Mathf.Sin(elapsed * 100f) * .04f;
            y = 0f;
        }
        else if (elapsed < RumbleDuration + SinkDuration)
        {
            float t = (elapsed - RumbleDuration) / SinkDuration;
            y = -depth * t * t;
            x = Mathf.Sin(elapsed * 85f) * .025f * (1f - t);
        }
        else if (elapsed < RiseAt) y = -depth;
        else if (elapsed < SettleAt)
        {
            float t = (elapsed - RiseAt) / RiseDuration;
            y = Mathf.Lerp(-depth, .09f, 1f - Mathf.Pow(1f - t, 3f));
            x = Mathf.Sin(elapsed * 70f) * .015f * (1f - t);
        }
        else
        {
            float t = (elapsed - SettleAt) / SettleDuration;
            y = Mathf.Lerp(.09f, 0f, Mathf.SmoothStep(0f, 1f, t));
        }
        visual.transform.position = body.transform.position + new Vector3(x, y, 0f);
    }

    private void BuildDebris(Vector3 ground)
    {
        debrisRoot = new GameObject("Branch ground debris");
        SceneManager.MoveGameObjectToScene(debrisRoot, gameObject.scene);
        debrisRoot.transform.position = ground;
        shardSprite = CreateDebrisSprite(false, out shardTexture);
        dustSprite = CreateDebrisSprite(true, out dustTexture);
    }

    private static Sprite CreateDebrisSprite(bool dust, out Texture2D texture)
    {
        const int size = 8;
        texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = dust ? "Branch pixel dust" : "Branch stone chip";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int distance = Mathf.Max(Mathf.Abs(x * 2 - 7), Mathf.Abs(y * 2 - 7));
                bool corner = (x < 2 || x >= size - 2) && (y < 2 || y >= size - 2);
                float alpha = corner ? 0f : dust ? (distance >= 5 ? .45f : 1f) : 1f;
                float shade = dust ? 1f : Mathf.Lerp(.65f, 1f, (float)y / (size - 1));
                pixels[y * size + x] = new Color(shade, shade, shade, alpha);
            }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size, 0, SpriteMeshType.FullRect);
    }

    private void Emit(int stones, int dust, float strength)
    {
        if (debrisRoot == null) return;
        for (int i = 0; i < stones + dust && count < Capacity; i++)
        {
            bool isDust = i >= stones;
            float x = Random.Range(-groundWidth * .5f, groundWidth * .5f);
            float size = isDust ? Random.Range(.22f, .45f) : Random.Range(.07f, .17f);
            Color color = isDust
                ? new Color(.58f, .48f, .35f, .32f)
                : Color.Lerp(new Color(.31f, .28f, .23f), new Color(.67f, .61f, .49f), Random.value);
            Vector2 particleSize = new Vector2(size * (isDust ? 1.8f : Random.Range(.7f, 1.4f)), size);
            float rotation = isDust ? 0f : Random.Range(0f, 360f);
            var particleObject = new GameObject(isDust ? "Dust" : "Stone chip");
            particleObject.layer = body.gameObject.layer;
            particleObject.transform.SetParent(debrisRoot.transform, false);
            var renderer = particleObject.AddComponent<SpriteRenderer>();
            renderer.sprite = isDust ? dustSprite : shardSprite;
            renderer.sharedMaterial = body.sharedMaterial;
            renderer.sortingLayerID = body.sortingLayerID;
            renderer.sortingOrder = body.sortingOrder + 1;
            renderer.color = color;
            particleObject.transform.localPosition = new Vector3(x, .04f, 0f);
            particleObject.transform.localScale = new Vector3(particleSize.x, particleSize.y, 1f);
            particleObject.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            debrisRenderers[count] = renderer;
            particles[count++] = new Debris {
                Position = new Vector2(x, .04f),
                Velocity = new Vector2(Mathf.Sign(x) * Random.Range(.7f, 2.5f), Random.Range(2.8f, 5.7f)) * strength * (isDust ? .35f : 1f),
                Size = particleSize,
                Color = color, Lifetime = Random.Range(.55f, .95f), Dust = isDust,
                Rotation = rotation, Spin = Random.Range(-280f, 280f)
            };
        }
    }

    private bool UpdateDebris(float dt)
    {
        if (debrisRoot == null) return false;
        bool alive = false;
        for (int i = 0; i < count; i++)
        {
            Debris p = particles[i];
            SpriteRenderer renderer = debrisRenderers[i];
            p.Age += dt;
            float progress = Mathf.Clamp01(p.Age / p.Lifetime);
            if (progress >= 1f)
            {
                renderer.enabled = false;
                particles[i] = p;
                continue;
            }
            if (progress < 1f)
            {
                alive = true;
                if (p.Dust)
                {
                    p.Velocity *= Mathf.Exp(-3f * dt);
                    p.Velocity.y += .5f * dt;
                }
                else p.Velocity.y -= 15f * dt;
                p.Position += p.Velocity * dt;
                if (!p.Dust && p.Position.y < p.Size.y * .25f)
                {
                    p.Position.y = p.Size.y * .25f;
                    p.Velocity = new Vector2(p.Velocity.x * .5f, p.Bounced ? 0f : Mathf.Abs(p.Velocity.y) * .25f);
                    p.Bounced = true; p.Spin *= .4f;
                }
                if (!p.Dust) p.Rotation += p.Spin * dt;
            }
            particles[i] = p;
            Color color = p.Color;
            color.a *= p.Dust ? (1f - progress) : 1f - Mathf.InverseLerp(.65f, 1f, progress);
            float expansion = p.Dust ? 1f + progress * 1.5f : 1f;
            renderer.color = color;
            renderer.transform.localPosition = p.Position;
            renderer.transform.localRotation = Quaternion.Euler(0f, 0f, p.Rotation);
            renderer.transform.localScale = new Vector3(p.Size.x * expansion, p.Size.y * expansion, 1f);
        }
        return alive;
    }

    private void RestorePresentation()
    {
        if (!captured || restored) return;
        restored = true; IsPlaying = false;
        if (body != null) { body.sprite = revealedSprite; body.color = revealedTint; body.enabled = bodyWasEnabled; }
        if (frame != null) frame.enabled = frameWasEnabled;
        if (label != null) label.text = revealedLabel;
        if (labelRenderer != null) labelRenderer.enabled = labelWasEnabled;
        if (visual != null) { visual.gameObject.SetActive(false); Destroy(visual.gameObject); }
    }

    private void OnDisable()
    {
        RestorePresentation();
        ReleaseDebris();
    }

    private void OnDestroy() => ReleaseDebris();

    private void ReleaseDebris()
    {
        if (debrisRoot != null) { debrisRoot.SetActive(false); Destroy(debrisRoot); debrisRoot = null; }
        if (shardSprite != null) { Destroy(shardSprite); shardSprite = null; }
        if (dustSprite != null) { Destroy(dustSprite); dustSprite = null; }
        if (shardTexture != null) { Destroy(shardTexture); shardTexture = null; }
        if (dustTexture != null) { Destroy(dustTexture); dustTexture = null; }
    }
}
