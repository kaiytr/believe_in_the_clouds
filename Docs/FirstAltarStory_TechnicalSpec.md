# 첫 재단 조우 — 1.1 소스 분석 및 개발명세서

최초 작성: 2026-11-07 / 1.0 소스 재분석·갱신: 2026-11-11. 대응 기획은 [상세기획](FirstAltarStory_Design.md)이다.

분석 기준은 최신 `origin/main`의 `781af6d83d5563a0ccbbe43e65c664dda8b65f52`(레벨 2 버그 수정), 작업 브랜치는 `codex/first-altar-story-v1.1`이다. 원본 문서는 `codex/first-altar-story-spec`의 `680491cc71a05a3bc0b9efb5183160735bf2768f`에서 두 파일만 옮겼다. 1.0 정리본 이전 소스와 개발 문서 전체를 병합하지 않았다.

**신규 타입·API·씬·v5 스키마는 구현 명세이며 아직 존재하지 않는다.** 신도 사망 후 재단 활성화 → 스토리 UI 열람·닫기 후 출구 활성화 → 방 출구에서 E로 로비 복귀하는 흐름은 확정이다. 2026-11-11 사용자 확인에 따라 **1.0 기존 프로필도 업데이트 후 다음 일반 전투 출구에서 1회 진입**한다.

사용자 제공 원고의 재생 규칙은 처음부터 첫 단독 “죄송합니다.”까지 1회 재생한 뒤 “죄송합니다.”만 계속 반복하는 것으로 확인했다. 반복 중 접근으로 사망 컷씬을 시작하는 구성으로 연결한다. 최신 사망 연출은 암전·빗소리 상승·둔탁한 타격음·타격음 뒤 3초 암전·피로 물든 재단 바닥 표시이며, ‘페이즈 없이 화면 인’은 페이드 없는 즉시 복귀로 해석한다.

## 1. 현재 프로젝트 구조와 근거

프로젝트 설정은 Unity `6000.3.5f2`, URP `17.3.0`, Cinemachine `3.1.7`, uGUI `2.0.0`을 사용한다. 현재 `Assets/Scenes`에는 12개 씬이 있고, 실제 씬 카탈로그와 Build Settings는 다음 흐름이다.

`Title → Tutorial(첫 시작) → Lobby → CombatLevel1~6 / Shop / Boss → Ending → Title`

일반 전투·상점은 완료 후 출구로 로비에 복귀한다. 아래는 신규 기능의 연결 지점을 중심으로 확인한 소스이며, 프로젝트 전체 코드의 무결성을 검증한 결과는 아니다.

| 영역 | 실제 소스 | 현재 동작과 이번 기능의 의미 |
| --- | --- | --- |
| 씬·빌드 | [ProjectScenes](../Assets/Scripts/Scenes/ProjectScenes.cs), [EditorBuildSettings](../ProjectSettings/EditorBuildSettings.asset) | 씬 경로·이름·빌드 목록의 기준. 새 씬을 양쪽에 등록해야 함 |
| 첫 시작 | [TitleScreen](../Assets/Scripts/DeathResult/TitleScreen.cs) `StartGame` | PlayerPrefs의 `tutorial_started`와 `hasRun`으로 튜토리얼 진입 결정. 완료 기록은 아님 |
| 튜토리얼 출구 | [TutorialLevelExit](../Assets/Scripts/Lobby/TutorialLevelExit.cs) | 기존 서비스 보관 후 별도 메모리 연습 런 사용. 출구는 로비 로드, 파괴 시 원래 서비스 복원. 별도 튜토리얼 완료 조건 검사는 없음 |
| 런 모델 | [LobbyRunData / LobbyRules](../Assets/Scripts/Lobby/LobbyRunData.cs) | `combatClears`는 런 단위. `RunArea`는 Lobby·TemporaryStage·Dying·Complete. 프로필 최초 이벤트 기록 없음 |
| 전투 완료 | [RunStageCompletionWatcher](../Assets/Scripts/Lobby/RunStageCompletionWatcher.cs) | 대상 적이 전부 죽으면 `CompleteStage` 요청. 이때는 완료 플래그만 저장 |
| 출구 | [TemporaryStageExit](../Assets/Scripts/Lobby/TemporaryStageExit.cs) | 일반 전투 완료 후 E로 `ReturnToLobby`. 상점은 출구에서 미완료 상태를 완료 처리할 수 있음 |
| 전환 거래 | [EconomyService.Lobby](../Assets/Scripts/Lobby/EconomyService.Lobby.cs) `TryReturnFromStage` | 전투 클리어 +1·방문 +1·새 후보·대기 코인 정산을 저장한 뒤 게시 |
| 런타임 | [LobbyFlowRuntime](../Assets/Scripts/Lobby/LobbyFlowRuntime.cs) | 커밋된 런 상태로 라우팅·HP/위치 복원. 현재 `ReturnToLobby`는 목적지를 로비로 고정 |
| 씬 연결 | [LobbySceneController](../Assets/Scripts/Lobby/LobbySceneController.cs) | 로비/일반 스테이지 두 종류의 연결·콘텐츠 생성·플레이어 활성화. 전용 스토리 구분이 필요 |
| 컷씬 | [CutsceneRunner](../Assets/Scripts/Cutscenes/CutsceneRunner.cs), [Definition](../Assets/Scripts/Cutscenes/CutsceneDefinition.cs), [ExternalStage](../Assets/Scripts/Cutscenes/CutsceneExternalStage.cs) | 입력 잠금·배우 이동·외부 연출 완료/취소 지원. 일반 Dialogue는 클릭 대기 방식 |
| 말풍선 | [CutsceneBubbleView](../Assets/Scripts/Cutscenes/CutsceneBubbleView.cs) | uGUI Text 기반, 머리 추적·화면 경계·자동 크기·표시 텍스트 교체 지원. 자동 기도 제어는 없음 |
| 신도 비주얼 | [BelieverVisual](../Assets/Prefabs/Enemy/Believer/BelieverVisual.prefab), [BelieverSpriteAnimator](../Assets/Scripts/Enemy/Believer/BelieverSpriteAnimator.cs) | 기존 애니메이터는 `BelieverEnemy`를 참조. 적 소유자가 없으면 프레임 갱신하지 않음 |
| 적 사망 | [EnemyUnit](../Assets/Scripts/Enemy/Core/EnemyUnit.cs) `Die` | 처치 통계·보상·교리 이벤트·파괴를 수행. 연출 NPC 사망에 호출하면 안 됨 |
| 재단·입력 | [FoundationScreen](../Assets/Scripts/Foundation/FoundationScreen.cs), [InteractionController](../Assets/Scripts/Interaction/InteractionController.cs), [UiModal](../Assets/Scripts/Interaction/UiModal.cs) | FoundationInteractable 이벤트는 교리 UI를 자동으로 엶. E/ESC는 모달 닫기 요청으로 전달 |
| 저장 | [EconomyModels](../Assets/Scripts/Economy/EconomyModels.cs), [EconomyService](../Assets/Scripts/Economy/EconomyService.cs), [JsonEconomyStore](../Assets/Scripts/Economy/JsonEconomyStore.cs) | 현재 스키마 v4. 복사 → 변경 → 검증·저장 → 게시, 거래 영수증으로 중복 방지 |
| 개발자 런 도구 | [EconomyService.DeveloperTools](../Assets/Scripts/Economy/EconomyService.DeveloperTools.cs), [DevToolsPanel](../Assets/Scripts/Debug/DevToolsPanel.cs) | 보스 해금은 `combatClears`를 3 이상으로 조정. 리롤은 Lobby에서만 허용. 스토리 자격을 클리어 수로 판정하면 안 됨 |
| 저장 초기화 | [EconomyRuntime](../Assets/Scripts/Economy/EconomyRuntime.cs) `ResetSavedProfile`, `JsonEconomyStore.DeleteSaveFiles` | F2 초기화는 실제 파일·백업·임시·복구 파일까지 삭제한 뒤 새 서비스를 생성. 새 내러티브도 새 프로필 기본값으로 초기화되어야 함 |
| 필드 음악 | [FieldMusic](../Assets/Scripts/Scenes/FieldMusic.cs) | Title·Boss·Ending 이외 씬에 필드 BGM 유지. 새 방의 음악 정책도 연결 필요 |
| 암전·빗소리 참고 | [CutsceneTutorialFlow](../Assets/Scripts/Cutscenes/CutsceneTutorialFlow.cs), [BossStageAudio](../Assets/Scripts/Boss/BossStageAudio.cs) | 튜토리얼에 전체 화면 검정 오버레이, 보스에 빗소리 루프 사례가 있음. 새 연출의 타이밍·즉시 복귀는 전용 제어 필요 |

