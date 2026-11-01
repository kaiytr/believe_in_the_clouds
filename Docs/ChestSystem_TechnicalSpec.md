# 상자 시스템 개발명세서

- 작성일: **2026-11-01 (Asia/Seoul)**
- 상태: **2026-11-01 사용자 확정 및 테스트 씬 범위 구현 완료.** 확정 규칙은 E 1.5초 유지·조각 1개 보장·중단 시 진행률 초기화다. [구현 결과](ChestSystem_ImplementationStatus.md), [테스트 가이드](ChestSystem_TestGuide.md)
- 기준 기획: [상세기획](ChestSystem_Design.md). 사용자 요청과 제안 정책의 구분도 해당 문서를 따른다.
- 구현은 기존 공통 상호작용·재화·교리 목록·로비 런 코드를 확장한다. 현재 결과와 실행 방법은 별도 구현 결과서·테스트 가이드를 기준으로 확인한다.

## 1. 기존 구조와 재사용 지점

| 현재 파일 | 확인한 동작 | 상자 개발 시 처리 |
| --- | --- | --- |
| [InteractionController.cs](../Assets/Scripts/Interaction/InteractionController.cs) | 최근접 대상 선택, E 즉시 실행, F 보조 실행, 모달에서 E/ESC 닫기 | 일반 대상을 유지하며 선택적 홀드 세션 추가 |
| [InteractionInput.cs](../Assets/Scripts/Interaction/InteractionInput.cs) | `WasPressed`와 `IsHeld` 존재 | 기존 입력 격리 계층 사용. 상자마다 Input을 직접 읽지 않음 |
| [InteractableBehaviour.cs](../Assets/Scripts/Interaction/InteractableBehaviour.cs) | 등록·해제, 콜라이더 표면 거리, void `Interact`, 호출 후 `Interacted` 알림 | 등록·거리 재사용. 해당 알림을 저장 성공으로 해석하지 않음 |
| [InteractionPrompt.cs](../Assets/Scripts/Interaction/InteractionPrompt.cs) | 하단 텍스트 안내만 표시 | 키캡·사각 진행선 표시 모드 추가 |
| [ShopCatalog.cs](../Assets/Scripts/Shop/ShopCatalog.cs) | 일반 5종·고급 5종·희귀 2종, 안정적인 조각 ID | 등급·종류의 원본 목록으로 사용 |
| [DoctrineInventory.cs](../Assets/Scripts/Doctrine/DoctrineInventory.cs) | 경제 원장 조회·지급의 호환 파사드 | 별도 상자 인벤토리 생성 금지 |
| [EconomyService.cs](../Assets/Scripts/Economy/EconomyService.cs) | 복사→저장→게시, 조각 지급·코인 예약·영수증·사망 처리 | partial에서 상자 개방을 단일 거래로 확장 |
| [CoinFlightView.cs](../Assets/Scripts/Economy/CoinFlightView.cs) | 드롭 퍼짐·HUD 비행·토큰별 정산·풀링 | 상자 드롭 위치만 전달해 그대로 재사용 |
| [EconomyModels.cs](../Assets/Scripts/Economy/EconomyModels.cs) | 개발 착수 시 스키마 v2, 코인·조각·대기 보상·영수증·런 저장 | 상자 기록 및 상세 영수증을 포함하는 v3로 확장 |
| [LobbyRunData.cs](../Assets/Scripts/Lobby/LobbyRunData.cs) | 안정적인 런·스테이지 ID, `TemporaryStageData` | 스테이지 인스턴스에 상자 배치·보상 계획 연결 |
| [PlayerHealth.cs](../Assets/Scripts/Combat/PlayerHealth.cs), [PlayerHitReact.cs](../Assets/Scripts/Player/PlayerHitReact.cs) | 실제 피해 수락 시 교리 `OnHit` 이벤트, 체력 변경·사망 상태 | 해당 플레이어의 수락된 OnHit로 홀드 취소. HP 차이만 비교하지 않음 |
| [LobbyFlowRuntime.cs](../Assets/Scripts/Lobby/LobbyFlowRuntime.cs) | 전환·복원·저장 실패에 따른 입력 제한 | 운영 씬에서 같은 조작 가능 상태를 참조 |

