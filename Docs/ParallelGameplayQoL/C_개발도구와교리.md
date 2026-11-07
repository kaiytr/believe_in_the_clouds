# C 세션 개발 도구·교리 제목 인계

작성일: 2026-11-07. 브랜치: `codex/qol-devtools-ui`.
공통 기준 커밋: `de63db8161ce9b97078e561be02d9488139dd607`.
완료 커밋 SHA는 최종 세션 보고와 해당 브랜치 tip을 기준으로 한다.

## 구현 내용

- F2 개발 도구에 최대 체력 회복, 보스 해금 조건 즉시 달성, 로비 분기 리롤을 추가했다. 현재 체력·지역·전투 클리어 수·보스 해금 상태와 처리 결과를 표시한다.
- 회복은 기존 `PlayerHealth.Heal`을 호출한다. 생명 ID, 사망 상태, 대시/피격 무적을 초기화하지 않는다. 사망 결과·저장된 사망 처리·씬 전환 중에는 조작을 제한한다.
- 보스 해금은 현재 런의 클리어 수를 `max(현재 값, LobbyRules.BossUnlockClears)`로 저장한다. 현재 기준은 3이다. 보스를 강제로 후보에 추가하지 않으며 기존 후보도 자동으로 바꾸지 않는다.
- 분기 리롤은 정상 로비에서만 사용한다. 후보 묶음·후보 ID·공개 상태를 새로 저장한 뒤 기존 Lobby 라우팅으로 씬을 다시 불러온다. 다른 UI가 열린 상태에서 개발 도구를 열었다면 리롤을 막고 다른 창을 닫은 뒤 F2를 다시 열도록 안내한다.
- 런/생명 ID, 클리어 수, 방문 수, 현재 HP/최대 HP·위치, 자산, 대기 코인, 교리 장착은 유지한다. 원래 후보 생성 규칙과 보스 추첨 규칙을 그대로 사용한다.
- 리롤 요청 ID는 원본 런·후보 묶음에서 결정적으로 만든다. 저장 실패 시 현재 상태를 유지하고 같은 요청·추첨으로 재시도한다. 서비스 재개/프로세스 재시작에도 같은 원본 묶음은 같은 요청으로 연결된다. 원본 묶음이나 클리어 수가 바뀐 낡은 요청은 거부한다.
- 클릭한 교리 설명 창의 제목은 `교리1`, `교리2`처럼 번호만 표시한다. 슬롯 내부 이름·설명 본문·효과 수치는 유지했다.
- 개발 도구 프리팹을 세로로 확장하고 버튼 3개와 상태/결과 텍스트를 연결했다. 루트 축척을 1로 지정하고 모든 Image/Text에 CanvasRenderer를 연결했다.

## 변경 파일과 다른 세션과의 경계

- `Assets/Scripts/Debug/DevToolsPanel.cs`
- `Assets/Scripts/Debug/DevToolsRefs.cs`
- `Assets/Resources/UI/DevTools.prefab`
- `Assets/Scripts/Lobby/LobbyFlowRuntime.cs`
- `Assets/Scripts/UI/DoctrineHud.cs`
- 신규 `Assets/Scripts/Economy/EconomyService.DeveloperTools.cs`와 `.meta`
- 이 인계 문서

기존 EconomyService·EconomyModels·JsonEconomyStore·Chest 코드, PlayerHealth·PlayerDash·Enemy·Boss 코드, 공통 씬/프로젝트 설정은 수정하지 않았다. 저장 스키마와 LobbyRunData 필드도 추가하지 않았다. A/B의 코드와 공통 부분을 병렬로 수정하지 않는다.

신규 서비스 API:

```csharp
CurrencyResult TryUnlockBossForDevelopment();
CurrencyResult TryRerollLobbyForDevelopment(string requestId, string expectedBatchId,
    int expectedCombatClears, float hp, Vector2 position);
```

기존 partial 클래스의 `Execute`, `RequestKey`, `Success`, `Fail`을 재사용한다. A 세션에서 이 공통 메서드나 `LobbyRules.GenerateCandidates` 계약을 바꾸면 통합 시 확인해야 한다.

## 검증 결과와 범위

- 기존 Unity 6000.3.5f2 컴파일 응답 파일·참조 어셈블리와 Roslyn으로 수정된 런타임 스크립트를 컴파일했다. Unity 실행, 워크트리의 Library 생성, 새 임포트 없이 통과했다.
- 실제 서비스·모델·거래·저장 검증 코드를 사용하는 관리형 하네스에서 아래 7개 검증을 통과했다. **이 하네스의 JsonUtility는 네이티브 Unity JSON 대신 공개 필드를 복사하는 관리형 직렬화 대체 구현이다. Unity Editor/PlayMode 실행 결과는 아니다.**
  1. 보스 해금의 런/자산/후보 보존과 중복 요청 멱등성.
  2. 이미 높은 클리어 수를 낮추지 않음.
  3. 리롤 진행값 보존·새 후보 ID·공개 초기화·중복 리롤 방지·새 후보 공개 시 정상 결제.
  4. 리롤 저장 실패 원자성·서비스 재개 후 동일 요청의 동일 추첨.
  5. 낡은 후보 요청과 미완료 스테이지에서의 리롤 거부.
  6. 보스 해금 저장 실패 시 부분 적용 없음·정상 재시도.
  7. 리롤에서 비행 중 대기 코인 예약 보존.
- 프리팹 85개 오브젝트의 ID·내부 참조, 그래픽 18개와 CanvasRenderer 연결, 패널 직계 컨트롤 10개의 영역·겹침을 정적으로 검사했다.
- `git diff --check` 통과. 전체 EditMode/PlayMode 테스트와 전체 빌드는 실행하지 않았다.
- 실제 Unity 화면에서 F2 패널 클릭·로비 재로드·교리 설명 표시를 직접 확인하지 않았다. 최종 통합 시 이 세 항목은 짧게 확인할 필요가 있다.

검증 스크립트·컴파일 산출물은 저장소 밖의 이 기기 작업 디렉터리 `C:/Users/PC/.codex/visualizations/2026/11/06/01a1b1a3-624f-7e01-8d44-ca2563fede9d/c-validation`에 두었고 게임 변경에는 포함하지 않았다.

## 브랜치·워크트리 상태와 후속 통합

- 원본 저장소: `C:/Users/PC/Documents/GitHub/believe_in_the_clouds` — Puri 원본 커밋·작업 파일을 유지한다.
- C 워크트리: `C:/Users/PC/.codex/visualizations/2026/11/06/01a1b1a3-624f-7e01-8d44-ca2563fede9d/qol-devtools-ui-worktree`.
- A/B 완료 브랜치와 최종 SHA 전달 전에는 Puri에 병합하지 않는다.
- 전달받으면 최신 origin/Puri에서 통합 브랜치를 만들고 A → B → C 순서로 한글 메시지의 일반 병합을 수행한다. 정상 푸시·조상 관계 확인 뒤 이번 작업의 브랜치·워크트리만 정리한다.
- 다른 기기의 worktree는 해당 기기에서 정리해야 한다. 기존 `codex/first-altar-story-spec`와 다른 기존 브랜치는 정리 대상이 아니다.
