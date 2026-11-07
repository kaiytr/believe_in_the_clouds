# B 세션 · 전투 조작감 인계 문서

- 작업 브랜치: `codex/qol-combat-feel`
- 기준 커밋: `de63db8161ce9b97078e561be02d9488139dd607` (세 세션 공통 개발 기준)
- 최종 병합 대상은 Puri이며, 통합은 C가 담당한다.

## 기능 1. 대시 종료 직후 무적 여유

`PlayerDash`

| 항목 | 값 | 비고 |
| --- | --- | --- |
| `dashTime` | 0.18초 | 변경 없음 (Player 프리팹 직렬화 값 그대로) |
| `cooldown` | 0.6초 | 변경 없음 |
| `dashSpeed` | 14 | 변경 없음 |
| `invulnerabilityGraceTime` | **0.15초** | 신규(처음 0.10초에서 조정). 인스펙터에서 조정 가능. 프리팹에는 값이 없어 코드 기본값이 적용된다 |

- 대시 이동이 끝나면 이동 잠금(`ControlLocked`)과 `IsDashing`은 즉시 풀린다. 대시 소유 무적(`PlayerHealth.SetDashInvulnerability`)만 0.15초 더 이어진다.
- 새 대시가 시작되면 이전 대시의 여유 코루틴을 멈추고 무적을 다시 직접 소유한다. 이전 대시의 늦은 종료가 새 대시 무적을 해제하지 않는다.
- 비활성화(`OnDisable`: 씬 전환, 컷씬에서의 비활성화 포함)에서는 코루틴을 모두 멈추고 대시 무적을 해제한다. 사망 시 `PlayerHealth.Die`가 대시 무적을 먼저 해제하고 이후 `SetDashInvulnerability(true)`는 사망 상태에서 무시된다.
- 일반 피격 후 무적(`isInvulnerable`)과 대시 무적(`isDashInvulnerable`)은 서로 다른 플래그라 서로 지우지 않는다. `PlayerHealth`는 수정하지 않았다.

## 기능 2. 플레이어 추적 방향 전환 0.5초 지연

플레이어가 현재 바라보는 쪽의 반대편에 **안정적으로 0.5초** 머문 뒤에야 몬스터가 돈다.

### 일반 몬스터 (개·신도·간부) — `EnemyUnit`, `EnemyAttack`

- 신규 항목 `turnDelaySeconds` (기본 **0.5초**, `EnemyUnit` 인스펙터). 기존 프리팹·씬에는 값이 없어 기본값이 적용된다.
- `TrackFacingToward(direction)`가 지연 회전 본체다. 매 프레임 호출해도 대기는 한 번만 시작되고, 플레이어가 원래 방향으로 돌아오거나 데드존(0.01) 안으로 들어오면 취소된다. 0.25초 동안 호출이 없으면 오래된 대기로 보고 버린다. 사망(`Die`)과 타깃 상실 때도 초기화한다.
- 플레이어 추적 경로를 모두 `TrackFacingToward`로 돌렸다: `FaceTarget`, `MoveTowardTarget`, `MoveAwayFromTarget`. 개·신도·간부 AI는 이 메서드들을 그대로 호출하므로 수정하지 않았다.
- `SetFacingDirection`은 **즉시 방향 지정**(공격 방향 고정·초기화·연출용)으로 남기고 대기 중인 회전을 지운다.
- **공격 우회 차단**: `EnemyAttack.TryAttack`은 대상이 몬스터가 바라보는 쪽에 있을 때만 시작한다. 반대편이면 `FaceTarget()`으로 지연 회전만 진행시키고 공격은 시작하지 않는다. 그래서 공격 시작 시점의 `SetFacingDirection`이 지연을 건너뛰는 지름길이 되지 않고, 예고·판정·투사체 원점은 이미 바라보는 쪽 기준으로 만들어진다.
- **이동 규칙**: 몬스터는 회전이 끝나 이동 방향을 바라볼 때만 움직인다. 추적(`MoveTowardTarget`)은 플레이어 쪽을 보고 있어야 걷고, 물러나기(`MoveAwayFromTarget`)는 원하는 바라보는 방향이 맞아야 걷는다. 회전을 기다리는 동안은 제자리에 선다(뒤를 본 채 다가오는 현상 없음).

### 보스 — `BossCombatController`, `BossAttackExecutor`

- 신규 항목 `turnDelaySeconds` (기본 **0.5초**, `BossCombatController`). Boss 프리팹에는 값이 없어 기본값이 적용된다.
- `TrackPlayerFacing()`: 대기 중 회전(`FacePlayerWhileIdle`)과 같은 지연 규칙을 쓴다. `Face`·`CancelCombat`·리셋 경로에서 대기를 지운다.
- `FacePlayer()`는 즉시 회전 API로 남겼지만 **공격 직전에는 더 이상 호출하지 않는다**. 근접 연속 베기(각 타 직전), 돌진, 투사체는 `TurnToPlayer()`로 보스가 플레이어 쪽을 볼 때까지(지연 회전이 끝날 때까지) 기다린 뒤 예고를 만든다.
- 이미 예고 중이거나 실행 중인 공격은 기존대로 `regularAttackActive` 동안 대기 중 회전이 꺼져 있어 방향이 바뀌지 않는다.

## 공격 방향 관련 주의

- 몬스터는 이제 **바라보는 쪽에 플레이어가 있을 때만 공격을 시작하고 이동**한다. 플레이어가 등 뒤로 돌아가면 최소 0.5초 동안 공격을 못 한다. 이것이 의도한 정책이다.
- 플레이어가 0.5초 안에 계속 좌우를 오가면 몬스터가 한 번도 돌지 못해 공격을 못 하는 상태가 이어질 수 있다. 보스의 연속 베기도 각 타 직전에 같은 확인을 하므로, 콤보 중 뒤로 돌아가면 다음 타가 지연된다.
- 간부의 마법진은 방향이 아니라 플레이어 발밑에 만들어지지만, 같은 공격 시작 조건(바라보는 쪽)을 따른다.
- `DogEnemy.cs`, `ExecutiveEnemy.cs`는 주석이 비UTF-8(cp949)이라 수정하지 않았다. 필요한 동작은 `EnemyUnit`/`EnemyAttack`만으로 처리된다.

## 변경 파일

- `Assets/Scripts/Player/PlayerDash.cs`
- `Assets/Scripts/Enemy/Core/EnemyUnit.cs`
- `Assets/Scripts/Enemy/Core/EnemyAttack.cs`
- `Assets/Scripts/Boss/BossCombatController.cs`
- `Assets/Scripts/Boss/BossAttackExecutor.cs`
- `Docs/ParallelGameplayQoL/B_전투조작감.md` (이 문서)

프리팹·씬·테스트·`.meta`·ProjectSettings·Packages는 수정하지 않았다.

## 검증

- 실행한 것: 정적 호출 경로 검토, `git diff --check`, 런타임 어셈블리 컴파일(Roslyn).
- **실행하지 않은 것**: Unity에서의 실행, EditMode·PlayMode 테스트. 특히 `BossCombatPlayModeTests`는 보스가 반대편 플레이어를 향해 공격을 시작하는 장면이 있어 0.5초 지연의 영향을 받을 수 있으므로 통합 시 확인이 필요하다.
- 기존 테스트의 기대값은 조정하지 않았다(실행하지 못해 영향 범위를 확정할 수 없음).