기존 `TryGrantPiece`와 `TryGrantWithEffect`를 두 번 호출하면 두 저장 사이에 부분 지급이 생길 수 있다. 두 API를 나란히 호출하는 방식으로 상자 보상을 구현하지 않는다.

## 2. 책임과 파일 계획

아래는 초기 책임 분할이다. 첫 구현에서는 고정 수치·드롭 테이블·순수 추첨을 `ChestRules.cs`에 모았으며, 별도 `ChestSettings.cs`·`ChestRewardTable.cs` 에셋은 만들지 않는다. 테스트 도구는 `ChestTestContext.cs`·`ChestTestNotice.cs`에 있다.

| 예정 위치 | 책임 |
| --- | --- |
| `Assets/Scripts/Chest/ChestInteractable.cs` | 상자 표시 정보, 유효성 검사, 홀드 완료 요청, 커밋된 개방 상태 반영 |
| `Assets/Scripts/Chest/ChestSettings.cs` | 유지 시간·범위·임시 외형·획득 안내 초기값 |
| `Assets/Scripts/Chest/ChestRewardTable.cs` | 드롭 테이블 버전, 등급 가중치, 카탈로그 풀 검증 |
| `Assets/Scripts/Chest/ChestRules.cs` | 순수 추첨·ID·보상 계획 검증 규칙 |
| `Assets/Scripts/Chest/ChestView.cs` | 닫힘·선택·열림 사각형과 조각 획득 표시. 자산 수정 없음 |
| `Assets/Scripts/Interaction/IHoldInteractable.cs` | 즉시 실행과 구분되는 선택적 홀드 계약 |
| 기존 `InteractionController.cs` | 입력·대상 선택·홀드 세션·취소·완료 1회 디스패치 |
| 기존 `InteractionPrompt.cs` + `SquareHoldProgressGraphic.cs` | 표시 모드와 사각형 둘레 UI 메시. 게임 규칙·타이머 소유 없음 |
| `Assets/Scripts/Chest/ChestModels.cs` | 보상 계획·상태·개방 상세 결과 DTO |
| `Assets/Scripts/Chest/EconomyService.Chests.cs` | 기존 `EconomyService`의 partial. 등록·개방·중복 방지·단일 저장 |
| 기존 `EconomyModels.cs`, `EconomyLedger.cs`, `JsonEconomyStore.cs`, `LobbyRunData.cs` | 스키마·깊은 복사·검증·v1/v2 이전 |
| `Assets/Prefabs/Chest/Chest.prefab` | 사각형 SpriteRenderer·trigger·상자 컴포넌트·드롭 기준점 |
| `Assets/Scenes/ChestTest.unity`, `Assets/Editor/ChestTestProjectSetup.cs` | 격리된 수동 검증 씬과 재생성 도구 |
| `Assets/Tests/EditMode/ChestEditModeTests.cs`, `Assets/Tests/PlayMode/ChestPlayModeTests.cs` | 추첨·저장·홀드 통합 검증 |

기존 `Assets/Art/Boss/UnitSquare.png`와 같은 단색 사각형을 재사용할 수 있다. 새 이미지 생성이나 디자인 에셋 확보를 개발 선행 조건으로 두지 않는다. 실제 스테이지 생성기는 상자 배치 명세를 공급하는 후속 연결 지점으로 남긴다.

## 3. 홀드 입력 계약

### 3.1 선택적 계약과 기존 동작 호환

- `IInteractable`의 기존 즉시 실행 계약은 유지한다.
- `IHoldInteractable`에 유지 시간, 홀드 시작·취소·완료 통지를 정의한다. 완료 통지는 범용 성공/실패 결과를 반환하고 공통 컨트롤러가 상자 보상 타입을 참조하지 않게 한다.
- 상자는 `InteractableBehaviour`를 상속하고 홀드 계약을 추가한다. 기존 상점·재단·분기·출구에는 홀드 계약을 붙이지 않는다.
- 공통 컨트롤러는 홀드 대상의 E Down에서 세션만 시작한다. 즉시 `Interact()`를 호출하지 않는다. 유지 완료 시 홀드 완료 통지를 한 번 호출한다.
- 홀드 완료 경로는 대상·플레이어·세션 번호가 일치하고 완료 조건을 만족한 경우만 수락한다. 상자에 대한 일반 `Interact()` 직접 호출은 미완료 홀드를 건너뛰어 보상을 열 수 없어야 한다.
- 기존 `Interacted`는 void 호출 경로의 통지다. 상자 개방 성공은 저장 이후의 별도 `Opened` 통지로만 알린다. 실패한 저장을 성공 로그·효과로 표시하지 않는다.
- 홀드 중인 대상에는 F 보조 입력을 전달하지 않는다. 기존 즉시 대상의 E/F 동시 입력에서는 기존 E 우선 규칙을 유지한다.

