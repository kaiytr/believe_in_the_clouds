# 재화 시스템 개발명세서

- 작성일: 2026-10-31
- 상태: **현재 범위의 게임 규칙 확정. 코드·API·테스트는 아직 구현하지 않는다.**
- 확정 범위: **재화 시스템을 테스트 씬에서 독립 검증. 메인 플레이 루프 연동은 후속 작업.**
- 메인 플레이 루프 상태: **미구현. 로비·분기·스테이지 진행·보스·엔딩 전체 흐름은 향후 구현 대상.**
- 초기 자산: **0코인·0조각·초기 교리 지급 없음·장착 없음.**
- 사망 규칙: **전부 미장착 → 보유 조각 목록 확정 → 그 목록의 30%를 버림해 무작위 소실.**
- [상세기획](CurrencySystem_Design.md) / [개발자·AI 가이드](CurrencySystem_Guide.md)
- 구현 시작은 사용자 별도 지시 이후다.

## 1. 기존 프로젝트와 변경 예정 지점

Unity 6000.3.5f2, uGUI, Runtime/EditMode/PlayMode asmdef 구조다.

| 기존 파일 | 현재 역할 | 이번 재화 검증에서 연결할 내용 |
| --- | --- | --- |
| Assets/Scripts/UI/DoctrineHud.cs | 고정 ‘재화’ 표시, 내장 예시 교리 표시 가능 | 잔액·획득 피드백·비행 목적지, 초기 지급과 예시 표시 구분 |
| Assets/Scripts/Shop/ShopCatalog.cs | 가격·등급 없는 12개 조각 정의 | 안정적 카드 ID·확정 가격·등급 |
| Assets/Scripts/Shop/ShopScreen.cs | 선택 조각을 무료로 지급 | 원자적 구매, 보유·합계·부족액 |
| Assets/Scripts/Shop/ShopCardView.cs | 카드 문구·선택 표시 | 가격·등급 표시 |
| Assets/Scripts/Doctrine/DoctrineInventory.cs | 세 종류 Dictionary, Add/Count/Describe | 보유 조각의 단일 원장 파사드 |
| Assets/Scripts/Doctrine/DoctrineManager.cs | 실행 중 교리·Owner·효과 관리 | 플레이어 교리 전체 해제와 효과 정리 어댑터 |
| Assets/Scripts/Enemy/Core/EnemyUnit.cs | 일반 적 공통 사망 | 드롭 추첨·일회 보상 접수 |
| Assets/Scripts/Combat/PlayerHealth.cs | HP·사망·교리 이벤트 | 사망 ID와 전체 해제 후 손실 처리 |
| Assets/Scripts/Boss/BossCombatController.cs | 일반 적과 독립된 보스 | 코인 보상 0 검증 |
| Assets/Scripts/Combat/StageTestController.cs | R 씬 재시작 | 실제 사망과 디버그 재시작 구분 |
| Assets/Editor/StageTestProjectSetup.cs | 테스트 씬·공통 프리팹 구성 | 필요 시 경제 테스트 구성의 재생성 대응 |

실제 로비·분기 진행·스테이지 전체 이어 하기·재단은 이번 범위에서 구현하지 않는다. 기존 테스트 씬과 전용 테스트 어댑터를 사용한다.

기존 ActiveDoctrine은 실행 엔진의 상태다. 별도의 ‘미장착 완성 교리 소유 자산’ 모델을 추가하지 않는다. 사망 손실의 기준은 전체 해제 후의 원본 보유 조각 목록이다.

## 2. 책임과 구현 범위

