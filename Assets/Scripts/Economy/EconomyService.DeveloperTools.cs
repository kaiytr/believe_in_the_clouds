using System;
using UnityEngine;

// Development actions use the normal copy/save/publish gate without resetting a run.
public sealed partial class EconomyService
{
    public CurrencyResult TryUnlockBossForDevelopment()
    {
        string runId = profile.run?.id;
        if (!profile.hasRun || string.IsNullOrEmpty(runId)) return Fail(EconomyError.StateConflict);
        string id = "dev:boss-unlock:" + runId;
        return Execute(id, RequestKey("dev.boss-unlock", runId), "dev.boss-unlock", data =>
        {
            if (!HasLivingDeveloperRun(data, runId)) return Fail(EconomyError.StateConflict, id);
            data.run.combatClears = Mathf.Max(data.run.combatClears, LobbyRules.BossUnlockClears);
            return Success(0, 0, data.balance, activeTransactionId);
        });
    }

    // The caller retains this request and its source state after a failed write.
    // A stable request seed also survives reopening the service before retrying.
    public CurrencyResult TryRerollLobbyForDevelopment(string requestId, string expectedBatchId,
        int expectedCombatClears, float hp, Vector2 position)
    {
        if (!Guid.TryParseExact(requestId, "N", out _) || string.IsNullOrEmpty(expectedBatchId))
            return Fail(EconomyError.InvalidTransaction);
        string runId = profile.run?.id;
        if (!profile.hasRun || string.IsNullOrEmpty(runId)) return Fail(EconomyError.StateConflict);
        string id = "dev:lobby-reroll:" + requestId;
        return Execute(id, RequestKey("dev.lobby-reroll", runId, expectedBatchId,
            expectedCombatClears.ToString(System.Globalization.CultureInfo.InvariantCulture)), "dev.lobby-reroll", data =>
        {
            if (!HasLivingDeveloperRun(data, runId) || data.run.area != RunArea.Lobby || data.run.hasStage ||
                data.run.batchId != expectedBatchId || data.run.combatClears != expectedCombatClears)
                return Fail(EconomyError.StateConflict, id);
            if (!LobbyRules.ValidHealth(hp, data.run.maxHp) || !LobbyRules.Finite(position.x) || !LobbyRules.Finite(position.y))
                return Fail(EconomyError.InvalidAmount, id);

            string batch = LobbyRules.StableId("dev:lobby-reroll:" + runId + ":" + requestId);
            int seed = BitConverter.ToInt32(new Guid(batch).ToByteArray(), 0) & int.MaxValue;
            LobbyRules.GenerateCandidates(data.run, seed);
            // GenerateCandidates normally derives identities from visit. Rerolls keep visit unchanged.
            data.run.batchId = batch;
            for (int i = 0; i < data.run.candidates.Count; i++)
                data.run.candidates[i].id = batch + ":" + i;
            data.run.hp = hp;
            data.run.hasPosition = true;
            data.run.playerX = position.x; data.run.playerY = position.y;
            return Success(0, 0, data.balance, activeTransactionId);
        });
    }

    private static bool HasLivingDeveloperRun(EconomyProfileData data, string runId) =>
        data.hasRun && data.run != null && data.run.id == runId && data.run.hp > 0 &&
        (data.run.area == RunArea.Lobby || data.run.area == RunArea.TemporaryStage);
}
