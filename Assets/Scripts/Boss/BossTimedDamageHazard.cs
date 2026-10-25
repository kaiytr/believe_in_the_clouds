using UnityEngine;

[RequireComponent(typeof(BossAttackVisual))]
public sealed class BossTimedDamageHazard : MonoBehaviour
{
    private BossCombatController owner;
    private BossAttackRecord record;
    private BossAttackVisual visual, warning;
    private float activatesAt, expiresAt;
    public bool Active => record != null && record.Activated && !record.IsComplete;
    public BossAttackRecord Record => record;
    public void Initialize(BossCombatController boss, BossAttackRecord attack)
    {
        owner = boss; record = attack;
        visual = GetComponent<BossAttackVisual>();
        visual.Show(record.Geometry, owner.Settings.AttackOrder);
        visual.Renderer.enabled = false;
        foreach (var line in GetComponentsInChildren<LineRenderer>()) line.enabled = false;
        warning = owner.GetComponent<BossAttackExecutor>().CreateWarning(record.Geometry);
        activatesAt = Time.time + owner.Settings.SpikeWarning;
        expiresAt = activatesAt + owner.Settings.SpikeDuration;
        warning.SetTint(new Color(1f, .47f, .32f));
        warning.SetCue("가시 예고", owner.Settings.SpikeWarning, 0f, owner);
    }
    private void FixedUpdate()
    {
        if (owner == null || !owner.IsAttackValid(record)) return;
        owner.Observe(record, record.Geometry);
        if (!record.Activated && Time.time >= activatesAt)
        {
            owner.DestroyEffect(warning != null ? warning.gameObject : null);
            record.Activate();
            visual.Renderer.enabled = true;
            foreach (var line in GetComponentsInChildren<LineRenderer>()) line.enabled = true;
        }
        if (Time.time >= expiresAt)
        {
            owner.CompleteAttack(record, BossAttackEndReason.Completed);
            visual.Fade(0.15f);
            Destroy(gameObject, 0.17f);
            return;
        }
        if (record.Activated)
        {
            visual.SetCue("가시 · 접근 금지", expiresAt - Time.time,
                1f - (expiresAt - Time.time) / owner.Settings.SpikeDuration, owner);
            if (record.Geometry.Overlaps(owner.PlayerCollider)) owner.TryHit(record);
        }
        else if (!record.Activated && warning != null)
        {
            warning.SetProgress(1f - (activatesAt - Time.time) / owner.Settings.SpikeWarning);
            warning.SetCue("가시 예고", activatesAt - Time.time,
                1f - (activatesAt - Time.time) / owner.Settings.SpikeWarning, owner);
        }
    }
}
