using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A single deadline and strike. No trigger callbacks or delayed damage.</summary>
public sealed class BossCloudLightning : MonoBehaviour
{
    private BossCombatController owner;
    private BossAttackRecord record;
    private readonly List<BossAttackVisual> warnings = new List<BossAttackVisual>();
    private Text countdown;
    private Image timer;
    private Canvas canvas;
    private bool struck;
    public AttackGeometry Shelter { get; private set; }
    public bool HasStruck => struck;
    public SpriteRenderer ProtectionRenderer { get; private set; }

    // The entire real body must fit. Inclusive edges match the drawn rectangle;
    // tolerance only absorbs floating point round-off, not a gameplay margin.
    public static bool Contains(AttackGeometry shelter, Collider2D body)
    {
        if (body == null || !body.enabled || !body.gameObject.activeInHierarchy) return false;
        Bounds bounds = body.bounds;
        Vector2 min = shelter.Center - shelter.Size * .5f;
        Vector2 max = shelter.Center + shelter.Size * .5f;
        const float epsilon = .0001f;
        return bounds.min.x >= min.x - epsilon && bounds.max.x <= max.x + epsilon &&
            bounds.min.y >= min.y - epsilon && bounds.max.y <= max.y + epsilon;
    }
    public bool ContainsPlayer() => owner != null && Contains(Shelter, owner.PlayerCollider);

