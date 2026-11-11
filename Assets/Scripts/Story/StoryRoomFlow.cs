using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-450)]
public sealed class StoryRoomFlow : MonoBehaviour
{
    public static StoryRoomFlow Instance { get; private set; }
    public static bool BlocksDeveloperActions => Instance != null &&
        (Instance.introPending || Instance.introPlaying || Instance.cinematic ||
            Instance.document != null && Instance.document.IsOpen || Instance.releasingInput);
    [SerializeField] private FirstAltarStoryDefinition definition;
    [SerializeField] private PlayerHealth player;
    [SerializeField] private CutsceneCharacter speaker;
    [SerializeField] private StoryBelieverView believer;
    [SerializeField] private GameObject blood;
    [SerializeField] private CutsceneRunner runner;
    private AmbientPrayerController prayer;
    private StoryDocumentScreen document;
    private AudioSource rain, impact;
    private Canvas blackout;
    private Text status;
    private Canvas statusCanvas;
    private FirstAltarProgress story;
    private EconomyService service;
    private long revision = -1;
    private bool ready, cinematic, revealed, releasingInput, invalid;
    private readonly object releaseToken = new object();
    private Coroutine deathRoutine;
    private CutsceneDefinition introSequence;
    private bool introPending, introPlaying;

    public FirstAltarPhase Phase => story != null ? story.phase : FirstAltarPhase.NotStarted;
    public bool CanInteract => ready && !invalid && !introPending && !introPlaying && !cinematic && !releasingInput && !UiModal.IsOpen &&
        player != null && !player.IsDead && LobbyFlowRuntime.Instance != null &&
        LobbyFlowRuntime.Instance.InputReady && LobbyFlowRuntime.Instance.State.area == RunArea.StoryRoom;

    public void Configure(FirstAltarStoryDefinition data, PlayerHealth hero, CutsceneCharacter actor,
        StoryBelieverView view, GameObject bloodView, CutsceneRunner cutscene)
    { definition = data; player = hero; speaker = actor; believer = view; blood = bloodView; runner = cutscene; }

    private void Awake() { Instance = this; }
    private void Start()
    {
        if (definition == null || !definition.IsUsable || player == null || speaker == null || believer == null ||
            !believer.IsConfigured || blood == null || runner == null)
        {
            invalid = true; UiModal.Push(releaseToken);
            ShowStatus("재단 스토리의 필수 연결을 확인해 주세요.");
            Debug.LogError("First altar story is missing a required asset or binding.", this);
            return;
        }
        prayer = new GameObject("Ambient prayer").AddComponent<AmbientPrayerController>();
        prayer.transform.SetParent(transform, false);
        document = gameObject.AddComponent<StoryDocumentScreen>();
        rain = CreateAudio("Story rain", definition.rainLoop, true, definition.rainBaseVolume);
        impact = CreateAudio("Story impact", definition.impactClip, false, definition.impactVolume);
        rain.Play();
        runner.ExternalStageRequested += HandleExternalStage;
        runner.Completed += HandleCompleted;
        runner.Cancelled += HandleCancelled;
        blood.SetActive(false);
    }

    private void Update()
    {
        if (invalid || prayer == null) return;
        LobbyFlowRuntime flow = LobbyFlowRuntime.Instance;
        if (flow == null || flow.Scene == null || flow.Scene.gameObject.scene != gameObject.scene ||
            flow.State?.area != RunArea.StoryRoom || flow.IsTransitioning) return;
        EconomyService current = flow.Service;
        if (current == null || !current.IsReady) return;
        if (current != service || revision != current.Revision)
        {
            bool rebound = current != service;
            service = current; revision = current.Revision;
            story = current.FirstAltarSnapshot();
            if (!ready || rebound) RestorePresentation();
        }
        if (introPending)
        {
            if (flow.InputReady) PlayIntroduction();
            return;
        }
        if (introPlaying) return;
        if (!ready || cinematic) return;
        believer.SetTrembling(prayer.IsLooping);
        // Check the current location after the first apology is visible: early approaches are never queued.
        if (Phase == FirstAltarPhase.AwaitingApproach && prayer.IsApologyLoopReady && CanInteract &&
            Mathf.Abs(player.transform.position.x - speaker.transform.position.x) <= definition.approachDistance &&
            Mathf.Abs(player.transform.position.y - speaker.transform.position.y) <= 4f)
        {
            cinematic = true;
            if (!runner.Play(definition.deathSequence, player)) cinematic = false;
        }
    }

    private void RestorePresentation()
    {
        ready = true; revealed = Phase >= FirstAltarPhase.BelieverDead;
        introPending = !revealed;
        believer.SetDead(revealed); blood.SetActive(revealed);
        prayer.StopPrayer();
        if (!revealed)
        {
            UiModal.Push(releaseToken); // Lock before the first playable frame and while waiting for run binding.
        }
    }

    private void PlayIntroduction()
    {
        if (introSequence == null) introSequence = definition.CreateIntroSequence();
        runner.SetActorBindings(new CutsceneActorBinding("firstAltarBeliever", speaker));
        introPlaying = true;
        if (runner.Play(introSequence, player))
        {
            introPending = false;
            UiModal.Pop(releaseToken); // The runner already owns the modal lock.
        }
        else
        {
            introPlaying = false;
            ShowStatus("기도 컷씬을 준비 중입니다.");
        }
    }

