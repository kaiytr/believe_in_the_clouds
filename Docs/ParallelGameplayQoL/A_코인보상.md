# 세션 A — 상자·몬스터 코인 보상

- 작업 브랜치: `codex/qol-coin-drops`
- 공통 기준: `de63db8161ce9b97078e561be02d9488139dd607`
- 원본 저장소: `/Users/ohjunmin/Desktop/Github/believe_in_the_clouds`
- 전용 worktree: `/private/tmp/believe-clouds-qol-coin-drops`
- 원본 시작 상태: `Puri`, HEAD는 위 기준과 동일. 추적 파일 변경·스테이징 없음. 미추적 `Assets/Design/Doctrine/교리 1.png`와 `.meta` 보존 대상.
- 저장소 및 상위 경로에 적용할 `AGENTS.md`는 없었다.
- 커밋 전 원본 상태 대조: HEAD·브랜치·작업 파일·스테이징·미추적 두 파일의 내용이 시작 기록과 모두 일치했다.

## 구현과 변경 파일

- `Assets/Scripts/Economy/EconomySettings.cs`: 기존 추첨 후 2를 곱한다. 개 10/12/14, 신자 6/8/10, 간부 14/16. 보스 0 유지.
- `Assets/Scripts/Chest/ChestRules.cs`: 기본 테이블 V2. 기존 7~17 추첨값 ×2로 14~34의 짝수만 생성한다. V1 시드·난수 호출 순서·조각 풀·등급·조각 1개를 유지한다. V1/V2별 금액 검증과 계획·영수증 버전 대조를 추가했다.
- `Assets/Scripts/Chest/ChestModels.cs`: 새 manifest 기본 버전은 V2. 영수증에 `rewardTableVersion` 추가. 필드가 없던 과거 JSON의 0은 V1로 해석한다.
- `Assets/Scripts/Chest/EconomyService.Chests.cs`: manifest의 버전으로 계획을 생성하고, 열 때 저장된 계획의 버전을 영수증에 기록한다.
- `Assets/Tests/EditMode/EconomyEditModeTests.cs`, `ChestEditModeTests.cs`: 바뀐 기대값, V1/V2 고정 추첨 벡터, 저장 호환·중복 지급 회귀 케이스.

## 저장 호환 정책

- 프로필 스키마는 4 유지. 저장된 V1 계획은 닫힌 상자까지 금액·조각·해시를 보존하며 재추첨하지 않는다. V1 계획을 나중에 열어도 V1 금액을 지급한다.
- 새 스테이지의 `new ChestManifest`는 V2가 된다. `DesignedLevelChests`는 기존 `hasChestState`를 재등록하지 않으므로 추가 연결 작업이 없다.
- 과거 영수증의 버전 0/1은 7~17, 새 버전 2는 짝수 14~34로 검증한다. 현재 상자 상태의 테이블 버전과 영수증 버전도 일치해야 한다. 과거·현재 영수증은 한 프로필에 함께 남을 수 있다.
- 명시적 V1 manifest도 재시도 호환용으로 지원한다. 기존 등록의 재호출은 원래 버전·배치를 사용한다. 다른 버전으로 기존 계획을 덮어쓰지 않는다.
- 처치 영수증 재조회는 기존 거래 ID로 원래 결과를 반환한다. 실패 재시도 캐시는 이미 두 배가 된 최종 금액을 재사용한다. `PendingGrant`/토큰 정산에 배율을 추가하지 않아 과거 비행 중 지급액도 원래 값으로 정산된다.
- `JsonEconomyStore`의 로드 실패 보호와 백업 정책은 유지한다. 구버전 저장을 새 0코인 프로필로 교체하는 경로를 추가하지 않았다.

## 조사 범위와 통합

- 실제 드롭 경로는 `EnemyUnit → EconomyRuntime/서비스 → RollDrop` 및 상자 등록·열기다. 보스 코드는 처치 통계만 기록하며 별도 코인 보상을 만들지 않는다. 일반 코인 지급 API 호출은 개발 도구·테스트 초기 지급이어서 배율을 적용하지 않았다.
- 상점 가격, 보유 한도, 사망 페널티, 개발 도구 지급 버튼, 이펙트 토큰 개수 설정은 변경하지 않았다.
- C는 이 브랜치를 최신 Puri에 병합하면 된다. 소유 범위 밖 연결 변경이나 새 패키지는 필요 없다. B/C 브랜치와 기획 브랜치는 가져오거나 변경하지 않았다.

## 확인 결과와 남은 사항

- 정적 호출·저장·지급 경로 조사 및 `git diff --check` 통과.
- 독립 .NET 9 환경에서 관련 도메인 코드와 선택한 테스트 본문 컴파일: 경고 0, 오류 0. 지급 구간 전체, 처치 재시도·중복 콜백, 과거 비행 중 금액, V1/V2 고정 벡터, 상자 등록/열기 실패 재시도, V1 JSON에서 V2 새 스테이지 전환, 버전·짝수 검증, 보유 한도, 이벤트 재진입 등 16개 확인 통과.
- 위 확인은 실제 C# 소스를 사용하되 Unity 타입·`JsonUtility`·assertion에 독립 호스트 어댑터를 사용했다. Unity 네이티브 직렬화 및 NUnit 실행 결과로 간주하지 않는다. 임시 확인 산출물은 `/private/tmp/believe-clouds-session-a-check`에 격리했으며 저장소에 포함하지 않았다.
- Unity EditMode/PlayMode 테스트와 전체 빌드는 미실행. 새 Unity Library 생성·씬 재생성도 하지 않았다. C가 준비된 환경에서 통합 컴파일을 담당한다.
- Unity에서 추가 확인할 핵심 케이스: `LegacyChestJsonKeepsPlansAndReceiptsWhenNewStagesUseDoubledRewards`, `LegacyDefeatReceiptAndPendingCoinsAreNotDoubledOnLoadOrReplay`, `CurrentChestValidationRejectsOddCoinsAndMismatchedReceiptVersions`.
