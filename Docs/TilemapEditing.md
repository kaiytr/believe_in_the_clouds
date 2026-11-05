# 타일맵으로 레벨 편집

적용 씬은 튜토리얼, 일반 전투 1~6, 로비, 상점의 9개다.

- 튜토리얼·일반 전투: `Assets/Scenes/Gameplay/Designed/`
- 로비: `Assets/Scenes/Gameplay/Lobby.unity`
- 상점: `Assets/Scenes/Gameplay/ShopTest.unity`

Tile Palette에서 `MainLevelBuild`를 고르고, Active Tilemap으로 다음 중 하나를 선택한다. 튜토리얼·일반 전투의 부모는 `Platformer Terrain`, 로비·상점의 부모는 `Platformer Floor`다.

| Active Tilemap | 용도 | 충돌 |
| --- | --- | --- |
| `Tiles` | 바닥·벽 등 일반 지형 | 칠하거나 지운 타일에 자동 반영 |
| `Platforms` | 바닥과 별도로 편집하는 발판 | 위·아래·옆 모든 방향에 적용 |
| `Background Decorations` | 배경 벽·창문·기둥 등 장식 | 없음 |

같은 팔레트를 사용해도 선택한 타일맵에 따라 충돌 여부가 달라진다. 배경·장식은 `Background Decorations`에 칠한다. 기존 배경·창문·기둥·발판 아래 장식 등 SpriteRenderer 오브젝트는 원래 위치와 그림을 유지하며 충돌을 부여하지 않는다. 로비·상점의 `Platforms`는 필요할 때 칠할 수 있는 빈 레이어다.

지형과 발판에는 각각 Ground 레이어, Static Rigidbody2D, TilemapCollider2D, CompositeCollider2D가 설정돼 있다. 타일 에셋의 Collider Type은 Grid이며, Composite Operation은 Merge, 합성 충돌 생성은 Synchronous이다. 별도의 지형 BoxCollider2D를 추가할 필요가 없다.

2026-11-06 최초 충돌 전환 당시 다음 배치를 유지했다. 아래 수량은 이후 수동 편집 전의 기록이다. 타일의 좌표·스프라이트·색·변환·플래그, 적의 위치와 활성 상태, 상자·플레이어 등 프리팹의 배치 설정을 파일 비교로 확인했다. 장식 타일맵은 기존 장식을 옮겨 그리는 작업 없이 빈 편집용 레이어로 추가했다.

| 씬 | 보존한 타일 | 일반 지형 | 한 방향 발판 | 제거한 지형 충돌 박스 |
| --- | ---: | ---: | ---: | ---: |
| CombatLevel1 | 2,251 | 2,208 | 43 | 9 |
| Tutorial | 1,731 | 1,696 | 35 | 7 |
| CombatLevel2 | 2,286 | 2,208 | 78 | 8 |
| CombatLevel3 | 2,278 | 2,208 | 70 | 9 |
| CombatLevel4 | 2,261 | 2,208 | 53 | 12 |
| CombatLevel5 | 2,277 | 2,208 | 69 | 12 |
| CombatLevel6 | 1,895 | 1,824 | 71 | 14 |
| Lobby | 960 | 960 | 0 | 1 |
| ShopTest | 480 | 480 | 0 | 1 |

제거 대상은 예전 `Ground`, `One-way ledge`, `Jump Link`의 지형용 BoxCollider2D와 불필요해진 Effector다. 좌우 경계 벽, 출입구 트리거, 튜토리얼 컷씬 트리거, CombatLevel6의 전투 봉쇄벽은 유지했다.

후속 수정에서 9개 씬의 `OneWay Platforms`를 `Platforms`로 변경하고 PlatformEffector2D를 제거했다. 기존 발판 타일도 이제 아래에서 머리를 부딪히며 통과할 수 없다. 일반 전투 1~6에서는 저장된 새 지형을 기준으로 적 23마리의 Y 위치만 지면에 맞췄다. 나머지 적과 모든 타일·상자·플레이어 배치는 유지했다. 이후 타일을 다시 편집하면 적의 시작 높이는 필요에 따라 직접 맞춘다.

CombatLevel6의 `Level Encounter`는 봉쇄벽 사이 중앙(X 약 33.14)을 플레이어 몸 중심이 통과할 때 활성화된다. 중앙에 도달하기 전까지 적은 비활성 상태이며, 활성화되면 봉쇄벽과 보스 전투용 카메라가 동작하고 배치된 적들이 흙먼지와 함께 솟아오른다. 약 0.61초의 등장 중에는 적의 AI·공격·물리·피격 판정을 잠그고, 연출 종료 후 함께 전투를 시작한다. 적을 새로 복제하지 않는다. 전멸하면 봉쇄를 풀고 탐색 카메라로 부드럽게 돌아간다.

발동 위치는 `DesignedLevelEncounter`의 `Arena Activation Progress`(현재 0.5 = 두 봉쇄벽의 중앙), 등장 연출은 `Reveal Enemies From Ground`로 조절한다. 같은 오브젝트의 `BossArenaCamera`에서 전투 카메라 크기(8), 최대 크기(10.5), 세로 오프셋(-1), 전환 시간(0.6초)을 조절한다. 카메라 범위는 두 봉쇄벽과 `Arena Min Y`/`Arena Max Y`로 계산하며 별도 투명 지형 충돌을 만들지 않는다.

Unity에서 저장하지 않은 편집 내용이 있으면 외부에서 씬 파일을 수정하기 전에 저장한다. 디스크 변경 알림에서 Reload하기 전에도 저장하지 않은 변경이 있으면 별도 씬 사본 등에 보관한다. 기존 엑셀 기반 레벨 생성 스크립트를 다시 적용하면 수동 편집이 덮어써질 수 있으므로, 수동 편집 중에는 실행하지 않는다.