### 3.2 런타임 세션

컨트롤러가 `target`, `interactor`, `sessionId`, `elapsed`, `duration`, `requireKeyRelease`, `completionDispatched`를 소유한다. 상자와 UI에서 별도 누적 타이머를 돌리지 않는다.

1. 홀드가 없을 때 범위 안의 가장 가까운 유효 대상을 선택한다. 후보별 범위를 검사한 뒤 거리순으로 선택해, 범위 밖의 가까운 대상이 범위 안의 다른 대상을 가리지 않게 한다.
2. 거리 동률이면 현재 선택 대상을 우선하고, 처음 선택할 때는 안정적인 대상 키로 결정한다.
3. 유효 대상에서 새로운 E Down을 받으면 세션을 만든다. 세션이 있는 동안 대상은 고정하고 그 대상의 표면 거리를 재검사한다.
4. 프레임에서 관측된 키 해제·취소 조건을 먼저 확인한다. 유효하면 게임 시간 기준 `deltaTime`을 누적한다.
5. `progress = clamp01(elapsed / duration)`를 UI에 전달한다. `duration`은 유한한 양수만 허용한다.
6. 100%에서 세션과 대상 유효성을 재검사하고 완료 통지를 한 번 보낸다. UI가 보낸 완료 콜백으로 보상을 지급하지 않는다.
7. 성공·실패·취소 어느 경우든 세션을 종료하고 `requireKeyRelease`를 세운다. E Up을 관측한 후에만 새 E Down을 받는다.

모달을 닫은 E, 범위 밖에서부터 누른 E, 포커스 복귀 때 눌린 E, 씬 이동 전 E는 새 세션 시작으로 사용하지 않는다. 일시 정지 시에는 시간을 보존하지 않고 세션을 취소한다. `timeScale == 0`과 실제 일시 정지 신호를 모두 처리한다.

### 3.3 전투·이동·생명주기 취소

- 좌우 이동은 허용한다. 점프 입력 또는 비접지, `PlayerAttack.IsAttacking`, `PlayerDash.IsDashing`은 시작·유지를 차단한다.
- 공격·점프·대시의 입력 시작도 같은 프레임에 취소 조건으로 수집해 Update 순서 때문에 개방이 먼저 완료되는 문제를 피한다. 상자 스크립트가 직접 키를 읽는 구조를 만들지 않는다.
- 해당 플레이어의 `DoctrineEventBus` OnHit는 실제 피해 수락 신호로 사용한다. 무적에 거부된 피해는 취소하지 않는다. 피격 직후 회복 교리로 HP가 복구돼도 취소해야 하므로 `HealthChanged`의 순 HP 감소로 추정하지 않는다.
- 접지는 기존 `PlayerMove.IsGrounded`를 조회한다. 상자에서 별도의 접지 판정을 복제하지 않는다.
- 생존·`UiModal.IsOpen`·외부 입력 잠금·운영 씬의 복원/전환/저장 실패 잠금도 검사한다. 홀드를 위해 모달 잠금을 걸어 전투를 멈추게 하지 않는다.
- 대상/플레이어 비활성화·파괴·씬 언로드·창 포커스 상실·ESC에서 구독·세션·진행 UI를 정리한다.
- 홀드 취소와 개방 거래는 메인 스레드에서 직렬 처리한다. 커밋 전에 관측된 취소는 개방을 막고, 커밋 뒤 발생한 사망은 기존 사망 거래가 처리한다.

## 4. 상태 전이

