using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public enum BranchRole { Combat, Shop, Event, Boss }
public enum RunArea { Lobby, TemporaryStage, Dying, Complete }

[Serializable]
public sealed class BranchCandidate
{
    public string id;
    public BranchRole role;
    public int slot;
    public bool revealed;
}

[Serializable]
public sealed class TemporaryStageData
{
    public string id;
    public string branchId;
    public BranchRole role;
    public bool completed;
}

[Serializable]
public sealed class LobbyRunData
{
    public string id;
    public string lifeId;
    public string batchId;
    public int visit;
    public int combatClears;
    public RunArea area;
    public float hp;
    public float maxHp;
    public bool hasPosition;
    public float playerX, playerY;
    public List<BranchCandidate> candidates = new List<BranchCandidate>();
    public bool hasStage;
    public TemporaryStageData stage;

    public LobbyRunData Copy()
    {
        var copy = JsonUtility.FromJson<LobbyRunData>(JsonUtility.ToJson(this));
        copy.Normalize();
        return copy;
    }
    public void Normalize() { if (!hasStage) stage = null; }
}

// Seeded, pure rules. Failed saves reuse the same plan rather than rerolling.
public static class LobbyRules
{
    public const int RevealPrice = 5;
    public const int BossUnlockClears = 3;

    public static string StableId(string cause)
    {
        using (var hash = SHA256.Create())
        {
            byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(cause));
            var guid = new byte[16]; Array.Copy(bytes, guid, 16);
            return new Guid(guid).ToString("N");
        }
    }

    public static LobbyRunData NewRun(string id, float maxHp, int seed)
    {
        var run = new LobbyRunData { id = id, lifeId = id, area = RunArea.Lobby, hp = maxHp, maxHp = maxHp };
        GenerateCandidates(run, seed);
        return run;
    }

    public static void GenerateCandidates(LobbyRunData run, int seed)
    {
        run.batchId = StableId(run.id + ":visit:" + run.visit);
        run.candidates.Clear();
        var random = new System.Random(seed);
        int count = random.Next(2, 4);
        var pool = new List<BranchRole> { BranchRole.Combat, BranchRole.Shop, BranchRole.Event };
        if (run.combatClears >= BossUnlockClears) pool.Add(BranchRole.Boss);
        run.candidates.Add(new BranchCandidate { id = run.batchId + ":0", role = BranchRole.Combat });
        for (int i = 1; i < count; i++)
        {
            BranchRole role = pool[random.Next(pool.Count)];
            run.candidates.Add(new BranchCandidate { id = run.batchId + ":" + i, role = role });
            if (role != BranchRole.Combat) pool.Remove(role);
        }
        for (int i = count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            BranchCandidate swap = run.candidates[i]; run.candidates[i] = run.candidates[j]; run.candidates[j] = swap;
        }
        for (int i = 0; i < count; i++) run.candidates[i].slot = i;
    }

    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public static bool ValidHealth(float hp, float maxHp) => Finite(hp) && Finite(maxHp) && maxHp > 0 && hp > 0 && hp <= maxHp;

    public static bool Validate(LobbyRunData run, bool deathPending)
    {
        if (run == null || string.IsNullOrWhiteSpace(run.id) || string.IsNullOrWhiteSpace(run.lifeId) ||
            string.IsNullOrWhiteSpace(run.batchId) || run.visit < 0 || run.visit == int.MaxValue ||
            run.combatClears < 0 || run.combatClears == int.MaxValue || !Enum.IsDefined(typeof(RunArea), run.area) ||
            !Finite(run.playerX) || !Finite(run.playerY) || !Finite(run.maxHp) || run.maxHp <= 0 || !Finite(run.hp) ||
            run.hp < 0 || run.hp > run.maxHp || (run.hp == 0 && run.area != RunArea.Dying) ||
            (run.area == RunArea.Dying && (!deathPending || run.hp != 0)) || run.candidates == null ||
            run.candidates.Count < 2 || run.candidates.Count > 3 || run.hasStage != (run.stage != null)) return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var slots = new HashSet<int>();
        int combat = 0; var special = new HashSet<BranchRole>();
        foreach (BranchCandidate candidate in run.candidates)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.id) || !ids.Add(candidate.id) ||
                candidate.slot < 0 || candidate.slot >= run.candidates.Count || !slots.Add(candidate.slot) ||
                !Enum.IsDefined(typeof(BranchRole), candidate.role)) return false;
            if (candidate.role == BranchRole.Combat) combat++;
            else if (!special.Add(candidate.role)) return false;
            if (candidate.role == BranchRole.Boss && run.combatClears < BossUnlockClears) return false;
        }
        if (combat == 0 || (run.area == RunArea.Lobby && run.hasStage) ||
            ((run.area == RunArea.TemporaryStage || run.area == RunArea.Complete) && !run.hasStage)) return false;
        if (run.hasStage)
        {
            BranchCandidate chosen = run.candidates.Find(candidate => candidate.id == run.stage.branchId);
            if (string.IsNullOrWhiteSpace(run.stage.id) || chosen == null || chosen.role != run.stage.role) return false;
            if (run.area == RunArea.Complete && (run.stage.role != BranchRole.Boss || !run.stage.completed)) return false;
            if (run.area == RunArea.TemporaryStage && run.stage.role == BranchRole.Boss && run.stage.completed) return false;
        }
        return true;
    }

    public static string RoleLabel(BranchRole role)
    {
        switch (role)
        {
            case BranchRole.Combat: return "전투";
            case BranchRole.Shop: return "상점";
            case BranchRole.Event: return "이벤트";
            case BranchRole.Boss: return "보스";
            default: return "알 수 없음";
        }
    }
}
