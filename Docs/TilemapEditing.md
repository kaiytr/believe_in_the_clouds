# 스테이지 1 타일맵 편집

적용 씬: `Assets/Scenes/Gameplay/Designed/CombatLevel1.unity`

Tile Palette에서 `MainLevelBuild`를 고르고, Active Tilemap으로 다음 중 하나를 선택한다.

| Active Tilemap | 용도 | 충돌 |
| --- | --- | --- |
| `Platformer Terrain/Tiles` | 바닥·벽 등 일반 지형 | 칠하거나 지운 타일에 자동 반영 |
| `Platformer Terrain/OneWay Platforms` | 아래에서 점프로 통과하고 위에서 착지하는 발판 | 위쪽에서만 적용 |
| `Platformer Terrain/Background Decorations` | 배경 벽·창문·기둥 등 장식 | 없음 |

같은 팔레트를 사용해도 선택한 타일맵에 따라 충돌 여부가 달라진다. 배경·장식은 `Background Decorations`에 칠한다. 기존 `Platformer Backdrop`과 `Interior Scenery`도 충돌 없는 배경이다.

지형과 발판에는 각각 Ground 레이어, Static Rigidbody2D, TilemapCollider2D, CompositeCollider2D가 설정돼 있다. 타일 에셋의 Collider Type은 Grid이며, Composite Operation은 Merge, 합성 충돌 생성은 Synchronous이다. 별도의 지형 BoxCollider2D를 추가할 필요가 없다.

2026-11-06 전환에서 기존 타일 2,251개의 좌표·스프라이트·색·변환·플래그를 유지했다. 이 중 일반 지형은 2,208개, 한 방향 발판은 43개이며, 장식 타일맵은 비어 있는 편집용 레이어로 추가했다. 기존 바닥·발판·점프 연결용 투명 충돌 박스 9개는 제거했고, 좌우 경계 벽 2개와 출구 트리거는 유지했다.

씬이 Unity에서 이미 열려 있었다면 디스크 변경 알림에서 Reload로 다시 불러온 뒤 편집한다. 기존 엑셀 기반 레벨 생성 스크립트를 다시 적용하면 수동 편집이 덮어써질 수 있으므로, 수동 편집 중에는 실행하지 않는다.
