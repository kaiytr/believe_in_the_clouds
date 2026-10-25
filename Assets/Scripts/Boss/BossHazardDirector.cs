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
        if (!BossCombatPhysics.FindGround(foot, 0.1f, 8f, owner, out Vector2 floor)) yield break;
        float halfWidth = data.CloudSize.x * 0.5f;
        float x = Mathf.Clamp(foot.x, data.ArenaX.x + data.CloudSpacing + halfWidth, data.ArenaX.y - data.CloudSpacing - halfWidth);
        float y = floor.y + data.CloudTopHeight - data.CloudSize.y * 0.5f;
        var blackShape = new AttackGeometry(new Vector2(x - data.CloudSpacing, y), data.CloudSize);
        var whiteShape = new AttackGeometry(new Vector2(x + data.CloudSpacing, y), data.CloudSize);
        var executor = owner.GetComponent<BossAttackExecutor>();
        var blackWarning = executor.CreateWarning(blackShape);
        var whiteWarning = executor.CreateWarning(whiteShape);
        blackWarning.SetTint(new Color(.9f, .5f, 1f));
        whiteWarning.SetTint(new Color(.5f, .9f, 1f));
        owner.SetConditionTimer(data.CloudWarning);
        owner.SetActionCue("구름 생성", "흰 구름은 발판 · 먹구름을 통과하면 발밑 가시", BossPresentationState.Charging, data.CloudWarning);
        while (owner.ConditionRemaining > 0f && token == owner.Generation && owner.CanContinue)
        {
            float progress = owner.ConditionProgress;
            blackWarning.SetProgress(progress); whiteWarning.SetProgress(progress);
            blackWarning.SetCue("먹구름 생성", owner.ConditionRemaining, progress, owner);
            whiteWarning.SetCue("발판 생성", owner.ConditionRemaining, progress, owner);
            yield return null;
        }
        if (token != owner.Generation || !owner.CanContinue) yield break;
        owner.DestroyEffect(blackWarning.gameObject);
        owner.DestroyEffect(whiteWarning.gameObject);
        var black = owner.SpawnEffect(data.BlackCloudPrefab);
        var white = owner.SpawnEffect(data.WhiteCloudPrefab);
        black.Initialize(owner, false, blackShape);
        white.Initialize(owner, true, whiteShape);
        owner.SetActionCue("구름 시험", $"구름에 오르지 않고 버티면 믿음 -{data.BeliefReduction:0}", BossPresentationState.Stare, data.CloudDuration);
        owner.SetConditionTimer(data.CloudDuration);
        yield return new WaitForSeconds(data.CloudDuration);
        if (token != owner.Generation) yield break;
        owner.DestroyEffect(black.gameObject);
        owner.DestroyEffect(white.gameObject);
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
