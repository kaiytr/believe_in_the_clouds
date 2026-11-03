using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PlayerDeathCoordinator : MonoBehaviour
{
    private string lifeId = Guid.NewGuid().ToString("N");
    private IEquipmentAdapter equipment;
    private string persistentLifeId;
    private DeathResultView view;
    private Texture2D capture;
    private Action deathEffects;
    private bool captureQueued;
    private float nextCaptureRetry;
    private bool choosing;
    private bool transitioning;
    public static PlayerDeathCoordinator Active { get; private set; }
    public static bool IsResultOpen => Active != null && Active.HasResult;
    public bool HasResult { get; private set; }
    public string DeathId => "life:" + lifeId + "/death";
    public CurrencyResult LastResult { get; private set; }
    public DeathResultData Result { get; private set; }

    public void SetEquipmentAdapter(IEquipmentAdapter adapter) { equipment = adapter; }
    public void BindRunLife(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A saved life ID is required.", nameof(id));
        persistentLifeId = id;
        lifeId = id;
    }
    public void BeginLife()
    {
        if (HasResult || (Currency.Service != null && !Currency.Service.BeginLife())) return;
        lifeId = persistentLifeId ?? Guid.NewGuid().ToString("N");
        LastResult = null;
    }
    public void PrepareDeath(string cause)
    {
        if (HasResult || IsResultOpen || Result != null) return;
        capture = DeathScreenCapture.Capture(Camera.main, out string captureError, GetComponent<PlayerHealth>());
        string png = null;
        try { if (capture != null) png = Convert.ToBase64String(capture.EncodeToPNG()); }
        catch (Exception ex) { captureError = "화면 저장 준비 실패: " + ex.Message; }
        Result = new DeathResultData
        {
            cause = cause, occurredAtUtc = DateTime.UtcNow.ToString("O"), sceneName = gameObject.scene.name,
            screenshotPngBase64 = png, captureError = captureError,
            statistics = Currency.Service?.StatisticsSnapshot() ?? new PlayStatistics()
        };
    }
    public void CaptureDeath(Action processExistingDeathEffects, string cause = "알 수 없는 원인")
    {
        if (HasResult || IsResultOpen) return;
        PrepareDeath(cause);
        HasResult = true; Active = this;
        equipment = equipment ?? new PlayerEquipmentAdapter(gameObject);
        // Keep the existing journal/effects order. RetryDeath may persist capture
        // again after a failed write, but cannot apply loss until a choice is made.
        deathEffects = processExistingDeathEffects;
        QueueCapture();
        UiModal.Push(this);
        Time.timeScale = 0f;
        DisableControls();
        StartCoroutine(ShowResultAfterDeathAnimation());
    }
    private IEnumerator ShowResultAfterDeathAnimation()
    {
        var animation = GetComponent<PlayerIdleSpriteAnimator>();
        while (animation != null && animation.isActiveAndEnabled && !animation.IsDeathAnimationComplete)
            yield return null;
        view = DeathResultView.Create(Result, capture, Continue, SaveAndExit);
        if (!LastResult.Success) view.SetError("사망 기록 저장 실패 · 저장 공간을 확인한 뒤 다시 시도해 주세요.");
    }
    private void QueueCapture()
    {
        if (captureQueued) return;
        EconomyService service = Currency.Service;
        LastResult = service?.CaptureDeferredDeath(DeathId, equipment, deathEffects, Result)
            ?? CurrencyResult.Failed(EconomyError.NotReady);
        captureQueued = LastResult.Success || (service != null && service.HasPendingDeath);
        nextCaptureRetry = Time.unscaledTime + .5f;
    }
    private void DisableControls()
    {
        if (TryGetComponent(out PlayerAttack attack)) attack.enabled = false;
        if (TryGetComponent(out PlayerDash dash)) dash.enabled = false;
        if (TryGetComponent(out PlayerMove move)) move.enabled = false;
    }
    private void LateUpdate()
    {
        if (!HasResult || transitioning) return;
        Time.timeScale = 0f;
        DisableControls();
        if (!captureQueued && Time.unscaledTime >= nextCaptureRetry) QueueCapture();
    }
    public void Continue() => Choose(false);
    public void SaveAndExit() => Choose(true);
    private void Choose(bool toTitle)
    {
        if (!HasResult || view == null || choosing || transitioning) return;
        choosing = true; view.SetBusy(true);
        string destination = toTitle ? TitleScreen.SceneName : LobbyFlowRuntime.LobbyScene;
        try
        {
            if (!Application.CanStreamedLevelBeLoaded(destination))
                throw new InvalidOperationException("Build Settings에서 " + destination + " 씬을 확인해 주세요.");
            QueueCapture();
            LastResult = Currency.Service?.CompleteDeferredDeath(DeathId) ?? CurrencyResult.Failed(EconomyError.NotReady);
            if (!LastResult.Success) throw new InvalidOperationException("저장 실패 (" + LastResult.Error + "). 다시 시도해 주세요.");
            // Both actions have now persisted the SAME penalty + result + next run.
            // Keep routing blocked until this explicit destination is accepted.
            transitioning = true;
            Time.timeScale = 1f;
            if (LobbyFlowRuntime.Instance != null && LobbyFlowRuntime.Instance.Scene != null)
                LobbyFlowRuntime.Instance.RouteAfterDeath(destination);
            else if (SceneManager.LoadSceneAsync(destination) == null)
                throw new InvalidOperationException("씬을 열 수 없습니다.");
        }
        catch (Exception ex)
        {
            transitioning = false; Time.timeScale = 0f;
            choosing = false; view.SetBusy(false); view.SetError(ex.Message);
        }
    }
    private void OnDestroy()
    {
        if (view != null) Destroy(view.gameObject);
        if (capture != null) Destroy(capture);
        UiModal.Pop(this);
        if (Active == this) { Active = null; Time.timeScale = 1f; }
    }
}
