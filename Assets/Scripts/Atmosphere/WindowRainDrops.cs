using UnityEngine;

/// <summary>
/// Rain streaks inside a rectangle centred on this transform (world-unit local space), looking exactly like
/// the boss stage rain (BossRuinedSanctuary "Distant rainfall" and "Foreground rainfall" particle systems):
/// same streak size, speed, slant, colour and material. Every streak is clipped to the rectangle each frame,
/// so nothing is ever drawn outside the glass and no sprite mask or shader support is needed.
/// </summary>
public sealed class WindowRainDrops : MonoBehaviour
{
    // Boss rain values (particle start size x/y, velocity, start colour).
    private const float Speed = 18.1f;                  // |(-1.8, -18)|
    private const float Length = .48f;
    private static readonly Vector2 Direction = new Vector2(-1.8f, -18f).normalized;
    private static readonly Color DistantColor = new Color(.67f, .76f, .8f, .3f);
    private static readonly Color ForegroundColor = new Color(.67f, .76f, .8f, .18f);
    private const float DistantWidth = .025f, ForegroundWidth = .018f;
    private const float ForegroundShare = .2f;          // boss emits 400/s distant and 100/s foreground

    // The boss stage fills a screen; a window is tiny, so show denser rain per area than the boss stage does
    // (boss: about .28 streaks per square unit on screen) or a window would only ever hold one streak.
    private const float StreaksPerSquareUnit = 2.2f;
    private const int MinStreaks = 5, MaxStreaks = 60;

    private struct Drop
    {
        public Transform Transform;
        public SpriteRenderer Renderer;
        public Vector2 Head;     // lowest point of the streak
        public float Thickness;
        public Color Color;
    }

    private Drop[] drops;
    private float halfWidth, halfHeight, spawnRange;
    private float sidewaysPerFall;   // x travel per unit of downward travel

    public void Build(Sprite sprite, Material material, float width, float height, int sortingLayerId, int sortingOrder)
    {
        halfWidth = width * .5f;
        halfHeight = height * .5f;
        sidewaysPerFall = Direction.x / Direction.y;      // positive: x decreases as y decreases
        // Drops wait above the glass for a random distance before falling in, so rain arrives in a stream.
        spawnRange = height;

        float visibleArea = width * (height + Length);
        float visible = visibleArea * StreaksPerSquareUnit;
        // Share of a drop's life spent in view; the rest is waiting above the glass.
        float cycle = height + Length + spawnRange * .5f;
        int count = Mathf.Clamp(Mathf.RoundToInt(visible * cycle / (height + Length)), MinStreaks, MaxStreaks);

        drops = new Drop[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Drop");
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (material != null) renderer.sharedMaterial = material;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;

            Drop drop = default;
            drop.Transform = go.transform;
            drop.Renderer = renderer;
            bool foreground = Random.value < ForegroundShare;
            drop.Thickness = foreground ? ForegroundWidth : DistantWidth;
            drop.Color = foreground ? ForegroundColor : DistantColor;
            Respawn(ref drop);
            // Start part-way through its fall so rain is already falling when the scene opens.
            drop.Head += Direction * Random.Range(0f, halfHeight * 2f + Length + spawnRange);
            drops[i] = drop;
        }
    }

    private void Respawn(ref Drop drop)
    {
        float y = halfHeight + Length + Random.Range(0f, spawnRange);
        // Drops drift left while falling, so spawn extra to the right to feed the right-hand corner too.
        float drift = sidewaysPerFall * (y + halfHeight);
        drop.Head = new Vector2(Random.Range(-halfWidth, halfWidth + drift), y);
    }

    private void Update()
    {
        if (drops == null) return;
        float dt = Mathf.Min(Time.deltaTime, .05f);
        for (int i = 0; i < drops.Length; i++)
        {
            ref Drop drop = ref drops[i];
            drop.Head += Direction * (Speed * dt);
            if (drop.Head.y < -halfHeight - Length) Respawn(ref drop);
            Draw(ref drop);
        }
    }

    // Clip the streak (head .. head - direction * length) to the rectangle and place the sprite on what is left.
    private void Draw(ref Drop drop)
    {
        Vector2 tail = drop.Head - Direction * Length;
        if (!Clip(drop.Head, tail, out Vector2 a, out Vector2 b))
        {
            if (drop.Renderer.enabled) drop.Renderer.enabled = false;
            return;
        }

        Vector2 axis = b - a;
        float length = axis.magnitude;
        if (length < .005f)
        {
            if (drop.Renderer.enabled) drop.Renderer.enabled = false;
            return;
        }

        if (!drop.Renderer.enabled) drop.Renderer.enabled = true;
        axis /= length;
        drop.Transform.localPosition = (a + b) * .5f;
        drop.Transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-axis.x, axis.y) * Mathf.Rad2Deg);
        drop.Transform.localScale = new Vector3(drop.Thickness, length, 1f);
        drop.Renderer.color = drop.Color;
    }

    // Liang-Barsky segment clip against [-halfWidth, halfWidth] x [-halfHeight, halfHeight].
    private bool Clip(Vector2 p0, Vector2 p1, out Vector2 a, out Vector2 b)
    {
        float t0 = 0f, t1 = 1f;
        Vector2 d = p1 - p0;
        a = b = default;
        if (!ClipEdge(-d.x, p0.x + halfWidth, ref t0, ref t1)) return false;
        if (!ClipEdge(d.x, halfWidth - p0.x, ref t0, ref t1)) return false;
        if (!ClipEdge(-d.y, p0.y + halfHeight, ref t0, ref t1)) return false;
        if (!ClipEdge(d.y, halfHeight - p0.y, ref t0, ref t1)) return false;
        a = p0 + d * t0;
        b = p0 + d * t1;
        return true;
    }

    private static bool ClipEdge(float p, float q, ref float t0, ref float t1)
    {
        if (Mathf.Approximately(p, 0f)) return q >= 0f;
        float t = q / p;
        if (p < 0f)
        {
            if (t > t1) return false;
            if (t > t0) t0 = t;
        }
        else
        {
            if (t < t0) return false;
            if (t < t1) t1 = t;
        }
        return true;
    }
}
