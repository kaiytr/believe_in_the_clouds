# 개 적 스프라이트 적용

사용자가 제공한 PNG를 수정하지 않고 `Assets/Design/Enemy/Dog/`에 역할별 이름으로 복사했다.

| 받은 파일 | 프로젝트 파일 | 사용 방식 |
| --- | --- | --- |
| `idel-Sheet.png` | `DogIdle.png` | 3프레임 대기 반복 |
| `walking-Sheet.png` | `DogWalk.png` | 4프레임 걷기 반복 |
| `attack2-Sheet.png` | `DogAttack.png` | 6프레임: 돌진 준비 3장, 돌진 2장 반복, 회복 1장 |
| `die.png` | `DogDeath.png` | 쓰러진 자세 1장. 기존 제거 시점까지 표시 |

각 프레임은 61×53 픽셀로 나누고, 기준점은 발 위치에 맞췄다. 포인트 필터와 무압축을 사용한다. 원본 그림은 왼쪽을 보므로 시각 프리팹에서 한 번 반전하고, 실제 방향 전환은 기존 적 루트의 방향을 따른다.

`Assets/Prefabs/Enemy/Dog/DogVisual.prefab`이 렌더러와 `DogSpriteAnimator`의 프레임을 관리한다. `DogCombatTest`, `StageTest`, 제작 원본 `EnemyTest`·`BeliefTest`의 개에 연결했다. 스프라이트의 가로세로 픽셀 비율과 바닥선을 맞추되 몸체 충돌체, 이동 속도, 돌진 피해·거리·예고, 코인 보상 시점은 기존 값을 사용한다. 돌진 잔상은 현재 개 스프라이트를 복사한다.

확인하려면 `Assets/Scenes/Gameplay/DogCombatTest.unity`를 열어 Play한다. `Tools > Combat > Apply dog sprite assets`는 위 규격의 이미지를 다시 분할하고 개 시각 프리팹과 네 씬에 연결한다. 이미 적용된 프로젝트에서는 메뉴를 다시 실행할 필요가 없다.

검증: Unity 6000.3.5f2에서 EditMode 199개와 관련 PlayMode 47개가 통과했다. 네 씬의 바닥선·공통 프리팹, 대기·걷기·돌진·회복·사망 프레임, 좌우 방향, 잔상, 돌진 피해와 시체 제거 후 코인 드롭을 확인했다.
