using System;
using UnityEngine;

[Serializable]
public sealed class PrayerCue
{
    [TextArea] public string text;
    // Retained for existing serialized data; the introduction now advances by click.
    [HideInInspector] public float holdSeconds = 2.8f;
    public PrayerCue(string text, float seconds = 2.8f) { this.text = text; holdSeconds = seconds; }
}

[CreateAssetMenu(menuName = "Story/First Altar", fileName = "FirstAltarStoryDefinition")]
public sealed class FirstAltarStoryDefinition : ScriptableObject
{
    public const string DeathStageId = "story.first-altar.death.v1";
    public const float PostImpactBlackSeconds = 3f;
    public PrayerCue[] introCues = DefaultPrayer();
    public string apologyLoopText = "죄송합니다.";
    [Min(.1f)] public float apologySeconds = 1.5f;
    [Min(1f)] public float charactersPerSecond = 22f;
    [Min(.1f)] public float approachDistance = 2.5f;
    public bool bubbleGlitch = true;
    public AudioClip rainLoop;
    public AudioClip impactClip;
    [Range(0f, 1f)] public float rainBaseVolume = .12f, rainPeakVolume = .8f, impactVolume = .9f;
    [Min(.1f)] public float rainRiseSeconds = 2.5f;
    [TextArea] public string documentTitle = "기록";
    [TextArea(5, 20)] public string documentBody = "스토리 본문을 준비 중입니다.";
    public CutsceneDefinition deathSequence;

    public CutsceneDefinition CreateIntroSequence()
    {
        var sequence = CreateInstance<CutsceneDefinition>();
        sequence.name = "First altar prayer introduction";
        var steps = new CutsceneStep[introCues.Length];
        for (int i = 0; i < steps.Length; i++)
            steps[i] = CutsceneStep.Say("firstAltarBeliever",
                string.IsNullOrEmpty(introCues[i].text) ? "..." : introCues[i].text, charactersPerSecond);
        sequence.Configure("story.first-altar.prayer.v1", steps);
        return sequence;
    }

    public static PrayerCue[] DefaultPrayer() => new[]
    {
        new PrayerCue("하나. 구름은 모든 것을 내려다본다"),
        new PrayerCue("둘. 비는 축복이다"),
        new PrayerCue("셋. 맑은 하늘은 없을 것이다."),
        new PrayerCue("넷. 구름을 믿어라."),
        new PrayerCue("", 1.8f),
        new PrayerCue("비는, 구름은, 비는, 구름은..."),
        new PrayerCue("", 1.8f),
        new PrayerCue("비가 너무 많이 와요."),
        new PrayerCue("모두가 잠겨버렸습니다."),
        new PrayerCue("비를 그쳐주시면..."),
        new PrayerCue("아...", 1.5f),
        new PrayerCue("제가 무슨 말을."),
        new PrayerCue("죄송합니다.")
    };

    public bool IsUsable => introCues != null && introCues.Length > 0 &&
        Array.TrueForAll(introCues, cue => cue != null) &&
        !string.IsNullOrWhiteSpace(apologyLoopText) && apologySeconds > 0 && charactersPerSecond > 0 &&
        approachDistance > 0 && rainRiseSeconds > 0 && rainLoop != null && impactClip != null &&
        deathSequence != null && deathSequence.Steps.Length == 1 &&
        deathSequence.Steps[0].Type == CutsceneStepType.ExternalStage &&
        deathSequence.Steps[0].ExternalStageId == DeathStageId;
}