| 구성 제안 | 책임 |
| --- | --- |
| Currency | 어디서든 호출하는 공통 지급·소비·조회·알림 API |
| EconomyService | 초기화, 거래 순서, 저장과 드롭·상점·사망 처리 조정 |
| EconomyLedger | 보유 코인·대기 보상·보유 조각 검증과 거래 계산 |
| EconomySettings | 시작 0코인, 초기 지급 없음, 드롭 범위·손실률·상한 |
| ShopCatalog / ShopEntry | 조각 ID·가격·등급의 단일 출처 |
| IEconomyStore / JsonEconomyStore | 경제 데이터 저장·복원·검증·백업 |
| CoinFlightView | 재사용 토큰, 좌표 변환, 비행·도착 보고 |
| IEquipmentAdapter | 전체 미장착, 효과 정리, 해제 후 원본 보유 조각 목록 제공 |
| PlayerDeathCoordinator | 사망 중복 방지, 해제 후 손실 계획, 저장, 완료 알림 |
| TestEquipmentAdapter / TestEconomyContext | 실제 재단·메인 루프 없이 검증용 장착·처치·사망 상태 제공 |

향후 연결 계약: IStageSnapshotProvider, GameSaveCoordinator, ILobbyTransition. 이번에는 전체 게임 구현체를 만들지 않는다. 재화 거래 성공이나 사망 완료 이벤트를 후속 진행 시스템이 받을 수 있게 책임 경계를 유지한다.

## 3. 공통 API 계약

Unity 메인 스레드에서 호출하는 안이다. 백그라운드 작업 결과는 메인 스레드 큐로 전달한다.

| 제안 시그니처 | 의미 |
| --- | --- |
| int Currency.Balance | 사용할 수 있는 보유 코인 |
| int Currency.PendingBalance | 아직 도착하지 않은 접수 보상 |
| bool Currency.IsReady | 복원 완료 및 거래 가능 상태 |
| bool Currency.CanAfford(int amount) | 안내용 조회 |
| CurrencyResult Currency.TryGrant(int amount, string reason, string transactionId = null) | 연출 없는 즉시 지급 |
| CurrencyResult Currency.TryGrantWithEffect(int amount, Vector3 position, string reason, string transactionId = null, Camera sourceCamera = null) | 예약·저장 후 드롭/비행 연출 |
| CurrencyResult Currency.TrySpend(int amount, string reason, string transactionId = null) | 잔액 검사 후 소비 |
| event Action&lt;CurrencyChange&gt; Currency.Changed | 커밋된 변경 알림 |
| EconomyResult EconomyService.TryPurchase(PurchaseRequest request) | 비용과 조각을 원자적으로 변경 |

TryGrantWithEffect 성공은 보상 권리의 접수·저장 성공이며 도착 완료를 뜻하지 않는다. 실제 도착 정산은 서비스 내부가 처리한다.

지급·소비는 양의 정수와 비어 있지 않은 reason만 허용한다. CanAfford(0)=true, 음수=false로 하되 실제 거래의 입력 검증을 대신하지 않는다.

CurrencyResult 제안 필드: Success, Error, RequestedAmount, AppliedAmount, BalanceAfter, TransactionId. 오류: NotReady, InvalidAmount, InvalidReason, InsufficientFunds, Busy, SaveFailed, InvalidTransaction, StateConflict. 상한 때문에 지급량이 줄면 AppliedAmount로 알린다. 소비는 일부 차감하지 않는다.

확정 상한은 보유·대기 코인 합계 999,999,999개, 같은 조각 종류당 9,999개다. 코인 지급은 상한까지 가능한 금액만 접수하고 초과분은 지급하지 않는다. 조각 보유 한도를 초과하는 구매는 비용 차감과 조각 지급 모두 실패 처리한다.

CurrencyChange 제안 필드: BeforeBalance, AfterBalance, BeforePending, AfterPending, ActualDelta, Reason, TransactionId. 커밋 후 알리고 이벤트 중 재진입은 Busy로 거절하는 안이다.

reason은 확장 가능한 진단 문자열이다. 예: enemy.defeat, quest.completed, altar.combine, upgrade.purchase. 새 기능마다 중앙 서비스의 사유 enum/switch를 고치지 않는다.

## 4. 거래와 데이터 소유

