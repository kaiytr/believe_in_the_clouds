# 사망 결과 UI 구현 결과 — 2026-11-03

## 1. 기존 구조 분석

- Unity 6000.3.5f2, URP 2D, uGUI, 기존 OS 한글 폰트 헬퍼를 사용한다. 외부 패키지를 추가하지 않았다.
- `PlayerHealth.ApplyDamage`에서 HP 0을 판정하고 즉시 적 보상을 차단한다. 기존 `OnHit` → `OnDeath` 교리 이벤트 순서를 유지한다.
- 기존 사망 경로는 `PlayerDeathCoordinator.CaptureDeath` → `EconomyService.TryHandleDeath` → `RetryDeath`였다.
- `RetryDeath` 순서: Captured 저널 저장 → 기존 사망 교리 효과 1회 → 플레이어 장비 해제(공유 원장의 소유량 검증) → 기존 `EconomyLedger.PrepareDeath` → LossPrepared 저장 → 손실 적용/미도착 코인 정산/다음 로비 런 생성 → Committed 저장 → `DeathCompleted` 통지.
- 기존 손실은 코인(보유+미도착)과 전체 소유 교리 조각 각각 30% 내림이며, 조각 선택에는 저장된 난수 시드를 쓴다. `PrepareDeath`와 이 계산은 수정하지 않았다.
- `LobbyFlowRuntime.Update`는 커밋된 런의 목적지가 현재 씬과 다르면 기존 `Route` 코루틴으로 로비에 이동한다. 씬 진입 시 `Attach`가 저장된 생명 ID, 체력, 위치를 복원한다.
- 코인은 `EconomyService`의 즉시 지급/비행 예약/상자 보상에서 취득하며, 비행 도착은 이미 획득한 예약 금액을 보유 잔액으로 옮긴다.
- 별도 키워드 저장소는 없다. `DoctrineInventory`는 경제 원장 `pieces`를 읽는 호환 계층이다. 주어/조건/결과 조각을 중복 소유할 수 있고, 구매·상자·기존 지급 API가 같은 원장으로 들어간다.
- 일반 적은 공통 `EnemyUnit`의 실제 사망 경로와 고유 `defeatId`를 사용한다. 기존 종류 판별은 Dog/Believer/그 외 Executive였다. 보스는 별도 `BossCombatController.Die`를 사용하며 기존처럼 코인 보상을 새로 추가하지 않는다.
- 저장은 `IEconomyStore` / `JsonEconomyStore`의 복사→검증→임시 파일 flush→원자적 교체 및 `.bak` 복구 구조다. 경제 상태와 런 상태를 같은 프로필에 저장한다.
- UI는 런타임 Canvas 생성이 널리 쓰이고, `UiTheme`, `CombatUiResources.KoreanFont`, 공용 `UiEventSystemBoot`, `UiModal`이 있다.
- 시간 일시정지는 별도 중앙 매니저 없이 `Time.timeScale`을 사용한다. 플레이어 피니시 타격 정지는 실시간 대기 후 시간을 복원하므로 결과 화면과 충돌하지 않도록 가드를 추가했다.
- 기존 타이틀 씬은 없었다.

## 2. 변경 후 사망 흐름

1. 기존 HP 0 판정 및 적 보상 차단.
2. `PrepareDeath`: 교리 피격/사망 효과가 월드를 변경하기 전에 현재 플레이 카메라를 동기 캡처하고 원인·UTC 시각·씬·통계를 동결한다.
3. 기존 피격 이벤트와 사망 진입. `CaptureDeferredDeath`로 기존 Captured 저널에 결과를 함께 저장한다.
4. 기존 사망 교리 효과는 첫 Captured 저장 성공 후 1회 처리한다. 저장 실패 시 기존 런타임 재시도 정책을 따른다.
5. `awaitingChoice`에서 대기: 장비 해제·재화 손실·자동 씬 이동을 진행하지 않는다.
6. `UiModal`, `Time.timeScale = 0`, 이동/공격/대시 비활성화. 현재 씬 위에 결과 Canvas를 1개 생성한다.
7. 어느 버튼이든 `CompleteDeferredDeath` → 기존 `RetryDeath`의 나머지 단계를 그대로 실행한다.
8. 성공한 경우에만 시간 복구 후 기존 `LobbyFlowRuntime.Route`를 호출한다. 이어하기는 Lobby, 저장 및 종료는 Title이다. 독립 테스트 씬처럼 연결된 로비 라우터가 없으면 같은 Lobby 목적지를 직접 로드한다.

