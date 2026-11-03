# 씬 선택과 플레이 테스트

Unity 6000.3.5f2 기준. 기본 시작점은 `Assets/Scenes/Gameplay/Lobby.unity`다.

## 평소 테스트할 때

1. `Gameplay/Lobby.unity`를 열고 Play한다.
2. F2 패널에서 **전체 플레이 루프 테스트**를 누른다. 일반 신규 시작과 구분되는 격리된 메모리 프로필이며 테스트용 100코인을 지급한다.
3. 전투 분기를 골라 개 → 신도 → 간부 전투를 차례로 완료하고, 각 전투의 오른쪽 출구에서 E로 로비에 돌아온다. 전투 사이에는 상점 분기와 로비 재단도 확인한다.
4. 전투 3회 완료 후 준비되는 보스 분기로 들어가 새 먹구름·번개 대피와 충격파를 확인한다.
5. 보스를 처치하면 현재 구현의 런 완료 상태를 확인한다. 전투 중 사망하면 손실 처리 후 새 런으로 로비에 돌아오는지 확인한다. 새 루프를 시작하려면 테스트 버튼을 다시 사용한다.

이 모드의 진행은 Play 종료 시 유지되지 않는다. 일반 Editor Play도 기본적으로 메모리 저장을 사용하므로 디스크 이어하기 검증과 구분한다.

## 폴더별 용도

| 위치 | 씬 | 용도 |
| --- | --- | --- |
| Gameplay | Lobby | 로비·재단·분기 선택, 빌드 시작점 |
| Gameplay | Title | 사망 결과에서 저장 및 종료 후 표시하는 타이틀 |
| Gameplay | DogCombatTest / BelieverCombatTest / ExecutiveCombatTest | 실제 런의 일반 적 전투 |
| Gameplay | ShopTest | 실제 런의 상점 |
| Gameplay | BossTest | 실제 런의 보스 전투. 직접 열면 패턴별 검증도 가능 |
| Tests | StageTest | 개·신도·간부·보스가 이어지는 단독 통합 전투 |
| Tests | EconomyTest | 코인·결제·사망 손실·저장 실패 검증 |
| Tests | ChestTest | E 홀드 상자·보상·입력 취소 검증 |
| Tests | FoundationTest | 조각을 준비해 재단을 독립적으로 점검하는 보조 씬 |
| Sources | PlayerTEST | 공통 플레이어·HUD 제작 원본 |
| Sources | EnemyTest | 일반 적 개별 전투 씬의 제작 원본 |
| Sources | BeliefTest | 믿음·적 전투 자동 검증 및 StageTest 적 구성 원본 |

`Test`가 포함된 Gameplay 씬도 현재 로비의 실제 목적지다. 씬 이름은 런 라우팅과 테스트가 사용하므로 유지하고, 역할은 폴더로 구분했다.

빌드에는 Lobby·Title과 전투·상점·보스 씬을 포함한다. Tests와 Sources는 에디터에서 직접 열어 사용한다. 빈 기본 `SampleScene`은 제거했으며 Unity가 제공하는 `Assets/Settings/Scenes/URP2DSceneTemplate.unity`는 새 씬 템플릿으로 유지한다.

## 문제가 있는 기능만 다시 확인할 때

| 확인하려는 기능 | 열 씬과 조작 |
| --- | --- |
| 실제 적 사망 후 코인, 간부 추격감 | Tests/StageTest. A/D 이동, Space 점프, 좌클릭 공격, Shift 대시, R 씬 재시작 |
| 흰 구름 아래 대피 | Gameplay/BossTest를 직접 열어 아레나 진입 후 위쪽 숫자 5 |
| 충격파 지상 피격·점프 회피 | 같은 BossTest에서 숫자 9. R은 전투 초기화, F1은 테스트 패널 |
| 연속 코인 보상·저장 재로드 | Tests/EconomyTest의 처치·저장 다시 불러오기 버튼 |
| 상자 홀드·중단·중복 개방 방지 | Tests/ChestTest에서 E를 1.5초 유지 |

BossTest의 패턴 선택 키는 직접 연 단독 테스트에서 사용한다. 로비에서 진행 중인 실제 보스 분기에서는 개발용 단축키를 막는다.

## 제작 도구와 자동 테스트

씬 자산 경로는 `Assets/Scripts/Scenes/ProjectScenes.cs`에서 관리한다. 생성 도구와 자동 테스트는 이 경로를 사용한다. 런 라우팅에 쓰는 씬 이름과 기존 씬 GUID는 유지했다.

