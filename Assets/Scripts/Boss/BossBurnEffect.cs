using UnityEngine;

public sealed class BossBurnEffect : MonoBehaviour
{
    private PlayerHealth health;
    private BossCombatController source;
    private float damage, interval, endsAt, nextTick;
    private SpriteRenderer statusRenderer;
    public float Remaining => Mathf.Max(0f, endsAt - Time.time);
    public bool IsBurning => Remaining > 0f;
    public int TickAttempts { get; private set; }
    public int TicksAccepted { get; private set; }
    private void Awake() { health = GetComponent<PlayerHealth>(); }
    public void Apply(BossCombatController owner, BossCombatSettings data)
    {
        if (health == null || health.IsDead) return;
        bool fresh = !IsBurning;
        source = owner; damage = data.BurnTickDamage; interval = data.BurnTickInterval;
        endsAt = Time.time + data.BurnDuration;
        if (fresh) { nextTick = Time.time + interval; TickAttempts = TicksAccepted = 0; }
        if (statusRenderer == null)
        {
            var mark = new GameObject("Burn status (independent of hit blink)");
            mark.transform.SetParent(transform, false);
            statusRenderer = mark.AddComponent<SpriteRenderer>();
            statusRenderer.sprite = data.BurnSprite;
            statusRenderer.sharedMaterial = data.EffectMaterial;
            statusRenderer.sortingOrder = data.AttackOrder + 4;
        }
        statusRenderer.enabled = true;
        statusRenderer.color = new Color(1f, 0.4f, 0.08f, 0.9f);
    }
    private void FixedUpdate()
    {
        if (endsAt <= 0f) return;
        if (health == null || health.IsDead) { Clear(); return; }
        // Include a tick exactly at the end; never run ticks beyond the saved deadline.
        while (Time.time + 0.0001f >= nextTick && nextTick <= endsAt + 0.0001f)
        {
            TickAttempts++;
            if (health.TakeDamage(new DamageInfo(damage, DamageSource.Enemy))) TicksAccepted++;
            nextTick += interval;
        }
        if (Time.time >= endsAt) Clear();
    }
    private void LateUpdate()
    {
        if (statusRenderer == null || !IsBurning || health.BodyCollider == null) return;
        statusRenderer.transform.position = health.BodyCollider.bounds.center + Vector3.up * (health.BodyCollider.bounds.extents.y + 0.3f);
        // Root facing must not stretch or mirror the status marker.
        statusRenderer.transform.localScale = new Vector3(0.38f / Mathf.Abs(transform.lossyScale.x), 0.5f / Mathf.Abs(transform.lossyScale.y), 1f);
        statusRenderer.enabled = true;
    }
    public void Clear(BossCombatController owner = null)
    {
        if (owner != null && owner != source) return;
        endsAt = nextTick = 0f;
        if (statusRenderer != null) statusRenderer.enabled = false;
    }
    private void OnDisable() { Clear(); }
}