거래 순서: 잠금/입력/거래 ID 검사 → 작업 스냅샷 복사 → 전체 변경 적용 → 저장 → 내부 상태 교체 → 알림.

코인 소비와 교리 조각 지급은 같은 거래다. 저장 실패 시 기존 공개 자산을 유지한다. UI의 합계나 구매 버튼 상태만 신뢰하지 않고 실제 실행 시 다시 검증한다. 저장 후 알림 실패는 거래를 재실행하는 이유가 되지 않는다.

향후 다른 저장 도메인의 강화·진행 결과를 함께 바꾸면 후속 저장 조정기와 하나의 작업 스냅샷으로 묶는다. TrySpend 후 실패 가능한 외부 효과를 별도로 실행하는 코드를 원자적 구매로 간주하지 않는다.

이번 경제 저장 루트 EconomyProfileData 제안:

| 영역 | 값 |
| --- | --- |
| 메타 | schemaVersion, revision, profileId |
| 코인 | balance, pendingGrants[] |
| 조각 | subjectCounts[], conditionCounts[], resultCounts[] |
| 중복 방지 | processedTransactions[]와 최초 결과 |
| 사망 | deathId, phase, 원본 revision, 확정 손실 수량·종류, 완료 여부 |
| 테스트 문맥 | 테스트 실행·처치 ID, 필요한 어댑터 스냅샷 식별자 |

장착에 사용되는 조각도 원본 소유량으로 정확히 한 번 표현한다. 장착/예약 상태 때문에 가용 조각만 조회되는 시스템은 어댑터가 전체 해제 후 보유 목록으로 정규화한다. 이미 보유량에 포함된 조각을 해제 때 다시 더하지 않는다.

별도 완성 교리 자산, 완성 교리 단위 손실 목록, 장착 수 기반 손실 필드는 만들지 않는다. 장착 연결은 실행 상태로 다루고 사망 완료 후 비어 있어야 한다.

ResultType에는 숫자 3이 비어 있다. enum 숫자를 배열 인덱스로 직접 쓰지 않는다. 고정 ID 또는 명시적 매핑을 사용한다. 조회 데이터는 깊은 복사/읽기 전용으로 노출한다. 음수·오버플로·잘못된 ID·중복 거래·미지원 저장 버전을 검증한다.

## 5. 거래 ID·드롭·중복 방지

일회성 보상은 안정적인 원인 거래 ID를 사용한다. 예: test:{testSessionId}/enemy:{spawnId}/defeat. 같은 ID 재요청은 최초 결과를 반환하며 재추첨·추가 지급·중복 연출하지 않는다. 같은 ID로 다른 입력은 InvalidTransaction이다.

개 5~7, 신도 3~5, 간부 7~8, 보스 0이며 양끝 포함 균등 정수 추첨으로 확정됐다. 드롭량은 최초 접수 시 한 번 결정한다. 적의 실제 사망에서 지급하며 OnDisable/OnDestroy는 지급 경로가 아니다.

플레이어가 살아 있다면 플레이어 공격·교리 효과·환경 피해 등 출처와 무관하게 처치 보상을 지급한다. 플레이어 사망 확정 이후에는 코인 처치 보상을 접수하지 않는다. 사망 교리 효과로 처치한 적도 무보상이며, 사망 전에 접수한 pending은 유지해 손실 계산 전 정산한다.

pendingGrants는 grantId, transactionId, remainingAmount를 가진다. 토큰 도착은 grantId/tokenId로 해당 예약만 정산한다. 완료·취소된 토큰의 늦은 콜백이 새 보상을 소비하지 못하게 한다.

테스트 씬에서 보상 ID의 저장·재요청을 검증한다. 향후 메인 루프에서는 stageInstanceId/spawnId와 적 생사 스냅샷을 보상 거래와 함께 연결한다. 실제 적 생사·HP 재개 저장기는 이번에 개발하지 않는다.

## 6. 드롭 연출·HUD·상점