`HasResult` / 공용 Active 소유자 / 버튼 처리 중 플래그 / 기존 death ID 영수증 / `awaitingChoice`가 각각 화면 중복, 입력 중복, 손실 중복, 자동 재시도에 의한 조기 손실을 막는다. 최초 사망 요청이 경제 트랜잭션 재진입 때문에 Busy일 때도 조정자가 실시간으로 캡처 저널 요청을 다시 시도한다.

## 3. 통계와 캡처

- 별도 통계 싱글턴을 추가하지 않고 기존 경제 서비스와 프로필에 `PlayStatistics`를 추가했다.
- 코인은 거래 저장 시 `(잔액+미도착 금액)`의 양의 증가량만 합산한다. 지출, 코인 도착, 일괄 정산, 같은 거래 ID 재요청은 추가 획득으로 집계하지 않는다. 보유 상한 때문에 실제 지급되지 않은 금액도 제외한다.
- 키워드는 같은 거래의 조각 종류별 양의 증가량을 합산한다. 중복 조각도 기존 소유 규칙대로 개수에 포함한다. 구매/상자/기존 지급을 공통 처리한다.
- 실제 일반 적/보스 사망 시 공통 종류와 고유 처치 ID를 관측한다. 보상 저장이 잠시 실패해도 관측한 처치를 유지하고 다음 거래 또는 플레이어 사망 캡처 저장에 합친다. 단순 비활성화·파괴·중복 피해는 집계하지 않는다. 사망 확정 뒤 새 처치는 결과에 들어가지 않는다.
- 일반 적의 기존 종류 판별을 기본으로 유지하고 Inspector의 `overrideRewardKind` / `rewardKind`로 향후 적이 기존 종류 체계에 참여할 수 있다. 종류 enum을 확장하면 통계/UI는 해당 항목을 순회하며, 새 표시명은 `DeathCause.EnemyLabel`에서 지정한다.
- 통계 단위는 기존 생명/런이다. 정상적인 분기 종료와 로비 방문은 같은 런이므로 누적 유지하며, 사망 커밋으로 새 런을 만들 때 초기화한다. 확정 결과는 별도 복사본으로 남는다.
- `DamageInfo`에 선택적 공격자와 상세 설명을 추가했다. 돌 투척, 개 돌진, 접촉, 마법진, 보스 공격과 화상이 실제 피해 경로에서 전달한다. `IHealth.TakeDamage(float, source)`도 기존 source를 활용한다. 식별 불가 시 기본 문구를 표시한다.
- 카메라 `RenderTexture`를 임시 할당하고 플레이어 몸 중심으로 크게 확대해 `ReadPixels`로 캡처한다. 기본 프레임 높이는 플레이어 몸 높이의 2.2배로, 플레이어가 캡처 높이의 약 45%를 차지한다. 렌더 크기는 가로·세로 최대 960픽셀이며 중앙을 16:9로 잘라 저장한다. 카메라 위치·확대값·타깃과 active RT는 `finally`에서 복원/반납한다. 카메라의 자동 화면비 설정은 변경하지 않는다.
- 결과 Canvas 생성 전 캡처하므로 결과 UI 자체가 들어가지 않는다. 게임 카메라 화면을 보존하며 기존 ScreenSpaceOverlay HUD는 캡처 대상에 포함하지 않는다.
- UI는 `RawImage + AspectRatioFitter.EnvelopeParent + RectMask2D`로 이미지 칸을 빈 여백 없이 채운다. 화면비 차이는 중앙 크롭으로 처리하며 플레이어 중심과 원본 비율을 유지한다. 카메라 부재/캡처 오류는 대체 문구와 `captureError`를 기록하며 사망 UI를 막지 않는다. 캡처 Texture2D는 씬 종료 시 조정자가 해제한다.
- 왼쪽 하단은 빈 레이아웃으로 남겼다. 결과 화면은 별도 씬이 아니다.

## 4. 저장 및 종료와 호환성

