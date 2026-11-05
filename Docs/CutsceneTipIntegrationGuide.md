# 컷씬 Tip UI 연결 가이드

Tip UI는 별도 구현 대상입니다. 이 모듈은 Tip 패널, 콘텐츠, 버튼, 이동/믿음 설명 창, 3페이지 안내, 발동 구역을 만들지 않습니다.

## 외부 단계 이름과 코드 위치

튜토리얼 신도 조우 데이터 `Assets/Data/Cutscenes/TutorialBelieverEncounter.asset`은 플레이어의 `…!?` 대사 다음에 `tutorial.believer.first-stone.tip` 외부 단계를 둡니다.

- 이벤트: `CutsceneRunner.ExternalStageRequested`
- 단계 대기 코드: `Assets/Scripts/Cutscenes/CutsceneRunner.cs`의 `WaitForExternalStage`
- 콜백 핸들: `Assets/Scripts/Cutscenes/CutsceneExternalStage.cs`
- 씬별 Runner/Flow 참조: `Assets/Prefabs/Cutscenes/TutorialCutsceneRig.prefab`, 설치 메뉴는 `Assets/Editor/Cutscenes/CutsceneSceneInstaller.cs`

외부 단계는 B 대사의 클릭 진행 후 도달합니다. 첫 돌은 그 전에 시각 전용 복제본으로 연출합니다. 복제본의 `Collider2D`, `Rigidbody2D`, `MonoBehaviour`는 모두 비활성화되므로 Player HP, 실제 피격/교리, 믿음 회피 결과, 보상 이벤트를 발생시킬 경로가 없습니다.

## 잠금 소유권

Runner는 외부 단계 동안 자신의 `UiModal` 토큰과 플레이어/지정 적 잠금을 계속 소유합니다. Tip 패널은 독립된 토큰을 사용해 `UiModal.Push(tipOwner)` / `Pop(tipOwner)`를 짝지으세요. Tip 패널을 닫을 때 **자기 토큰만** 제거하고, 그 다음 완료/취소 콜백을 부릅니다. Tip이 닫혀도 Runner 토큰이 남아 있으므로 실제 전투는 완료 콜백 뒤에만 재개됩니다.

## 연결 예시

~~~csharp
using UnityEngine;

public sealed class TutorialTipProviderAdapter : MonoBehaviour
{
    [SerializeField] private CutsceneRunner runner;
    [SerializeField] private TutorialTipPresenter tipPresenter;

    private object tipModalOwner;
    private CutsceneExternalStage pendingStage;

    private void OnEnable()
    {
        if (runner != null)
            runner.ExternalStageRequested += HandleExternalStage;
    }

    private void OnDisable()
    {
        if (runner != null)
            runner.ExternalStageRequested -= HandleExternalStage;

        CloseTip();
        pendingStage = null; // 씬 전환 중 늦은 UI 콜백은 더 이상 적용하지 않음
    }

    private void HandleExternalStage(CutsceneExternalStage stage)
    {
        if (stage == null || stage.Id != "tutorial.believer.first-stone.tip")
            return;

        pendingStage = stage;
        tipModalOwner = new object();
        UiModal.Push(tipModalOwner);
        tipPresenter.Open(OnTipCompleted, OnTipCancelled);
    }

    private void OnTipCompleted()
    {
        CutsceneExternalStage stage = pendingStage;
        CloseTip();
        pendingStage = null;
        stage?.Complete();
    }

    private void OnTipCancelled()
    {
        CutsceneExternalStage stage = pendingStage;
        CloseTip();
        pendingStage = null;
        stage?.Cancel();
    }

    private void CloseTip()
    {
        if (tipModalOwner != null)
            UiModal.Pop(tipModalOwner);
        tipModalOwner = null;
        tipPresenter?.Close();
    }
}
~~~

`TutorialTipPresenter`는 미래 Tip 구현체의 예시 이름입니다. 실제 패널의 열기/닫기 완료 콜백에 위 adapter의 `OnTipCompleted`/`OnTipCancelled`를 연결합니다. 같은 완료/취소가 두 번 들어와도 `pendingStage`를 먼저 비우므로 두 번째 콜백은 아무 동작도 하지 않습니다. Runner가 먼저 취소되거나 씬이 바뀐 뒤 도착한 stage 콜백은 현재 실행 토큰과 일치하지 않아 무시됩니다.

## Tip 제공자가 없을 때

구독자가 전혀 없으면 `CutsceneRunner.WaitForExternalStage`가 `CutsceneExternalStage.Complete()`를 호출해 즉시 통과합니다. Tip UI 없이도 무한 대기하거나 빈 창을 열지 않고, B가 정상 종료되며 지정 신도의 기존 AI가 재개됩니다. 이벤트를 구독한 adapter는 이 stage ID를 인식하지 못하면 건드리지 말고, 인식한 stage는 반드시 완료 또는 취소를 전달해야 합니다.

## 미래 Tip 구현에서 수정할 대상

Tip 구현 AI는 Tip UI 소유 파일/프리팹과 새 adapter 컴포넌트를 추가하고, `TutorialCutsceneRig`의 adapter 인스턴스에서 `runner`와 Tip presenter 참조를 배정합니다. 연결 메뉴가 필요하면 전용 `CutsceneSceneInstaller`에 옵션 설치만 추가하고 Tutorial/Boss 이외 씬이나 Build Settings를 건드리지 않습니다. 기존 컷씬 데이터의 stage ID, Runner의 입력 잠금 토큰, `CutsceneExternalStage` 구현을 바꾸지 않습니다.