일부 기존 문서의 `Assets/Scenes/Gameplay/...`, `BossTest`, `ShopTest` 경로는 현재 파일 구성과 다르다. 이번 명세는 실제 소스·씬 카탈로그를 기준으로 한다. 기존 `CombatHitEffect`는 금색 타격 불꽃이며, 확인한 런타임·셰이더 경로에는 국소 글리치·재단 바닥 혈흔 전용 구현이 없다.

### 1.1 1.0 기준으로 다시 확인한 연결 제약

- `main`의 `3bfa328`은 빌드·소스 제출용 정리본 반영 커밋이다. 기획 브랜치와 `main`은 정리 이전 공통 이력을 가지므로 예전 브랜치를 통째로 병합하는 대신 최신 소스 위에 문서만 복원했다. 현재 저장 버전은 게임 표시 버전 `1.0`과 별개인 **v4**다.
- `TrySynchronizeRunMaxHp`는 기존 런의 체력 비율을 유지하고 저장 성공 후 반영하며 `returnPlans`를 비운다. 스토리 진입·퇴장 계획 캐시를 추가하면 이 무효화 대상에 포함해야 한다.
- `LobbyFlowRuntime.MatchesScene`은 로비 여부만 나누고, `LobbySceneController.Update`는 비로비 콘텐츠 키로 `run.stage.id`를 사용한다. StoryRoom을 기존 비로비로 취급하면 전투 출구 생성이나 잘못된 HP·좌표 복원이 생기므로 두 곳 모두 별도 분기가 필요하다.
- `LobbyFlowRuntime.InputReady`는 `UiModal.IsOpen`을 검사하지 않는다. 스토리 상호작용·접근은 입력 준비에 더해 모달·Runner 실행·현재 씬까지 직접 검사한다.
- `UiModal.CloseRequested`는 소유자별 라우팅이 아닌 전체 구독자 방송이다. 스토리 UI가 실제로 열려 있을 때만 닫기를 처리하고, 컷씬 중 E/ESC를 진행 완료로 사용하지 않는다. [SettingsScreen](../Assets/Scripts/UI/SettingsScreen.cs)은 다른 모달이 열려 있으면 ESC 설정을 열지 않는다.
- `TutorialFirstRunReset` 메뉴는 지금도 PlayerPrefs 키만 지운다. 반면 F2 초기화는 저장 파일·교리 슬롯·런타임까지 초기화한다. 두 도구를 같은 기능으로 가정하지 않는다.
- [ChestRules](../Assets/Scripts/Chest/ChestRules.cs)는 V1/V2 보상·기존 영수증을 검증한다. StoryRoom 안에 원본 stage를 보관하므로 상자 상태·영수증을 함께 보존해야 한다. 상자 사용 API의 `IsCurrentStage`는 TemporaryStage만 허용하므로 방 안에서 원본 상자를 다시 열 수 없어야 한다.
- `ProjectSettings`의 `bundleVersion=1.0`, `companyName=DefaultCompany`, `productName=DigitalContest2026`을 확인했다. 기존 빌드 저장 경로를 이어받도록 회사·제품명·저장 파일명은 유지하고, 표시 버전 변경은 실제 1.1 출시 준비에 포함한다.

## 2. 구현 방향

1. 전투 출구의 저장 거래에서 조건을 판정하고, 다음 목적지를 로비 또는 스토리 방으로 확정한다.
2. `BranchRole`은 확장하지 않는다. 기존 값 `2`는 삭제된 이벤트 역할의 마이그레이션에 쓰이고 있으므로 재사용하지 않는다.
3. 프로필의 스토리 진행과 런의 현재 위치를 함께 저장한다. 씬 로드만으로 이벤트 진입을 간주하지 않는다.
4. 기도는 별도 자동 제어기, 사망은 기존 `CutsceneRunner`와 전용 외부 연출 제공자, 재단은 전용 상호작용·UI로 구성한다.
5. 전용 씬에도 공통 Player·PlayerHud 프리팹과 기존 경제·교리 저장 경로를 사용한다.
6. 재단·출구 활성화는 저장된 진행 단계와 현재 입력 잠금을 기준으로 판정한다. 스토리 UI는 열람 저장과 닫기만 수행하고, 로비 전환은 방 출구의 새 E 입력에서 요청한다.
7. 사망은 암전 중에 결과를 준비하고, 타격음 후 3.0초가 지나면 혈흔 화면으로 즉시 복귀하는 제어로 바꾼다. 기존 노출 사망 VFX 시간표를 그대로 재생하지 않는다.
8. 신규 프로필과 마이그레이션된 기존 프로필 모두 프로필의 미완료 이벤트를 기준으로 판정한다. 기존 프로필에서 클리어 수가 0이 아니어도 다음 일반 전투 출구에서 진입시키고, 업데이트 직후 강제 씬 전환은 하지 않는다.

## 3. 저장 모델 제안

다음 이름은 설계용이다. 기존 enum의 저장 숫자는 유지하고 새 값을 뒤에 추가한다.

```csharp
// 기존 Lobby=0, TemporaryStage=1, Dying=2, Complete=3 유지
RunArea.StoryRoom = 4;

enum TutorialProgress { NotCompleted = 0, Completed = 1, LegacyEligible = 2 }
enum FirstAltarPhase
{
    NotStarted = 0,
    AwaitingApproach = 1,
    BelieverDead = 2,
    StoryRead = 3,
    Completed = 4
}

// EconomyProfileData.narrative에 추가할 프로필 영역
NarrativeProgressData {
    int version; // 첫 내러티브 형식은 1. v5 입력의 필수 영역인지 검증
    TutorialProgress tutorial;
    FirstAltarProgress firstAltar;
}
FirstAltarProgress {
    FirstAltarPhase phase;
    string instanceId;
    string sourceRunId;
    string sourceStageId;
}
```

위 코드는 데이터 모양을 설명한 의사 코드다. `instanceId`는 프로필 ID와 고정 이벤트 ID `story.first-altar.v1`에서 결정적으로 생성한다. 프로필 ID를 단독 거래 ID로 쓰지 않고 작업별 접미사를 붙인다.