| 현재 | 조건/사건 | 다음 | 처리 |
| --- | --- | --- | --- |
| `Closed` | 범위 내 선택 | `Ready` | E 안내 표시 |
| `Ready` | 유효한 새 E Down | `Holding` | 세션 시작, 진행률 0 |
| `Holding` | 유효한 E 유지 | `Holding` | 진행률 증가 |
| `Holding` | 취소 조건 | `Ready` 또는 `Closed` | 0으로 초기화, 재입력 대기 |
| `Holding` | 시간 충족 및 최종 검사 통과 | `Committing` | 개방 거래 1회 요청 |
| `Committing` | 저장 성공 | `Opened` | 열림 표시·획득 안내·코인 연출 |
| `Committing` | 실패 | `Ready` 또는 `Closed` | 보상·열림 변경 없음, 오류 표시 |
| `Opened` | 재접근/중복 요청 | `Opened` | 입력 후보에서 제외, 중복 효과 없음 |

`Ready`, `Holding`, `Committing`은 런타임 상태다. 저장에는 **닫힘/열림 및 보상 계획·결과**만 남긴다. 저장된 닫힘 상자를 불러오면 부분 홀드 없이 시작한다.

## 5. 사각형 진행 UI 명세

### 5.1 기하와 표시

- 선택한 상자의 `IWorldInteractionPrompt` 앵커를 기존 prompt가 추적한다. 상자 로컬 기준점은 기본 `(0, 1.65, 0)`이며 `ChestInteractable.promptOffset`에서 조정할 수 있다. 동시에 선택 대상 하나만 표시한다.
- `InteractionPrompt`는 카메라 추적 후 LateUpdate에서 월드 위치를 Canvas 좌표로 변환한다. 카메라/대상이 없거나 기준점이 화면 밖이면 숨긴다. 크기는 기존 화면 공간 UI 크기를 유지한다.
- 대기·홀드·완료/실패 피드백은 같은 상자 앵커를 사용한다. 상점·재단·로비 분기·스테이지 출구도 같은 월드 앵커 계약을 구현하며, 콜라이더 윗면에서 `promptGap`(기본 0.8 월드 유닛)만큼 위에 표시한다. 그 밖에 앵커가 없는 대상은 기존 하단 위치로 복귀한다. 일반 안내는 문구 길이에 맞춰 크기를 조정하고, 홀드 키캡 안내는 기존 크기를 유지한다.
- 키캡은 정사각형이며 진행선의 중심 경로 한 변을 `L`이라고 한다. 둘레는 `4L`, 그릴 길이는 `4L × progress`다.
- 경로는 `위 중앙 → 우상단 → 우하단 → 좌하단 → 좌상단 → 위 중앙`이다. 구간 길이는 각각 `L/2, L, L, L, L/2`다.
- 지나간 구간은 완전히 그리고 현재 구간만 길이에 비례해 그린다. 선 두께를 포함한 코너는 겹침·틈이 보이지 않게 연결한다.
- 진행용 `Graphic` 메시 또는 동등한 직선 분할 방식으로 구현한다. 원형 Radial360 채우기를 그대로 쓰지 않는다.
- `progress == 0`은 밝은 선이 없고, `progress == 1`은 끊김 없는 닫힌 사각 외곽선이다. E 글자는 선에 가려지지 않는다.
- `raycastTarget`은 끈다. UI는 유지 시간 계산·보상 지급·상자 상태 변경을 수행하지 않는다.

### 5.2 표시 상태

`SetHoldPrompt(key, label, progress, state)`와 같은 표시 API를 제안한다. 기존 텍스트 전용 `SetMessage` 경로는 유지한다. 대상을 변경·숨김·씬 로드할 때 진행률과 완료 강조를 반드시 초기화한다.

기준 1920×1080, 키캡 44×44, 기본선 2, 진행선 3을 초기값으로 한다. `CanvasScaler`와 기존 글꼴을 사용한다. 완성 강조 약 0.1초는 **커밋 성공 후** 표시하며, 실패하면 오류 안내를 보여준다. 코인·모달 Canvas 정렬과 충돌하지 않게 기존 UI 순서를 따른다.

## 6. 보상 계획과 추첨

### 6.1 순수 추첨 규칙

주입 가능한 난수원과 버전이 고정된 추첨 절차를 사용한다. V1은 다음 순서다.

1. 정수 `r = Next(0, 100)` 1회: `0..64` 일반, `65..89` 고급, `90..99` 희귀.
2. 해당 등급의 조각 풀을 안정적인 `ShopEntry.Id` 순서로 정렬한 뒤 `Next(0, pool.Count)`로 1개 선택.
3. 별도 호출 `Next(7, 18)`로 코인 추첨. 조각의 등급·종류에 따라 코인 범위를 바꾸지 않는다.
4. 조각 수량은 V1에서 항상 1이다.

