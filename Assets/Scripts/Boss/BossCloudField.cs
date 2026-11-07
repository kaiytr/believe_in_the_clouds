using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent arena clouds: 2-3 moving storm clouds and a few moving white landing platforms.
/// Prepared once per arena (idempotent), independent of the combat lanes and of phase changes, and
/// released only on boss death, boss disable, arena release or scene end.
/// </summary>
public sealed class BossCloudField : MonoBehaviour
{
    private readonly List<BossCloudPlatform> storms = new List<BossCloudPlatform>();
    private readonly List<BossCloudPlatform> whites = new List<BossCloudPlatform>();
    private readonly List<(float left, float right)> intervalBuffer = new List<(float, float)>();
    private readonly List<(float left, float right)> stormIntervalBuffer = new List<(float, float)>();
    private readonly List<(float left, float right)> sortBuffer = new List<(float, float)>();
    private BossCombatController owner;
    private Transform root;
    private BossCloudLightning lightning;
    private Collider2D riderCollider;
    private Rigidbody2D riderBody;
    private float nextRiderSearch;

    public bool IsPrepared => root != null && storms.Count > 0;
    public float FloorY { get; private set; }
    public Vector2 LimitsX { get; private set; }
    public IReadOnlyList<BossCloudPlatform> StormClouds => storms;
    public IReadOnlyList<BossCloudPlatform> WhiteClouds => whites;
    public bool LightningActive => lightning != null;
    public BossCloudLightning Lightning => lightning;

    public bool Prepare(BossCombatController boss)
    {
        owner = boss;
        if (IsPrepared) return true;
        BossCombatSettings data = boss.Settings;
        if (data == null || data.BlackCloudPrefab == null || data.WhiteCloudPrefab == null) return false;

        Release();
        LimitsX = boss.PatternXLimits;
        Physics2D.SyncTransforms();
        Bounds bodyBounds = boss.BodyCollider.bounds;
        FloorY = BossCombatPhysics.FindGround(new Vector2(bodyBounds.center.x, bodyBounds.min.y), .15f, 2f, boss, out Vector2 ground)
            ? ground.y : bodyBounds.min.y;

        root = new GameObject(boss.name + " clouds").transform;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root.gameObject, boss.gameObject.scene);
        float arenaWidth = LimitsX.y - LimitsX.x;

        int stormCount = Random.Range(data.StormCloudCountRange.x, data.StormCloudCountRange.y + 1);
        float stormWidth = Mathf.Min(arenaWidth, Mathf.Max(data.StormCloudSize.x, arenaWidth / stormCount * data.StormCloudCoverage));
        float stormTop = FloorY + data.CloudTopHeight;
        if (boss.ArenaCamera != null && boss.ArenaCamera.TryGetStormCloudTop(
                boss.PlayerHealth != null ? boss.PlayerHealth.transform : null, out float cameraTop))
            stormTop = cameraTop;
        float stormY = stormTop - data.StormCloudSize.y * .5f;
        for (int i = 0; i < stormCount; i++)
        {
            // One slot per storm cloud: with 3 clouds the arena starts almost fully covered, then they wander and overlap.
            float slot = arenaWidth / stormCount;
            float slotCenter = LimitsX.x + slot * (i + .5f);
            float half = stormWidth * .5f;
            // Each cloud drifts only around its own slot, so together they keep covering almost the whole arena.
            // The outermost clouds may leave the arena by a fraction of their own width, so the edge is not a hard wall.
            float overhang = stormWidth * data.StormCloudOverhang;
            float driftMin = slotCenter - half - data.StormCloudWander, driftMax = slotCenter + half + data.StormCloudWander;
            var drift = new Vector2(i == 0 ? Mathf.Min(driftMin, LimitsX.x - overhang) : Mathf.Max(LimitsX.x, driftMin),
                i == stormCount - 1 ? Mathf.Max(driftMax, LimitsX.y + overhang) : Mathf.Min(LimitsX.y, driftMax));
            float x = Mathf.Clamp(slotCenter, drift.x + half, Mathf.Max(drift.x + half, drift.y - half));
            var cloud = Instantiate(data.BlackCloudPrefab, root);
            cloud.name = "Storm cloud " + (i + 1);
            cloud.Initialize(boss, false, new AttackGeometry(new Vector2(x, stormY), new Vector2(stormWidth, data.StormCloudSize.y)));
            cloud.Bind(this, drift, data.StormCloudSpeed, data.CloudTurnInterval);
            storms.Add(cloud);
        }

