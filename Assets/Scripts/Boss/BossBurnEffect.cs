using UnityEngine;

public sealed class BossBurnEffect : MonoBehaviour
{
    private PlayerHealth health;
    private BossCombatController source;
    private BossCombatSettings data;
    private float damage, interval, endsAt, nextTick;
    public float Remaining => Mathf.Max(0f, endsAt - Time.time);
    public bool IsBurning => Remaining > 0f;
    public int TickAttempts { get; private set; }
    public int TicksAccepted { get; private set; }

    // Fire as particles (see BossFireParticles), following the body. Presentation only; the player's own sprite
    // color and hit blink are never touched.
    private ParticleSystem flames, embers;
    private SpriteRenderer bodySprite;
    private bool fxBuilt;

    private void Awake() { health = GetComponent<PlayerHealth>(); }
    public void Apply(BossCombatController owner, BossCombatSettings data)
    {
        if (health == null || health.IsDead) return;
        bool fresh = !IsBurning;
        this.data = data;
        source = owner; damage = data.BurnTickDamage; interval = data.BurnTickInterval;
        endsAt = Time.time + data.BurnDuration;
        if (fresh) { nextTick = Time.time + interval; TickAttempts = TicksAccepted = 0; }
        BuildFx();
        PlayFx();
    }
    private void FixedUpdate()
    {
        if (endsAt <= 0f) return;
        if (health == null || health.IsDead) { Clear(); return; }
        // Include a tick exactly at the end; never run ticks beyond the saved deadline.
        while (Time.time + 0.0001f >= nextTick && nextTick <= endsAt + 0.0001f)
        {
            TickAttempts++;
            if (health.TakeDamage(new DamageInfo(damage, DamageSource.Enemy, source != null ? source.gameObject : null, "화상"))) TicksAccepted++;
            nextTick += interval;
        }
        if (Time.time >= endsAt) Clear();
    }
    private void LateUpdate()
    {
        if (!fxBuilt || !IsBurning || health == null || health.BodyCollider == null) return;
        // Follow the body: flames fill it from the feet to just above the head. World simulation lets them trail behind movement.
        Bounds bounds = health.BodyCollider.bounds;
        Vector2 root = new Vector2(Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.x)), Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.y)));
        Vector3 center = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * .45f, transform.position.z);
        ParticleSystem.ShapeModule flameShape = flames.shape;
        flameShape.position = transform.InverseTransformPoint(center);
        flameShape.scale = new Vector3(bounds.size.x * .8f / root.x, bounds.size.y * .8f / root.y, .05f);
        ParticleSystem.ShapeModule emberShape = embers.shape;
        emberShape.position = flameShape.position;
        emberShape.scale = flameShape.scale;
        int order = bodySprite != null ? bodySprite.sortingOrder : data.AttackOrder + 3;
        flames.GetComponent<ParticleSystemRenderer>().sortingOrder = order + 1;
        embers.GetComponent<ParticleSystemRenderer>().sortingOrder = order + 2;
    }
    public void Clear(BossCombatController owner = null)
    {
        if (owner != null && owner != source) return;
        endsAt = nextTick = 0f;
        if (!fxBuilt) return;
        // Stop emitting; what is already in the air fades out on its own.
        if (flames != null) flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (embers != null) embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
    private void OnDisable() { Clear(); }

    private void PlayFx()
    {
        if (flames != null && !flames.isEmitting) flames.Play(true);
        if (embers != null && !embers.isEmitting) embers.Play(true);
    }

    private void BuildFx()
    {
        if (fxBuilt) return;
        fxBuilt = true;
        bodySprite = GetComponentInChildren<SpriteRenderer>();
        int layer = bodySprite != null ? bodySprite.sortingLayerID : 0;
        BossFireParticles.Build(transform, layer, data.EffectMaterial, 1f, out flames, out embers);
    }
}
