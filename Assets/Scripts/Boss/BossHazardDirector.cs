using System.Collections;
using UnityEngine;

public sealed class BossHazardDirector : MonoBehaviour
{
    private BossCombatController owner;
    public void Initialize(BossCombatController boss) { owner = boss; }
    public void Cancel() { StopAllCoroutines(); }

    public IEnumerator Clouds()
    {
        var data = owner.Settings;
        int token = owner.Generation;
        Vector2 foot = new Vector2(owner.PlayerCollider.bounds.center.x, owner.PlayerCollider.bounds.min.y);
        if (!BossCombatPhysics.FindGround(foot, .1f, 8f, owner, out Vector2 floor)) yield break;
        float halfWidth = Mathf.Min(data.StormCloudSize.x, owner.PatternXLimits.y - owner.PatternXLimits.x) * .5f;
        if (halfWidth < Mathf.Abs(data.CloudSpacing) + data.CloudSize.x * .5f) yield break;
        float x = Mathf.Clamp(foot.x, owner.PatternXLimits.x + halfWidth, owner.PatternXLimits.y - halfWidth);
        var storm = new AttackGeometry(new Vector2(x, floor.y + data.CloudTopHeight - data.StormCloudSize.y * .5f),
            new Vector2(halfWidth * 2f, data.StormCloudSize.y));
        var whiteShape = new AttackGeometry(new Vector2(x + data.CloudSpacing, floor.y + data.ShelterHeight + data.CloudSize.y * .5f), data.CloudSize);
        var shelter = new AttackGeometry(new Vector2(whiteShape.Center.x, floor.y + data.ShelterHeight * .5f),
            new Vector2(data.CloudSize.x, data.ShelterHeight));
        var strike = new AttackGeometry(new Vector2(x, floor.y + (data.CloudTopHeight - data.StormCloudSize.y) * .5f),
            new Vector2(halfWidth * 2f, data.CloudTopHeight - data.StormCloudSize.y));
        var record = owner.CreateAttack(BossAttackKind.Lightning, 0, strike, data.LightningDamage, BossBeliefKind.Cloud, false);
        var black = owner.SpawnEffect(data.BlackCloudPrefab);
        var white = owner.SpawnEffect(data.WhiteCloudPrefab);
        black.Initialize(owner, false, storm);
        white.Initialize(owner, true, whiteShape);
        var lightning = black.gameObject.AddComponent<BossCloudLightning>();
        lightning.Initialize(owner, record, shelter);
        try
        {
            owner.SetConditionTimer(data.CloudWarning);
            owner.SetActionCue("먹구름 생성", "청록색 보호 범위 안으로 몸 전체를 대피하세요.", BossPresentationState.Charging, data.CloudWarning);
            while (owner.ConditionRemaining > 0f && token == owner.Generation && owner.CanContinue)
            { lightning.ShowCountdown(owner.ConditionRemaining + data.CloudDuration); yield return null; }
            if (token != owner.Generation || !owner.CanContinue) yield break;
            owner.SetConditionTimer(data.CloudDuration);
            owner.SetActionCue("번개 대피", $"흰 구름 아래 청록색 범위로! 성공 시 믿음 -{data.BeliefReduction:0}", BossPresentationState.Windup, data.CloudDuration);
            while (owner.ConditionRemaining > 0f && token == owner.Generation && owner.CanContinue)
            {
                lightning.ShowCountdown(owner.ConditionRemaining);
                record.Observe(strike.Overlaps(owner.PlayerCollider) && !lightning.ContainsPlayer(), owner.PlayerCollider.bounds.center);
                yield return null;
            }
            if (token != owner.Generation || !owner.CanContinue) yield break;
            // Sync at the deadline: a transform moved this frame must use its actual current collider.
            Physics2D.SyncTransforms();
            record.Activate();
            bool sheltered = lightning.ContainsPlayer();
            lightning.Strike();
            if (sheltered) record.ProtectFromLightning();
            else if (strike.Overlaps(owner.PlayerCollider)) owner.TryHit(record);
            owner.CompleteAttack(record, BossAttackEndReason.Completed);
            owner.SetActionCue("번개 발동", "흰 구름 아래가 보호됩니다.", BossPresentationState.Active, data.LightningDuration);
            owner.ResolveCloudShelter(sheltered);
            float endsAt = Time.time + data.LightningDuration;
            while (Time.time < endsAt && token == owner.Generation && owner.CanContinue) yield return null;
        }
        finally
        {
            owner.DestroyEffect(black != null ? black.gameObject : null);
            owner.DestroyEffect(white != null ? white.gameObject : null);
            if (owner.IsAttackValid(record)) owner.CompleteAttack(record, BossAttackEndReason.Cancelled);
        }
    }

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