CoinFlightView는 퍼짐 후 곡선 비행을 표현한다. 충돌체·줍기 입력은 없다. 전용 ScreenSpaceOverlay Canvas를 HUD 위·모달 아래에 두고, 출발점은 해당 월드 카메라로 변환한다. 목적지는 DoctrineHud의 실제 CurrencyBadge RectTransform 중심이다.

RectTransformUtility로 같은 Canvas 로컬 좌표로 변환한다. 비행 중 해상도/화면 비율 변화에 맞춰 목적지를 갱신한다. HUD/카메라가 없으면 즉시 정산하며 화면 밖 출발점은 가장자리로 제한한다. 토큰 raycastTarget=false다.

퍼짐 0.2초, 비행 0.45~0.7초, 토큰 시차 0.03초, 동시 표시 64개를 테스트 초기값으로 사용한다. 시간·크기·색상·동시 표시 수는 테스트하면서 조정할 수 있도록 설정으로 분리한다. 풀과 unscaledDeltaTime을 사용하고 초과량은 묶음 토큰으로 표현한다. UiTheme.CircleSprite 활용을 제안한다. 연출 설정 변경이 실제 지급량을 바꾸지 않게 한다.

도착 때 해당 대기량을 보유량으로 옮겨 저장하고 HUD를 갱신한다. 저장 실패면 pending을 유지하고 재시도한다. 사망·씬 전환·종료에서는 이전 콜백을 취소하고 잔여 예약을 일괄 정산한다.

ShopEntry에는 안정적 ID, Price, Rarity를 둔다. 가격은 일반 30, 고급 55, 희귀 95이며 카드별 대응은 상세기획 가격표를 따른다. 신뢰 카탈로그 ID로 금액을 계산하고 UI가 준 가격을 사용하지 않는다. 합계는 long으로 계산한다. 구매 성공에만 선택을 비운다.

상점은 무제한 재고이며 결제 후 같은 카드를 다시 살 수 있다. 한 장바구니에서는 카드당 1장만 허용하고 중복 카드 ID 요청을 거절한다. 다른 거래에서의 같은 카드 구매를 과거 구매 이력만으로 막지 않는다.

DoctrineInventory Add/Count/Describe는 호환 파사드로 유지하는 안이다. 실제 자산은 원장 하나가 소유한다. 신규 코드는 실패를 반환하는 Try 계열을 사용한다.

초기 교리·조각은 지급하지 않는다. DoctrineHud의 내장 예시를 실제 소유/장착으로 저장하지 않는다. 신규 프로필 검증 시 실제 빈 상태가 보이도록 예시 표시를 끄거나 검증용 표시와 명확히 구분한다.

## 7. 사망: 전체 해제 후 단일 조각 목록에서 손실

### 7.1 상태와 어댑터

현재 범위의 상태: Alive → DeathCaptured → DeathEffectsProcessed → Unequipped → LossPrepared → Committed. 실제 ReturningToLobby는 후속 메인 루프 상태다.

PlayerDeathCoordinator는 생명 순번이 포함된 deathId로 같은 사망을 한 번만 처리한다. 회복 후 재사망은 새로운 ID다.

확정된 사망 순서는 처치 보상 접수 차단 → 기존 사망 교리 효과 한 번 처리 → 전체 미장착·효과 정리 → 단일 조각 목록에서 손실이다. 기존 OnDeath 이벤트를 재사용하고 경제 조정기가 같은 사망 이벤트를 다시 발행하지 않는다.

IEquipmentAdapter의 계약:

1. 원본 상태를 복구/검증용으로 캡처한다.
2. 플레이어의 교리를 전부 미장착 상태로 전환한다.
3. 이벤트 구독·지속 버프·슬롯 UI 연결을 정리한다.
4. **해제 후 원본 보유 조각 전체 목록**을 제공한다.

기존 DoctrineManager.Clear는 모든 owner를 대상으로 하므로 플레이어만 정리하는 어댑터가 필요하다. 적의 교리까지 해제하지 않는다. 실제 재단을 구현하지 않고 테스트 어댑터로 동일 계약을 검증한다.

