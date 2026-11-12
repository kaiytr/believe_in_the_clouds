using System;
using System.Collections.Generic;
using UnityEngine;

// Run and economy transactions share the existing copy/save/publish gate.
public sealed partial class EconomyService
{
    private LobbyRunData initialRunPlan;
    public LobbyRunData RunSnapshot() => profile.hasRun ? profile.run.Copy() : null;
    public EconomyService Reopen() => new EconomyService(store);

    public CurrencyResult TryInitializeRun(float maxHp)
    {
        if (!LobbyRules.ValidHealth(maxHp, maxHp)) return Fail(EconomyError.InvalidAmount);
        if (busy || deathWork != null) return Fail(EconomyError.Busy);
        if (!initialized) return Fail(EconomyError.NotReady);
        if (profile.hasRun) return Success(0, 0, Balance, null);
        initialRunPlan = initialRunPlan ?? LobbyRules.NewRun(Guid.NewGuid().ToString("N"), maxHp, random.Next(0, int.MaxValue));
        return Execute("run:init:" + profile.profileId, "run.initialize", "run.initialize", data =>
        {
            data.hasRun = true; data.run = initialRunPlan.Copy();
            return Success(0, 0, data.balance, activeTransactionId);
        });
    }

    // 체력 밸런스를 바꿔도 기존 런을 유지한다. 남은 체력 비율을 보존하고,
    // 새 최대 체력을 저장한 뒤에만 플레이어에 반영한다.
    public CurrencyResult TrySynchronizeRunMaxHp(float maxHp)
    {
        if (!LobbyRules.ValidHealth(maxHp, maxHp)) return Fail(EconomyError.InvalidAmount);
        if (busy || deathWork != null) return Fail(EconomyError.Busy);
        if (!initialized) return Fail(EconomyError.NotReady);
        if (!profile.hasRun || profile.run == null || profile.run.area == RunArea.Dying)
            return Fail(EconomyError.StateConflict);
        if (Mathf.Approximately(profile.run.maxHp, maxHp)) return Success(0, 0, Balance, null);

        busy = true;
        try
        {
            var work = profile.Copy();
            work.run.hp = Mathf.Clamp(work.run.hp / work.run.maxHp * maxHp, 0f, maxHp);
            work.run.maxHp = maxHp;
            work.revision++;
            if (!Save(work)) return Fail(EconomyError.SaveFailed);
            Publish(work, "run.max-hp.sync", null);
            return Success(0, 0, Balance, null);
        }
        finally { busy = false; }
    }

    /// <summary>Builds a predictable role set for the isolated end-to-end play loop fixture.</summary>
    public CurrencyResult TryConfigurePlayLoopTestCandidates()
    {
        if (!(store is MemoryEconomyStore)) return Fail(EconomyError.StateConflict);
        if (!profile.hasRun || profile.run == null) return Fail(EconomyError.StateConflict);

        int visit = profile.run.visit;
        string runId = profile.run.id;
        string transactionId = "playloop:candidates:" + runId + ":" + visit;
        string requestKey = RequestKey("run.playloopTestCandidates", runId, visit.ToString());
        return Execute(transactionId, requestKey, "run.playloopTestCandidates", data =>
        {
            if (!data.hasRun || data.run == null || data.run.area != RunArea.Lobby || data.run.hasStage)
                return Fail(EconomyError.StateConflict, transactionId);

            data.run.batchId = LobbyRules.StableId("playloop:" + data.run.id + ":" + data.run.visit);
            data.run.candidates.Clear();
            BranchRole[] roles = data.run.combatClears >= LobbyRules.BossUnlockClears
                ? new[] { BranchRole.Combat, BranchRole.Shop, BranchRole.Boss }
                : new[] { BranchRole.Combat, BranchRole.Shop };
            for (int i = 0; i < roles.Length; i++)
            {
                data.run.candidates.Add(new BranchCandidate
                {
                    id = data.run.batchId + ":" + i,
                    role = roles[i],
                    slot = i,
                    revealed = false
                });
            }

            return Success(0, 0, data.balance, activeTransactionId);
        });
    }

    public CurrencyResult TryRevealBranch(string branchId)
    {
        if (string.IsNullOrWhiteSpace(branchId)) return Fail(EconomyError.InvalidTransaction);
        return Execute("reveal:" + branchId, RequestKey("run.reveal", branchId), "run.reveal", data =>
        {
            BranchCandidate candidate = data.run?.candidates.Find(item => item.id == branchId);
            if (!data.hasRun || data.run.area != RunArea.Lobby || candidate == null) return Fail(EconomyError.StateConflict);
            if (candidate.revealed) return Success(0, 0, data.balance, activeTransactionId);
            if (data.balance < LobbyRules.RevealPrice) return Fail(EconomyError.InsufficientFunds);
            data.balance -= LobbyRules.RevealPrice; candidate.revealed = true;
            return Success(LobbyRules.RevealPrice, LobbyRules.RevealPrice, data.balance, activeTransactionId);
        });
    }