기존 경제 JSON의 **최신 사망 기록 1개**(`death.result`)에 원인, UTC 시각, 씬 이름, 확정 통계, PNG Base64, 캡처 실패 정보를 함께 저장한다. PNG를 별도 파일로 쓰지 않으므로 결과/이미지/페널티/다음 런을 같은 원자적 프로필 저장으로 확정할 수 있다. 기존의 최신 사망 기록 정책을 따르며 전체 사망 이력 목록은 추가하지 않았다.

스키마를 4로 올렸고 v1/v2/v3 JSON은 기존 자산·런을 유지하면서 변환한다. 예전 저장에 없던 플레이 통계는 0에서 시작하며 과거 획득량을 보유량으로 역산하지 않는다. 미래 버전/손상 파일 거부와 백업 복구 정책은 유지한다.

선택 후 저장 실패 시 이동하지 않고 오류 문구와 재시도 가능한 두 버튼을 유지한다. 커밋 직전 실패해도 기존 저널의 시드/손실 계획을 재사용한다. 백그라운드 재시도로 저장이 완료되어도 사용자 재선택 전 자동 이동하지 않는다.

결과 대기 중 프로세스를 종료한 경우에는 다음 초기화에서 기존 Captured 저널을 같은 페널티로 커밋해 사망 이전 자산으로 돌아가는 것을 막는다. 사망 교리 효과는 프로세스 재시작 시 재실행하지 않는 기존 정책을 유지한다. Captured 저널 자체가 저장 장치 오류로 한 번도 기록되지 않은 경우에는 디스크에 없는 사건을 재시작 후 복구할 수 없다.

**에디터 저장 정책은 기존 그대로다.** 일반 Editor Play와 자동 테스트는 메모리 저장이다. 디스크 영속성을 확인하려면 기존 `-usePersistentEditorProfile` 인자를 사용한다. 이 경우 `Library/Economy/Editor/economy-save-v1.json`, 빌드는 `Application.persistentDataPath/economy-save-v1.json`이다. 메모리 테스트 화면의 ‘저장 및 종료’도 해당 격리 저장소에 저장하므로 실제 사용자 파일을 수정하지 않는다.

## 5. 타이틀 구현 범위

`Assets/Scenes/Gameplay/Title.unity`에는 `TitleScreen` 루트가 있고 Play 시 동일한 공통 uGUI/한글 폰트/갈색 프레임 스타일로 제목과 ‘로비로 이동’ 버튼을 생성한다. 기존 저장 서비스를 유지하며 로비의 기존 체력·런 복구 흐름으로 연결한다. 새로운 게임 재개/슬롯/설정 메뉴는 만들지 않았다.

Build Settings에 Title을 추가했고 기존 첫 씬 Lobby는 유지했다. `Tools/Death Result/Create minimal title scene`은 최소 씬 재생성 도구다. 기존 플레이 루프 빌드 설정 도구도 Title 목적지를 포함하도록 수정했다. 플레이어/HUD 전파 도구의 씬 목록은 게임플레이 제작 씬용이므로 플레이어가 없는 Title을 포함하지 않는다.

## 6. 신규 파일과 역할

모든 신규 Unity 폴더/스크립트/씬에는 `.meta`를 포함한다.

| 파일 | 역할 |
| --- | --- |
| `Assets/Scripts/DeathResult/DeathResultData.cs` | 플레이 통계, 적별 처치, 확정 사망 결과, 데이터 검증, 원인 표시명 |
| `Assets/Scripts/DeathResult/EconomyService.DeathResult.cs` | 기존 서비스의 획득 통계/처치 관측/지연 사망 시작·완료 API |
| `Assets/Scripts/DeathResult/DeathScreenCapture.cs` | 실제 카메라 동기 캡처, 해상도 제한, RT 복구/반납 |
| `Assets/Scripts/DeathResult/DeathResultView.cs` | 현재 씬 결과 오버레이, 통계/원인/캡처, 두 버튼과 오류 표시, 공통 메뉴 UI 헬퍼 |
| `Assets/Scripts/DeathResult/TitleScreen.cs` | 최소 타이틀 UI와 기존 로비 연결 |
| `Assets/Scenes/Gameplay/Title.unity` | 신규 타이틀 목적지 씬 |
| `Assets/Editor/DeathResultProjectSetup.cs` | 타이틀 재생성 및 빌드 등록 도구 |
| `Assets/Tests/EditMode/DeathResultEditModeTests.cs` | 획득 중복 방지, 지연 손실, 저장 실패/재시작 복구, v3 마이그레이션 |
| `Assets/Tests/PlayMode/DeathResultPlayModeTests.cs` | 실제 사망 UI/입력/씬 흐름, 실제 적 원인·통계, 저장 실패, 캡처·비율·해제 검증 |
| `Docs/DeathResultSystem_ImplementationStatus.md` | 구조 분석, 구현, 저장 정책, 파일별 역할, 검증 및 수동 확인 |
| `Docs/DeathResultVerification/*` | 검증 요약과 16:9/4:3 실제 렌더 이미지 |