신규 파일은 `version=1`, `tutorial=NotCompleted`, `firstAltar.phase=NotStarted`로 생성한다. 기존 v1~v4 파일은 `tutorial=LegacyEligible`, `phase=NotStarted`로 마이그레이션한다. LegacyEligible은 실제 튜토리얼 완료를 추정한 값이 아니라 구버전 사용자의 이벤트 진입을 허용하는 표시다. 이전 초안의 LegacySkipped는 미구현 제안이었으므로 도입하지 않는다.

신규 진입 자격은 `(tutorial == Completed || tutorial == LegacyEligible) && phase == NotStarted`이며, 현재 런·일반 전투·완료된 stage·생존 조건을 추가 확인한다. 신규 프로필은 튜토리얼 전에는 진입할 수 없고, 정상 경로에서 첫 성공 전투 출구가 이벤트를 소비한다. `run.combatClears == 0`이나 보스 해금 여부로 프로필 자격을 대체하지 않는다.

- 이벤트 방 안에서는 완료된 원래 `run.stage`를 유지하고 `run.area=StoryRoom`으로 둔다. 별도 전투 역할이나 가짜 후보를 생성하지 않는다.
- 이때 `run.stage.role=Combat`, `completed=true`, 이벤트의 원본 런·스테이지 ID와 현재 값이 일치해야 한다.
- `combatClears`는 방 입장 거래에서 증가한 값이다. 방 퇴장에서 다시 증가시키지 않는다.
- 로비 복귀 때 `stage`를 비우고 `visit`과 후보를 갱신한다. 프로필의 이벤트 완료 기록은 사망 후 새 런 생성에도 유지한다.
- 1회 기도·사과 반복·암전·빗소리 상승·타격음·3초 대기·화면 복귀의 세부 단계는 메모리에만 둔다. 저장 단계는 신도 생존 중인 AwaitingApproach와 최종 혈흔 화면이 드러난 사망 결과로 구분한다. 사망 저장 전 씬을 새로 로드하면 1회 기도부터 다시 시작한다.
- `Normalize`, `LobbyRules.Validate`, `EconomyLedger.Validate`에 새 영역·단계·ID 일치 검사를 연결한다. enum 범위 검사만 추가해서 끝내지 않는다.
- StoryRoom에서는 `hasRun=true`, `hasStage=true`, 생존 HP, 완료된 Combat stage, 원본 ID 일치, 진행 중 phase(AwaitingApproach·BelieverDead·StoryRead)를 모두 요구한다. Completed 상태로 방에 남거나 NotStarted 상태인데 방 내부라고 저장되는 조합은 거부한다.
- NotStarted에는 원본 런·스테이지 연결을 두지 않고, 입장 시 결정한다. 미완료 상태로 사망·새 런이 된 경우에는 프로필 phase와 instanceId를 보존하되 현재 StoryRoom이 아니므로 이전 source ID와 현재 런 ID가 달라도 허용한다. 재진입 거래에서 새 출처를 연결한다. Completed 기록도 새 런 교체 때 유지한다.

재단·출구의 활성 플래그를 별도로 저장하지 않고 다음 규칙으로 복원한다. 모든 상호작용에는 현재 StoryRoom·입력 준비·생존·컷씬 종료·모달 없음 조건을 함께 적용한다. 비활성 대상의 E 안내도 표시하지 않는다.

| 저장 phase | 재단 사용 | 방 출구 사용 |
| --- | --- | --- |
| AwaitingApproach | 불가. 대사·암전·타격음 뒤 대기·화면 복귀와 사망 저장 재시도 중도 포함 | 불가 |
| BelieverDead | 가능. 사망 컷씬 정상 종료 또는 해당 결과 복원 후 | 불가 |
| StoryRead | 가능, 재열람 지원 | 가능. 열람 UI가 닫히고 입력 해제 경계를 지난 뒤 새 E 입력 필요 |
| Completed | 현재 이벤트 방의 활성 진행으로 취급하지 않음 | 현재 이벤트 방의 활성 진행으로 취급하지 않음 |

스토리 진행과 현재 방 위치는 구분한다. 정상 플레이에서 방은 피해원이 없는 안전 구간이다. 개발 도구의 강제 사망 등으로 런이 교체될 경우 미완료 진행은 재진입 대기로 보존하는 안을 권장한다. 다음 일반 전투 출구에서 원본 ID를 새 런에 재연결하고 저장된 단계부터 재개하며, Completed는 재진입시키지 않는다. 이벤트 입장 전에 저장된 사망 저널이 있었다면 기존 사망 처리를 먼저 마친다.

## 4. 거래와 진행 책임

| 신규/변경 API 제안 | 선행 조건 | 한 번의 저장으로 확정할 내용 |
| --- | --- | --- |
| `TryMarkTutorialCompleted()` | 원래 정식 프로필 서비스가 준비됨, 튜토리얼 출구 사용 | 프로필 tutorial=Completed |
| `TryExitCompletedStage(stageId, hp)` — 이벤트 경로 | 일반 전투 완료·생존·tutorial이 Completed 또는 LegacyEligible·이벤트 미완료 | 원래 클리어 +1, HP, 대기 코인 정산, StoryRoom 위치, 이벤트 단계·원본 ID, 씬 간 위치 초기화. 기존 프로필의 클리어 수는 진입 전제에 사용하지 않음 |
| 같은 API — 일반 경로 | 이벤트 완료 또는 자격 없음, 기존 출구 조건 | 기존 정상 복귀 계약: 필요 시 클리어 +1, 방문 +1, 다음 후보, HP, 대기 코인 정산 |
| `TryCommitFirstAltarDeath(instanceId)` | 현재 StoryRoom, AwaitingApproach, 타격음 후 3.0초 암전과 최종 혈흔 화면 복귀 완료 | BelieverDead. 성공 후 외부 단계 완료, Runner 정상 종료 뒤 재단 활성화 |
| `TryMarkFirstAltarStoryRead(instanceId)` | 현재 StoryRoom, BelieverDead, 본문 표시 이후 닫기 수락 | StoryRead. 성공 후 UI 닫기·출구 활성화, 씬 이동 없음 |
| `TryLeaveFirstAltar(instanceId, hp)` | 현재 StoryRoom, StoryRead, 생존. 호출부는 모달이 닫힌 뒤 방 출구의 새 E 입력에서만 요청 | Completed, Lobby 위치, 방문 +1, 다음 후보, HP, stage 제거, 씬 간 위치 초기화·대기 코인 정산 |

원본 스테이지 판정과 phase 전이는 서비스에서 다시 검사한다. 상호작용 대상의 `CanInteract`만 믿지 않는다. 이미 처리된 거래는 기존 영수증 경로로 성공 결과를 반환하고, 과거 요청 때문에 완료된 phase를 되돌리지 않는다.

기존 `TryReturnFromStage`는 새 출구 API로 위임하거나 호출부를 교체한다. 기존 `return:<stageId>` 영수증과 요청 키의 호환을 유지해, 과거 출구 요청을 새 이벤트로 재처리하지 않는다. `returnPlans`에 보관하는 계획은 목적지와 스토리 변경을 함께 고려하도록 바꾸고, 실패 뒤 재시도에서 다른 계획을 만들지 않는다.

