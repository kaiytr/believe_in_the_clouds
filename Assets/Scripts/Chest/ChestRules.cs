using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

// V1 is immutable: stored plans and failed registration retries use this algorithm.
public static class ChestRules
{
    public const float HoldSeconds = 1.5f;
    public const float InteractionRadius = 2.5f;
    public const int TableVersion = 1;

    public static string OpenId(string stageId, string spawnId) =>
        "chest.open:" + Key(stageId, spawnId);
    public static string RegisterId(string stageId) => "chest.register:" + Key(stageId);
    private static string Key(params string[] parts) =>
        string.Concat(parts.Select(part => (part?.Length ?? -1) + ":" + part));

    public static ShopRarity RarityFor(int roll)
    {
        if (roll < 0 || roll >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
        return roll < 65 ? ShopRarity.Common : roll < 90 ? ShopRarity.Uncommon : ShopRarity.Rare;
    }

    public static ShopEntry[] Pool(ShopRarity rarity) =>
        Enum.GetValues(typeof(ShopColumn)).Cast<ShopColumn>()
            .SelectMany(ShopCatalog.GetColumn).Where(item => item.Rarity == rarity)
            .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();

    public static ChestRecord Roll(string spawnId, IEconomyRandom random)
    {
        ShopRarity rarity = RarityFor(random.Next(0, 100));
        ShopEntry[] pool = Pool(rarity);
        if (pool.Length == 0) throw new InvalidOperationException("Empty chest reward pool.");
        return new ChestRecord { spawnId = spawnId, rarity = rarity,
            pieceId = pool[random.Next(0, pool.Length)].Id, pieceAmount = 1,
            rolledCoins = random.Next(7, 18) };
    }

    public static bool TryNormalizeManifest(ChestManifest source, out ChestManifest manifest)
    {
        manifest = null;
        if (source == null || string.IsNullOrWhiteSpace(source.stageInstanceId) ||
            source.layoutVersion <= 0 || source.rewardTableVersion != TableVersion ||
            source.spawnIds == null || source.spawnIds.Length == 0 ||
            source.spawnIds.Any(string.IsNullOrWhiteSpace) ||
            source.spawnIds.Distinct(StringComparer.Ordinal).Count() != source.spawnIds.Length ||
            Enum.GetValues(typeof(ShopRarity)).Cast<ShopRarity>().Any(r => Pool(r).Length == 0)) return false;
        manifest = new ChestManifest { stageInstanceId = source.stageInstanceId,
            layoutVersion = source.layoutVersion, rewardTableVersion = source.rewardTableVersion,
            spawnIds = source.spawnIds.OrderBy(id => id, StringComparer.Ordinal).ToArray() };
        return true;
    }

    public static string ManifestHash(ChestManifest manifest) => Hash(Key(
        manifest.stageInstanceId, manifest.layoutVersion.ToString(), manifest.rewardTableVersion.ToString(),
        Key(manifest.spawnIds)));

    public static ChestRecord Plan(string stageId, string spawnId) =>
        Roll(spawnId, new StableRandom(Key("chest.v1", stageId, spawnId)));

    public static string Hash(string value)
    {
        using (var hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
    }

    // Explicit little-endian SHA256 seed + xorshift32, rejection sampling for bounds.
    private sealed class StableRandom : IEconomyRandom
    {
        private uint state;
        public StableRandom(string key)
        {
            using (var hash = SHA256.Create())
            {
                byte[] b = hash.ComputeHash(Encoding.UTF8.GetBytes(key));
                state = (uint)b[0] | (uint)b[1] << 8 | (uint)b[2] << 16 | (uint)b[3] << 24;
                if (state == 0) state = 0x9e3779b9;
            }
        }
        public int Next(int minInclusive, int maxExclusive)
        {
            uint bound = (uint)(maxExclusive - minInclusive);
            if (bound == 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            uint threshold = unchecked(0u - bound) % bound;
            uint value;
            do { state ^= state << 13; state ^= state >> 17; state ^= state << 5; value = state; }
            while (value < threshold);
            return minInclusive + (int)(value % bound);
        }
    }

    public static bool ValidateReward(ChestRewardReceipt reward)
    {
        return reward != null && !string.IsNullOrWhiteSpace(reward.stageInstanceId) &&
            !string.IsNullOrWhiteSpace(reward.spawnId) && reward.pieceAmount == 1 &&
            ShopCatalog.TryGet(reward.pieceId, out ShopEntry entry) && entry.Rarity == reward.rarity &&
            reward.rolledCoins >= 7 && reward.rolledCoins <= 17 &&
            reward.appliedCoins >= 0 && reward.appliedCoins <= reward.rolledCoins &&
            reward.grantId == OpenId(reward.stageInstanceId, reward.spawnId);
    }

    public static bool ValidateProfile(EconomyProfileData data)
    {
        foreach (EconomyReceipt receipt in data.processedTransactions)
        {
            if (receipt.hasChestReward != (receipt.chestReward != null)) return false;
            if (!receipt.hasChestReward) continue;
            ChestRewardReceipt reward = receipt.chestReward;
            if (!ValidateReward(reward) || receipt.id != reward.grantId ||
                receipt.requestKey != "chest.open" || receipt.result.RequestedAmount != reward.rolledCoins ||
                receipt.result.AppliedAmount != reward.appliedCoins) return false;
        }
        TemporaryStageData stage = data.run?.stage;
        if (stage == null) return true;
        if (stage.hasChestState != (stage.chestState != null)) return false;
        if (!stage.hasChestState) return true;
        ChestStageState state = stage.chestState;
        if (state.stageInstanceId != stage.id || state.rewardTableVersion != TableVersion ||
            state.records == null || state.records.Any(record => record == null)) return false;
        var manifest = new ChestManifest { stageInstanceId = stage.id, layoutVersion = state.layoutVersion,
            rewardTableVersion = state.rewardTableVersion, spawnIds = state.records.Select(r => r.spawnId).ToArray() };
        if (!TryNormalizeManifest(manifest, out ChestManifest normalized) || ManifestHash(normalized) != state.manifestHash)
            return false;
        if (!data.processedTransactions.Exists(r => r.id == RegisterId(stage.id) && r.requestKey == "chest.register:" + state.manifestHash))
            return false;
        foreach (ChestRecord record in state.records)
        {
            if (!ShopCatalog.TryGet(record.pieceId, out ShopEntry entry) || entry.Rarity != record.rarity ||
                record.pieceAmount != 1 || record.rolledCoins < 7 || record.rolledCoins > 17) return false;
            if (!record.opened)
            {
                if (!string.IsNullOrEmpty(record.openTransactionId) || record.appliedCoins != 0 ||
                    data.processedTransactions.Exists(r => r.id == OpenId(stage.id, record.spawnId))) return false;
                continue;
            }
            EconomyReceipt receipt = data.processedTransactions.Find(r => r.id == record.openTransactionId);
            ChestRewardReceipt reward = receipt?.chestReward;
            if (reward == null || reward.stageInstanceId != stage.id || reward.spawnId != record.spawnId ||
                reward.pieceId != record.pieceId || reward.rarity != record.rarity ||
                reward.pieceAmount != record.pieceAmount || reward.rolledCoins != record.rolledCoins ||
                reward.appliedCoins != record.appliedCoins) return false;
        }
        return true;
    }
}