카탈로그 항목을 12개 전체에서 균등 추첨하면 요청한 등급 확률과 달라지므로 금지한다. `ShopRarity`를 재사용하고, 가격에서 등급을 역추론하거나 상자 전용 조각 ID를 만들지 않는다.

테이블 검증은 가중치 65/25/10·합계 100, 각 등급의 비어 있지 않은 풀, 유효하고 중복 없는 ID, 코인 범위 7..17, 조각 수량 1을 확인한다. 잘못된 설정은 보상 생략·다른 등급 대체 대신 상자 활성화를 차단하고 진단한다.

### 6.2 보상 계획 확정 시점

저장 실패·재로드로 재추첨되지 않도록 **스테이지 상자 명세를 처음 등록할 때 숨겨진 보상 계획까지 함께 저장**하는 안을 사용한다. 이것은 지급이 아니며 자산은 개방 거래에서만 변경한다.

- 입력 명세: `stageInstanceId`, `layoutVersion`, `rewardTableVersion`, 각 상자의 `spawnId`.
- `spawnId`는 배치 시 직렬화한 고유 키다. 이름·위치·계층 인덱스·`GetInstanceID()`·씬 로드 때 새 GUID를 사용하지 않는다.
- 생성형 배치는 스테이지 생성 데이터에 ID를 저장한 뒤 등록한다. 같은 씬을 다른 스테이지 인스턴스에서 사용하면 다른 상자가 된다.
- 첫 등록 저장 이전의 재시도도 같은 결과가 되도록, 안정적인 스테이지 ID·spawnId·테이블 버전의 해시를 시드로 사용한다. 프로세스마다 달라질 수 있는 문자열 `GetHashCode()`는 사용하지 않는다. 시드 해석·난수 알고리즘·추첨 순서를 버전별로 고정하고 테스트 벡터를 둔다.
- 등록 저장이 실패하면 해당 상자들은 상호작용 불가 상태로 두고 같은 등록을 재시도한다.
- 등록 이후는 저장된 실제 계획을 읽는다. 보유 여부·상한·취소·키 재입력 때문에 계획을 다시 뽑지 않는다.
- 보상 계획은 UI·외형에 미리 공개하지 않는다. 일반 개발 로그에서도 미개방 내용은 기본 숨김으로 둔다.

## 7. 저장 데이터와 API 계약

### 7.1 데이터 제안

| 저장 위치 | 제안 필드 |
| --- | --- |
| `TemporaryStageData` | `hasChestState`, `chestState` |
| `ChestStageState` | `stageInstanceId`, `layoutVersion`, `rewardTableVersion`, `manifestHash`, `records[]` |
| `ChestRecord` | `spawnId`, `pieceId`, `rarity`, `pieceAmount`, `rolledCoins`, `opened`, `openTransactionId`, `appliedCoins` |
| 기존 `EconomyReceipt` 추가 영역 | `hasChestReward`, `chestReward` |
| `ChestRewardReceipt` | `stageInstanceId`, `spawnId`, `pieceId`, `rarity`, `pieceAmount`, `rolledCoins`, `appliedCoins`, `grantId` |

Unity JSON의 인라인 null 처리를 고려해 `hasChestState`·`hasChestReward`로 존재 여부를 구분하고 로드·깊은 복사 시 정규화한다. 상세 영수증은 기존 영수증과 **같은 ID**에 붙이며 별도 지급 거래를 추가하지 않는다.

열린 상자 기록은 실제 지급 결과를 보존한다. 현재 스테이지를 떠나 배치 상태를 정리해도 기존 영수증은 유지해 과거 개방 요청을 다시 지급하지 않게 한다. 오래된 영수증 정리 정책은 기존 재화 시스템 정책을 따르며 이번 기능에서 임의 삭제하지 않는다.

### 7.2 제안 API

| API | 계약 |
| --- | --- |
| `TryRegisterStageChests(manifest)` | 현재 유효 스테이지의 배치·보상 계획을 한 번 저장. 같은 ID·같은 명세는 최초 결과, 다른 명세는 충돌 |
| `TryOpenChest(stageInstanceId, spawnId, worldPosition, sourceCamera)` | 저장된 상자를 조회해 조각·코인 예약·열림·영수증을 단일 거래로 커밋하고 상세 결과 반환 |
| `GetChestState(stageInstanceId, spawnId)` | 저장 상태의 읽기 전용 복사. 뷰가 목록을 직접 변경할 수 없음 |
| `ChestOpenResult` | 성공 여부·오류·거래 ID·조각 ID/등급/수량·추첨 코인·실제 코인·`IsReplay` |

