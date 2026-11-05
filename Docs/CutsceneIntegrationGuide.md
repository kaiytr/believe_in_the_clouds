# 컷씬 통합 가이드

## 산출물

- 시퀀스 데이터: `Assets/Data/Cutscenes/`
- 모듈/액터/트리거/튜토리얼 리그 프리팹: `Assets/Prefabs/Cutscenes/`
- 확인 씬: `Assets/Scenes/Cutscenes/CutsceneBubbleShowcase.unity` (Build Settings 미등록)
- 설치 도구: `Assets/Editor/Cutscenes/CutsceneSceneInstaller.cs`

시퀀스는 actor key와 anchor key만 참조합니다. 씬별 실제 Transform은 Runner의 Actor/Anchor Bindings에 배정합니다.

## Tutorial.unity 연결

다른 레벨 세션이 아래 Transform을 준비한 뒤 `Tools > Cutscenes > Install Tutorial Flow`를 실행합니다.

- `TutorialCutsceneAnchors/IntroPlayer`
- `TutorialCutsceneAnchors/BelieverEncounter`
- `TutorialCutsceneAnchors/PlayerTalk`
- `TutorialCutsceneAnchors/BelieverStart`
- `TutorialCutsceneAnchors/BelieverNear`
- `TutorialCutsceneAnchors/BelieverThrow`
- 서사 대상 신도 GameObject 이름: `TutorialStoryBeliever`, 컴포넌트 `BelieverEnemy`

설치 도구는 이 대상과 앵커, Player 태그의 `PlayerHealth`, 전용 리그/데이터를 먼저 검사합니다. 누락 시 저장 전 중단합니다. 대상에 `CutsceneCharacter`가 없을 때만 추가하고, 기존 `TutorialCutsceneRig` 및 `BelieverEncounter/CutsceneEncounterTrigger`가 있으면 재사용합니다. 반복 실행해도 트리거, 말풍선, 신도를 중복 생성하지 않습니다.

설치 후 흐름은 A 시작 독백 → 조작 복귀 → 본체 전체가 조우 구역에 들어옴 → B 대화/접근/무피해 돌/외부 Tip 단계 → 실제 신도 AI/전투 → 동일한 `BelieverEnemy.IsDead` 확인 → C 독백입니다. 비활성화, 씬 언로드, 거리 이탈은 처치로 보지 않습니다. B는 `TutorialBelieverEncounter.asset`의 `tutorial.believer.first-stone.tip` 외부 단계로 끝납니다. Tip 제공자가 없으면 통과해 적 AI 잠금을 풀고 실제 전투를 재개합니다.

## BossTest.unity와 BossEncounter.prefab 연결

`Tools > Cutscenes > Install Boss Introduction`은 **`Assets/Prefabs/Boss/BossEncounter.prefab`만** 수정하는 전용 설치 메뉴입니다. 이번 세션에서는 실행하지 않았습니다. 통합 브랜치가 준비된 뒤 실행합니다.

설치 도구는 기존 `BossEncounterTrigger`의 `boss`, `gates`, `arenaCamera` 참조와 입구 `BoxCollider2D`를 읽어 새 `CutsceneBossEncounterCoordinator`에 복사합니다. 새 Runner와 보스 배우를 붙이고 `BossCombatController.startOnAwake`를 끈 다음, 기존 `BossEncounterTrigger` 컴포넌트를 제거합니다. 이전 트리거를 남기면 Awake에서 Boss에 arena를 바인딩하고 진입 즉시 전투를 시작하므로 교체가 필요합니다. 기존 보스 코드나 `BossEncounterTrigger.cs` 소스는 수정하지 않습니다. 다시 실행하면 기존 Coordinator와 Runner를 찾아 재사용합니다.

실행 순서는 다음과 같습니다.

1. Player 본체 콜라이더 전체가 보스 입구의 x 경계를 통과합니다.
2. 새 Coordinator가 입구를 한 번 기록하고 게이트를 닫으며 `BossArenaCamera.Enter`를 호출합니다.
3. `CloudPreparationRequested` 이벤트와 인스펙터 `onCloudPreparationRequested` UnityEvent가 호출됩니다. 구름 준비만 연결하고 전투를 시작하지 않습니다.
4. `BossIntroduction.asset`을 재생합니다. 선택지는 대사만 바꾸며 전투 수치에는 연결되지 않습니다.
5. 정상 완료 이벤트에서 `BossCombatController.BeginEncounter()`를 한 번 호출합니다. 컷씬 취소/씬 종료에서는 완료 콜백을 보내지 않고 임시 잠금을 정리합니다.

기존 프리팹의 gate/camera 연결을 확인하고, Runner Actor Bindings에 `boss`를 연결합니다. `player`는 진입한 Player 태그 오브젝트에서 런타임 해석됩니다. BossTest 씬은 기존 BossEncounter 프리팹 인스턴스를 사용하므로 프리팹 교체 후 참조를 확인합니다.

## 공개 이벤트와 연결 지점

- `CutsceneRunner.Play(CutsceneDefinition, PlayerHealth, bool suppressBoundEnemyActors)`: 실행을 시작합니다. 재생 중인 Runner는 다시 시작되지 않습니다.
- `CutsceneRunner.Completed` / `Cancelled`: 한 실행의 terminal callback입니다.
- `CutsceneRunner.ExternalStageRequested`: UI/외부 연출 단계 연결 이벤트입니다.
- `CutsceneExternalStage.Id`, `.Complete()`, `.Cancel()`: 외부 단계 식별과 재개/취소 API입니다.
- `CutsceneBossEncounterCoordinator.CloudPreparationRequested`: 구름 준비 연결점입니다. 실제 전투 시작은 Runner 완료 뒤에만 이루어집니다.

카메라, 봉쇄, 씬 전환, 전투 시작을 일반 시퀀스 데이터에서 자동 호출하지 않습니다. 보스 연결은 Coordinator의 공개 이벤트/API를 씁니다.

## 설치 도구 재실행과 보존

`CutsceneSceneInstaller`는 메뉴 명령으로만 실행됩니다. Tutorial 명령은 대상 씬 경로 하나만 열고, Boss 명령은 대상 프리팹 경로 하나만 불러 저장합니다. 둘 다 준비된 앵커/참조가 빠지면 적용 전 중단합니다. 범용 씬 재생성, 빌드 씬 목록 수정, 프로젝트 설정 수정을 하지 않습니다. 리그/트리거가 이미 있으면 재사용합니다.

브랜치 통합 전에는 두 설치 메뉴를 실행하지 않습니다. 레벨 세션의 씬/앵커와 보스 세션의 프리팹 참조가 합쳐진 뒤 통합 담당이 해당 메뉴를 각각 적용하고 결과를 리뷰합니다.