출구 거래 ID는 기존 `return:<stageId>`, 요청 키는 기존 `RequestKey("run.return", stageId)`를 유지한다. 사망·열람·퇴장은 각각 `story:first-altar:death:<instanceId>`, `story:first-altar:read:<instanceId>`, `story:first-altar:leave:<instanceId>`처럼 작업별 ID를 사용한다. ID 하나로 서로 다른 단계를 처리하거나 요청 키를 바꾸어 기존 성공 영수증과 충돌시키지 않는다. 신규 API의 이름과 실제 저장 거래 ID는 별개다.

기존 `returnPlans`는 런 스냅샷만 보관한다. 새 계획은 목적지·출처·phase까지 하나의 변경안으로 묶고, 실패한 계획을 재사용할 때 현재 원본 런·stage·내러티브 상태·maxHp가 여전히 일치하는지 확인한다. `TrySynchronizeRunMaxHp`·서비스 재바인딩·프로필 초기화에서 무효화한다. 다른 거래가 성공한 뒤 오래된 전체 프로필을 덮어쓰지 않으며, 현재 work 데이터에 유효한 계획의 변경만 적용한다.

이벤트 입장 시에는 후보 생성·`visit++`를 수행하지 않는다. 방 퇴장 시 다음 후보는 원본 스테이지 ID로부터 결정적인 seed를 만들어 저장 실패·프로세스 재시작 뒤에도 같은 결과를 사용한다. 기존 정상 복귀의 후보 생성 로직을 공통화하되, 전투 횟수 증가를 포함한 전체 복귀 메서드를 다시 호출하지 않는다.

구체적으로 퇴장 후보 seed는 현재와 같은 `StableId("return:" + sourceStageId)`의 Guid 바이트에서 구하고, 증가한 visit에 `LobbyRules.GenerateCandidates`를 한 번 적용한다. 기존 프로필의 전투 3회 이상 보스 해금 상태와 기존 후보 생성 규칙은 유지한다. 원본 stage의 상자 상태는 입장 시 보관하고 퇴장 시 stage와 함께 제거하되, 프로필의 처리 영수증은 남긴다.

진행 중 UI 재열람은 추가 거래를 요구하지 않는다. StoryRead 이후 반복 닫기는 phase를 유지한다. UI 닫기와 열람 저장에서는 `TryLeaveFirstAltar`를 호출하지 않는다. 이벤트 완료·방문 증가·다음 후보 생성·로비 이동은 출구 거래 성공 시에만 처리한다. 런타임은 반환 성공 여부와 **최신 저장 스냅샷**을 함께 확인하고, 호출 메서드 이름만 보고 로비를 로드하지 않는다.

## 5. 튜토리얼 완료 연결

`tutorial_started`는 튜토리얼 진입 때 기록되므로 새 조건의 근거로 사용할 수 없다. 현재는 튜토리얼 도중 종료 후 다시 시작하면 진입 여부 키만으로 튜토리얼을 건너뛸 수 있는 구조다.

- `TutorialLevelExit`에서 `previous`로 보관한 원래 서비스에 완료 거래를 수행한다. 현재 `Currency.Service`는 연습용 메모리 서비스이므로 여기에 기록하면 사라진다.
- 저장 성공 후에만 `leaving=true`와 로비 로드를 진행한다. 저장 실패면 현재 튜토리얼을 유지하고 재시도 가능하게 한다.
- `TitleScreen`의 튜토리얼 판정을 프로필의 tutorial 단계로 연결한다. 신규 프로필의 미완료 튜토리얼은 다시 진입할 수 있어야 한다.
- `LegacyEligible` 프로필은 튜토리얼 재플레이 없이 기존 런을 재개한다. `tutorial_started`만으로 신규 v5 프로필을 구버전 자격으로 바꾸지 않는다. 기존 키는 하위 호환 보조 값으로만 유지한다.
- 기존 PlayerPrefs 키를 지우는 [ResetTutorialFirstRun](../Assets/Editor/ResetTutorialFirstRun.cs) 메뉴는 새 프로필 기록과 일치하도록 갱신하되, 기존 프로필을 몰래 초기화하지 않는다. 신규/완료 프로필 검증은 별도 메모리 픽스처로 제공한다. 실제 저장 삭제는 명시적인 F2 초기화에만 맡긴다.
- 튜토리얼의 연습 재화·처치·클리어 수를 정식 프로필로 복사하지 않는다. 완료 기록만 원래 프로필에 남긴다.
- 정식 서비스 저장 뒤에는 연습 서비스를 정리하고 원래 서비스를 복원한 후 로비로 전환한다. 현재 코드는 OnDestroy에서만 복원하므로, 로비 Attach보다 복원이 늦어지지 않게 순서를 명시하고 로드 실패 시 재시도 상태를 유지한다.

## 6. 씬 라우팅과 플레이어 연결

신규 씬은 `Assets/Scenes/Story/FirstAltarStory.unity`, 씬 이름은 `FirstAltarStory`를 제안한다.

- `ProjectScenes`의 경로·이름·`GameplayPaths`에 추가하고 Build Settings에 포함한다. `PlayLoopProjectSetup`은 카탈로그로 빌드 목록을 재생성하므로 수동 빌드 등록만 해서는 안 된다.
- `PlayLoopProjectSetup.RunScenes`의 Combat/Shop/Boss 설정 루프에 스토리 방을 넣지 않는다. 별도 씬 설정 도구로 작성하고 새 폴더·씬·스크립트·프리팹의 `.meta`를 함께 관리한다.
- `LobbyFlowRuntime`에 저장 상태에서 목적지를 구하는 단일 경로를 둔다: Lobby → Lobby, TemporaryStage → 기존 역할별 씬, StoryRoom → FirstAltarStory, Complete → 기존 보스 종료 처리.
- `ReturnToLobby`의 고정 로비 로드를 제거하거나 출구 처리 이름으로 변경한다. Title에서 Lobby를 먼저 로드한 재개도 저장된 StoryRoom으로 라우팅한다.
- 전체 플레이 루프 테스트의 `TryConfigurePlayLoopTestCandidates`는 실제 Lobby 복귀에서만 실행한다. 이벤트 방 입장 때 로비 후보를 준비하려고 하면 현재 서비스의 영역 검사에 막힌다.
- `MatchesScene`은 현재 로비/비스테이지 이분법을 확장하고 목적 씬 이름까지 확인한다. 같은 비로비 씬이라는 이유로 다른 씬의 위치·HP를 복원하면 안 된다.
- `LobbySceneController`에 명시적인 스토리 방 구분을 추가한다. 예를 들어 `isStoryRoom=false`를 추가하고 기존 `isLobby` 직렬화는 보존한다. 새 방에서는 전투 역할 검사와 일반 출구·적·로비 후보 자동 생성을 수행하지 않는다.
- `Start`의 `runStageOnly` 검사, `Update`의 일치 판정·contentKey, `BuildContent`의 비로비 자동 출구 생성도 StoryRoom 전용 경로로 분리한다. 스토리용 출구는 StoryRead 조건을 가진 전용 컴포넌트만 배치한다.
- 공통 Attach/Detach, HP 복원, 체크포인트, 플레이어 입력 준비는 새 방에서도 동작하게 한다. 씬 전환 거래에서는 `hasPosition=false`로 바꿔 이전 맵 좌표를 가져오지 않는다.
- `ClampPosition`은 기존 `authoredExit != null`에 따른 하한과 새 방 동선이 맞는지 확인하고 스토리용 경계를 명시한다. 저장된 신도·혈흔·phase를 복원한 뒤 플레이어 입력과 접근 판정을 연다.
- StoryRoom에서 기존 `IsActiveRunStage(Combat)`은 false를 유지한다. 전투 완료 감시·적·상자 생성 로직을 실행하지 않는다. 전용 씬을 직접 열어 테스트할 때는 명시적인 격리 픽스처를 준비하고 정식 저장에 임의의 완료 stage를 만들지 않는다.
- [ProjectSceneSynchronization](../Assets/Editor/ProjectSceneSynchronization.cs)의 공통 Player·HUD·수평 카메라 검사를 통과하도록 씬을 구성한다. 전용 연출은 Player 프리팹 전체를 복제·수정해서 만들지 않는다.

