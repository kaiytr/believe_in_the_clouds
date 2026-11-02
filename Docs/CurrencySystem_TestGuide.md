# 재화 시스템 테스트

Unity **6000.3.5f2**에서 `Assets/Scenes/Tests/EconomyTest.unity`를 열고 Play한다. 씬을 다시 만들려면 `Tools > Economy > Build independent test scene`을 실행한다.

먼저 왼쪽 패널의 `새 빈 테스트 프로필`로 0코인·0조각·장착 없음 상태를 준비한다. 다음을 확인한다.

- `+100 코인`: 공통 즉시 지급 API. 우측 하단 잔액이 갱신된다.
- `개/신도/간부 처치`: 지정 범위의 코인이 퍼진 뒤 HUD로 날아간다. 도착 전에는 비행 중 금액이며 사용할 수 없다.
- `보스 처치`: 0코인. 실제 보스도 재화 보상을 지급하지 않는다.
- `상점 열기`: 12종 가격·등급, 보유량·합계·부족액을 표시한다. 카드당 한 장씩 선택하고 결제 후 재구매할 수 있다.
- `13조각 준비·2개 장착`: **테스트 프로필을 비운 뒤** 미사용 7조각과 장착에 참조된 6조각을 준비한다. 실제 신규 지급 규칙과 구분한 테스트 자료다.
- `사망`: 사망 교리 효과 → 전체 해제 → 13개 중 3조각 손실 → 10조각·장착 0개를 확인한다. 코인은 비행 중 금액을 합친 총액의 30%를 버린다.
- `저장 실패 ON/OFF`: 결제·도착·사망 저장 실패를 주입한다. 자산은 유지되고 사망 처리는 잠긴다. OFF로 돌리면 사망/도착 저장을 재시도한다.
- `부활`: 사망 완료 뒤 새로운 생명 ID로 다시 테스트한다. 손실한 조합을 자동 장착하지 않는다.
- `저장 다시 불러오기`: 경제 프로필만 복원하고 대기 코인을 한 번 정산한다. 테스트 장착은 해제한다.
- `새 빈 테스트 프로필`: 0자산 메모리 프로필로 전환한다. 이후 다시 불러오기는 해당 메모리 저장의 복원이다.

기본 테스트 파일은 `Library/Economy/Fixtures/currency-test-v1.json`이다. 테스트 프로필 초기화 후에는 메모리 저장을 사용하므로 앱 재실행으로 해당 메모리 프로필을 이어 하지는 않는다. 자동 테스트는 메모리 또는 `Library/Economy/UnitTests/` 아래 임시 파일을 사용한다.

기존 `StageTest`·`BeliefTest`에서도 실제 일반 적 사망 보상이 연결되어 있다. `R` 씬 재시작은 사망 손실을 추가로 실행하지 않는다. 씬이 다시 생성한 적은 새 처치 ID를 갖는다. 적 생사 전체 저장과 안정적인 스테이지/스폰 ID 연결은 후속 메인 루프 범위다.

일반 Editor Play는 기본적으로 메모리 저장이다. `-usePersistentEditorProfile`을 명시한 Editor의 지속 저장은 `Library/Economy/Editor/economy-save-v1.json`, 빌드 저장은 `Application.persistentDataPath/economy-save-v1.json`을 사용한다. EconomyTest 자체의 fixture 저장은 위 별도 경로를 사용한다. 손상본·미지원 버전은 거래를 잠그고 보존하며 정상 백업이 있으면 복구한다.

자동 테스트는 `EconomyEditModeTests`, `EconomyPlayModeTests`에 있다. 화면 캡처는 그래픽을 켠 `VisualCaptureCurrencyHudFlightShopAndDeathAtTwoAspectRatios`가 `Logs/EconomyVerification/`에 저장한다. 전체 실행 결과는 [구현 결과서](CurrencySystem_ImplementationStatus.md)에 기록한다.
