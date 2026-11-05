# 컷씬 말풍선 모듈

## 구성

- `Assets/Scripts/Cutscenes/CutsceneDefinition.cs`: 인스펙터/에셋에서 편집하는 `Dialogue`, `Choice`, `MoveActor`, `HarmlessProjectile`, `ExternalStage` 단계와 배우/앵커 키를 정의합니다.
- `Assets/Scripts/Cutscenes/CutsceneRunner.cs`: 시퀀스를 실행하고 입력 잠금, 클릭 경계, 이동, 외부 단계, 완료/취소 정리를 소유합니다.
- `Assets/Scripts/Cutscenes/CutsceneBubbleView.cs`: 화자의 머리 위치를 카메라 좌표로 바꾸고 화면 안전 영역 안에 한글 말풍선을 둡니다. 문장은 Text UI의 줄바꿈을 사용합니다.
- `Assets/Scripts/Cutscenes/CutsceneCharacter.cs`: 표시 이름, 머리 위치, 돌 시작 위치, 배우 연출 상태를 제공합니다.
- `Assets/Scripts/Cutscenes/CutsceneHarmlessProjectile.cs`: 새로 생성한 투사체 시각 복제본의 콜라이더, Rigidbody2D, MonoBehaviour를 끕니다.
- `Assets/Scripts/Cutscenes/CutsceneTrigger.cs`: 2D 트리거에서 Player 본체 콜라이더가 구역 안에 전부 들어왔을 때 시퀀스를 한 번 실행합니다.
- `Assets/Scripts/Cutscenes/CutsceneTutorialFlow.cs` / `CutsceneDeathWatcher.cs`: 튜토리얼 시작 구간과 지정 신도 사망 뒤 구간을 연결합니다.
- `Assets/Scripts/Cutscenes/CutsceneBossEncounterCoordinator.cs`: 아레나 진입/봉쇄/카메라 준비와 컷씬 완료 후 `BeginEncounter` 호출을 분리합니다.

이전 프로젝트 `OnionKingdom`의 `DialogueData`, `DialogueBubble`, `ChoiceButton`, `NPCController`에서 페이지별 대사, 선택지, 타이핑 표시 방식을 읽었습니다. NPC 클릭, 보글 게임, 이전 프로젝트 코드와 GUID는 옮기지 않았습니다.

## 입력 규칙

1. 대사가 타이핑 중일 때 좌클릭은 그 대사만 완성합니다.
2. 완성된 대사에서 새 좌클릭을 하면 다음 단계로 갑니다.
3. 선택지 버튼 클릭은 대사 진행 클릭으로 취급하지 않습니다. 선택된 문장은 새 플레이어 대사로 타이핑된 뒤 다음 클릭을 기다립니다.
4. 시퀀스 완료 후에는 좌클릭을 놓고 추가 한 프레임 동안 `UiModal` 토큰을 유지합니다. 마지막 클릭이 공격으로 넘어가지 않습니다.
5. 이동/점프/대시/공격/상호작용은 `UiModal.Push`로 잠급니다. 이미 시작한 공격은 `PlayerAttack.CancelAttack`으로 취소합니다. 이미 진행 중인 대시는 `PlayerDash`를 잠시 비활성화해 중단하고 원래 enabled 상태와 `PlayerMove.ControlLocked` 값을 복원합니다.
6. 지정된 `EnemyUnit`, `EnemyAttack`, `EnemyContactDamage`, `BeliefView`의 enabled 상태를 기록하고 잠금 중 비활성화합니다. 시퀀스 종료/취소에서 시작 전 활성 상태였던 항목만 복원합니다.
7. `Time.timeScale`은 변경하지 않습니다. 타이핑·배우 이동·투사체 연출은 unscaled delta time을 사용합니다.

## 배우와 앵커 설정

각 `CutsceneRunner`의 Actor Bindings에서 `player`, `boss`, `believer` 키에 `CutsceneCharacter`를 연결합니다. Anchor Bindings는 시퀀스 정의의 `anchorId`와 같은 키를 사용합니다. 머리 앵커가 비어 있으면 배우 위치에 기본 높이를 더해 말풍선을 배치합니다. 카메라가 움직이거나 배우가 이동해도 매 프레임 화면 위치를 다시 계산하며, 패널 전체가 화면 안전 영역을 벗어나지 않도록 제한합니다.

행동 지시는 별도의 단계입니다. 예를 들어 `(다가오기)`라는 문자열을 대사로 넣지 않고 `MoveActor`를 사용합니다. 선택지는 선택된 문장만 대사로 출력하며 전투 수치를 변경하지 않습니다.

## 데이터와 프리팹

Unity 메뉴 `Tools > Cutscenes > Create Owned Samples`는 없는 항목만 생성합니다.

- `Assets/Data/Cutscenes/BossIntroduction.asset`
- `Assets/Data/Cutscenes/TutorialIntro.asset`
- `Assets/Data/Cutscenes/TutorialBelieverEncounter.asset`
- `Assets/Data/Cutscenes/TutorialAfterBelieverDeath.asset`
- `Assets/Prefabs/Cutscenes/CutsceneRunner.prefab`
- `Assets/Prefabs/Cutscenes/CutsceneActor.prefab`
- `Assets/Prefabs/Cutscenes/CutsceneEntryTrigger.prefab`
- `Assets/Prefabs/Cutscenes/TutorialCutsceneRig.prefab`
- `Assets/Scenes/Cutscenes/CutsceneBubbleShowcase.unity`

확인 씬은 Build Settings에 추가되지 않습니다. 쇼케이스 씬을 열고 Play를 누르면 보스 도입 대사와 선택지를 확인할 수 있습니다. 출력된 대사에서 좌클릭을 눌러 타이핑 스킵/다음 단계 구분과 선택지 입력을 확인합니다.

## 완료/취소 규칙

`CutsceneRunner.Completed` 또는 `Cancelled` 이벤트는 한 실행에서 최대 한 번만 발생합니다. 재진입 중에는 `Play`가 거부됩니다. 새 씬 인스턴스의 새 Runner는 다시 실행할 수 있습니다. Runner가 비활성화/파괴되거나 씬이 바뀌면 실행을 취소하고 구독 중인 외부 단계, 임시 투사체, 모달 토큰, 배우/적 enabled 상태를 정리합니다. 다른 `UiModal` 소유자의 토큰은 건드리지 않습니다.

`ExternalStageRequested`에 구독자가 없으면 단계는 즉시 완료됩니다. 구독자가 있으면 `CutsceneExternalStage.Complete()` 또는 `.Cancel()`을 불러야 합니다. 취소 뒤에 도착한 콜백은 현재 실행이 아니므로 무시됩니다.

## 확인 결과

- EditMode `CutsceneDialogueInputGateTests`: 2개 통과. 타이핑 완료 클릭과 다음 단계 클릭의 경계, 선택지 클릭 차단을 확인했습니다.
- PlayMode `CutsceneRunnerPlayModeTests`: 5개 통과. Tip 제공자가 없는 단계 통과, 취소 정리와 늦은 콜백 무시, 돌 복제본의 피해/행동 차단, 한글 폰트 글리프, 움직이는 화자 앵커와 화면 안전 영역 처리를 확인했습니다.
- 전체 EditMode/PlayMode 묶음과 전체 빌드는 실행하지 않았습니다.