등록 ID는 `{stageInstanceId}/chests/register`, 개방 ID는 `{stageInstanceId}/chest:{spawnId}/open`처럼 서비스에서 정규 생성한다. 호출자가 임의의 ID나 조각·코인 수량을 넣어 동일 상자를 다시 지급할 수 없게 한다. 위치·카메라는 연출 정보이며 보상 내용의 원본이 아니다.

같은 거래 ID·같은 요청은 최초 결과를 반환하고 `IsReplay = true`로 표시한다. 다른 요청에는 `InvalidTransaction`을 반환한다. `IsReplay`는 현재 호출에 대한 정보이며 최초 영수증의 보상 결과를 수정하지 않는다. 중복 요청에서는 알림·연출을 다시 발생시키지 않는다.

### 7.3 개방 거래 순서

1. 재진입 잠금을 확인하고 정규 거래 ID의 기존 영수증을 조회한다. 이미 처리한 요청이면 상태·사망 여부와 무관하게 최초 결과만 반환한다.
2. 신규 요청은 프로필 준비, 현재 스테이지 일치, 등록된 상자 존재, 미개방 상태, 사망/복원/보상 차단 여부를 확인한다. 범위·홀드·현재 플레이어 검사는 상호작용 계층에서도 완료 직전에 수행한다.
3. 기존 프로필의 깊은 복사본에서 저장된 보상 계획을 조회·검증한다.
4. 선택 조각의 보유량+1이 9,999를 넘으면 `ItemLimit`으로 전체 실패한다. 다른 조각 재추첨 없음.
5. `appliedCoins = min(rolledCoins, MaxCoins - balance - pendingTotal)`을 계산한다. 0도 정상 개방 결과다.
6. 조각 +1, 상자 `opened=true`, 적용 금액·거래 ID 기록, 적용 코인이 양수면 기존 `CreateGrant` 규칙의 대기 보상 추가를 **같은 복사본**에서 수행한다.
7. 기존 `EconomyReceipt.result`에는 코인의 RequestedAmount/AppliedAmount를 기록해 기존 대기 토큰 합계 검증을 만족시키고, 조각 상세 정보는 `chestReward`에 기록한다.
8. revision·영수증을 포함해 기존 검증→저장 경로를 한 번 통과시킨다. 실패하면 복사본을 폐기한다.
9. 성공 이후에만 프로필 게시·자산 변경 알림·상자 열림 통지·기존 `FlightRequested` 연출 요청을 발행한다. 이벤트 중 재진입은 기존 거래 잠금으로 거부한다.

partial은 기존 private 거래·예약 헬퍼를 공유할 수 있으나, 상세 영수증을 **저장 전**에 작성할 수 있도록 공통 거래 헬퍼를 최소 확장한다. 저장 성공 후 추가 저장으로 조각 정보나 열림 플래그를 붙이는 구현은 금지한다.

`Busy`, `NotReady`, `SaveFailed`, `InvalidTransaction`, `StateConflict`, `InvalidItem`, `ItemLimit`, `PlayerDead`를 기존 오류와 일관되게 매핑한다. 예상 가능한 실패는 성공 표시 없이 안내하고 새 E 입력으로 재시도한다. 상자 저장 실패 때문에 다른 거래의 원장을 직접 복원하거나 덮어쓰지 않는다.

### 7.4 저장 검증과 버전 이전

