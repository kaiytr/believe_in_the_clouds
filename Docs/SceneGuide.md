# 씬 구성

Unity 6000.3.5f2 기준. 게임은 `Title`에서 시작한다. 실제 플레이 루프에서 쓰는 씬만 남겼고, 개발용 테스트 씬·제작 원본·데모 씬은 정리(삭제)했다.

## 플레이 루프

```
Title ─▶ Tutorial(첫 실행) ─▶ Lobby ─┬─▶ CombatLevel1~6 ─┐
                                      ├─▶ Shop            ├─▶ Lobby
                                      └─▶ Boss ─▶ Ending ─▶ Title
```

사망하면 결과 화면에서 로비로 돌아가거나 저장 후 Title로 간다.

## 폴더와 씬

```
Assets/Scenes/
  Title.unity          타이틀 (빌드 시작점)
  Tutorial.unity       첫 실행 튜토리얼
  Lobby.unity          로비 · 재단 · 분기 선택
  Ending.unity         엔딩 (임시: 배경과 엔딩롤은 추후 수정)
  Stages/
    CombatLevel1~6     전투 스테이지 (로비 분기에서 무작위로 배정)
    Shop               상점 스테이지
    Boss               보스 스테이지
```

- 씬 이름은 런 라우팅에서 문자열로 쓴다(`LobbyFlowRuntime`의 `ShopScene`·`BossScene`, `ProjectScenes.CombatLevelName`). 이름을 바꾸면 해당 상수도 함께 바꾼다.
- 씬 자산 경로는 `Assets/Scripts/Scenes/ProjectScenes.cs`에서 관리한다. 생성 도구와 테스트는 이 경로를 사용한다.
- 빌드 목록(`ProjectSettings/EditorBuildSettings.asset`)에는 위 씬 전부가 들어 있다. `Tools/Lobby/Connect run content scenes`가 상점·보스 씬을 로비 런에 연결하고 빌드 목록을 이 구성으로 맞춘다.
- `Assets/Settings/Scenes/URP2DSceneTemplate.unity`는 Unity의 새 씬 템플릿이므로 유지한다.

## 확인할 때

1. `Lobby.unity`를 열고 Play한다.
2. F2 패널의 **전체 플레이 루프 테스트**는 격리된 메모리 프로필로 시작하고 테스트용 100코인을 준다. 전투 분기는 CombatLevel1부터 차례로 들어간다.
3. 상점·보스는 로비 분기를 통해 들어간다. 직접 열어도 되며, 상점은 격리된 테스트 런을 자동으로 준비한다.

## 공통 플레이어·HUD

플레이어는 `Assets/Prefabs/Player/Player.prefab`, HUD는 `Assets/Prefabs/UI/PlayerHud.prefab`을 기준으로 한다. 두 프리팹을 수정한 뒤 `Tools > Project > Sync shared player and UI in all scenes`를 실행하면 프로젝트 씬을 동기화하고, `Audit shared scene setup`은 현재 연결 상태를 검사한다.

## 정리 내역

- 삭제: DogCombatTest · BelieverCombatTest · ExecutiveCombatTest, StageTest · EconomyTest · ChestTest · FoundationTest, PlayerTEST · EnemyTest · BeliefTest, CutsceneBubbleShowcase, PlatformerSet1 데모 씬.
- 이름 변경: ShopTest → Shop, BossTest → Boss.
- 이 씬들에만 쓰이던 코드와 에디터 제작 도구, 테스트도 함께 삭제했다. 과거 검증 문서(`Docs/*Verification`, 날짜가 붙은 문서 등)는 당시 기록이므로 예전 씬 이름이 그대로 남아 있다.
