# 코인·일반 적 / 보스 패턴 통합 검증

검증일: 2026-11-02 (Asia/Seoul)

## Git 통합

- 원본: `C:/Users/PC/Documents/GitHub/believe_in_the_clouds`, 대상 브랜치 `Puri`.
- 병합 전 Puri: `8964c91ecd2366f8011ca1ce99b1c18e55c80249`.
- 세션 A: `origin/codex/coins-enemies`, 완료 커밋 `4d63faa6e3544d00f693a66577880533f11ada14`.
- 세션 B: 로컬 `codex/boss-patterns`, 완료 커밋 `8964c91ecd2366f8011ca1ce99b1c18e55c80249`.
- `git fetch origin` 후 A의 완료 커밋이 원격 브랜치 tip에 포함된 것을 확인했다. A의 로컬 worktree는 필요하지 않다.
- A의 부모와 B의 생성 기준은 모두 `ac9dc7f0a8e80085ed4ef329b12050ce6ca19343`이다. A에는 이후 B의 `f49d124`, `8964c91`이 없었으나, 기준 코드의 차이는 없다.
- 공통 기준 대비 A는 13개 파일, B는 16개 파일을 변경했다. 변경 경로의 교집합은 없다.
- `git merge --no-ff origin/codex/coins-enemies`로 `bb4308797b5c4a2e00a39038d73c91d480b5a799`을 생성했다. 충돌은 없었다.
- 이어 `git merge --no-ff codex/boss-patterns`를 실행했다. B는 이미 Puri에 포함되어 `Already up to date`였으며 중복 병합 커밋을 만들지 않았다.
- A와 B 완료 커밋 모두 최종 통합 이력의 조상임을 `git merge-base --is-ancestor`로 확인했다.
- B worktree에 기존 `ProjectSettings/ProjectSettings.asset` 미커밋 변경이 있다. 다른 worktree의 파일과 상태는 변경하지 않았다.
- 원격 push 및 작업 브랜치/worktree 삭제는 실행하지 않았다.

## 설정과 연결 검토

- 코인: 보상 기록은 사망 즉시 처리하며 시각 연출만 사망 모션 후 표시한다. 기본 바닥 체류 0.4초, 토큰 지름 16 → 24이다.
- 간부: EnemyTest, BeliefTest, ExecutiveCombatTest, StageTest의 직렬화된 이동속도 비율은 1 → 0.5이다. 개·신도·보스 이동속도는 유지한다.
- StageTest는 기존 보스 설정 에셋 및 흰/검은 구름 프리팹을 사용한다. 구름 대피 시간 3초와 충격파 속도 6 → 9가 같은 설정에서 적용된다.
- StageTest 생성 코드는 수정된 BeliefTest와 BossTest의 보스 설정을 복제한다. 간부 속도를 1로 되돌리는 설정이나 보스의 예전 구름 동작을 재생성하는 코드는 발견하지 않았다.
- 실제 원본 StageTest의 지형·플레이어 콜라이더·설정·구름 프리팹을 사용하는 PlayMode 통합 검증을 추가했다.

## 통합 검증 보완

- 기존 보스 무보상 테스트는 아레나 입장 전 보스에게 피해를 주고 있었다. 실제 아레나 입장 및 전투 시작을 기다린 뒤 2페이즈 보스를 처치하도록 테스트 준비를 수정했다. 런타임 보상 정책은 변경하지 않았다.
- StageTest 통합 검증에 실제 적 처치의 즉시 보상 예약, 중복 처치 거부, 모션 중 코인 숨김, 확대된 토큰, 바닥 체류 중 카메라 이동 추적, 최종 정산을 추가했다.
- StageTest 통합 검증에 실제 설정을 사용한 흰 구름과 안전 폭의 일치, 대피 성공, 범위 밖 피격, 번개 프레임의 예고 제거, 패턴 취소 후 잔여 번개 방지를 추가했다.

## 실행 결과

