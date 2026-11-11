using System;

public sealed partial class EconomyService
{
    public TutorialProgress TutorialState => profile.narrative.tutorial;
    public FirstAltarProgress FirstAltarSnapshot() => profile.Copy().narrative.firstAltar;

    public CurrencyResult TryMarkTutorialCompleted() => Execute("story:tutorial:complete:" + profile.profileId,
        RequestKey("story.tutorial.complete"), "story.tutorial.complete", data =>
        {
            data.narrative.tutorial = TutorialProgress.Completed;
            return Success(0, 0, data.balance, activeTransactionId);
        });

    public CurrencyResult TryExitCompletedStage(string stageId, float hp)
    {
        if (string.IsNullOrWhiteSpace(stageId)) return Fail(EconomyError.InvalidTransaction);
        // Keep the 1.0 receipt identity so an old successful exit cannot be played again.
        return Execute("return:" + stageId, RequestKey("run.return", stageId), "run.return", data =>
        {
            if (!IsCurrentStage(data, stageId) || !data.run.stage.completed || data.run.stage.role == BranchRole.Boss)
                return Fail(EconomyError.StateConflict);
            if (!LobbyRules.ValidHealth(hp, data.run.maxHp)) return Fail(EconomyError.InvalidAmount);
            LobbyRunData run = data.run;
            bool combat = run.stage.role == BranchRole.Combat;
            if (combat)
            {
                if (run.combatClears >= int.MaxValue - 1) return Fail(EconomyError.StateConflict);
                run.combatClears++;
            }
            run.hp = hp; run.hasPosition = false;
            if (combat && NarrativeRules.CanEnter(data.narrative))
            {
                FirstAltarProgress story = data.narrative.firstAltar;
                if (story.phase == FirstAltarPhase.NotStarted) story.phase = FirstAltarPhase.AwaitingApproach;
                story.instanceId = NarrativeRules.InstanceId(data.profileId);
                story.sourceRunId = run.id; story.sourceStageId = stageId;
                run.area = RunArea.StoryRoom;
            }
            else if (!PrepareNextLobby(run, stageId)) return Fail(EconomyError.StateConflict);
            FlushReservations(data);
            return Success(0, 0, data.balance, activeTransactionId);
        });
    }

    public CurrencyResult TryCommitFirstAltarDeath(string instanceId) => AdvanceFirstAltar(instanceId,
        FirstAltarPhase.AwaitingApproach, FirstAltarPhase.BelieverDead, "death");

    public CurrencyResult TryMarkFirstAltarStoryRead(string instanceId) => AdvanceFirstAltar(instanceId,
        FirstAltarPhase.BelieverDead, FirstAltarPhase.StoryRead, "read");

    private CurrencyResult AdvanceFirstAltar(string instanceId, FirstAltarPhase from, FirstAltarPhase to, string operation)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return Fail(EconomyError.InvalidTransaction);
        return Execute("story:first-altar:" + operation + ":" + instanceId,
            RequestKey("story.first-altar." + operation, instanceId), "story.first-altar." + operation, data =>
            {
                if (!IsCurrentFirstAltar(data, instanceId) || data.narrative.firstAltar.phase != from)
                    return Fail(EconomyError.StateConflict);
                data.narrative.firstAltar.phase = to;
                return Success(0, 0, data.balance, activeTransactionId);
            });
    }

    public CurrencyResult TryLeaveFirstAltar(string instanceId, float hp)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return Fail(EconomyError.InvalidTransaction);
        return Execute("story:first-altar:leave:" + instanceId, RequestKey("story.first-altar.leave", instanceId),
            "story.first-altar.leave", data =>
            {
                if (!IsCurrentFirstAltar(data, instanceId) || data.narrative.firstAltar.phase != FirstAltarPhase.StoryRead)
                    return Fail(EconomyError.StateConflict);
                if (!LobbyRules.ValidHealth(hp, data.run.maxHp)) return Fail(EconomyError.InvalidAmount);
                if (!PrepareNextLobby(data.run, data.narrative.firstAltar.sourceStageId)) return Fail(EconomyError.StateConflict);
                data.run.hp = hp;
                data.narrative.firstAltar.phase = FirstAltarPhase.Completed;
                FlushReservations(data);
                return Success(0, 0, data.balance, activeTransactionId);
            });
    }

    private static bool IsCurrentFirstAltar(EconomyProfileData data, string instanceId) =>
        data.hasRun && data.run != null && data.run.area == RunArea.StoryRoom && data.run.hp > 0 &&
        data.run.hasStage && data.run.stage.role == BranchRole.Combat && data.run.stage.completed &&
        data.narrative.firstAltar.instanceId == instanceId && data.narrative.firstAltar.sourceRunId == data.run.id &&
        data.narrative.firstAltar.sourceStageId == data.run.stage.id;

    private static bool PrepareNextLobby(LobbyRunData run, string stageId)
    {
        if (run.visit >= int.MaxValue - 1) return false;
        run.visit++;
        run.area = RunArea.Lobby; run.hasStage = false; run.stage = null; run.hasPosition = false;
        int seed = BitConverter.ToInt32(new Guid(LobbyRules.StableId("return:" + stageId)).ToByteArray(), 0) & int.MaxValue;
        // Recompute on the current transaction copy: no stale HP/progress cache after a failed save.
        LobbyRules.GenerateCandidates(run, seed);
        return true;
    }
}