- 첫 구현에서 v2를 **v3으로 확장**했다. 후속 변경 시에는 최신 스키마를 다시 확인한다.
- v1→현행 이전에 이어 v2→v3 이전을 순서대로 적용한다. 현재 런·코인·조각·사망 저널·비행 예약·기존 영수증을 유지한다.
- 이전 데이터의 `hasChestState`·`hasChestReward`는 false로 시작한다. 기존 임시 스테이지에는 상자가 없었던 것으로 취급하고, 상자가 실제 배치된 씬을 연결할 때만 등록한다.
- 이전 과정에서 초기 자산 지급·원장 초기화·자동 개방을 수행하지 않는다. 저장 파일명과 경로는 현행을 유지한다.
- 검증 시 stageInstanceId 일치, spawnId 유일성, 조각 ID와 등급 대응, 조각 수량 1, 코인 7..17, 적용 코인 범위, 개방 기록과 영수증의 일치 여부를 확인한다.
- 현재 열린 상자의 영수증은 조각이 이후 사망 손실로 사라지거나 코인이 소비됐어도 유효하다. 현재 잔액·보유량이 과거 지급량 이상이어야 한다는 검증을 넣지 않는다.
- 기존 토큰의 `grantId`/`tokenId`·금액 합계·정산 상태 검증을 그대로 유지한다. 코인이 모두 도착해 pending에서 사라져도 과거 개방은 유효하다.
- 손상·미래 버전은 기존 보존·잠금 규칙을 따른다. 명세 또는 테이블 버전이 달라진 저장을 조용히 재추첨·덮어쓰기하지 않는다. 저장된 계획은 해당 버전으로 검증한다.

## 8. 연출·정산·씬 연결

- 상자 뷰는 커밋된 데이터만 읽어 닫힘/열림을 반영한다. 개방 요청 단계에서 먼저 상자를 없애지 않는다.
- 신규 성공의 조각 획득 안내는 표시만 담당한다. `DoctrineInventory.AddEntry`나 `TryGrantPiece`를 효과 콜백에서 다시 호출하지 않는다.
- 코인은 단일 개방 거래에서 생성한 대기 예약을 기존 `CoinFlightView`로 보낸다. 위치는 프리팹의 드롭 기준점이다.
- 최대 8토큰 묶음, 기존 동시 표시 상한·풀·화면 밖 위치 보정·실제 HUD 목표 추적을 유지한다.
- 카메라/HUD 부재·씬 전환·종료·재로드 정산은 기존 재화 런타임을 사용한다. 도착 이벤트에서 추가 `TryGrant`를 호출하지 않는다.
- 개방 뒤 사망하면 조각과 예약 코인은 기존 전체 해제·30% 손실 흐름에 포함한다. 상자용 사망 손실 경로를 새로 만들지 않는다.
- 실제 런에서는 현재 `TemporaryStageData.id`를 사용한다. 스테이지를 다시 로드해도 같은 spawnId로 저장 상태를 붙인다.
- 격리된 ChestTest는 메모리 저장소와 테스트용 런/스테이지 컨텍스트를 준비한다. 운영 씬의 `LobbyFlowRuntime.InputReady`가 없다는 이유로 테스트가 불가능하거나, 반대로 운영 입력 잠금을 우회하지 않도록 컨텍스트를 구분한다.
- 테스트 씬 진입·이탈 시 기존 전역 서비스 바인딩과 이벤트를 안전하게 정리한다. 실제 사용자 저장 파일·프로필은 사용하지 않는다.

## 9. 검증 계획과 인수 조건

아래는 **향후 개발 시 수행할 검증 계획**이다. 현재 통과한 테스트 결과를 뜻하지 않는다.

### 9.1 EditMode

| 검증 | 기준 |
| --- | --- |
| 등급 경계 | 난수 0·64=일반, 65·89=고급, 90·99=희귀. 0..99 전수 입력 시 65/25/10 |
| 등급 내 선택 | 각 등급의 모든 인덱스가 도달 가능, 카탈로그 순서가 달라도 ID 정렬 기준 결과 동일 |
| 코인 범위 | 7·17 포함, Next 상한 18, 등급에 따라 범위 변화 없음 |
| 테이블 오류 | 빈 풀·중복/미등록 ID·잘못된 확률·잘못된 범위는 등록 차단 |
| 재시도 계획 | 같은 ID·버전·테스트 벡터는 프로세스 재생성 및 등록 저장 실패 후 동일 계획 |
| 단일 개방 | 열림+조각+코인 예약+상세 영수증이 한 저장에서 반영 |
| 저장 실패 | 열림·자산·예약·영수증이 전부 변경되지 않고 저장 성공 후 1회만 적용 |
| 중복·충돌 | 같은 ID 재호출/재로드 후 추가 지급·추첨·연출 없음, 변조된 요청은 거부 |
| 조각 상한 | 9,998→9,999 성공, 9,999에서 전체 실패·코인 없음·계획 유지 |
| 코인 상한 | 보유+대기 합계 기준 부분 접수·0 접수, 조각과 열림 정상 처리 |
| 저장 이전 | v1/v2 자산·런·저널·토큰 유지, 기존 상세 영수증 미존재 정규화, 미래/손상 데이터 보존 |
| 열린 기록 검증 | 토큰 정산·코인 소비·사망 조각 손실 후에도 정상 로드 |
| 진행선 기하 | 0·12.5·25·37.5·50·62.5·75·87.5·100% 끝점과 전체 길이 일치 |

