using UnityEngine;

public sealed partial class EconomyService
{
    public bool CanUseChestStage(string stageId) => IsReady && !rewardsBlocked &&
        IsCurrentStage(profile, stageId) && profile.run.stage.hasChestState;

    public ChestRecord GetChestState(string stageId, string spawnId) =>
        !string.IsNullOrEmpty(stageId) && profile.run?.stage?.id == stageId && profile.run.stage.hasChestState
            ? profile.run.stage.chestState.records.Find(record => record.spawnId == spawnId)?.Copy() : null;

    public CurrencyResult TryRegisterStageChests(ChestManifest manifest)
    {
        if (!ChestRules.TryNormalizeManifest(manifest, out ChestManifest normalized)) return Fail(EconomyError.InvalidItem);
        string hash = ChestRules.ManifestHash(normalized);
        string id = ChestRules.RegisterId(normalized.stageInstanceId);
        return Execute(id, "chest.register:" + hash, "chest.register", data =>
        {
            if (!IsCurrentStage(data, normalized.stageInstanceId) || data.run.stage.hasChestState)
                return Fail(EconomyError.StateConflict, id);
            var state = new ChestStageState { stageInstanceId = normalized.stageInstanceId,
                layoutVersion = normalized.layoutVersion, rewardTableVersion = normalized.rewardTableVersion, manifestHash = hash };
            foreach (string spawnId in normalized.spawnIds)
                state.records.Add(ChestRules.Plan(normalized.stageInstanceId, spawnId, normalized.rewardTableVersion));
            data.run.stage.hasChestState = true;
            data.run.stage.chestState = state;
            return Success(0, 0, data.balance, id);
        });
    }

    public ChestOpenResult TryOpenChest(string stageId, string spawnId, Vector3 position, Camera sourceCamera = null)
    {
        if (string.IsNullOrWhiteSpace(stageId) || string.IsNullOrWhiteSpace(spawnId))
            return new ChestOpenResult { Error = EconomyError.InvalidTransaction };
        string id = ChestRules.OpenId(stageId, spawnId);
        bool replay = profile.processedTransactions.Exists(receipt => receipt.id == id);
        ChestRewardReceipt reward = null;
        // Existing receipts can be queried after death/return without issuing another reward.
        if (!replay && (!initialized || rewardsBlocked))
            return new ChestOpenResult { Error = initialized ? EconomyError.PlayerDead : EconomyError.NotReady, TransactionId = id };
        CurrencyResult result = Execute(id, "chest.open", "chest.open", data =>
        {
            if (!IsCurrentStage(data, stageId) || !data.run.stage.hasChestState) return Fail(EconomyError.StateConflict, id);
            ChestRecord record = data.run.stage.chestState.records.Find(item => item.spawnId == spawnId);
            if (record == null) return Fail(EconomyError.InvalidItem, id);
            if (record.opened) return Fail(EconomyError.StateConflict, id);
            if (EconomyLedger.Count(data.pieces, record.pieceId) >= EconomySettings.MaxPiecesPerKind)
                return Fail(EconomyError.ItemLimit, id);
            int applied = System.Math.Min(record.rolledCoins, EconomySettings.MaxCoins - data.balance - EconomyLedger.Pending(data));
            EconomyLedger.Add(data.pieces, record.pieceId, 1);
            if (applied > 0) data.pendingGrants.Add(CreateGrant(id, applied));
            record.opened = true; record.openTransactionId = id; record.appliedCoins = applied;
            reward = new ChestRewardReceipt { stageInstanceId = stageId, spawnId = spawnId,
                rewardTableVersion = data.run.stage.chestState.rewardTableVersion,
                pieceId = record.pieceId, rarity = record.rarity, pieceAmount = 1,
                rolledCoins = record.rolledCoins, appliedCoins = applied, grantId = id };
            return Success(record.rolledCoins, applied, data.balance, id);
        }, () => RequestFlight(id, position, sourceCamera), (data, receipt) =>
        {
            receipt.hasChestReward = true;
            receipt.chestReward = reward.Copy();
        });
        return new ChestOpenResult { Success = result.Success, Error = result.Error, TransactionId = id,
            IsReplay = result.Success && replay,
            Reward = result.Success ? profile.processedTransactions.Find(receipt => receipt.id == id).chestReward?.Copy() : null };
    }
}
