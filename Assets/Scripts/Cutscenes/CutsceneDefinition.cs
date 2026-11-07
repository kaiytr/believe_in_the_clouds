using System;
using UnityEngine;

public enum CutsceneStepType
{
    Dialogue,
    Choice,
    MoveActor,
    HarmlessProjectile,
    ExternalStage
}

[Serializable]
public sealed class CutsceneStep
{
    [SerializeField] private CutsceneStepType type;
    [SerializeField] private string actorId;
    [TextArea(1, 5)] [SerializeField] private string text;
    [SerializeField] private string[] choices = Array.Empty<string>();
    [SerializeField] private string anchorId;
    [Min(0f)] [SerializeField] private float movementSpeed = 4f;
    [Min(0.1f)] [SerializeField] private float typingCharactersPerSecond = 30f;
    [SerializeField] private GameObject harmlessProjectilePrefab;
    [Min(0f)] [SerializeField] private float projectileDuration = 0.55f;
    [Min(0f)] [SerializeField] private float projectileArcHeight = 0.6f;
    [SerializeField] private string externalStageId;
    [Tooltip("Move step only: keep facing this actor while walking, so the mover backs away instead of turning its back. Empty = face the walking direction.")]
    [SerializeField] private string facingActorId;

    public CutsceneStepType Type => type;
    public string ActorId => actorId;
    public string Text => text;
    public string[] Choices => choices ?? Array.Empty<string>();
    public string AnchorId => anchorId;
    public float MovementSpeed => movementSpeed;
    public float TypingCharactersPerSecond => typingCharactersPerSecond;
    public GameObject HarmlessProjectilePrefab => harmlessProjectilePrefab;
    public float ProjectileDuration => projectileDuration;
    public float ProjectileArcHeight => projectileArcHeight;
    public string ExternalStageId => externalStageId;
    public string FacingActorId => facingActorId;

    public static CutsceneStep Say(string speakerId, string line, float charsPerSecond = 30f)
    {
        return new CutsceneStep
        {
            type = CutsceneStepType.Dialogue,
            actorId = speakerId,
            text = line,
            typingCharactersPerSecond = charsPerSecond
        };
    }

    public static CutsceneStep Choose(string speakerId, params string[] options)
    {
        return new CutsceneStep
        {
            type = CutsceneStepType.Choice,
            actorId = speakerId,
            choices = options ?? Array.Empty<string>()
        };
    }

    /// <summary>Walks backwards: moves to the destination while always facing `facingActorId`.</summary>
    public static CutsceneStep BackAway(string speakerId, string destinationId, string facingActorId, float speed = 4f)
    {
        CutsceneStep step = Move(speakerId, destinationId, speed);
        step.facingActorId = facingActorId;
        return step;
    }

    public static CutsceneStep Move(string speakerId, string destinationId, float speed = 4f)
    {
        return new CutsceneStep
        {
            type = CutsceneStepType.MoveActor,
            actorId = speakerId,
            anchorId = destinationId,
            movementSpeed = speed
        };
    }

    public static CutsceneStep ThrowHarmlessly(string actorId, string destinationId, GameObject projectilePrefab,
        float duration = 0.55f, float arcHeight = 0.6f)
    {
        return new CutsceneStep
        {
            type = CutsceneStepType.HarmlessProjectile,
            actorId = actorId,
            anchorId = destinationId,
            harmlessProjectilePrefab = projectilePrefab,
            projectileDuration = duration,
            projectileArcHeight = arcHeight
        };
    }

    public static CutsceneStep External(string stageId)
    {
        return new CutsceneStep
        {
            type = CutsceneStepType.ExternalStage,
            externalStageId = stageId
        };
    }
}

[CreateAssetMenu(fileName = "CutsceneDefinition", menuName = "Cutscenes/Definition")]
public sealed class CutsceneDefinition : ScriptableObject
{
    [SerializeField] private string sequenceId;
    [SerializeField] private CutsceneStep[] steps = Array.Empty<CutsceneStep>();

    public string SequenceId => sequenceId;
    public CutsceneStep[] Steps => steps ?? Array.Empty<CutsceneStep>();

    public void Configure(string id, CutsceneStep[] sequenceSteps)
    {
        sequenceId = id;
        steps = sequenceSteps ?? Array.Empty<CutsceneStep>();
    }
}

[Serializable]
public sealed class CutsceneActorBinding
{
    [SerializeField] private string id;
    [SerializeField] private CutsceneCharacter character;

    public string Id => id;
    public CutsceneCharacter Character => character;

    public CutsceneActorBinding(string actorId, CutsceneCharacter actor)
    {
        id = actorId;
        character = actor;
    }
}

[Serializable]
public sealed class CutsceneAnchorBinding
{
    [SerializeField] private string id;
    [SerializeField] private Transform anchor;

    public string Id => id;
    public Transform Anchor => anchor;

    public CutsceneAnchorBinding(string anchorId, Transform value)
    {
        id = anchorId;
        anchor = value;
    }
}