## 7. 수정 파일과 역할

| 파일 | 변경 |
| --- | --- |
| `Assets/Scripts/Combat/PlayerHealth.cs` | 기존 HP/이벤트 순서를 유지하며 치명타 원인 전달, 피격 효과 전 캡처, 결과 중 체력 리셋 방지 |
| `Assets/Scripts/Combat/DamageInfo.cs` | 기존 생성자 호출과 호환되는 선택적 공격자/피해 설명 |
| `Assets/Scripts/Economy/PlayerDeathCoordinator.cs` | 기존 생명 ID/장비 어댑터 유지, 캡처/저널 대기/정지/UI/선택/리소스 해제 연결 |
| `Assets/Scripts/Economy/EconomyService.cs` | 기존 사망 파이프라인 중 선택 대기와 재시작 복구, 거래 커밋 시 통계 연결 |
| `Assets/Scripts/Economy/EconomyModels.cs` | 스키마 4, 통계와 선택 대기/확정 결과 필드, 선택적 데이터 정규화 |
| `Assets/Scripts/Economy/EconomyLedger.cs` | 신규 필드 무결성 검사; 기존 손실 계산은 유지 |
| `Assets/Scripts/Economy/JsonEconomyStore.cs` | v3까지 기존 자산을 유지하는 스키마 변환 |
| `Assets/Scripts/Lobby/LobbyFlowRuntime.cs` | 결과 중 자동 라우팅 대기, 버튼에서 기존 Route 호출 |
| `Assets/Scripts/Player/PlayerAttack.cs` | 실시간 히트스톱 종료가 결과 정지를 해제하지 않도록 가드 |
| `Assets/Scripts/Enemy/Core/EnemyUnit.cs` | 공통 종류 속성/향후 종류 설정, 실제 사망 관측 |
| `Assets/Scripts/Enemy/Dog/DogChargeAttack.cs` | 돌진 공격 원인 전달 |
| `Assets/Scripts/Enemy/Believer/StoneProjectile.cs` | 투사체 소유 신자와 돌 투척 원인 전달 |
| `Assets/Scripts/Enemy/Common/EnemyContactDamage.cs` | 접촉 피해의 실제 소유자 전달 |
| `Assets/Scripts/Enemy/Executive/MagicCircle.cs` | 마법진 피해의 공격자 수신/전달 |
| `Assets/Scripts/Enemy/Executive/MagicCircleAttack.cs` | 생성한 마법진에 소유자 전달 |
| `Assets/Scripts/Boss/BossCombatController.cs` | 보스 실제 사망 관측, 공격 종류별 원인 전달 |
| `Assets/Scripts/Boss/BossBurnEffect.cs` | 화상 피해의 보스 소유자와 피해 종류 전달 |
| `Assets/Scripts/Scenes/ProjectScenes.cs` | Title 경로 상수 |
| `Assets/Editor/PlayLoopProjectSetup.cs` | 기존 플레이 루프 씬 빌드 목록 재작성 시 Title 유지 |
| `ProjectSettings/EditorBuildSettings.asset` | Title 빌드 등록 |
| `Assets/Tests/EditMode/ChestEditModeTests.cs` | 저장 스키마 기대값을 현재 버전 상수로 변경 |
| `Assets/Tests/PlayMode/EconomyPlayModeTests.cs` | 결과 표시 후 기존 페널티 검증, 실제 처치 통계, 모달 테스트 정리 |
| `Assets/Tests/PlayMode/LobbyPlayModeTests.cs` | 자동 복귀 기대를 명시적 이어하기로 변경하고 저장 복구 후에도 선택 대기 검증 |
| `Assets/Tests/PlayMode/BossCombatPlayModeTests.cs` | 사망 정지에서도 검증 가능한 실시간 대기 및 테스트 종료 정리 |
| `README.md` | 사망 결과 진입/문서/영속 저장 안내 |