## 7. 신규 컴포넌트와 에셋 제안

| 항목 | 책임 |
| --- | --- |
| `StoryRoomFlow` | 프로필 단계로 신도·기도·재단·출구 상태 복원, 사망 저장·컷씬 종료 후 재단 활성화, 열람 저장·UI 닫기 후 출구 활성화 |
| `StoryApproachTrigger` | 플레이어 본체만 인식, 입력 준비·AwaitingApproach·사과 반복 준비·다른 모달·중복 실행을 확인한 뒤 Flow에 접근 전달 |
| `AmbientPrayerController` | 원고 처음부터 첫 단독 사과까지 1회 재생 후 사과만 반복. Text/Pause 타이머·전용 말풍선·반복 준비 상태를 제공하고 사망 컷씬 시작 시 즉시 정지 |
| `StoryBelieverView` | AI 없는 idle 프레임·사망 스프라이트·글리치 강도·시체 상태 표시 |
| `FirstAltarDeathStageProvider` | `story.first-altar.death.v1` 외부 단계 제공. 기도를 정지한 뒤 암전·빗소리·타격음·3초 대기·혈흔 화면 복귀·결과 저장을 순서대로 실행 후 Complete. 원고를 다시 재생하지 않음 |
| `StoryBlackoutView` | HUD·말풍선·월드를 가리는 완전 불투명 검정 화면과 한 프레임 즉시 복귀, 취소·언로드 시 정리 |
| `StoryRoomAudio` | 빗소리 루프·음량 상승·둔탁한 타격음 1회·복귀 후 음량·씬 종료 정리 |
| `StoryAltarInteractable` | `InteractableBehaviour` 직접 상속, BelieverDead 또는 StoryRead와 공통 입력 조건·거리 확인 후 전용 스토리 UI 열기 |
| `StoryDocumentScreen / View` | 제목·본문·스크롤·열람 저장·닫기·모달 토큰. 닫기 성공 후 방에 남으며, 이후 디자인 교체 가능하게 표시부 분리 |
| `StoryRoomExit` | StoryRead와 공통 입력 조건·거리 확인 후 출구의 새 E 입력으로 퇴장 거래 요청. 일반 TemporaryStageExit 재사용 금지 |
| `FirstAltarStoryDefinition.asset` | 이벤트 ID, 1회 기도 Text/Pause 배열인 introCues, apologyLoopText="죄송합니다.", 반복 표시·유지 시간, 원문, 제목/본문, 빗소리·타격음·암전 시간·거리 등 편집 데이터 |
| `FirstAltarDeath.asset` | 기존 CutsceneDefinition 형식. 클릭 대기 Dialogue 없이 전체 접근 연출 ExternalStage 연결 |
| `StoryBeliever.prefab` | 연출 전용 신도, 머리 앵커·왜곡 기준 영역·사망 자세 |
| `AltarBloodAnchor`와 혈흔 표시 에셋 | 재단 앞 바닥의 지속 혈흔. 암전 중 준비하고 화면 복귀 첫 프레임부터 표시 |
| `StoryBelieverGlitch.shader` | 기존 요청의 신체 일부 왜곡. 사과 구간 전조로 배치하는 안 |

신도 아트는 `Assets/Design/Enemy/Believer/BelieverIdle.png`, `BelieverDeath.png`를 재사용할 수 있다. 기존 `BelieverVisual`의 `BelieverSpriteAnimator`는 적 의존성이 있으므로 그대로 두지 않는다. 단순 idle은 기존 `SpriteFrameAnimator`를 재사용하거나 전용 View가 구동한다. `CutsceneCharacter`는 EnemyUnit 없이도 머리 앵커와 말풍선 화자로 쓸 수 있지만, `SetCutscenePose`는 적 상태를 바꾸는 기능이므로 NPC 애니메이션은 StoryBelieverView가 소유한다.

프리팹에 `EnemyUnit`, `BelieverEnemy`, `EnemyAttack`, `ThrowStoneAttack`, `EnemyContactDamage`, `BeliefController/View`, `IDamageable` 구현을 붙이지 않는다. `EnemyUnit.Die`도 호출하지 않는다. 시각적 죽음으로 코인·처치 통계·교리 사망 효과가 발생해서는 안 된다.

재단은 `FoundationInteractable`의 하위 타입으로 만들지 않는다. 기존 FoundationScreen이 해당 타입을 감지해 조합 UI와 TIP를 열기 때문이다. 기존 재단 아트를 사용하더라도 컴포넌트와 이벤트는 스토리용으로 둔다. 자동 이름 등록은 이미 InteractableBehaviour가 있으면 건너뛰므로 씬에 전용 컴포넌트를 미리 배치한다.

## 8. 말풍선·VFX·오디오

