using UnityEngine;

/// <summary>Timed ground hazards (legacy spikes). Clouds and lightning live in <see cref="BossCloudField"/>.</summary>
public sealed class BossHazardDirector : MonoBehaviour
{
    private BossCombatController owner;
    public void Initialize(BossCombatController boss) { owner = boss; }
    public void Cancel() { StopAllCoroutines(); }

    public bool ScheduleSpike(Vector2 dropPoint)
    {
        if (owner == null || !owner.CanContinue ||
            !BossCombatPhysics.FindGround(dropPoint, 0.05f, 20f, owner, out Vector2 ground)) return false;
        var data = owner.Settings;
        var shape = new AttackGeometry(ground + Vector2.up * data.SpikeSize.y * 0.5f, data.SpikeSize);
        var record = owner.CreateAttack(BossAttackKind.Spike, 0, shape, data.SpikeDamage, BossBeliefKind.None, false);
        var spike = owner.SpawnEffect(data.SpikePrefab);
        spike.Initialize(owner, record);
        return true;
    }
}
