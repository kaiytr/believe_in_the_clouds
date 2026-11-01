# 상자 시스템 구현 결과

작업일: **2026-11-01 (Asia/Seoul)** · Unity **6000.3.5f2**

사용자가 확정한 **E 1.5초 유지·교리 조각 1개 보장·중단 시 진행률 초기화**를 격리된 테스트 씬에서 구현했다. Unity에서 `Assets/Scenes/ChestTest.unity`를 열고 Play한다. 조작과 테스트 도구는 [테스트 가이드](ChestSystem_TestGuide.md)에 있다.

## 구현

| 영역 | 결과 |
| --- | --- |
| 테스트 에셋 | `ChestTest.unity`, `Chest.prefab`, 상자 4개, 즉시 E 안내 기둥, 기존 Player·PlayerHud 재사용 |
| 외형 | 단색 사각형, 닫힘/선택 강조/어두운 열림 표시, 통과 가능한 trigger |
| 입력 | 공통 컨트롤러의 선택적 홀드 계약. 기존 즉시 E/F 동작 유지, 시작 대상 고정, 완료·취소 뒤 키 해제 필요 |
| 취소 | 키 해제·이탈·수락된 피격·공격/점프/대시·모달·ESC·일시 정지·포커스 상실·생명주기 변경에서 0% |
| 진행 UI | 상자 위에 부착한 E 사각 키캡, 카메라/상자 이동 추적. 위 중앙부터 시계 방향으로 외곽선, 성공 시 완성 강조 |
| 조각 | 기존 카탈로그에서 등급 65/25/10 → 등급 내 균등 1개. 중복 허용, 보유 원장 반영, 자동 장착 없음 |
| 코인 | 별도 7~17 포함 정수 추첨. 상자에서 퍼짐 → 기존 코인 HUD 비행 → 토큰별 정산 |
| 저장 | 안정적인 스테이지/상자 ID, 등록 시 숨겨진 보상 계획 저장, 개방·조각·코인 예약·상세 영수증 단일 커밋 |
| 실패·상한 | 저장 실패 시 전부 유지, 재시도/재로드 중복 방지, 조각 한도는 전체 실패, 코인 한도는 기존 접수량 제한 |
| 검증 도구 | 메모리 프로필, 피해 10, 저장 실패 전환, 저장 재로드, 새 빈 프로필, 모달, 회복 |

새 기능의 수치·테이블·추첨 검증은 `ChestRules.cs`에 모았다. 별도의 설정 에셋은 만들지 않았다. `IHoldInteractable`은 대상의 문구와 완료 결과를 받으므로 공통 컨트롤러는 상자 모듈을 참조하지 않는다. 신규 성공 통지는 `ChestInteractable.Opened`로 전달한다.

테스트에서 확인한 공통 경계도 보완했다. 씬 전환 정산이 이미 성공했는데 재시도 플래그가 남아 새 코인 비행을 조기 정산하던 경우를 수정했다. 씬 재로드 시에는 플레이어 탐색 대기 시간과 피격 취소 표식을 초기화한다.

## 저장 호환과 격리

경제 프로필을 **schemaVersion 3**으로 확장했다. v1→v2→v3을 순서대로 읽고 기존 코인·조각·런·사망 저널·거래·대기 코인을 유지한다. 저장 파일명과 경로는 바꾸지 않았다. 선택적 상자 상태와 상세 영수증은 Unity JSON의 null 직렬화를 고려해 존재 플래그로 정규화한다.

열린 상자의 영수증은 코인 정산·소비·사망 손실 후에도 유효하다. 상자 상태와 영수증이 충돌하거나 토큰 금액이 맞지 않으면 저장 검증을 통과하지 못한다. 실제 JSON 파일의 저장→재로드→대기 정산→중복 개방 방지도 자동 검증했다.

ChestTest는 **실제 사용자 저장을 사용하지 않는 메모리 프로필**이다. 버튼의 재로드는 같은 메모리 저장을 새 서비스로 여는 검증이고, Play를 종료하면 초기화된다. 씬을 떠날 때는 진입 전 서비스로 복원한다.

## 검증 결과

열린 원본 Unity를 유지하고 `.utmp/lobby-verification` 복사본에서 실행했다. 기존 실패 여부는 수정하지 않은 HEAD `d0001d4d2f57c8045d173858dbc94773f530b903`를 `.utmp/chest-baseline`에 추출해 별도로 확인했다.

| 최종 실행 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| 전체 EditMode | 154 | 154 | 0 | 0 |
| 전체 PlayMode, 그래픽 켬 | 85 | 80 | 5 | 0 |
| 상자 전용, 위 결과에 포함 | 42 | 42 | 0 | 0 |
| 기존 실패 5개를 원래 HEAD에서 재실행 | 5 | 0 | 5 | 0 |

상자 전용은 EditMode 33개·PlayMode 9개다. 확률 경계·균등 풀·안정적인 추첨 벡터, 원자적 지급·상한·저장 실패, JSON 이전/왕복, 홀드 임계·취소·대상 고정·재입력, 피격 즉시 회복·무적 거부, 실제 HUD 비행·재로드·원래 서비스 복원을 포함한다.