대사와 연출 원문은 [상세기획 6절](FirstAltarStory_Design.md#6-사용자-제공-원고와-대사-재생-규칙)을 기준으로 한다. “하나. 구름은 모든 것을 내려다본다”부터 첫 단독 “죄송합니다.”까지 순서대로 한 번 재생하고 이후 사과만 반복한다. 원고의 마지막 연속 사과 여섯 번은 원문 기록으로 보존하되, 실행은 횟수 제한 없는 apologyLoopText 반복으로 구성한다. 재단 스토리 UI 본문에 이 원고를 임의로 복사하지 않는다.

자동 기도는 Runner를 반복 실행하지 않는다. AmbientPrayerController가 동일한 전용 `CutsceneBubbleView`의 `ShowLine(actor, visibleText, fullText)`, `SetVisibleText`, `Hide`로 1회 기도와 사과 반복을 이어서 표시한다. 두 구간 모두 플레이어 조작을 잠그지 않는다. 반복 상태에서 사망 컷씬을 시작할 때 기도 타이머와 말풍선을 정지·숨긴다. 원문을 기준으로 레이아웃을 잡고 글리치용 표시 문자열만 바꿔 패널 크기가 흔들리지 않게 한다.

기존 Runner의 Dialogue는 클릭 대기를 포함한다. 자동 진행 기도를 해당 단계에 그대로 넣지 않고, AmbientPrayerController가 `Text`·`Pause` 큐와 표시 시간·유지 시간을 순서대로 실행한 뒤 사과 루프로 전환한다. 원고의 독립된 `...`는 1회 구간의 침묵 큐로 해석한 안으로 구분하고, 문장 안의 말줄임표와 괄호 속 연출 지시를 섞지 않는다. 기존 공통 Dialogue의 클릭 규칙은 변경하지 않는다.

1회 큐가 끝난 뒤에는 큐 인덱스를 0으로 되돌리지 않고 ApologyLoop로 전환한다. 여섯 번 뒤 자동 종료하지 않으며, 접근 범위 진입/이탈로 introCues를 다시 시작하지 않는다. 재생 중단·씬 언로드 후 늦은 타이머 콜백이 큐를 진행하지 않도록 재생 세대를 검사한다.

조기 접근 처리안은 1회 큐를 생략하지 않도록 접근 조건을 반복 준비 상태로 제한하는 것이다. 사과 루프를 최소 한 번 표시한 뒤 IsApologyLoopReady를 열고, 플레이어가 여전히 범위 안에 있을 때 현재 위치를 다시 확인해 사망 컷씬을 시작한다. 먼저 접근했다가 떠난 플레이어에게 과거 접근을 예약 실행하지 않는다. 1회 큐를 보호하는 이 조건은 기획서의 조기 접근 처리 제안에 대응한다.

말풍선 글리치는 작은 위치 오프셋·짧은 잔상·일부 문자 치환부터 구현한다. 현재 UI가 uGUI Text라는 점을 전제로 하며 TMP 전용 기능은 가정하지 않는다. 치환 문자는 기존 한글 폰트에 있는 글리프만 사용하고, 원문 데이터는 변경하지 않는다.

신체 글리치는 사과 구간에서 로컬 신체 영역 마스크와 가로 띠별 오프셋으로 전조를 표시하는 안이다. 전체 오브젝트 Transform을 흔드는 것과 구분한다. 스프라이트 아틀라스 UV를 고려하고, URP 2D에서 기존 투명도·정렬이 유지되는지 확인한다. 개체별 강도는 MaterialPropertyBlock 또는 해당 개체 전용 재질로 제어하며 공용 재질을 직접 변경하지 않는다.

기도·접근·암전·오디오·화면 복귀는 다음 로컬 상태 순서로 명세한다. PrayerIntro와 ApologyLoop는 주변 연출 제어기가, 입력 잠금 이후는 사망 외부 단계가 소유한다.

| 로컬 연출 단계 | 처리 |
| --- | --- |
| `PrayerIntro` | 원고 처음부터 첫 단독 사과까지 자동 대사·침묵 큐를 1회 재생. 플레이어 자유 이동, 재단·출구 비활성 |
| `ApologyLoop` | “죄송합니다.”만 반복. 플레이어 자유 이동, 재단·출구 비활성. 반복 준비 후 현재 접근 조건을 확인 |
| `StopAndLock` | 유효한 접근에서 Runner를 한 번 시작, 기도 타이머·말풍선 정지, 입력 잠금. 추가 대사 재생 없이 암전으로 진행 |
| `BlackoutRainRise` | 말풍선 숨김, HUD를 포함한 게임 화면 위에 검정 alpha=1 오버레이 표시. 빗소리 루프를 설정 음량까지 점진적으로 상승 |
| `Impact` | 둔탁한 타격음 한 번 재생, 재생 기준 시각 기록. 오버레이 아래에서 NPC 사망 자세·피로 물든 재단 바닥을 준비 |
| `PostImpactBlack` | 타격음 기준 시각부터 unscaled 시간 3.0초 동안 완전 암전 유지. 앞의 빗소리 상승 시간과 별도 |
| `RevealCut` | 혈흔·사망 결과가 준비됐는지 확인하고 검정 오버레이를 한 프레임에 제거. alpha 보간·페이드·씬 로드 없이 기존 방 화면으로 복귀하는 해석안 |
| `CommitDeath` | 최종 화면이 표시된 뒤 사망 거래 요청. 성공하면 외부 단계 Complete, Runner 정상 종료 후 재단 활성화 |

데이터에는 `rainLoop`, `rainBaseVolume`, `rainPeakVolume`, `rainRiseSeconds`, `impactClip`, `impactVolume`, `postImpactBlackSeconds=3.0`, `AltarBloodAnchor`를 연결한다. 3.0초는 원고의 기준값이며 다른 시간·음량은 조정 가능하게 둔다. 진입 기도·사과 반복·빗소리 상승 시간은 타격음 뒤 3초와 별개다.

혈흔은 NPC 분출 입자의 우연한 도착에 의존하지 않고 정적인 바닥 표시로 준비한다. 복귀 첫 프레임부터 재단 바닥이 피로 물들어 있어야 한다. 사망 결과 복원에서도 같은 혈흔을 표시하고 대사·암전·타격음은 다시 실행하지 않는다. 보조 입자를 사용하더라도 암전 아래에서 정리하며, 피해 콜라이더·보상 로직을 갖지 않는다.

검정 오버레이는 화면 전체를 채우고 모든 게임 HUD·말풍선보다 위에서 그린다. 화면비가 바뀌어도 가장자리가 노출되지 않아야 한다. 기존 튜토리얼의 Canvas 구성은 참고할 수 있지만 FadeIn 코루틴을 화면 복귀에 그대로 사용하지 않는다. 암전 도중에도 대사 캔버스가 맨 위에 남아서는 안 된다.

빗소리 소스 후보는 기존 `Assets/Sounds/Boss/보스전-비.mp3`이며 실제 분위기를 확인한 뒤 선택한다. 둔탁한 타격음은 전용 클립을 지정한다. BossStageAudio의 보스 전투 구독은 가져오지 않고 오디오 소스 구성만 참고한다. FieldMusic의 새 방 정책을 연결해 필드 BGM을 줄이거나 끄는 안으로 두고, 로비 복귀 시 정상 음악 정책을 복구한다. 빗소리 상승은 개별 소스 음량으로 제어하며, 사용자 설정이 사용하는 AudioListener.volume을 덮어쓰지 않는다. 복귀 후 빗소리 음량은 조정 항목으로 둔다.

## 9. 입력·중단·저장 실패

- 접근 트리거는 저장 상태 복원·InputReady·사과 반복 준비 이후에만 사망 요청을 전달한다. 기존 CutsceneTrigger의 `consumed`는 씬 인스턴스 내부 값이므로 프로필 1회 처리의 근거로 사용하지 않는다.
- 접근 구역은 점프 높이와 대시 이동을 고려해 배치한다. 최초 Play 성공 시에만 로컬 재생 중 상태로 전환한다.
- 사망 연출은 기존 Runner의 UiModal·공격 취소·대시 중단·종료 정리를 사용한다. 전역 Time.timeScale을 연출 목적으로 바꾸지 않는다.
- ExternalStage는 제공자가 없으면 즉시 통과하는 현재 규칙이 있다. 이 방은 제공자·필수 참조를 사전 검증하고, 실제 연출과 저장 없이 BelieverDead를 확정하거나 재단을 열지 않는다.
- 타격음 뒤 3초와 화면 복귀가 끝난 뒤 저장이 실패하면, 외부 단계는 미완료로 유지하되 혈흔이 드러난 마지막 화면은 유지한다. 로컬 재생 상태를 우선해 AwaitingApproach 저장 스냅샷 때문에 살아 있는 신도로 되돌리지 않는다. 입력 잠금 상태에서 저장만 재시도하고 성공한 뒤 `stage.Complete()`를 호출한다. 타격음·음량 상승·암전을 재실행하거나 저장 대기만큼 검정 화면을 연장하지 않는다.
- 재단은 최종 화면 복귀·BelieverDead 저장·Runner 정상 종료를 모두 확인한 뒤 활성화한다. 사망 자세만 바뀌었거나 암전·저장 재시도 중이면 재단과 출구를 비활성으로 유지한다.
- UI를 연 프레임의 E와 키 유지가 즉시 닫기로 처리되지 않게 한다. 닫기 입력 수락 시 StoryRead 저장이 실패하면 창을 유지하고 출구는 비활성으로 두며 오류·재시도를 제공한다.
- StoryRead 저장 성공 후 UI를 닫고 출구를 활성화한다. 해당 닫기 입력을 해제한 뒤의 새 E 입력만 방 출구가 받는다. 재열람 중에는 모달 잠금으로 출구 사용을 차단하되 StoryRead 기록은 유지한다.
- UI/컷씬 종료 입력은 키·마우스 해제와 다음 프레임 경계를 소비해 월드 공격·재상호작용으로 전달하지 않는다.
- 취소·비활성화·씬 언로드에서는 기도 타이머·코루틴·외부 단계·검정 오버레이·임시 입자/재질·오디오·구독·본인 소유의 모달 토큰만 정리한다. 늦은 콜백은 실행 세대와 instanceId를 검사한다. 사망 저장 전 취소에서는 완료를 확정하지 않고, 씬을 새로 진입하면 1회 기도부터 재생한다.
- 저장된 StoryRoom의 씬 로드가 실패하면 진행을 완료로 바꾸지 않고 같은 목적지 재시도를 제공한다. 전환 중 입력 잠금이 영구히 남지 않도록 오류 상태를 처리한다.

### 9.1 현재 개발자 도구와의 연결

- [EconomyService.DeveloperTools](../Assets/Scripts/Economy/EconomyService.DeveloperTools.cs)의 `HasLivingDeveloperRun`은 현재 Lobby/TemporaryStage만 허용한다. StoryRoom에는 이 범위를 확장하지 않고 보스 해금·분기 리롤을 거부한다. 런타임의 `CanUseDeveloperRunTools`와 버튼 상태도 같은 조건을 적용해 사용 가능한 것처럼 표시하지 않는다.
- F2 패널은 다른 모달 위에서도 열릴 수 있고, 현재 강제 사망·초기화 버튼은 모달 여부를 검사하지 않는다. 사망 컷씬·스토리 UI가 열린 동안에는 해당 동작이 연출·저장 거래를 끊지 않도록 비활성화한다. 자유 이동 상태의 강제 사망을 복구 테스트에 허용한다면 3절의 미완료 재진입 규칙을 적용한다.
- 보스 해금 버튼으로 클리어 수를 3으로 올려도 이벤트 완료로 간주하지 않는다. 일반 전투 출구만 정상 진입 지점이며, 리롤과 로비 리로드는 이벤트를 소비하지 않는다.
- `ResetSavedProfile`은 새 v5 프로필을 생성하므로 tutorial=NotCompleted, phase=NotStarted가 된다. 기존 `TutorialStartedKey`·교리 슬롯·런타임 정리와 함께 동작하고, 스토리 모달·기도 타이머·오디오가 남지 않아야 한다. 이는 기존 프로필 마이그레이션과 다른, 명시적인 전체 초기화 기능이다.
- `StartMemoryFixture`, `StartPlayLoopTest`, `ReloadForTest`는 현재 테스트 계약을 유지한다. 기본 루프 테스트에 이벤트를 무조건 끼워 넣지 않고, 튜토리얼 완료·레거시 자격·방 단계별 픽스처를 명시적으로 구분한다.

## 10. 저장 버전과 테스트 환경

스키마는 v4 → v5로 올린다. 구현 시 `CurrentSchemaVersion`, 생성 기본값, 복사·정규화·검증·Json 저장 마이그레이션을 함께 변경한다. 게임 표시 버전 1.1과 저장 스키마 v5는 서로 다른 번호다.

| 입력 | 내러티브 초기화·재개 정책 |
| --- | --- |
| 파일 없음 | 신규 v5 프로필. version=1, tutorial=NotCompleted, phase=NotStarted. 기존 초기 재화 정책 유지 |
| 유효한 v1~v4 파일 | 기존 자산·런·사망 저널·통계·TIP·상자·거래 영수증 보존, tutorial=LegacyEligible, phase=NotStarted 추가. 다음 일반 전투 출구에서 진입 |
| 유효한 v5 파일 | 저장된 자격·phase·ID 유지. 신규 값으로 다시 초기화하지 않고 현재 영역으로 재개 |
| 미래 버전·손상 파일 | 기존 오류·백업 복구 정책 유지. 새 프로필로 덮어쓰거나 이벤트 미시작으로 치환하지 않음 |

Json 로더는 **로드 직후 원본 schemaVersion을 보관**하고 기존 버전별 마이그레이션을 순서대로 적용한 뒤 최종 v5로 올린다. 현재 v2~v3가 `CurrentSchemaVersion`으로 바로 바뀌는 코드에 상수만 5로 바꾸면 내러티브 초기화를 건너뛸 수 있으므로 별도 단계를 둔다. v1의 `hasRun` 거부와 삭제된 BranchRole=2의 기존 정규화 규칙도 유지한다.

구버전의 튜토리얼 실제 완료와 프로필 전체 최초 전투는 복원할 수 없으므로 LegacyEligible로 자격만 부여한다. 런별 combatClears·PlayerPrefs·보스 해금으로 소급 판정하지 않는다. 업데이트 당시 Lobby라면 다음 전투를, 이미 완료한 Combat stage라면 그 출구를, 진행 중 Combat이라면 완료 후 출구를 기다린다. Shop·Boss·Dying·Complete 상태는 기존 처리를 마치며 업데이트만으로 StoryRoom에 이동시키지 않는다.

v5 입력의 `narrative` 필수 영역·version·phase·출처 ID는 검증한다. 새 필드 누락을 JsonUtility 기본값만으로 NotStarted로 보정해서 완료 이벤트를 재발생시키지 않는다. 실제 저장에서 schemaVersion을 먼저 읽고 필수 키 존재 여부와 도메인 값을 함께 검사한다. 유효한 v1~v4에만 레거시 초기화를 적용한다.

1.0의 `economy-save-v1.json` 파일명·회사/제품 식별자는 유지한다. 마이그레이션과 첫 후속 거래의 저장 실패는 기존 파일·게시 상태를 보존하고 재시도한다. 변경된 v5 파일을 읽지 못하는 1.0 실행 파일로 되돌리는 기능은 제공하지 않으며, 1.1 공개 전 격리된 1.0 저장 복사본으로 호환을 검증한다. 실제 사용자 저장은 테스트 픽스처로 사용하지 않는다.

기본 Editor Play는 `MemoryEconomyStore`이므로 실행 간 1회 기록이 유지되지 않는다. 여러 번 Play해서 방이 다시 나오는 것만으로 저장 버그라고 판정하지 않는다. 세션 간 저장은 기존 `-usePersistentEditorProfile` 옵션 또는 별도 임시 Json 저장으로 확인한다.

테스트 픽스처는 실제 저장과 분리하고, 튜토리얼 완료·이벤트 미시작 / 사망 후 / 열람 후 / 완료 상태를 준비할 수 있게 한다. 기존 전체 플레이 루프 픽스처는 튜토리얼을 건너뛰므로 기본 테스트의 이벤트 포함 여부를 명시하고, 이벤트 포함 경로를 별도로 제공한다.

## 11. 구현 순서와 검증 계획

1. UI 원문·오디오/혈흔 소스 등 남은 연출 항목 정리 → 데이터·확정된 기존 프로필 정책에 따른 v5 마이그레이션·거래·검증 규칙.
2. 튜토리얼 완료 기록 → 출구 분기·라우팅·빈 전용 씬·정상 로비 복귀.
3. AI 없는 신도·자동 기도 → 접근 컷씬·사망 상태 저장·복원.
4. 원고 1회 기도 → 사과 반복 → 접근 암전·빗소리 상승·타격음·3초 암전·혈흔 화면 복귀 → 임시 스토리 UI·전조/말풍선 글리치 조정.
5. 실제 화면 연출 조정·기존 루프 회귀·저장 실패 검증과 결과 기록.

| 구분 | 의미 있는 검증 항목 |
| --- | --- |
| EditMode: 거래 | 신규 프로필은 튜토리얼 전 진입 안 함·완료 후 첫 전투에서 진입, 레거시 프로필은 기존 클리어 수와 무관하게 다음 전투에서 진입, 상점 제외, 중복 출구 요청에 클리어 +1만 적용 |
| EditMode: 단계 | AwaitingApproach → BelieverDead → StoryRead → Completed, 사망 전 재단·열람 전 출구 사용 거부, 잘못된 ID·역순 요청 거부, 반복 요청의 멱등성 |
| EditMode: 실패 | 각 커밋 저장 실패에 이전 상태 유지, 같은 다음 후보·거래 ID로 재시도, 재시작 후 결과 일치 |
| EditMode: 호환 | v1~v4 자격 추가·자산/런/저널 보존, v5 완료 상태 재초기화 방지, enum 숫자 보존, 삭제된 BranchRole=2 마이그레이션, v5 필수 영역 누락·잘못된 단계/원본 ID 거부 |
| PlayMode: 정상 흐름 | 튜토리얼 정식 프로필 기록 → 첫 전투 → 전용 방 → 접근 사망·재단 활성화 → 스토리 UI 열람·닫기·출구 활성화 → 방 출구 E → 로비 |
| PlayMode: 재개 | 1회 기도 중·사과 반복 중·빗소리 상승 중·타격음 뒤 3초 중·화면 복귀 후 사망 저장 전·사망 저장 후·열람 후·퇴장 후 재로드, 저장 단계에 맞는 신도·혈흔·재단·출구 복원 |
| PlayMode: 기도 | 원고 처음부터 첫 단독 사과까지 순서대로 1회 재생, 이후 사과를 여섯 번 넘게 반복해도 교리 재시작·자동 사망 없음, 두 구간 모두 자유 이동, 조기 접근 처리안에서 원고 생략·과거 접근 예약 실행 없음 |
| PlayMode: 연출 | 사과 반복 상태에서 접근하면 반복 정지·입력 잠금·암전 시작, 빗소리 상승 뒤 타격음 1회, 타격음부터 3.0초 암전(렌더 프레임 오차 이내), 즉시 복귀 첫 프레임에 혈흔 표시 |
| PlayMode: 연출 실패 | 필수 오디오·오버레이·혈흔 참조 누락을 검증, 저장 재시도에 타격음·암전 반복 없음, 취소/언로드에 검정 화면·빗소리·모달 잔류 없음 |
| PlayMode: 입력 | 자동 기도 중 이동 가능, 접근 연타/다중 콜라이더에도 컷씬 1회, E 열기/닫기와 마우스 공격 누출 없음, UI 닫기에 쓴 E 유지로 퇴장하지 않음, 재열람 중 출구 차단 |
| PlayMode: 진행 차단 | 사망 저장 실패·컷씬 진행 중 재단 비활성, 최초 열람 저장 실패·UI 열린 동안 출구 사용 불가, UI 닫기만으로 씬·클리어 수·방문·후보 변화 없음 |
| PlayMode: 부작용 | NPC에 적/피해 컴포넌트 없음, 처치 통계·코인·조각·교리 효과 변화 없음, 재단 조합/TIP UI 동시 표시 없음 |
| PlayMode: 정리 | 외부 제공자 누락·취소·언로드·늦은 콜백에서 진행 오확정과 모달/입자/오디오 잔류 없음 |
| 회귀 | 변경한 거래·라우팅에 해당하는 Lobby·Economy·DeathResult 테스트를 우선 선택. 씬 추가 시 해당 씬의 ScenePropagation, Runner 변경 시 해당 CutsceneRunner 테스트. 기존 3회 전투 보스 해금·상점·사망 루프 유지 |
| 수동 화면 | 신체 글리치 전조·원고 가독성·침묵의 호흡, 암전의 완전한 화면 가림, 빗소리 상승과 둔탁한 타격음의 구분, 3초 유지 뒤 즉시 혈흔 화면 복귀, 16:9·4:3 카메라/혈흔 구도와 UI |

위 표는 구현 후 필요한 검증 후보이며, 매 수정마다 전체 EditMode·PlayMode를 실행하라는 뜻이 아니다. 저장·거래 변경에는 실패/재시도·호환·중복 방지처럼 실제 위험을 확인하는 작은 테스트를 우선하고, UI·연출은 실제 흐름의 필요한 지점을 확인한다. 변경과 무관한 전체 테스트 반복으로 시간을 늘리지 않는다.

## 12. 이번 문서 갱신의 범위와 브랜치 운영

이번에는 최신 main 기반 브랜치 생성, 기존 두 문서 이동, 현 소스 정적 분석, 확정된 기존 프로필 정책 반영만 수행했다. 게임 코드·씬·스키마·ProjectSettings는 수정하지 않았고 Unity 실행·빌드·EditMode·PlayMode 테스트도 수행하지 않았다. 문서 내부 링크·원고 보존·소스 근거·변경 범위만 검증한다.

1.1 문서 커밋은 한글 메시지로 작성하고 `codex/first-altar-story-v1.1`에 푸시한다. 1.0 기준 `main`은 이 작업에서 변경하지 않는다. 후속 구현도 이 브랜치에서 이어가며, 완료·검증 후 main에 병합한다.

브랜치 정리 기준은 다음과 같다. 실제 정리 결과는 작업 완료 보고의 Git 상태를 기준으로 한다.

| 브랜치 | 판단 근거·처리 |
| --- | --- |
| `codex/first-altar-story-spec` | Puri에 없는 커밋은 문서 커밋 `680491c` 한 개. 두 문서의 1.1 반영·원격 보관 확인 후 로컬/원격 정리 |
| `codex/pre-merge-leesc-20261104` | `f080db4`가 보존하는 origin/Puri의 조상임을 확인. 병합 전 로컬 보조 브랜치 정리 |
| `codex/pre-merge-puri-20261104` | `739a1ef`가 origin/Puri의 조상임을 확인. 병합 전 로컬 보조 브랜치 정리 |
| `codex/boss-polish` | 원격 tip `986eb6c`가 origin/Puri의 조상임을 확인. 작업 완료된 원격 보조 브랜치 정리 |
| `codex/backup-before-history-sync-20261104` | origin/Puri에 없는 커밋 46개가 남아 있어 보존 |
| `main`, `Puri`, `Leesc` | 기준·협업 브랜치로 보존 |

삭제 직전 원격 tip을 다시 확인하고 바뀌었다면 삭제하지 않는다. 현재 작업과 무관한 미커밋 파일이나 다른 기기의 워크트리를 삭제하지 않는다.
