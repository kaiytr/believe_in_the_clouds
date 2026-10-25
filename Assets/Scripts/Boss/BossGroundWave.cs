using UnityEngine;

[RequireComponent(typeof(BossAttackVisual))]
public sealed class BossGroundWave : MonoBehaviour
{
    private BossCombatController owner;
    private BossAttackRecord record;
    private BossAttackVisual visual;
    private Vector2 ground;
    private int direction;
    private float travelled;
    private BossWavePath path;
    public bool Finished { get; private set; }
    public bool Blocked { get; private set; }
    public bool Threatens(Collider2D target) => !Finished && path != null && path.Threatens(target, travelled);

    public void Initialize(BossCombatController boss, BossAttackRecord attack, BossWavePath lockedPath)
    {
        owner = boss; record = attack; path = lockedPath; ground = path.Points[0]; direction = path.Direction;
        visual = GetComponent<BossAttackVisual>();
        Present();
    }
    private void Present() => visual.Show(new AttackGeometry(ground + Vector2.up * (owner.Settings.WaveSize.y * 0.5f + 0.02f), owner.Settings.WaveSize), owner.Settings.AttackOrder);
    private void FixedUpdate()
    {
        if (Finished || owner == null || !owner.IsAttackValid(record)) return;
        var data = owner.Settings;
        float step = Mathf.Min(path.Distance-travelled, data.WaveSpeed * owner.SpeedMultiplier * Time.fixedDeltaTime);
        Vector2 move = Vector2.right * direction;
        Vector2 center = ground + Vector2.up * (data.WaveSize.y * 0.5f + 0.02f);
        step = BossCombatPhysics.ClearDistance(center, data.WaveSize, move, step, owner, false, out bool wall);
        Vector2 next = path.GroundAt(travelled+step);
        if (!BossCombatPhysics.FindGround(next, data.MaximumGroundStep, data.MaximumGroundStep, owner, out Vector2 currentGround) ||
            !path.Encloses(new AttackGeometry(currentGround+Vector2.up*(data.WaveSize.y*.5f+.02f),data.WaveSize)))
        { Blocked = true; End(); return; }
        next=currentGround;
        Vector2 displacement=next-ground;
        if (BossCombatPhysics.SweptTarget(center, data.WaveSize, displacement.normalized, displacement.magnitude, owner, false)) owner.TryHit(record);
        ground = next;
        travelled += step;
        Present();
        if (wall || travelled >= path.Distance-.001f) { Blocked = wall || path.Blocked; End(); }
    }
    private void End()
    {
        Finished = true;
        if (Blocked) owner.GetComponent<BossAttackExecutor>().Impact(ground, true);
        visual.Fade(0.1f);
    }
}