확률 검증은 고정 입력·경계·전수 분류로 수행한다. 대량 무작위 표본이 우연히 비율에서 벗어난다는 이유로 테스트가 흔들리지 않게 한다.

### 9.2 PlayMode 및 수동 검증

| 상황 | 인수 기준 |
| --- | --- |
| 일반 접근 | 표면 거리 경계에서 안내 표시, 겹침만으로 개방 없음 |
| 시간 경계 | 1.5초 미만 미개방, 유효 유지 1.5초 충족 후 1회 개방. 프레임당 1회 판정 오차 범위 |
| 조기 해제·재유지 | 99% 해제 후 0부터 시작, 진행률 이어받기 없음 |
| 사전 키 유지 | 미리 E를 누르고 접근·모달 닫기·포커스 복귀·씬 이동 후 자동 개방 없음 |
| 여러 대상 | 선택된 상자 하나만 진행, 더 가까운 대상 등장에도 진행 이전 없음, 완료 후 다음 대상 자동 실행 없음 |
| 전투 취소 | 수락된 피격·피격 후 즉시 회복은 취소, 무적 거부 피해는 유지. 공격/점프/대시와 동시 완료 충돌 검증 |
| 생명주기 | 범위 이탈·ESC·모달·일시 정지·포커스 상실·파괴·전환에서 세션·UI·이벤트 정리 |
| 기존 상호작용 | 상점/재단/분기/출구 E 즉시 실행·F 보조·모달 E/ESC 닫기 회귀 없음 |
| 코인 비행 | 상자에서 퍼짐 후 실제 HUD로 이동, 표시 토큰의 합계=적용 코인, 도착 때만 소비 가능 잔액 증가 |
| 코인 연출 중 중단 | HUD 부재·씬 전환·종료·사망·늦은 콜백에서 누락/중복 정산 없음 |
| 재접속 | 열린 상태·조각·코인 예약/정산 결과 유지, 닫힌 상자의 보상 계획 유지 |
| 조각 표시 | 등급·이름·×1 안내, 원장 추가 1회, 자동 장착 없음 |
| 화면 | 16:9·16:10·4:3에서 사각 키캡·E·테두리 모서리·문구·HUD 겹침 확인 |

ChestTest에는 최소 3개 상자, 인접한 즉시 상호작용 대상, 피해·모달·저장 실패·재로드·상한 데이터 준비 도구를 제안한다. 강제 등급/보상 시드는 테스트 컨텍스트에만 둔다.

변경 범위의 자동 검증과 기존 InteractionRange·Economy·Lobby·StageIntegration 회귀 검증을 수행한다. 실제 실행 결과와 기존 코드에서도 재현되는 실패는 [구현 결과서](ChestSystem_ImplementationStatus.md)에 기록한다. 유지 조작감은 [테스트 가이드](ChestSystem_TestGuide.md)에 따라 직접 플레이로 확인한다.

## 10. 향후 개발 순서

1. 상세기획의 유지 시간·취소 조건·조각 1개·등급 내 균등·UI 위치 제안 검토 및 문서 반영.
2. 안정적인 ID·순수 추첨·배치/보상 계획 저장·스키마 이전·단일 개방 거래와 핵심 저장 검증.
3. 공통 홀드 계약·세션·취소·재입력 처리 및 기존 즉시 상호작용 호환.
4. 사각 외곽선 UI·임시 상자·조각 획득 안내·기존 코인 연출 연결.
5. 격리된 테스트 씬에서 검증하고 결과·남은 수동 확인 항목 기록.
6. 실제 스테이지별 상자 수·위치가 정해지면 배치 생성기에 연결.

사용자 개발 지시에 따라 격리된 `ChestTest` 씬 범위부터 구현한다. 실제 스테이지별 상자 배치 연결은 후속 범위다.