### 7.2 손실 계획

계산은 전체 해제와 조각 정규화가 끝난 작업 스냅샷에서 한다.

- lostCoins=floor((balance+pending)*30/100).
- lostPieces=floor(allPiecesAfterUnequip*30/100).

장착 이전/이후로 손실 모집단을 분리하지 않는다. 장착 교리 개수를 별도로 세어 소실시키지 않는다.

조각은 총 보유 수량을 기준으로 가중 추출하여 개체별 동일 확률로 뽑는다. 뽑은 조각을 제거하고 다음을 뽑는 비복원 방식이다. 종류별 손실 수를 따로 버림 계산하지 않는다.

예: 미장착 7조각과 두 교리에 사용된 6조각이면 해제 후 13조각, 손실 3조각, 잔여 10조각, 장착 없음이다. 장착 교리 3개에 사용된 9조각만 있으면 손실 2조각이다.

계획에는 원본 revision, deathId, 정규화한 전체 목록, 소실할 종류별 수량, 최종 코인·조각 상태를 기록한다. 재시도 때 다시 추첨하거나 해제 반환량을 다시 더하지 않는다.

### 7.3 커밋과 실패 처리

1. 거래·비행 정산 재진입을 잠그고 원본을 캡처한다.
2. 사망 확정 후 신규 처치 보상 접수를 차단한 상태에서 기존 사망 교리 효과를 한 번 처리한다.
3. 전체 미장착과 효과·구독·슬롯 정리를 수행한다.
4. 전체 조각 목록을 확정하고 불변 손실 계획을 만든다.
5. 대기 코인 정산·코인 손실·조각 손실·전체 미장착·사망 완료를 한 번에 저장한다.
6. 성공 후 남은 조각과 코인을 게시하고 사망 완료 이벤트를 한 번 발행한다.
7. 테스트 씬에서 결과를 확인한다. 실제 로비 씬 로드는 수행하지 않는다.

저장 실패면 공개 원장의 자산을 바꾸지 않고 사망 처리 잠금을 유지한다. 일시적인 전체 해제를 일반 플레이에 노출하지 않는다. 최초 계획을 그대로 재시도한다. 앱 재실행/중복 이벤트에도 동일 deathId의 손실이 재적용되지 않게 저장한다.

완료 후 장착은 비어 있으며, 손실로 일부 조각이 없어진 이전 조합을 자동 복원하지 않는다. 손실 계획에 완성 교리 제거/보호 규칙을 추가하지 않는다.

## 8. 경제 저장과 후속 메인 루프 계약

### 8.1 이번 범위

저장 경로 제안: Application.persistentDataPath/economy-save-v1.json. 자동 테스트는 주입한 별도 경로를 사용한다.

임시 UTF-8 JSON 기록·flush → 원자 교체 → 정상본 백업. 백업 복구 중 손상된 본 파일이 정상 백업을 덮어쓰지 않게 한다.

Load 결과: Missing/Loaded/RecoveredBackup/Invalid/FutureVersion. Missing만 신규 프로필을 만들며 0코인·0조각·장착 없음으로 시작한다. 복원 때 초기값을 다시 적용하지 않는다. Invalid/FutureVersion은 원본을 보존하고 거래를 잠근다.

코인·조각·pending·거래 ID·사망 결과의 저장/복원을 테스트한다. 테스트 장착 자료는 고립된 픽스처이며 실제 신규 프로필 지급과 구분한다.

### 8.2 후속 범위

메인 루프 개발 시 GameSaveCoordinator가 경제와 스테이지 상태를 같은 시점에 캡처하도록 연결한다. 적 사망/처치 보상, 장착 연결, 위치·HP·적 생사, 역할 진행, 사망 로비 복귀를 일관되게 복원한다.