## 8. 실제 검증

Unity 6000.3.5f2의 그래픽 활성 batchmode로 실행했다. 원본이 Unity에서 열려 있어 `/private/tmp/death-result-verification`에 Assets/Packages/ProjectSettings와 Library를 복제하여 검증했다. 원본 에디터를 종료하거나 열린 씬을 덮어쓰지 않았다.

- EditMode 전체: **186/186 통과**. 기존 경제·상자·로비·전투·믿음·씬 전파와 신규 사망 데이터 7개 테스트를 포함한다.
- PlayMode 대상 **25/25 통과**: `DeathResultPlayModeTests;EconomyPlayModeTests;LobbyPlayModeTests;BossCombatPlayModeTests.CloudPlayerDeathHasNoDelayedStrike`.
- PlayMode 검증 범위: 치명타 이후 같은 씬 유지, UI 1개, 플레이어 입력 차단/시간 정지, 코인·키워드 누적과 보유량 분리, 실제 일반 적 3종 처치 중복 방지, 실제 개 사망 원인 전달, 화면 캡처, 저장 실패 시 버튼 재시도, 두 버튼의 같은 페널티, 타이틀 메뉴로 로비 재진입, 새 체력/통계, 시간·모달·UI 정리, 보스 사망 취소 경로.
- 1600×900 / 1200×900 결과 화면을 실제 카메라+Canvas로 렌더링하고 버튼이 화면 안에 있는지 확인했다. 이미지는 `Docs/DeathResultVerification/`에 포함한다.
- 캡처 카메라 부재 처리, RT 타깃/active 복원, 세로 9:16 비율과 최대 크기 제한도 검증했다.
- 전체 PlayMode 모음과 배포 빌드의 수동 플레이/디스크 장애·메모리 프로파일링은 실행하지 않았다. 테스트 수와 최종 결과는 함께 제공하는 `test-summary.json`을 기준으로 한다.

## 9. 수동 확인 및 추가 설정

1. `Lobby.unity`에서 Play → 기존 테스트 도구로 100코인 지급 또는 전체 플레이 루프 시작 → 실제 전투에서 사망한다. 현재 씬에 결과가 남고 배경/조작이 정지하는지 확인한다.
2. 통계는 마지막 보유량이 아니라 그 런의 지급 누계다. 상점에서 소비한 뒤 사망해 차이를 확인한다. 키워드 중복 지급과 상자 지급도 포함한다.
3. 이어하기를 누르면 기존 손실과 로비 체력 초기화가 일어나야 한다. 두 번 클릭해도 손실은 1회다.
4. 다시 사망 → 저장 및 종료 → Title → 로비로 이동. 같은 페널티 상태와 정상 조작을 확인한다.
5. 메모리 테스트의 저장 실패 토글을 켠 채 사망/버튼을 누르면 씬을 유지한다. 복구 후 재시도한다. UI가 모달이므로 실패 토글을 미리 켜고 자동화 또는 에디터 디버거에서 복구할 수도 있다.
6. 실제 디스크 저장은 기존 영속 에디터 옵션 또는 빌드에서 확인한다. 최신 사망은 JSON `death.result`에 있다. UI에서 사망 이력을 조회하는 기능은 범위에 포함하지 않았다.
7. 추가 Inspector 배선은 필요 없다. `PlayerHealth`가 기존처럼 조정자를 보장하고 결과 UI는 런타임 생성한다. Title이 빌드 목록에 포함되어 있는지만 커스텀 빌드 설정을 덮어쓰는 도구가 있을 경우 확인한다.
8. 캡처에서 커스텀 카메라 스택/특수 렌더 효과를 추가하는 경우 해당 타깃 플랫폼에서 결과 이미지를 다시 확인한다. 현재 프로젝트의 실제 URP 카메라 경로로 자동 렌더 검증했다.