검증 도구는 Unity 6000.3.5f2 / Test Framework 1.6.0이며 모든 실행의 `-projectPath`는 원본 저장소다. PlayMode는 그래픽을 켜서 실행했다. 자동 플레이 및 자동 캡처는 사람이 직접 키보드로 전투를 완주한 결과가 아니다.

| 실행 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| 전체 EditMode | 165 | 165 | 0 | 0 |
| 전체 PlayMode, 그래픽 켬 | 100 | 96 | 4 | 0 |
| BossCombat PlayMode (위 결과에 포함) | 41 | 41 | 0 | 0 |
| Economy PlayMode (위 결과에 포함) | 10 | 10 | 0 | 0 |
| StageIntegration PlayMode (위 결과에 포함) | 8 | 8 | 0 | 0 |
| Chest PlayMode (위 결과에 포함) | 9 | 9 | 0 | 0 |
| BeliefSystem + BeliefExpansion PlayMode (위 결과에 포함) | 25 | 25 | 0 | 0 |

코인·보스·상자·StageTest 관련 검증은 모두 통과했다. 보스 테스트는 대피의 콜라이더 경계, 흰 구름 무작위 배치, 번개 단일 판정, 보스/플레이어 사망·비활성화·페이즈 변경·씬 언로드 시 정리, 속도 9의 지상 충격파 및 실제 점프 회피, 속도 200에서 얇은 콜라이더의 swept 충돌과 중복 피해 방지를 포함한다. 재화 테스트는 일시정지 중 진행, 연속 처치의 단일 보상, 저장 실패 후 재시도, 플레이어 사망 손실, HUD 부재·씬 전환 후 단일 정산을 포함한다.

다음 4개 로비 테스트는 `TemporaryStage`를 기대하지만 실제 분기가 `BelieverCombatTest` 또는 `ExecutiveCombatTest`로 이동하여 실패한다.

- `LobbyPlayModeTests.DeathCannotChangeSceneBeforeSaveThenReturnsFullHealthWithOneLoss`
- `LobbyPlayModeTests.LoadingLobbyWithAnUnfinishedSavedStageRoutesBackWithoutGrantingAClear`
- `LobbyPlayModeTests.UnrevealedDepartureLockedExitDamageReloadCompletionAndReturnPreserveHealth`
- `LobbyPlayModeTests.VisualCaptureHiddenRevealedAndCompletedStageAtTwoAspectRatios`

이 4개는 [기존 상자 구현 결과](ChestSystem_ImplementationStatus.md)의 동일한 실패 목록에 기록되어 있다. 이번 공통 기준 `ac9dc7f` 대비 `Assets/Scripts/Lobby`와 `LobbyPlayModeTests.cs`는 변경되지 않았다. 이번 통합과 무관한 로비 동작 및 테스트는 수정하지 않았다. **전체 PlayMode가 전부 통과한 상태는 아니다.**

검증 산출물은 원본 저장소의 다음 로컬 경로에 있다. `Logs`는 Git에서 제외된다.

- `Logs/MergeValidation/editmode.xml`, `editmode.log`
- `Logs/MergeValidation/playmode.xml`, `playmode.log`
- `Logs/MergeValidation/test-summary.json`
- `Logs/MergeValidation/asset-audit.json`
- `Logs/BossCombatVerification/`, `Logs/EconomyVerification/`, `Logs/StageTestVerification/`의 자동 캡처

정적 에셋 검사에서 변경된 29개 파일의 `.meta` 누락, 전체 Assets의 중복 GUID, 변경된 씬/프리팹/설정의 해결되지 않는 Assets/Package 참조는 모두 0건이었다. 실행 로그에 C# 컴파일 오류 및 Missing Script 로드 오류는 없었다. 새 보스 구름 대피/번개 캡처를 열어 흰 구름 아래 보호 영역과 번개 발동 시 빨간 예고 제거를 확인했다. 자동 캡처가 갱신한 기존 Docs PNG 두 개는 원래 내용으로 복구하여 커밋에서 제외했다.

사람이 직접 키보드로 난이도·조작감·장시간 무작위 전투를 확인하는 수동 플레이는 실행하지 않았다.
