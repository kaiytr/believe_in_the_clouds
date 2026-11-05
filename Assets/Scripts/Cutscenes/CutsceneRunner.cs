using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CutsceneRunner : MonoBehaviour
{
    [Header("Sequence")]
    [SerializeField] private CutsceneDefinition definition;
    [SerializeField] private CutsceneBubbleView bubbleView;
    [SerializeField] private bool playOnStart;
    [SerializeField] private CutsceneActorBinding[] actors = Array.Empty<CutsceneActorBinding>();
    [SerializeField] private CutsceneAnchorBinding[] anchors = Array.Empty<CutsceneAnchorBinding>();

    private readonly CutsceneDialogueInputGate dialogueGate = new CutsceneDialogueInputGate();
    private readonly List<BehaviourState> suppressedBehaviours = new List<BehaviourState>(16);
    private CutsceneDefinition activeDefinition;
    private CutsceneDefinition lastCompletedDefinition;
    private CutsceneDefinition lastCancelledDefinition;
    private PlayerHealth player;
    private PlayerMove playerMove;
    private PlayerAttack playerAttack;
    private PlayerDash playerDash;
    private Rigidbody2D playerBody;
    private bool previousControlLocked;
    private bool playerDashWasEnabled;
    private bool dashDisabledForCutscene;
    private object modalToken;
    private Coroutine runningCoroutine;
    private CutsceneExternalStage activeExternalStage;
    private GameObject activeProjectileVisual;
    private int runGeneration;
    private bool running;
    private bool cleanupApplied;

    private struct BehaviourState
    {
        public Behaviour Behaviour;
        public bool WasEnabled;
        public BehaviourState(Behaviour component)
        {
            Behaviour = component;
            WasEnabled = component != null && component.enabled;
        }
    }

    public bool IsRunning => running;
    public CutsceneDefinition ActiveDefinition => activeDefinition;
    public CutsceneDefinition LastCompletedDefinition => lastCompletedDefinition;
    public CutsceneDefinition LastCancelledDefinition => lastCancelledDefinition;

    public event Action<CutsceneRunner> Completed;
    public event Action<CutsceneRunner> Cancelled;
    public event Action<CutsceneExternalStage> ExternalStageRequested;

    private void Awake()
    {
        if (bubbleView == null)
            bubbleView = GetComponent<CutsceneBubbleView>();
    }

    private void Start()
    {
        if (playOnStart && definition != null)
            Play(definition);
    }

    public bool Play()
    {
        return Play(definition);
    }

    public bool Play(CutsceneDefinition sequence, PlayerHealth playerOverride = null,
        bool suppressBoundEnemyActors = false)
    {
        if (!isActiveAndEnabled || running || sequence == null)
            return false;

        activeDefinition = sequence;
        lastCompletedDefinition = null;
        lastCancelledDefinition = null;
        running = true;
        cleanupApplied = false;
        runGeneration++;
        dialogueGate.Reset();

        if (bubbleView == null)
            bubbleView = GetComponent<CutsceneBubbleView>();
        if (bubbleView == null)
            bubbleView = gameObject.AddComponent<CutsceneBubbleView>();

        player = playerOverride != null ? playerOverride : FindPlayer();
        AcquireInputLock();
        if (suppressBoundEnemyActors)
            SuppressBoundEnemies();

        runningCoroutine = StartCoroutine(RunSequence(runGeneration));
        return true;
    }

    public void SetDefaultDefinition(CutsceneDefinition value) => definition = value;

    public void SetActorBindings(params CutsceneActorBinding[] values)
    {
        actors = values ?? Array.Empty<CutsceneActorBinding>();
    }

    public void SetAnchorBindings(params CutsceneAnchorBinding[] values)
    {
        anchors = values ?? Array.Empty<CutsceneAnchorBinding>();
    }

    public void Cancel()
    {
        if (!running)
            return;

        CutsceneDefinition cancelledDefinition = activeDefinition;
        running = false;
        runGeneration++;
        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }
        activeExternalStage = null;
        lastCancelledDefinition = cancelledDefinition;
        Cleanup();
        Notify(Cancelled);
    }

    internal void ResolveExternalStage(CutsceneExternalStage stage, int generation, bool cancel)
    {
        if (!running || stage == null || stage != activeExternalStage || generation != runGeneration)
            return;

        stage.MarkResolved(cancel);
        activeExternalStage = null;
        if (cancel)
            Cancel();
    }

    private IEnumerator RunSequence(int generation)
    {
        CutsceneStep[] steps = activeDefinition.Steps;
        for (int i = 0; i < steps.Length && running && generation == runGeneration; i++)
        {
            CutsceneStep step = steps[i];
            if (step == null)
                continue;

            switch (step.Type)
            {
                case CutsceneStepType.Dialogue:
                {
                    CutsceneCharacter speaker = ResolveActor(step.ActorId);
                    if (speaker == null)
                    {
                        Fail("화자 '" + step.ActorId + "' 참조가 없습니다.");
                        yield break;
                    }
                    bubbleView.ShowLine(speaker, string.Empty);
                    yield return TypeAndWaitForAdvance(step, speaker, step.Text, generation);
                    break;
                }
                case CutsceneStepType.Choice:
                {
                    CutsceneCharacter speaker = ResolveActor(step.ActorId);
                    if (speaker == null || step.Choices.Length == 0)
                    {
                        Fail("선택지 화자 또는 선택 항목이 없습니다: " + step.ActorId);
                        yield break;
                    }

                    int selected = -1;
                    int selectionFrame = -1;
                    dialogueGate.BeginChoice();
                    bubbleView.ShowChoices(speaker, step.Choices, index =>
                    {
                        if (running && dialogueGate.ChoicePending && selected < 0 &&
                            index >= 0 && index < step.Choices.Length)
                        {
                            selected = index;
                            selectionFrame = Time.frameCount;
                        }
                    });

                    while (running && generation == runGeneration && selected < 0)
                        yield return null;

                    if (!running || generation != runGeneration)
                        yield break;

                    bubbleView.HideChoices();
                    dialogueGate.ChoiceSelected();
                    // A Button click must not also become the first click on the selected line.
                    while (running && generation == runGeneration && Time.frameCount <= selectionFrame)
                        yield return null;

                    yield return TypeAndWaitForAdvance(step, speaker, step.Choices[selected], generation);
                    break;
                }
                case CutsceneStepType.MoveActor:
                {
                    CutsceneCharacter actor = ResolveActor(step.ActorId);
                    Transform destination = ResolveAnchor(step.AnchorId);
                    if (actor == null || destination == null)
                    {
                        Fail("이동 배우 또는 앵커 참조가 없습니다: " + step.ActorId + " -> " + step.AnchorId);
                        yield break;
                    }
                    yield return MoveActor(actor, destination, step.MovementSpeed, generation);
                    break;
                }
                case CutsceneStepType.HarmlessProjectile:
                {
                    CutsceneCharacter actor = ResolveActor(step.ActorId);
                    Transform destination = ResolveAnchor(step.AnchorId);
                    if (actor == null || destination == null || step.HarmlessProjectilePrefab == null)
                    {
                        Fail("무피해 투사체의 배우, 목적지 또는 시각 프리팹 참조가 없습니다.");
                        yield break;
                    }
                    yield return PlayHarmlessProjectile(actor, destination, step, generation);
                    break;
                }
                case CutsceneStepType.ExternalStage:
                    yield return WaitForExternalStage(step.ExternalStageId, generation);
                    break;
            }
        }

        if (!running || generation != runGeneration)
            yield break;

        // Keep the modal token through mouse-up and one further frame so the final click
        // cannot be observed by PlayerAttack after this runner releases its token.
        while (Input.GetMouseButton(0) && running && generation == runGeneration)
            yield return null;
        yield return null;

        if (running && generation == runGeneration)
            FinishCompleted();
    }

    private IEnumerator TypeAndWaitForAdvance(CutsceneStep step, CutsceneCharacter speaker,
        string fullText, int generation)
    {
        string value = fullText ?? string.Empty;
        int[] boundaries = StringInfo.ParseCombiningCharacters(value);
        int total = boundaries.Length;
        float rate = Mathf.Max(0.1f, step.TypingCharactersPerSecond);
        float elapsed = 0f;
        int shown = 0;
        bool consumedTypingClick = false;

        dialogueGate.BeginTyping();
        bubbleView.ShowLine(speaker, string.Empty);

        while (shown < total && running && generation == runGeneration)
        {
            if (Input.GetMouseButtonDown(0))
            {
                CutsceneDialogueClickResult result = dialogueGate.HandleClick();
                if (result == CutsceneDialogueClickResult.TypingCompleted)
                {
                    shown = total;
                    bubbleView.SetVisibleText(value);
                    consumedTypingClick = true;
                    break;
                }
            }

            elapsed += Time.unscaledDeltaTime * rate;
            int desired = Mathf.Min(total, Mathf.FloorToInt(elapsed));
            if (desired > shown)
            {
                shown = desired;
                int end = shown >= total ? value.Length : boundaries[shown];
                bubbleView.SetVisibleText(value.Substring(0, end));
            }

            yield return null;
        }

        if (!running || generation != runGeneration)
            yield break;

        bubbleView.SetVisibleText(value);
        dialogueGate.MarkFullyTyped();
        if (consumedTypingClick)
            yield return null;

        while (running && generation == runGeneration)
        {
            if (Input.GetMouseButtonDown(0) &&
                dialogueGate.HandleClick() == CutsceneDialogueClickResult.Advanced)
                yield break;
            yield return null;
        }
    }

    private IEnumerator MoveActor(CutsceneCharacter actor, Transform destination, float speed, int generation)
    {
        Vector3 target = destination.position;
        actor.FaceToward(target);
        actor.SetCutscenePose(CutscenePose.Walking);

        if (speed <= 0f)
        {
            actor.SetWorldPosition(target);
        }
        else
        {
            while (running && generation == runGeneration &&
                   (actor.transform.position - target).sqrMagnitude > 0.0004f)
            {
                Vector3 next = Vector3.MoveTowards(actor.transform.position, target, speed * Time.unscaledDeltaTime);
                if (actor.Enemy != null && actor.Body != null)
                    actor.Body.linearVelocity = (next - actor.transform.position) / Mathf.Max(.0001f, Time.unscaledDeltaTime);
                actor.SetWorldPosition(next);
                yield return null;
            }
        }

        if (running && generation == runGeneration)
        {
            actor.SetWorldPosition(target);
            if (actor.Enemy != null && actor.Body != null) actor.Body.linearVelocity = Vector2.zero;
            actor.SetCutscenePose(CutscenePose.Idle);
        }
    }

    private IEnumerator PlayHarmlessProjectile(CutsceneCharacter actor, Transform destination,
        CutsceneStep step, int generation)
    {
        Vector3 start = actor.ProjectileWorldPosition;
        Vector3 end = destination.position;
        activeProjectileVisual = Instantiate(step.HarmlessProjectilePrefab, start, Quaternion.identity);
        CutsceneHarmlessProjectile.Prepare(activeProjectileVisual);
        actor.FaceToward(end);
        actor.SetCutscenePose(CutscenePose.Throwing);

        float duration = Mathf.Max(0.1f, step.ProjectileDuration);
        float elapsed = 0f;
        while (running && generation == runGeneration && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 position = Vector3.Lerp(start, end, t);
            position.y += 4f * step.ProjectileArcHeight * t * (1f - t);
            activeProjectileVisual.transform.position = position;
            yield return null;
        }

        if (activeProjectileVisual != null)
            Destroy(activeProjectileVisual);
        activeProjectileVisual = null;
        if (running && generation == runGeneration)
            actor.SetCutscenePose(CutscenePose.Idle);
    }

    private IEnumerator WaitForExternalStage(string stageId, int generation)
    {
        CutsceneExternalStage stage = new CutsceneExternalStage(this, generation, stageId);
        activeExternalStage = stage;
        Action<CutsceneExternalStage> handlers = ExternalStageRequested;

        if (handlers == null)
        {
            stage.Complete();
        }
        else
        {
            foreach (Delegate callback in handlers.GetInvocationList())
            {
                try { ((Action<CutsceneExternalStage>)callback)(stage); }
                catch (Exception ex) { Debug.LogException(ex, this); }
            }
        }

        while (running && generation == runGeneration && !stage.IsResolved)
            yield return null;

        if (stage.WasCancelled)
            Cancel();
    }

    private void AcquireInputLock()
    {
        modalToken = new object();
        UiModal.Push(modalToken);

        if (player == null)
            return;

        playerMove = player.GetComponent<PlayerMove>();
        playerAttack = player.GetComponent<PlayerAttack>();
        playerDash = player.GetComponent<PlayerDash>();
        playerBody = player.GetComponent<Rigidbody2D>();

        playerDashWasEnabled = playerDash != null && playerDash.enabled;

        if (playerAttack != null && playerAttack.IsAttacking)
            playerAttack.CancelAttack();

        if (playerDash != null && playerDash.enabled && playerDash.IsDashing)
        {
            playerDash.enabled = false;
            dashDisabledForCutscene = true;
        }

        // 취소한 공격/대시가 소유하던 잠금은 복원하지 않는다.
        previousControlLocked = playerMove != null && playerMove.ControlLocked;
        if (playerMove != null)
            playerMove.ControlLocked = previousControlLocked;
        if (playerBody != null)
            playerBody.linearVelocity = new Vector2(0f, playerBody.linearVelocity.y);
    }

    private void SuppressBoundEnemies()
    {
        if (actors == null)
            return;

        HashSet<EnemyUnit> seen = new HashSet<EnemyUnit>();
        for (int i = 0; i < actors.Length; i++)
        {
            CutsceneCharacter character = actors[i] != null ? actors[i].Character : null;
            EnemyUnit enemy = character != null ? character.Enemy : null;
            if (enemy == null || enemy.IsDead || !seen.Add(enemy))
                continue;

            DisableAndRemember(enemy);
            foreach (EnemyAttack attack in enemy.GetComponentsInChildren<EnemyAttack>(true))
                DisableAndRemember(attack);
            foreach (EnemyContactDamage contact in enemy.GetComponentsInChildren<EnemyContactDamage>(true))
                DisableAndRemember(contact);
            foreach (BeliefView view in enemy.GetComponentsInChildren<BeliefView>(true))
                DisableAndRemember(view);

            Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
            if (body != null)
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }
    }

    private void DisableAndRemember(Behaviour component)
    {
        if (component == null)
            return;
        suppressedBehaviours.Add(new BehaviourState(component));
        component.enabled = false;
    }

    private CutsceneCharacter ResolveActor(string id)
    {
        if (actors != null)
        {
            for (int i = 0; i < actors.Length; i++)
            {
                if (actors[i] != null && actors[i].Id == id)
                    return actors[i].Character;
            }
        }

        if (id == "player")
        {
            PlayerHealth foundPlayer = player != null ? player : FindPlayer();
            if (foundPlayer != null)
            {
                CutsceneCharacter character = foundPlayer.GetComponent<CutsceneCharacter>();
                if (character == null)
                {
                    character = foundPlayer.gameObject.AddComponent<CutsceneCharacter>();
                    character.Configure("플레이어", null);
                }
                return character;
            }
        }

        return null;
    }

    private Transform ResolveAnchor(string id)
    {
        if (anchors == null)
            return null;
        for (int i = 0; i < anchors.Length; i++)
        {
            if (anchors[i] != null && anchors[i].Id == id)
                return anchors[i].Anchor;
        }
        return null;
    }

    private static PlayerHealth FindPlayer()
    {
        GameObject actor = GameObject.FindGameObjectWithTag("Player");
        return actor != null ? actor.GetComponent<PlayerHealth>() : null;
    }

    private void Fail(string message)
    {
        Debug.LogError(name + ": cutscene cancelled. " + message, this);
        Cancel();
    }

    private void FinishCompleted()
    {
        if (!running)
            return;

        CutsceneDefinition completedDefinition = activeDefinition;
        running = false;
        runningCoroutine = null;
        lastCompletedDefinition = completedDefinition;
        Cleanup();
        Notify(Completed);
    }

    private void Cleanup()
    {
        if (cleanupApplied)
            return;
        cleanupApplied = true;

        if (activeProjectileVisual != null)
            Destroy(activeProjectileVisual);
        activeProjectileVisual = null;
        bubbleView?.Hide();
        dialogueGate.Reset();

        for (int i = suppressedBehaviours.Count - 1; i >= 0; i--)
        {
            BehaviourState state = suppressedBehaviours[i];
            if (state.Behaviour != null && !state.Behaviour.enabled && state.WasEnabled)
                state.Behaviour.enabled = true;
        }
        suppressedBehaviours.Clear();

        if (dashDisabledForCutscene && playerDash != null)
            playerDash.enabled = playerDashWasEnabled;
        dashDisabledForCutscene = false;

        if (playerMove != null)
            playerMove.ControlLocked = previousControlLocked;
        if (playerBody != null)
            playerBody.linearVelocity = new Vector2(0f, playerBody.linearVelocity.y);

        if (modalToken != null)
            UiModal.Pop(modalToken);
        modalToken = null;

        player = null;
        playerMove = null;
        playerAttack = null;
        playerDash = null;
        playerBody = null;
        activeExternalStage = null;
    }

    private void Notify(Action<CutsceneRunner> callbacks)
    {
        if (callbacks == null)
            return;
        foreach (Delegate callback in callbacks.GetInvocationList())
        {
            try { ((Action<CutsceneRunner>)callback)(this); }
            catch (Exception ex) { Debug.LogException(ex, this); }
        }
    }

    private void OnDisable() => Cancel();
    private void OnDestroy() => Cancel();
}
