using System;

public enum TutorialProgress { NotCompleted = 0, Completed = 1, LegacyEligible = 2 }
public enum FirstAltarPhase { NotStarted = 0, AwaitingApproach = 1, BelieverDead = 2, StoryRead = 3, Completed = 4 }

[Serializable]
public sealed class NarrativeProgressData
{
    public int version = 1;
    public TutorialProgress tutorial;
    public FirstAltarProgress firstAltar = new FirstAltarProgress();
}

[Serializable]
public sealed class FirstAltarProgress
{
    public FirstAltarPhase phase;
    public string instanceId;
    public string sourceRunId;
    public string sourceStageId;
}

public static class NarrativeRules
{
    public const string EventId = "story.first-altar.v1";
    public static string InstanceId(string profileId) => LobbyRules.StableId(EventId + ":" + profileId);
    public static bool CanEnter(NarrativeProgressData narrative) => narrative != null &&
        narrative.tutorial != TutorialProgress.NotCompleted && narrative.firstAltar != null &&
        narrative.firstAltar.phase != FirstAltarPhase.Completed;

    public static bool Validate(EconomyProfileData profile)
    {
        NarrativeProgressData narrative = profile.narrative;
        FirstAltarProgress story = narrative?.firstAltar;
        if (narrative == null || narrative.version != 1 || story == null ||
            !Enum.IsDefined(typeof(TutorialProgress), narrative.tutorial) ||
            !Enum.IsDefined(typeof(FirstAltarPhase), story.phase)) return false;
        bool inRoom = profile.hasRun && profile.run.area == RunArea.StoryRoom;
        if (story.phase == FirstAltarPhase.NotStarted)
            return !inRoom && string.IsNullOrEmpty(story.instanceId) &&
                string.IsNullOrEmpty(story.sourceRunId) && string.IsNullOrEmpty(story.sourceStageId);
        if (narrative.tutorial == TutorialProgress.NotCompleted || story.instanceId != InstanceId(profile.profileId) ||
            string.IsNullOrWhiteSpace(story.sourceRunId) || string.IsNullOrWhiteSpace(story.sourceStageId)) return false;
        if (!inRoom) return true; // A forced death can replace the run; the profile's unfinished story survives.
        LobbyRunData run = profile.run;
        return story.phase != FirstAltarPhase.Completed && run.hp > 0 && run.hasStage &&
            run.stage.role == BranchRole.Combat && run.stage.completed && run.combatClears > 0 &&
            story.sourceRunId == run.id && story.sourceStageId == run.stage.id;
    }
}
