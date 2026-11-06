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
    public void Initialize(BossCombatController boss, BossAttackRecord attack, Vector2 origin, Vector2 aim, float travelSpeed, float distance, float lifetime, bool endBlocked)
    {
        owner = boss; record = attack; position = origin; launchOrigin = origin; direction = aim;
        speed = travelSpeed; remaining = distance; expiresAt = Time.time + lifetime;
        blockedAtEnd = endBlocked;
        visual = GetComponent<BossAttackVisual>();
        Present();
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
                owner.TryHit(record);
                Finish(BossAttackEndReason.Contact);
                return;
            }
            // Dodged: the projectile is already farther from where it was fired than the player is, and it did not hit.
            if (Vector2.Distance(launchOrigin, position) > Vector2.Distance(launchOrigin, owner.PlayerCollider.bounds.center) &&
                record.TryMarkPassedTarget())
            {
                owner.CompleteAttack(record, BossAttackEndReason.Completed);
                settled = true;
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
        if (other == owner.PlayerCollider) { owner.TryHit(record); Finish(BossAttackEndReason.Contact); }
        else if (BossCombatPhysics.IsObstacle(other, owner))
        { owner.GetComponent<BossAttackExecutor>().Impact(position, true); Finish(BossAttackEndReason.Wall); }
    }
    private void Finish(BossAttackEndReason reason)
    { owner.CompleteAttack(record, reason); owner.DestroyEffect(gameObject); }
}
