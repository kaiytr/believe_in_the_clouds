using System;
using System.Collections.Generic;

[Serializable]
public sealed class ChestManifest
{
    public string stageInstanceId;
    public int layoutVersion = 1;
    public int rewardTableVersion = 1;
    public string[] spawnIds;
}

[Serializable]
public sealed class ChestStageState
{
    public string stageInstanceId;
    public int layoutVersion;
    public int rewardTableVersion;
    public string manifestHash;
    public List<ChestRecord> records = new List<ChestRecord>();
}

[Serializable]
public sealed class ChestRecord
{
    public string spawnId;
    public string pieceId;
    public ShopRarity rarity;
    public int pieceAmount = 1;
    public int rolledCoins;
    public bool opened;
    public string openTransactionId;
    public int appliedCoins;
    public ChestRecord Copy() => (ChestRecord)MemberwiseClone();
}

[Serializable]
public sealed class ChestRewardReceipt
{
    public string stageInstanceId;
    public string spawnId;
    public string pieceId;
    public ShopRarity rarity;
    public int pieceAmount;
    public int rolledCoins;
    public int appliedCoins;
    public string grantId;
    public ChestRewardReceipt Copy() => (ChestRewardReceipt)MemberwiseClone();
}

public sealed class ChestOpenResult
{
    public bool Success;
    public EconomyError Error;
    public string TransactionId;
    public ChestRewardReceipt Reward;
    public bool IsReplay;
}
