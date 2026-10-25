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
    public void Initialize(BossCombatController boss, BossAttackRecord attack, Vector2 origin, Vector2 aim, float travelSpeed, float distance, float lifetime, bool endBlocked)
    {
        owner = boss; record = attack; position = origin; direction = aim;
        speed = travelSpeed; remaining = distance; expiresAt = Time.time + lifetime;
        blockedAtEnd = endBlocked;
        visual = GetComponent<BossAttackVisual>();
        Present();
    }
    private void Present() => visual.Show(new BossAttackGeometry(position, Vector2.one * owner.Settings.ProjectileDiameter, 0f, true), owner.Settings.AttackOrder, owner.IsBurning);
    private void FixedUpdate()
    {
        if (owner == null || !owner.IsAttackValid(record)) { gameObject.SetActive(false); return; }
        float diameter = owner.Settings.ProjectileDiameter;
        float step = Mathf.Min(remaining, speed * Time.fixedDeltaTime);
        step = BossCombatPhysics.ClearDistance(position, Vector2.one * diameter, direction, step, owner, true, out bool blocked);
        owner.Observe(record, BossAttackGeometry.Corridor(position - direction * diameter * 0.5f, direction, remaining + diameter, diameter));
        if (BossCombatPhysics.SweptTarget(position, Vector2.one * diameter, direction, step, owner, true))
        {
            owner.TryHit(record);
            Finish(BossAttackEndReason.Contact);
            return;
        }
        position += direction * step;
        remaining -= step;
        Present();
        if (blocked) { owner.GetComponent<BossAttackExecutor>().Impact(position, true); Finish(BossAttackEndReason.Wall); }
        else if (remaining <= 0.001f || Time.time >= expiresAt) Finish(blockedAtEnd ? BossAttackEndReason.Wall : BossAttackEndReason.Completed);
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner == null || !owner.IsAttackValid(record)) return;
        if (other == owner.PlayerCollider) { owner.TryHit(record); Finish(BossAttackEndReason.Contact); }
        else if (BossCombatPhysics.IsObstacle(other, owner))
        { owner.GetComponent<BossAttackExecutor>().Impact(position, true); Finish(BossAttackEndReason.Wall); }
    }
    private void Finish(BossAttackEndReason reason)
    { owner.CompleteAttack(record, reason); owner.DestroyEffect(gameObject); }
}