    public CurrencyResult TryDepartBranch(string branchId, float hp)
    {
        if (string.IsNullOrWhiteSpace(branchId)) return Fail(EconomyError.InvalidTransaction);
        return Execute("depart:" + branchId, RequestKey("run.depart", branchId), "run.depart", data =>
        {
            BranchCandidate candidate = data.run?.candidates.Find(item => item.id == branchId);
            if (!data.hasRun || data.run.area != RunArea.Lobby || candidate == null) return Fail(EconomyError.StateConflict);
            if (!LobbyRules.ValidHealth(hp, data.run.maxHp)) return Fail(EconomyError.InvalidAmount);
            FlushReservations(data);
            data.run.area = RunArea.TemporaryStage; data.run.hp = hp; data.run.hasPosition = false;
            data.run.hasStage = true;
            data.run.stage = new TemporaryStageData {
                id = "stage:" + branchId, branchId = branchId, role = candidate.role,
                usesDesignedCombatLevel = candidate.role == BranchRole.Combat
            };
            return Success(0, 0, data.balance, activeTransactionId);
        });
    }

    public CurrencyResult TryCompleteTemporaryStage(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId)) return Fail(EconomyError.InvalidTransaction);
        return Execute("complete:" + stageId, RequestKey("run.complete", stageId), "run.complete", data =>
        {
            if (!IsCurrentStage(data, stageId)) return Fail(EconomyError.StateConflict);
            data.run.stage.completed = true;
            if (data.run.stage.role == BranchRole.Boss) data.run.area = RunArea.Complete;
            return Success(0, 0, data.balance, activeTransactionId);
        });
    }

    public CurrencyResult TryReturnFromStage(string stageId, float hp)
        => TryExitCompletedStage(stageId, hp);

    // Frequent checkpoints do not grow the idempotent gameplay receipt list.
    public CurrencyResult TryCheckpointRun(float hp, Vector2 position)
    {
        if (busy || deathWork != null) return Fail(EconomyError.Busy);
        if (!initialized) return Fail(EconomyError.NotReady);
        if (!profile.hasRun || profile.run.area == RunArea.Dying || profile.run.area == RunArea.Complete) return Fail(EconomyError.StateConflict);
        if (!LobbyRules.ValidHealth(hp, profile.run.maxHp) || !LobbyRules.Finite(position.x) || !LobbyRules.Finite(position.y))
            return Fail(EconomyError.InvalidAmount);
        LobbyRunData run = profile.run;
        if (run.hp == hp && run.hasPosition && run.playerX == position.x && run.playerY == position.y)
            return Success(0, 0, Balance, null);
        busy = true;
        try
        {
            var work = profile.Copy();
            work.run.hp = hp; work.run.hasPosition = true; work.run.playerX = position.x; work.run.playerY = position.y;
            work.revision++;
            if (!Save(work)) return Fail(EconomyError.SaveFailed);
            Publish(work, "run.checkpoint", null);
            return Success(0, 0, Balance, null);
        }
        finally { busy = false; }
    }

    private static bool IsCurrentStage(EconomyProfileData data, string stageId) =>
        data.hasRun && data.run.area == RunArea.TemporaryStage && data.run.hasStage && data.run.stage.id == stageId;

    // 재단 TIP 최초 1회 열람 기록 (멱등: 이미 봤으면 성공으로 통과).
    public CurrencyResult TryMarkFoundationTipSeen()
    {
        return Execute("foundation.tip.seen", RequestKey("foundation.tip.seen"), "foundation.tip.seen", data =>
        {
            data.foundationTipSeen = true;
            return Success(0, 0, data.balance, activeTransactionId);
        });
    }

    // 상점 TIP 최초 1회 열람 기록 (멱등: 이미 봤으면 성공으로 통과).
    public CurrencyResult TryMarkShopTipSeen()
    {
        return Execute("shop.tip.seen", RequestKey("shop.tip.seen"), "shop.tip.seen", data =>
        {
            data.shopTipSeen = true;
            return Success(0, 0, data.balance, activeTransactionId);
        });
    }
    private static void FlushReservations(EconomyProfileData data)
    { data.balance += EconomyLedger.Pending(data); data.pendingGrants.Clear(); }
}