기존 Tools 메뉴는 새 폴더에 생성한다. `Tools/Lobby/Connect run content scenes`는 로비·전투·상점·보스와 Title을 빌드 목록에 연결한다. `Tools/Stage/Sync test players and build StageTest`는 개별 검증 씬을 빌드 목록에 추가하지 않는다.

기존 씬을 Play하는 데 재생성 메뉴는 필요하지 않다. 재생성 메뉴를 실행하면 해당 씬의 수동 배치가 대체될 수 있으므로 실제 구성을 다시 만들 때 사용한다.

## 공통 공격·재단·상점을 모든 씬에 적용할 때

플레이어의 공통 공격 설정은 `Assets/Prefabs/Player/Player.prefab`, HUD 구성은 `Assets/Prefabs/UI/PlayerHud.prefab`을 기준으로 한다. 두 프리팹을 수정한 뒤 `Tools > Project > Sync shared player and UI in all scenes`를 실행하면 카탈로그의 프로젝트 씬 14개를 동기화한다. `Audit shared scene setup`은 현재 연결 상태를 검사한다.

독립 복사본 플레이어는 공통 프리팹으로 연결하고 발 위치·카메라 타깃·더미 도구 참조를 이어 준다. 공통 공격 설정의 씬별 덮어쓰기를 해제하고 HUD는 씬마다 하나를 배치한다. 재단·상점 화면은 공통 런타임 부팅 코드를 사용한다. 상점·전투·보스·신앙·스테이지·런 연결 생성 도구도 이 동기화를 호출한다.

`ShopTest`, `EnemyTest`, `FoundationTest`의 누락을 보완했으며, 공통 플레이어에 Player 교리 주체를 지정했다. 교리 슬롯을 클릭하면 좌측 상단 설명이 테스트 패널 위에 나타나고, 3초 표시 후 0.5초 동안 사라진다. 기본 URP 새 씬 템플릿은 플레이용 씬 14개에 포함되지 않는다.

전수 검증 결과와 브랜치 반영 기록은 [전체 씬 적용 검증](SharedScenePropagation_2026-11-03.md)에 기록한다.

## 정리 검증 결과 · 2026-11-02

| 실행 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| 전체 EditMode | 165 | 165 | 0 | 0 |
| 전체 PlayMode, 그래픽 켬 | 101 | 100 | 1 | 0 |
| 상자 PlayMode 보완 후 재실행 | 9 | 9 | 0 | 0 |

PlayMode 판정은 전체 실행과 상자 재실행 결과를 합친다. 최신 결과를 케이스별로 적용하면 101개 모두 통과하며 미해결 실패는 없다. 전체 실행의 유일한 실패는 코인 바닥 체류가 추가된 뒤에도 상자 테스트가 고정 1.2초 후 잔액을 확인하던 문제였다. 4초 제한 안에서 실제 대기 코인의 정산 완료를 확인하도록 보완한 뒤 상자 9개 전체를 다시 실행했다. 정산 완료 캡처에도 같은 완료 조건을 적용했다.

예전 `TemporaryStage`를 기대하던 로비 테스트는 실제 전투 씬을 확인하고 같은 씬으로 재로드되는지를 검증하도록 갱신했다. 라우팅·저장 HP 검증에서는 무작위 전투 피해를 분리하며, 전투 자체는 기존 전투 전용 테스트로 확인한다. 로비 7/7, 보스 41/41, 재화 10/10, StageIntegration 9/9가 전체 실행에서 통과했다. StageIntegration에는 빌드 목록에 없는 StageTest의 실제 씬 재시작·체력 및 구역 초기화 검증을 추가했다.

정적 검사에서 이동한 14개 씬과 기존 `.meta` 내용은 이동 전과 동일했다. 씬 GUID·빌드 경로 불일치와 중복 에셋 GUID는 0건이며, 빌드 씬은 Gameplay의 7개와 Lobby 시작점으로 확인했다. 기존 검증 산출물의 과거 경로 기록은 역사 자료로 유지하고 현재 생성·로드 경로만 새 위치로 연결한다.

원본 XML·로그와 `test-summary.json`, `scene-audit.json`, 이동 전 무결성 기록은 `Logs/SceneCleanup/`에 있다. 자동 캡처가 갱신한 기존 Docs PNG 두 개는 원래 내용으로 복구했다. 자동 검증은 사람이 직접 플레이한 조작감·난이도 검증과 구분하며, 이번 정리에서 직접 키보드로 전투를 완주하는 수동 테스트는 수행하지 않았다.