        float whiteWidth = Mathf.Min(data.CloudSize.x, arenaWidth);
        float whiteY = FloorY + data.WhiteCloudTopHeight - data.CloudSize.y * .5f;
        for (int i = 0; i < data.WhiteCloudCount; i++)
        {
            // One slot per white cloud keeps the initial layout spread out; they wander freely afterwards.
            float slot = arenaWidth / data.WhiteCloudCount;
            float x = LimitsX.x + slot * (i + .5f) + Random.Range(-slot * .2f, slot * .2f);
            x = Mathf.Clamp(x, LimitsX.x + whiteWidth * .5f, Mathf.Max(LimitsX.x + whiteWidth * .5f, LimitsX.y - whiteWidth * .5f));
            var cloud = Instantiate(data.WhiteCloudPrefab, root);
            cloud.name = "White cloud " + (i + 1);
            cloud.Initialize(boss, true, new AttackGeometry(new Vector2(x, whiteY), new Vector2(whiteWidth, data.CloudSize.y)));
            cloud.Bind(this, LimitsX, data.WhiteCloudSpeed, data.CloudTurnInterval);
            whites.Add(cloud);
        }
        return true;
    }

    public void Release()
    {
        CancelLightning();
        storms.Clear();
        whites.Clear();
        if (root != null)
        {
            root.gameObject.SetActive(false);
            Destroy(root.gameObject);
        }
        root = null;
        riderCollider = null;
        riderBody = null;
    }

    /// <summary>Starts one lightning (warning, deadline, single strike). Refuses a second while one is in progress.</summary>
    /// <summary>Pause / resume every cloud's random walk (e.g. while a cutscene needs a still arena).</summary>
    public void SetMotionEnabled(bool enabled)
    {
        foreach (BossCloudPlatform cloud in storms) if (cloud != null) cloud.MotionEnabled = enabled;
        foreach (BossCloudPlatform cloud in whites) if (cloud != null) cloud.MotionEnabled = enabled;
    }

    public bool BeginLightning()
    {
        if (!IsPrepared || lightning != null || owner == null || !owner.CanContinue) return false;
        var holder = new GameObject("Lightning");
        holder.transform.SetParent(root, false);
        lightning = holder.AddComponent<BossCloudLightning>();
        if (!lightning.Initialize(owner, this))
        {
            Destroy(holder);
            lightning = null;
            return false;
        }
        return true;
    }

    public void CancelLightning()
    {
        if (lightning != null)
        {
            lightning.gameObject.SetActive(false);
            Destroy(lightning.gameObject);
        }
        lightning = null;
    }

    internal void LightningEnded(BossCloudLightning ended)
    {
        if (lightning == ended) lightning = null;
    }

    /// <summary>The one geometry shared by the lightning warning and the damage test: storm range minus shelter volumes.</summary>
    public void ComputeDangerRects(List<AttackGeometry> output)
    {
        output.Clear();
        if (!IsPrepared) return;
        float stormBottom = storms[0].Bottom;
        for (int i = 1; i < storms.Count; i++) stormBottom = Mathf.Min(stormBottom, storms[i].Bottom);
        Merge(storms, stormIntervalBuffer);
        Merge(whites, intervalBuffer);
        float whiteBottom = whites.Count > 0 ? whites[0].Bottom : stormBottom;
        for (int i = 1; i < whites.Count; i++) whiteBottom = Mathf.Min(whiteBottom, whites[i].Bottom);

        foreach ((float left, float right) storm in stormIntervalBuffer)
        {
            float cursor = storm.left;
            foreach ((float left, float right) white in intervalBuffer)
            {
                float l = Mathf.Max(storm.left, white.left), r = Mathf.Min(storm.right, white.right);
                if (r <= l) continue;
                if (l > cursor) AddRect(output, cursor, l, FloorY, stormBottom);
                AddRect(output, l, r, whiteBottom, stormBottom);
                cursor = r;
            }
            if (cursor < storm.right) AddRect(output, cursor, storm.right, FloorY, stormBottom);
        }
    }

    private static void AddRect(List<AttackGeometry> output, float left, float right, float bottom, float top)
    {
        if (right - left <= .0001f || top - bottom <= .0001f) return;
        output.Add(new AttackGeometry(new Vector2((left + right) * .5f, (bottom + top) * .5f), new Vector2(right - left, top - bottom)));
    }

    private void Merge(List<BossCloudPlatform> clouds, List<(float left, float right)> output)
    {
        output.Clear();
        var sorted = sortBuffer;
        sorted.Clear();
        foreach (BossCloudPlatform cloud in clouds) if (cloud != null) sorted.Add((cloud.Left, cloud.Right));
        sorted.Sort((a, b) => a.left.CompareTo(b.left));
        foreach ((float left, float right) span in sorted)
        {
            if (output.Count > 0 && span.left <= output[output.Count - 1].right)
                output[output.Count - 1] = (output[output.Count - 1].left, Mathf.Max(output[output.Count - 1].right, span.right));
            else output.Add(span);
        }
    }

    /// <summary>Union of the storm clouds' lightning columns (floor to cloud underside), ignoring shelters.</summary>
    public bool OverlapsStormRange(Collider2D body)
    {
        if (!IsPrepared || body == null) return false;
        foreach (BossCloudPlatform storm in storms)
        {
            var column = new AttackGeometry(new Vector2(storm.Position.x, (FloorY + storm.Bottom) * .5f),
                new Vector2(storm.Size.x, storm.Bottom - FloorY));
            if (column.Overlaps(body)) return true;
        }
        return false;
    }

    /// <summary>Shelter volume under a white cloud: its width, floor up to the cloud underside.</summary>
    public AttackGeometry ShelterOf(BossCloudPlatform white) =>
        new AttackGeometry(new Vector2(white.Position.x, (FloorY + white.Bottom) * .5f), new Vector2(white.Size.x, white.Bottom - FloorY));

    /// <summary>The whole body fits under a single white cloud. Overlapping storms never cancel this.</summary>
    public bool IsInsideShelter(Collider2D body)
    {
        foreach (BossCloudPlatform white in whites)
            if (white != null && BossCloudLightning.Contains(ShelterOf(white), body)) return true;
        return false;
    }

    public bool TryGetRider(out Collider2D collider, out Rigidbody2D body)
    {
        if ((riderCollider == null || riderBody == null) && Time.time >= nextRiderSearch)
        {
            nextRiderSearch = Time.time + .25f;
            GameObject actor = GameObject.FindGameObjectWithTag("Player");
            PlayerHealth health = actor != null ? actor.GetComponent<PlayerHealth>() : null;
            if (health != null && health.BodyCollider != null)
            {
                riderCollider = health.BodyCollider;
                riderBody = actor.GetComponent<Rigidbody2D>();
            }
        }
        collider = riderCollider; body = riderBody;
        return collider != null && body != null && collider.enabled;
    }

    private void OnDisable() => Release();
    private void OnDestroy() => Release();
}
