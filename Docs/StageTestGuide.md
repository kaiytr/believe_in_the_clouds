# StageTest 통합 테스트

Unity에서 `Assets/Scenes/StageTest.unity`를 열고 Play를 누른다.

- A/D 또는 좌우 방향키: 이동
- Space: 점프
- 마우스 좌클릭: 지상 4타 콤보 / 공중 공격
- Left Shift: 공격을 취소하고 대쉬, 파란 잔상 표시
- R: StageTest 전체 재시작

시작 지점에서 오른쪽으로 진행하면 개 → 신도 → 간부 → 보스 순서로 만난다. 일반 적은 각 구역 입구에서 활성화되고, 보스는 마지막 투기장에 들어가면 전투를 시작한다. 바닥은 연속으로 연결되어 있고, 일반 적 구간에는 점프와 공중 공격을 확인할 수 있는 단방향 발판이 있다. 보스 공간은 기존 설정의 좌표 범위(-17~17)를 유지한다.

PlayerTEST, BeliefTest, BossTest, StageTest는 `Assets/Prefabs/Player/Player.prefab`과 `Assets/Prefabs/UI/PlayerHud.prefab`을 공유한다. 이동 속도 7, 점프 14, 중력 배율 4, 콤보의 개별 타격 설정, 대쉬 속도 14·지속 0.18초·쿨타임 0.6초는 PlayerTEST에서 가져왔다. 하단 초상·교리 슬롯·체력·재화 UI도 PlayerTEST 구성이다. 보스 프리팹의 별도 플레이어 체력 Canvas는 제거했고, 보스 체력·믿음 UI와 F1 디버그 기능은 유지한다.

대쉬는 적 돌진과 같은 `CombatAfterimage` / `CombatAfterimageEmitter` 풀을 사용한다. 플레이어 잔상은 파란색이며, 공격 판정이나 콜라이더가 없다. 대쉬 중 새 공격은 무시하고, 대쉬가 중단되면 이동 잠금과 잔상을 정리한다.

테스트 씬과 공통 프리팹을 다시 구성하려면 `Tools > Stage > Sync test players and build StageTest`를 실행한다. 이 명령은 현재 PlayerTEST를 기준으로 공통 플레이어/UI를 저장하고 BeliefTest·BossTest의 플레이어를 교체한 뒤 StageTest를 다시 생성하므로, 수작업으로 편집한 StageTest 배치는 재생성된다.

`StageIntegrationPlayModeTests`는 네 씬의 공통 구성, 세 일반 적과 보스에 대한 실제 근접 공격, 대쉬 취소·잔상 회수, 구역별 활성화를 확인한다. 그래픽을 켠 상태에서는 `VisualCaptureStageLayoutSharedUiAndPlayerAfterimages`가 `Logs/StageTestVerification`에 화면을 저장한다.
