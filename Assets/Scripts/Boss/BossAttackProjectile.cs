using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(BossAttackVisual))]
public sealed class BossAttackProjectile : MonoBehaviour
{
    private BossCombatController owner;
    private BossAttackRecord record;
    private BossAttackVisual visual;
    private Vector2 position, direction;
    private float speed, remaining, expiresAt;
    private bool blockedAtEnd;
    private Vector2 launchOrigin;
    private bool settled;   // evade already settled; the projectile just keeps flying
    /// <summary>The attack is already settled as an evade; what is left is only the shot flying on.</summary>
    public bool IsSettled => settled;
    public BossAttackRecord Record => record;
    private ParticleSystem flames, embers;
    public void Initialize(BossCombatController boss, BossAttackRecord attack, Vector2 origin, Vector2 aim, float travelSpeed, float distance, float lifetime, bool endBlocked)
    {
        owner = boss; record = attack; position = origin; launchOrigin = origin; direction = aim;
        speed = travelSpeed; remaining = distance; expiresAt = Time.time + lifetime;
        blockedAtEnd = endBlocked;
        visual = GetComponent<BossAttackVisual>();
        Present();
        if (record.BurnEnhanced) AttachFire();
    }

    // A burning shot trails the same fire the burning player gets. The particles live in world space, so they
    // stream out behind the projectile as it flies.
    private void AttachFire()
    {
        float diameter = owner.Settings.ProjectileDiameter;
        SpriteRenderer body = visual.Renderer;
        BossFireParticles.Build(transform, body.sortingLayerID, owner.Settings.EffectMaterial, .6f, out flames, out embers);
        foreach (ParticleSystem system in new[] { flames, embers })
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.position = Vector3.zero;
            shape.scale = new Vector3(diameter * .8f, diameter * .8f, .05f);
        }
        flames.GetComponent<ParticleSystemRenderer>().sortingOrder = body.sortingOrder + 1;
        embers.GetComponent<ParticleSystemRenderer>().sortingOrder = body.sortingOrder + 2;
        flames.Play(true);
        embers.Play(true);
    }
    private bool PlayerIntangible => owner.PlayerHealth != null && owner.PlayerHealth.IsDashIntangible;
    private void OnTriggerStay2D(Collider2D other)
    {
        if (other == owner?.PlayerCollider) OnTriggerEnter2D(other);
    }
    private void Present()
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        visual.Show(new AttackGeometry(position, Vector2.one * owner.Settings.ProjectileDiameter, angle, true),
            owner.Settings.AttackOrder, record.BurnEnhanced);
    }
    private void FixedUpdate()
    {
        if (owner == null || (settled ? !owner.CanContinue : !owner.IsAttackValid(record))) { gameObject.SetActive(false); return; }
        float diameter = owner.Settings.ProjectileDiameter;
        float step = Mathf.Min(remaining, speed * Time.fixedDeltaTime);
        step = BossCombatPhysics.ClearDistance(position, Vector2.one * diameter, direction, step, owner, true, out bool blocked);
        if (!settled)
        {
            owner.Observe(record, AttackGeometry.Corridor(position - direction * diameter * 0.5f, direction, remaining + diameter, diameter));
            if (BossCombatPhysics.SweptTarget(position, Vector2.one * diameter, direction, step, owner, true))
            {
                if (PlayerIntangible) record.NoteAvoidedByDash();   // dashing: fly through
                else
                {
                    owner.TryHit(record);
                    Finish(BossAttackEndReason.Contact);
                    return;
                }
            }
            // Dodged: the projectile is already farther from where it was fired than the player is, and it did not hit.
            if (Vector2.Distance(launchOrigin, position) > Vector2.Distance(launchOrigin, owner.PlayerCollider.bounds.center) &&
                record.TryMarkPassedTarget())
            {
                // Mark it settled first: completing the attack can drop the boss's belief to zero, which cancels
                // combat, and a settled projectile is what that cancel must leave flying.
                settled = true;
                owner.CompleteAttack(record, BossAttackEndReason.Completed);
            }
        }
        position += direction * step;
        remaining -= step;
        Present();
        if (blocked) { owner.GetComponent<BossAttackExecutor>().Impact(position, true); Finish(BossAttackEndReason.Wall); }
        else if (remaining <= 0.001f || Time.time >= expiresAt) Finish(blockedAtEnd ? BossAttackEndReason.Wall : BossAttackEndReason.Completed);
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner == null || settled || !owner.IsAttackValid(record)) return;
        if (other == owner.PlayerCollider)
        {
            if (PlayerIntangible) { record.NoteAvoidedByDash(); return; }   // dashing: fly through
            owner.TryHit(record); Finish(BossAttackEndReason.Contact);
        }
        else if (BossCombatPhysics.IsObstacle(other, owner))
        { owner.GetComponent<BossAttackExecutor>().Impact(position, true); Finish(BossAttackEndReason.Wall); }
    }
    private void Finish(BossAttackEndReason reason)
    { owner.CompleteAttack(record, reason); owner.DestroyEffect(gameObject); }
}
