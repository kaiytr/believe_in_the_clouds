# 타일맵으로 레벨 편집

적용 씬은 튜토리얼, 일반 전투 1~6, 로비, 상점의 9개다.

- 튜토리얼·일반 전투: `Assets/Scenes/Gameplay/Designed/`
- 로비: `Assets/Scenes/Gameplay/Lobby.unity`
- 상점: `Assets/Scenes/Gameplay/ShopTest.unity`

Tile Palette에서 `MainLevelBuild`를 고르고, Active Tilemap으로 다음 중 하나를 선택한다. 튜토리얼·일반 전투의 부모는 `Platformer Terrain`, 로비·상점의 부모는 `Platformer Floor`다.

| Active Tilemap | 용도 | 충돌 |
| --- | --- | --- |
| `Tiles` | 바닥·벽 등 일반 지형 | 칠하거나 지운 타일에 자동 반영 |
| `OneWay Platforms` | 아래에서 점프로 통과하고 위에서 착지하는 발판 | 위쪽에서만 적용 |
| `Background Decorations` | 배경 벽·창문·기둥 등 장식 | 없음 |

같은 팔레트를 사용해도 선택한 타일맵에 따라 충돌 여부가 달라진다. 배경·장식은 `Background Decorations`에 칠한다. 기존 배경·창문·기둥·발판 아래 장식 등 SpriteRenderer 오브젝트는 원래 위치와 그림을 유지하며 충돌을 부여하지 않는다. 로비·상점의 `OneWay Platforms`는 필요할 때 칠할 수 있는 빈 레이어다.

지형과 발판에는 각각 Ground 레이어, Static Rigidbody2D, TilemapCollider2D, CompositeCollider2D가 설정돼 있다. 타일 에셋의 Collider Type은 Grid이며, Composite Operation은 Merge, 합성 충돌 생성은 Synchronous이다. 별도의 지형 BoxCollider2D를 추가할 필요가 없다.

2026-11-06 전환에서 다음 배치를 유지했다. 타일의 좌표·스프라이트·색·변환·플래그, 적의 위치와 활성 상태, 상자·플레이어 등 프리팹의 배치 설정을 파일 비교로 확인했다. 장식 타일맵은 기존 장식을 옮겨 그리는 작업 없이 빈 편집용 레이어로 추가했다.

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

Unity에서 저장하지 않은 편집 내용이 있으면 외부에서 씬 파일을 수정하기 전에 저장한다. 디스크 변경 알림에서 Reload하기 전에도 저장하지 않은 변경이 있으면 별도 씬 사본 등에 보관한다. 기존 엑셀 기반 레벨 생성 스크립트를 다시 적용하면 수동 편집이 덮어써질 수 있으므로, 수동 편집 중에는 실행하지 않는다.
