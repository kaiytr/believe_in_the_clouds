# 컷씬 통합 가이드

2026-11-05 병렬 작업 통합 시 보스의 새 아레나 API와 제작 레벨 앵커에 맞춰 연결 방식을 갱신했다.

## 설치 대상

- Tutorial: Assets/Scenes/Gameplay/Designed/Tutorial.unity
- 보스 실제 씬: Assets/Scenes/Gameplay/BossTest.unity
- 보스 재사용 프리팹: Assets/Prefabs/Boss/BossEncounter.prefab
- 상점 출구: Assets/Scenes/Gameplay/ShopTest.unity

Unity 메뉴 Tools > Cutscenes > Install Tutorial Flow는 튜토리얼만 갱신한다.
Tools > Cutscenes > Install Boss Introduction은 BossEncounter와 BossTest를 함께 갱신한다.
배치 진입점 ParallelWorkIntegrationSetup.Install은 위 두 작업과 상점의 기존 로비 복귀 출구 배치를 수행한다. 전체 맵 재생성·빌드 목록 수정은 하지 않는다.

## 보스: 기존 아레나를 유지

BossEncounterTrigger를 제거하거나 다른 트리거로 교체하지 않는다. 이 컴포넌트가 보스의 아레나 범위, 봉쇄, 카메라, 구름 준비와 사망 후 해제를 소유한다.

CutsceneBossEncounterCoordinator는 같은 오브젝트에서 ArenaEntered에 구독한다.
1. 몸 전체 진입: BossEncounterTrigger가 HasEntered를 기록한다.
2. 구름 준비와 봉쇄가 끝난 뒤 ArenaEntered를 한 번 발생시킨다.
3. AutoBeginEncounter=false이므로 전투·접촉 피해·패턴은 아직 시작되지 않는다.
4. Coordinator가 BossIntroduction 데이터를 재생한다.
5. Runner 정상 완료 시 BeginEncounter를 한 번 호출한다.
6. 사망 시 기존 아레나가 봉쇄와 카메라를 해제한다.

구름은 PrepareArenaVisuals로 이미 준비되어 있으므로 별도 UnityEvent에 다시 연결할 필요가 없다. CloudPreparationRequested는 추가 연출용 알림으로 남겨둔다.

도입 취소는 CancelIntro로 전투 전의 진입 상태와 구름·봉쇄를 정리한다. 씬 종료 중 늦게 온 완료 콜백은 전투를 시작하지 않는다. 이미 진행 중인 전투를 CancelIntro로 중단하지 않는다.

BossTest는 BossEncounter 전체 인스턴스가 아니라 Boss 프리팹과 독립 아레나를 사용하므로 프리팹과 실제 씬 모두 설치해야 한다.

## 튜토리얼

레벨 세션이 만든 TutorialCutsceneAnchors 아래 IntroPlayer, BelieverEncounter, PlayerTalk, BelieverStart, BelieverNear, BelieverThrow와 TutorialStoryBeliever를 사용한다.

설치 도구는 기존 신도를 재사용하고 CutsceneCharacter, TutorialCutsceneRig, 조우 트리거만 연결한다. 신도나 보상을 중복 생성하지 않는다. 조우 구역은 레벨 인수인계의 권장 크기인 6×4 유닛이다.

CutsceneTutorialFlow가 시작 시 신도의 AI·공격·접촉 피해·믿음 표시 상태와 Rigidbody2D.simulated를 보관하고 대기시킨다. 시작 독백 후 자유 이동 중에도 대기한다. 조우 연출과 무피해 돌, 외부 Tip 단계가 정상 완료돼야 원래 상태를 복원하고 실제 전투를 시작한다. Rigidbody를 멈춘 동안에도 연출 이동과 스프라이트 걷기를 표시할 수 있다.

첫 돌은 시각 전용 복제본이며 체력·교리·실제 회피 결과를 발생시키지 않는다. 같은 신도의 실제 IsDead에만 처치 후 독백을 연결한다. 플레이어가 이미 죽었으면 후속 독백을 시작하지 않는다.

## Tip 연결

Tip UI는 구현하지 않았다. 신도 조우의 …!? 다음 단계 ID는 tutorial.believer.first-stone.tip이다. 제공자가 없으면 즉시 통과한다.

외부 단계·모달 토큰·완료/취소 예시는 Docs/CutsceneTipIntegrationGuide.md를 따른다. UI 입력은 실제 전투 공격으로 전달하지 않는다.

## 공개 API

- CutsceneRunner.Play, Completed, Cancelled
- CutsceneRunner.ExternalStageRequested
- CutsceneExternalStage.Id / Complete() / Cancel()
- BossEncounterTrigger.AutoBeginEncounter / ArenaEntered / CancelIntro()
- BossCombatController.PrepareArenaVisuals() / BeginEncounter()

## 재설치와 보존

설치 도구는 기존 Runner, Coordinator, 리그, 트리거, 배우를 재사용한다. 자동 진입은 끄고 필수 참조를 배정한다.

BossProjectSetup의 보스 프리팹/씬 재생성 경로에도 ConfigureBossIntroduction을 연결했다. 보스 배경 갱신은 기존 아레나를 유지하므로 컷씬 연결을 삭제하지 않는다. 레벨 전용 배치 도구는 기존 씬 문서와 추가 컴포넌트를 유지한다. 설치 이후 전용 배치 확인도 함께 수행한다.

기존 보스 패턴 테스트는 도입을 명시적으로 비활성화하고 자동 진입을 켠다. 실제 도입/전투 연결은 ParallelWorkIntegrationPlayModeTests가 검증한다.