    private void HandleExternalStage(CutsceneExternalStage stage)
    {
        if (stage.Id != FirstAltarStoryDefinition.DeathStageId || !cinematic || deathRoutine != null) return;
        deathRoutine = StartCoroutine(PerformDeath(stage));
    }

    private IEnumerator PerformDeath(CutsceneExternalStage stage)
    {
        prayer.StopPrayer();
        blackout = DeathResultView.CreateCanvas("First altar blackout", 32760);
        blackout.transform.SetParent(transform, false);
        DeathResultView.Box("Black", blackout.transform, Color.black, 0, 0, 1, 1).GetComponent<Image>().raycastTarget = true;
        yield return null; // Let a completely black frame reach the screen before the sound rises.
        float start = Time.unscaledTime;
        while (Time.unscaledTime - start < definition.rainRiseSeconds)
        {
            rain.volume = Mathf.Lerp(definition.rainBaseVolume, definition.rainPeakVolume,
                (Time.unscaledTime - start) / definition.rainRiseSeconds);
            yield return null;
        }
        rain.volume = definition.rainPeakVolume;
        impact.Play();
        float hitAt = Time.unscaledTime;
        believer.SetDead(true); blood.SetActive(true); revealed = true;
        while (Time.unscaledTime - hitAt < FirstAltarStoryDefinition.PostImpactBlackSeconds) yield return null;
        blackout.gameObject.SetActive(false); // No alpha interpolation and no scene reload.
        yield return null;
        while (!stage.IsResolved)
        {
            CurrencyResult result = service.TryCommitFirstAltarDeath(story.instanceId);
            if (result.Success)
            {
                ShowStatus(""); deathRoutine = null;
                stage.Complete();
                yield break;
            }
            ShowStatus("사망 상태 저장 중 · 같은 결과로 재시도합니다.");
            yield return new WaitForSecondsRealtime(.5f);
        }
        deathRoutine = null;
    }

    private void HandleCompleted(CutsceneRunner completed)
    {
        if (introPlaying && completed.LastCompletedDefinition == introSequence)
        {
            introPlaying = false;
            ShowStatus("");
            prayer.BeginLoop(definition, speaker);
            StartCoroutine(ReleaseInput());
            return;
        }
        if (!cinematic || completed.LastCompletedDefinition != definition.deathSequence) return;
        cinematic = false;
        story = service.FirstAltarSnapshot(); revision = service.Revision;
        StartCoroutine(ReleaseInput());
    }

    private IEnumerator ReleaseInput()
    {
        releasingInput = true; UiModal.Push(releaseToken);
        yield return null;
        while (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Escape) || Input.GetMouseButton(0)) yield return null;
        yield return null;
        UiModal.Pop(releaseToken); releasingInput = false;
    }

    private void HandleCancelled(CutsceneRunner cancelled)
    {
        if (deathRoutine != null) StopCoroutine(deathRoutine);
        deathRoutine = null; cinematic = false; introPlaying = false;
        if (blackout != null) Destroy(blackout.gameObject);
        if (!isActiveAndEnabled || service == null) return;
        // A cancellation never grants progress; restore from the saved checkpoint.
        story = service.FirstAltarSnapshot(); RestorePresentation();
        rain.volume = definition.rainBaseVolume;
    }

    public void OpenDocument()
    {
        if (!CanInteract || Phase < FirstAltarPhase.BelieverDead) return;
        releasingInput = true;
        document.Open(definition, () => Phase >= FirstAltarPhase.StoryRead
            ? new CurrencyResult { Success = true }
            : service.TryMarkFirstAltarStoryRead(story.instanceId), () =>
        {
            story = service.FirstAltarSnapshot(); revision = service.Revision;
            releasingInput = false;
        });
    }

    public void LeaveRoom()
    {
        if (!CanInteract || Phase != FirstAltarPhase.StoryRead) return;
        CurrencyResult result = LobbyFlowRuntime.Instance.LeaveFirstAltar(story.instanceId);
        if (!result.Success) ShowStatus("출구 저장 실패 · E로 다시 시도해 주세요.");
    }

    private AudioSource CreateAudio(string label, AudioClip clip, bool loop, float volume)
    {
        var child = new GameObject(label); child.transform.SetParent(transform, false);
        AudioSource source = child.AddComponent<AudioSource>();
        source.clip = clip; source.loop = loop; source.volume = volume;
        source.spatialBlend = 0f; source.playOnAwake = false; return source;
    }

    private void ShowStatus(string message)
    {
        if (status == null)
        {
            statusCanvas = DeathResultView.CreateCanvas("Story status", 32765);
            statusCanvas.transform.SetParent(transform, false);
            status = DeathResultView.Label(statusCanvas.transform, "", 24, Color.white, .1f, .03f, .9f, .12f);
        }
        status.text = message;
    }

    private void OnDisable()
    {
        ready = false;
        if (runner != null)
        {
            runner.ExternalStageRequested -= HandleExternalStage;
            runner.Completed -= HandleCompleted; runner.Cancelled -= HandleCancelled;
            runner.Cancel();
        }
        StopAllCoroutines(); prayer?.StopPrayer();
        if (introSequence != null) Destroy(introSequence);
        if (blackout != null) Destroy(blackout.gameObject);
        if (rain != null) rain.Stop(); if (impact != null) impact.Stop();
        UiModal.Pop(releaseToken);
        if (document != null) document.enabled = false;
        if (Instance == this) Instance = null;
    }
}