이번에는 실제 로비 생성·분기 UI·스테이지 진행 관리자·전체 이어 하기·엔딩·재단을 개발하지 않는다. 필요한 이벤트·스냅샷 계약만 유지하며, 이번 테스트 통과가 메인 루프 연동 완료를 뜻하지 않는다.

## 9. 검증 계획

아직 구현/테스트 실행은 하지 않는다. 후속 구현 지시를 받은 뒤 테스트 씬과 EditMode/PlayMode에서 다음을 검증한다.

| 범위 | 검증 |
| --- | --- |
| 초기 자산 | 0코인·0조각·장착 없음, 예시 교리 실제 지급 안 됨 |
| 공통 API | 양수/0/음수, 부족액, 상한·오버플로, 사유, 중복 ID |
| 보유 한도 | 코인 보유·대기 합산 상한, 부분 지급과 초과분 미지급, 조각 종류당 9,999개 |
| 결제 | 확정 12개 가격·등급, 무제한 재고·재구매, 장바구니 중복 거부, 저장 실패 무변경 |
| 드롭 | 개 5~7·신도 3~5·간부 7~8·보스 0, 양끝 포함 균등 추첨, 중복 사망 보상 방지 |
| 보상 자격 | 생존 중 환경·교리 처치도 지급, 사망 이후 처치 무보상, 사망 전 pending 유지 |
| 비행 | 퍼짐→HUD, 도착 증액, 다양한 화면비, 없는 HUD, 다중 토큰 |
| 사망 순서 | 사망 교리 효과 한 번 처리 → 전부 미장착·효과 정리 → 전체 조각 목록 확정 → 손실 계산 |
| 단일 조각 목록 | 미장착 7+장착 사용 6=13, 손실 3, 잔여 10 |
| 계산 경계 | 총 0~3조각 손실 0, 4조각 손실 1, 9조각 손실 2 |
| 중복·실패 | 해제 시 중복 추가 없음, 재시도 동일 추첨, 사망 중복 차감 없음 |
| 저장 | 경제 재실행 복원, pending 정산, 손상/백업/미지원 버전 |
| 경계 전환 | 비행 중 사망·씬 이동·종료, 취소 토큰의 늦은 도착 |
| 회귀 | 기존 교리·전투·보스·StageIntegration 테스트 |

실제 위치·HP·적 생사 이어 하기와 사망 로비 전환은 이번 완료 조건에서 제외한다. 테스트 어댑터가 상태·완료 이벤트를 전달하는 계약까지만 확인한다.

연출 초기값은 테스트 결과에 따라 조정한다. 엔딩 이후 자산 계승, 실제 스테이지 보상 구성·적 수, 첫 구매/조합 목표 시간과 메인 루프 구체 연동 설계는 후속 결정으로 보류한다. 현재 재화 검증 범위의 게임 규칙은 확정 상태다.

Unity 테스트는 설치된 6000.3.5f2를 사용한다. [Unity 공식 명령행 테스트 문서](https://docs.unity.com/ja-jp/engine/6000.3/manual/programming-environment/test-framework-introduction/running-tests/run-tests-from-command-line)에 따라 -runTests -batchmode -testPlatform -testResults로 실행하는 안이다. 연출은 그래픽을 켠 검증과 캡처로 확인한다.

## 10. 후속 구현 순서와 인수 조건

1. 문서 검토와 별도 구현 지시.
2. 경제 설정·프로필·저장·원장·공통 API.
3. 기존 인벤토리·상점·가격·등급 연결.
4. 적 드롭·자동 비행·HUD.
5. 테스트 장착 어댑터와 전체 해제 후 조각 손실.
6. 테스트 씬의 경제 저장/복원, 중복·실패 시나리오.
7. 자동 테스트·화면 검증·구현 결과서.

결과서에는 재화 독립 검증 범위와 메인 루프 미연동 상태를 명시한다. 실제 메인 루프 구현은 후속 별도 작업이다. 현재는 문서만 수정하며 코드·프리팹·씬을 변경하지 않는다.
