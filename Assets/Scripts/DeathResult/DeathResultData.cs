using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class EnemyKillCount
{
    public EnemyRewardKind kind;
    public int count;
}

[Serializable]
public sealed class PlayStatistics
{
    public long coins;
    public long keywords;
    public List<EnemyKillCount> kills = new List<EnemyKillCount>();
    public List<string> defeatedIds = new List<string>();
    public PlayStatistics Copy() => JsonUtility.FromJson<PlayStatistics>(JsonUtility.ToJson(this));
    public int Count(EnemyRewardKind kind) => kills.Find(item => item.kind == kind)?.count ?? 0;
    public void Defeat(EnemyRewardKind kind, string id)
    {
        if (defeatedIds.Contains(id)) return;
        defeatedIds.Add(id);
        EnemyKillCount entry = kills.Find(item => item.kind == kind);
        if (entry == null) { entry = new EnemyKillCount { kind = kind }; kills.Add(entry); }
        entry.count++;
    }
    public bool IsValid()
    {
        if (coins < 0 || keywords < 0 || kills == null || defeatedIds == null) return false;
        var kinds = new HashSet<EnemyRewardKind>();
        long total = 0;
        foreach (EnemyKillCount item in kills)
        {
            if (item == null || item.count < 0 || !Enum.IsDefined(typeof(EnemyRewardKind), item.kind) || !kinds.Add(item.kind)) return false;
            total += item.count;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in defeatedIds) if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) return false;
        return total == defeatedIds.Count;
    }
}

[Serializable]
public sealed class DeathResultData
{
    public string cause;
    public string occurredAtUtc;
    public string sceneName;
    public string screenshotPngBase64;
    public string captureError;
    public PlayStatistics statistics;
    // Doctrines the player held when they died (the death clears them right after). Older saves have none.
    public List<Doctrine> doctrines = new List<Doctrine>();
    public DeathResultData Copy() => JsonUtility.FromJson<DeathResultData>(JsonUtility.ToJson(this));
    public bool IsValid() => !string.IsNullOrWhiteSpace(cause) &&
        DateTime.TryParse(occurredAtUtc, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out _) && statistics != null && statistics.IsValid() &&
        (screenshotPngBase64 == null || screenshotPngBase64.Length <= 8 * 1024 * 1024);
}

public static class DeathCause
{
    public static string EnemyLabel(EnemyRewardKind kind)
    {
        switch (kind)
        {
            case EnemyRewardKind.Believer: return "신자";
            case EnemyRewardKind.Dog: return "개";
            case EnemyRewardKind.Executive: return "간부";
            case EnemyRewardKind.Boss: return "보스";
            default: return kind.ToString();
        }
    }
    public static string Describe(GameObject attacker, DamageSource? type, string detail = null)
    {
        string label = null;
        if (attacker != null)
        {
            EnemyUnit enemy = attacker.GetComponentInParent<EnemyUnit>();
            if (enemy != null) label = EnemyLabel(enemy.RewardKind);
            else if (attacker.GetComponentInParent<BossCombatController>() != null) label = "보스";
        }
        if (label == null) label = type == DamageSource.Environment ? "환경 피해" :
            type == DamageSource.Enemy ? "적의 공격" : "알 수 없는 원인";
        return string.IsNullOrWhiteSpace(detail) ? label : label + " · " + detail;
    }
}
