using UnityEngine;

// Docs/CutsceneTipIntegrationGuide.md의 연결 예시를 구현한 어댑터.
//
// - CutsceneRunner.ExternalStageRequested를 구독해
//   tutorial.believer.first-stone.tip 단계에서 TIP 패널을 연다.
// - Runner 토큰은 건드리지 않고 자기 UiModal 토큰만 Push/Pop한다.
// - 닫히면 stage.Complete()로 컷씬을 재개한다 (ESC/E/버튼 모두 완료扱い).
// - 구독자가 없으면 Runner가 자동 통과하므로, 이 컴포넌트가
//   TutorialCutsceneRig에 없으면 TIP 없이 그대로 진행된다.
[DisallowMultipleComponent]
public sealed class TutorialTipProviderAdapter : MonoBehaviour
{
    [SerializeField] private CutsceneRunner runner;
    [SerializeField] private TutorialTipPanel tipPresenter;

    private object tipModalOwner;
    private CutsceneExternalStage pendingStage;

    private void Reset()
    {
        if (runner == null)
            runner = GetComponent<CutsceneRunner>();
        if (tipPresenter == null && TutorialTipPanel.Instance != null)
            tipPresenter = TutorialTipPanel.Instance;
    }

    private void OnEnable()
    {
        if (tipPresenter == null && TutorialTipPanel.Instance != null)
            tipPresenter = TutorialTipPanel.Instance;
        if (runner != null)
            runner.ExternalStageRequested += HandleExternalStage;
        UiModal.CloseRequested += HandleCloseRequested;
    }

    private void OnDisable()
    {
        if (runner != null)
            runner.ExternalStageRequested -= HandleExternalStage;
        UiModal.CloseRequested -= HandleCloseRequested;

        CloseTip();
        pendingStage = null; // 씬 전환 중 늦은 UI 콜백은 더 이상 적용하지 않음
    }

    private void HandleExternalStage(CutsceneExternalStage stage)
    {
        if (stage == null || stage.Id != TutorialTipPanel.StageId)
            return;

        pendingStage = stage;
        tipModalOwner = new object();
        UiModal.Push(tipModalOwner);

        if (tipPresenter == null && TutorialTipPanel.Instance != null)
            tipPresenter = TutorialTipPanel.Instance;
        if (tipPresenter == null)
        {
            Debug.LogError("TIP 패널을 찾을 수 없어 외부 단계를 통과합니다.", this);
            CompletePending();
            return;
        }

        tipPresenter.Closed -= OnTipClosed;
        tipPresenter.Closed += OnTipClosed;
        tipPresenter.Open();
    }

    private void HandleCloseRequested()
    {
        if (pendingStage == null)
            return;
        if (tipPresenter != null && tipPresenter.IsOpen)
            tipPresenter.HandleCloseRequested();
    }

    private void OnTipClosed()
    {
        // ESC/E/버튼 어떤 경로로 닫혀도 컷씬은 재개한다.
        CompletePending();
    }

    private void CompletePending()
    {
        CutsceneExternalStage stage = pendingStage;
        CloseTip();
        pendingStage = null;
        stage?.Complete();
    }

    private void CloseTip()
    {
        if (tipPresenter != null)
            tipPresenter.Closed -= OnTipClosed;
        if (tipModalOwner != null)
            UiModal.Pop(tipModalOwner);
        tipModalOwner = null;
        if (tipPresenter != null && tipPresenter.IsOpen)
            tipPresenter.Close();
    }
}