최종 PlayMode 실패 이름 5개는 원래 HEAD의 실패 목록과 정확히 같다. **전체 PlayMode가 모두 통과한 상태는 아니다.** 다음 기존 실패는 상자 변경에서 수정하지 않았다.

| 기존 실패 | 확인한 내용 |
| --- | --- |
| `EconomyPlayModeTests.ActualBossDefeatGivesZeroCoins` | 보스 사망 기대가 실패. 원래 HEAD에서도 동일하게 재현 |
| `LobbyPlayModeTests.DeathCannotChangeSceneBeforeSaveThenReturnsFullHealthWithOneLoss` | `TemporaryStage`를 기대하지만 현재 전투 분기는 실제 전투 씬으로 이동 |
| `LobbyPlayModeTests.LoadingLobbyWithAnUnfinishedSavedStageRoutesBackWithoutGrantingAClear` | 위와 같은 씬 기대값 불일치 |
| `LobbyPlayModeTests.UnrevealedDepartureLockedExitDamageReloadCompletionAndReturnPreserveHealth` | 위와 같은 씬 기대값 불일치 |
| `LobbyPlayModeTests.VisualCaptureHiddenRevealedAndCompletedStageAtTwoAspectRatios` | 위와 같은 씬 기대값 불일치 |

16:9·4:3에서 절반 진행, 개방/코인 비행, 정산 완료 화면을 자동 캡처하고 직접 검토했다. 보상 문구를 캐릭터 머리 위로 올려 겹침을 줄였다. 16:10과 실제 키 유지 조작감은 테스트 가이드에 따라 사용자가 직접 확인할 항목이다.

검증 요약은 [test-summary.json](ChestSystemVerification/test-summary.json)에 있다. 원본 XML·로그는 로컬 `Logs/Chest*`, 캡처는 `Logs/ChestVerification/`에 있으며 Git에는 포함하지 않는다.

### 상자 부착 UI 수정

후속 사용자 요청으로 E 안내를 상자 위에 부착했다. 대기·진행·완료/실패 안내가 동일한 상자를 따르며, 카메라 추적 뒤 위치를 갱신한다. 화면 밖 기준점·삭제/비활성 대상·카메라 부재에서는 숨긴다. 이후 상점·재단·로비 분기·스테이지 출구에도 같은 계약을 적용했다. 그 밖에 앵커가 없는 대상은 하단 안내를 사용한다.

이 UI 변경 뒤 상자 PlayMode **9/9개**를 다시 실행해 통과했다. 16:9·4:3 및 카메라를 수평 이동한 캡처에서 부착 위치를 확인했다. 위 전체 EditMode/PlayMode 표는 최초 구현 시점의 실행 결과이며 이번 UI 수정에서 전체를 재실행하지 않았다. 후속 XML은 `Logs/ChestWorldPrompt.xml`, 카메라 추적 캡처는 `Logs/ChestVerification/02b-follow-camera.png`다.

추가 요청으로 상점·재단에도 같은 부착 방식을 적용했다. 두 대상은 크기가 다른 씬에서도 콜라이더 윗면 위 `Prompt Gap`(기본 0.8 월드 유닛)에 안내를 표시하고 E 한 번으로 연다. 기존 PlayMode 검증 **2/2개**를 통과했으며, 검증 복사본에만 추가한 캡처 단계로 ShopTest·FoundationTest에서 안내 부착·카메라 이동·실제 E 패널 열림을 직접 확인했다. 원본 테스트 파일은 변경하지 않았다. XML은 `Logs/ShopFoundationWorldPrompt.xml`, 캡처는 `Logs/InteractionPromptVerification/`에 있다.

로비 분기·스테이지 출구도 콜라이더 윗면 위 0.8 유닛을 기준으로 부착했다. 공통 컴포넌트 변경이므로 해당 컴포넌트를 사용하는 모든 씬과 런타임 생성 대상에 적용된다. 긴 분기 E/F 안내는 상자 너비·높이를 문구에 맞춰 조정해 잘리지 않게 했다. 기존 분기 PlayMode 검증 2개와 복사본의 캡처 흐름 1개가 통과했다. 캡처 흐름은 이벤트 분기의 F 공개→E 출발→이벤트 출구/완료 후 출구→E 로비 복귀를 확인했고 16:9·4:3 화면을 검토했다. 원본 테스트 파일은 유지했다. 이 확인은 앞서 기록한 기존 전투 씬 기대값 불일치를 수정한 결과가 아니다. XML은 `Logs/BranchExitWorldPrompt-final.xml`, 캡처는 `Logs/BranchExitPromptVerification/`에 있다.

## 후속 범위

- 실제 스테이지별 상자 수·생성·배치 연결.
- 정식 아트·음향·개방 애니메이션과 플레이 감각 조정.
- 기존 로비/보스 테스트의 현재 진행 규칙 반영.

테스트 씬·프리팹은 생성된 상태로 제공하므로 실행을 위해 메뉴를 다시 돌릴 필요가 없다. 재생성 메뉴는 `Tools > Chest > Build independent test scene`이다.