    public void Initialize(BossCombatController boss, BossAttackRecord attack, AttackGeometry shelter)
    {
        owner = boss; record = attack; Shelter = shelter;
        Vector2 min = attack.Geometry.Center - attack.Geometry.Size * .5f;
        Vector2 max = attack.Geometry.Center + attack.Geometry.Size * .5f;
        Vector2 safeMin = shelter.Center - shelter.Size * .5f;
        Vector2 safeMax = shelter.Center + shelter.Size * .5f;
        AddDanger(new Vector2(min.x, min.y), new Vector2(safeMin.x, max.y));
        AddDanger(new Vector2(safeMax.x, min.y), max);
        AddDanger(new Vector2(safeMin.x, safeMax.y), new Vector2(safeMax.x, max.y));
        var fill = Child("Shelter protection (exact bounds)").AddComponent<SpriteRenderer>();
        fill.sprite = boss.Settings.WarningPrefab.Renderer.sprite;
        fill.sharedMaterial = boss.Settings.EffectMaterial;
        fill.color = new Color(.2f, 1f, .8f, .18f);
        fill.sortingOrder = boss.Settings.WarningOrder + 2;
        SetWorldShape(fill.transform, shelter);
        ProtectionRenderer = fill;
        var border = Line("Shelter boundary", new Color(.3f, 1f, .85f), .035f);
        border.loop = true; border.positionCount = 4;
        border.SetPositions(new[] { new Vector3(safeMin.x,safeMin.y), new Vector3(safeMin.x,safeMax.y),
            new Vector3(safeMax.x,safeMax.y), new Vector3(safeMax.x,safeMin.y) });
        BuildCountdown();
    }
    private void AddDanger(Vector2 min, Vector2 max)
    {
        if (max.x <= min.x || max.y <= min.y) return;
        var warning = owner.GetComponent<BossAttackExecutor>().CreateWarning(new AttackGeometry((min + max) * .5f, max - min));
        warnings.Add(warning);
    }
    private GameObject Child(string label)
    {
        var child = new GameObject(label); child.transform.SetParent(transform, false); return child;
    }
    private void SetWorldShape(Transform target, AttackGeometry shape)
    {
        target.position = shape.Center;
        target.localScale = new Vector3(shape.Size.x / transform.lossyScale.x, shape.Size.y / transform.lossyScale.y, 1f);
    }
    private LineRenderer Line(string label, Color color, float width)
    {
        var line = Child(label).AddComponent<LineRenderer>();
        line.useWorldSpace = true; line.sharedMaterial = owner.Settings.EffectMaterial;
        line.startWidth = line.endWidth = width; line.startColor = line.endColor = color;
        line.sortingOrder = owner.Settings.AttackOrder + 1;
        return line;
    }
    private void BuildCountdown()
    {
        var panel = new GameObject("Lightning shelter countdown", typeof(RectTransform), typeof(Canvas));
        panel.transform.SetParent(transform, false);
        canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 60;
        ((RectTransform)panel.transform).sizeDelta = new Vector2(290f, 52f);
        var background = panel.AddComponent<Image>();
        background.color = new Color(.025f,.035f,.065f,.93f); background.raycastTarget = false;
        countdown = new GameObject("Rule and deadline", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        countdown.transform.SetParent(panel.transform, false);
        countdown.font = owner.PresentationFont; countdown.fontSize = 20; countdown.alignment = TextAnchor.MiddleCenter;
        countdown.color = Color.white; countdown.raycastTarget = false;
        countdown.horizontalOverflow = HorizontalWrapMode.Overflow;
        countdown.verticalOverflow = VerticalWrapMode.Overflow;
        countdown.rectTransform.sizeDelta = new Vector2(290f, 48f);
        timer = new GameObject("Time remaining", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        timer.transform.SetParent(panel.transform, false); timer.color = new Color(.3f, 1f, .85f); timer.raycastTarget = false;
        timer.rectTransform.pivot = new Vector2(0f,.5f); timer.rectTransform.anchoredPosition = new Vector2(-135f,-24f);
        PlaceCountdown();
    }
    private void LateUpdate() => PlaceCountdown();
    private void PlaceCountdown()
    {
        if (canvas == null) return;
        Camera view = Camera.main;
        float scale = view != null && view.orthographic ? view.orthographicSize * 2f / Mathf.Max(1,view.pixelHeight) : .015f;
        canvas.worldCamera = view;
        canvas.transform.position = Shelter.Center + Vector2.up * (Shelter.Size.y * .5f + 1.3f);
        canvas.transform.localScale = new Vector3(scale / transform.lossyScale.x, scale / transform.lossyScale.y, scale);
    }
    public void ShowCountdown(float remaining)
    {
        if (struck) return;
        countdown.text = $"번개까지 {remaining:0.0}초\n흰 구름 아래 청록색 범위로!";
        float progress = 1f - Mathf.Clamp01(remaining / (owner.Settings.CloudWarning + owner.Settings.CloudDuration));
        timer.rectTransform.sizeDelta = new Vector2(270f * (1f - progress), 4f);
        foreach (var warning in warnings)
        {
            warning.SetProgress(progress);
            warning.SetCue("번개 공격 범위", remaining, progress, owner);
        }
    }
    public void Strike()
    {
        if (struck || owner == null || !owner.IsAttackValid(record)) return;
        struck = true;
        countdown.text = "번개 발동!\n흰 구름 아래 보호";
        timer.rectTransform.sizeDelta = new Vector2(270f,4f);
        var data = owner.Settings;
        Vector2 min = record.Geometry.Center - record.Geometry.Size * .5f;
        Vector2 max = record.Geometry.Center + record.Geometry.Size * .5f;
        float safeLeft = Shelter.Center.x - Shelter.Size.x * .5f;
        float safeRight = Shelter.Center.x + Shelter.Size.x * .5f;
        int count = Mathf.Max(2, Mathf.CeilToInt(record.Geometry.Size.x / data.LightningSpacing));
        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(min.x + .08f, max.x - .08f, (i + .5f) / count);
            bool shieldedColumn = x >= safeLeft && x <= safeRight;
            float endY = shieldedColumn ? Shelter.Center.y + Shelter.Size.y * .5f + data.CloudSize.y : min.y;
            var line = Line("Downward lightning", new Color(1f,.94f,.55f), data.LightningWidth);
            line.positionCount = 9;
            for (int j = 0; j < 9; j++)
            {
                float jitter = j == 0 || j == 8 ? 0f : (j % 2 == 0 ? -.12f : .12f);
                float boltX = Mathf.Clamp(x + jitter, min.x, max.x);
                if (!shieldedColumn) boltX = x < safeLeft ? Mathf.Min(boltX, safeLeft - .01f) : Mathf.Max(boltX, safeRight + .01f);
                line.SetPosition(j, new Vector3(boltX, Mathf.Lerp(max.y, endY, j / 8f), 0f));
            }
        }
    }
    private void OnDisable()
    {
        foreach (var warning in warnings) if (warning != null && owner != null) owner.DestroyEffect(warning.gameObject);
        warnings.Clear();
    }
}
