# Puri · Leesc 병합 및 전투 연결

- 기준일: 2026-10-25
- 병합 전 Puri: `0684fd2445df2656434776a9e28bd9a25ba29917`
- 병합 전 Leesc: `7dbc4daa95292db8daf66a0197a7a858dca56125`
- 순서: Puri에서 Leesc 변경을 병합·해결·검증한 뒤 Leesc에 통합한다.
- 상태: 충돌 해결 및 최종 검증 완료.

## 합의한 해결 방식

### 일반 적의 공통 적중 후처리

`EnemyAttack.NotifyPlayerHpDamageApplied(attackId)`가 이번 공격의 HP 피해 기록과 공격자 교리의 `OnAttack` 알림을 담당한다. 돌 투사체, 개 돌진, 마법진 공격은 `TakeDamage`가 피해를 받아들인 경우에만 이 함수를 호출한다. 공격 ID가 오래됐거나 이미 종료됐거나 같은 공격에서 알린 적이 있으면 재통지하지 않는다.

피해 적용을 한 번 수행한 뒤 믿음 기록을 먼저 남기고 교리 알림을 보낸다. HP 피해 여부는 공격 전후 순 HP 변화가 아닌 피해 수락 결과를 사용하므로 피격 직후 회복 교리가 실행되어도 공격의 피해 기여가 사라지지 않는다.

### 마법진의 결과 반환

`MagicCircle.Activate`는 유해 효과 적용 여부와 `out bool hpDamageApplied`만 반환한다. 공격자 참조를 저장하거나 교리 이벤트를 발행하지 않는다. 호출자인 `MagicCircleAttack`이 HP 피해 결과를 공통 후처리에 전달한다. 슬로우만 적용되면 유해 효과는 참이지만 HP 피해 기록과 공격 적중 교리는 발생하지 않는다.

Puri의 공격 ID 고정, 취소 정리, `completedNormally` 구분을 유지한다. Leesc의 교리 규칙, 플레이어 공격·대시·이동 잠금, 상점·상호작용·HUD를 함께 보존한다.

## 실제 플레이어 공격 연결

- 지상 콤보 각 타격과 공중 단타마다 새 `PlayerAttackContext`를 만든다. 여러 콜라이더가 같은 대상을 가리켜도 한 타격으로 처리한다.
- 공격 시작 시점의 위치·방향·회전된 공격 박스를 기준으로 대상을 기록한다. 전진 공격은 현재 선딜 시간과 전진 속도로 도달할 수 있는 구간도 포함한다. 공중 후방 공격은 실제 후방 공격 박스를 기준으로 삼는다.
- 시작 범위에 있는 대상에만 `AttackAttempt`를 발행한다. 선딜 후 범위에 들어온 대상은 실제 적중할 수 있지만 시작 시도로 소급 집계하지 않는다. `PlayerAttackContext.Begin(..., publishAttempt: false)`로 해당 대상을 등록한다.
- 실제 피해는 동일 컨텍스트를 일반 적·보스의 `IDamageable`에 전달한다. 수신 측이 중복을 검사하고 `HitConfirmed`와 일반 적의 피격 기반 믿음 이벤트를 알린다. 플레이어의 교리 `OnAttack`은 피해가 받아들여졌을 때 알린다.
- 각 타격 종료·공격 취소·컴포넌트 비활성화 시 컨텍스트를 폐기한다. 취소 시 히트박스 표시도 회수한다.
- `PlayerAttack`이 공유 전투 신호 컴포넌트를 보장한다. 기존에 직렬화된 플레이어도 Awake에서 누락된 컴포넌트를 보충한다.
- 지상·공중 공격의 타격 실행과 대상 필터를 공유하며, 물리 검색은 호출자 소유의 재사용 버퍼를 사용한다.

## 충돌 파일

| 파일 | 해결 |
| --- | --- |
| StoneProjectile.cs | Puri의 공격별 HP 피해 기록·정상 종료를 유지하고 교리 알림을 공통 후처리에 연결 |
| DogChargeAttack.cs | 중복 접촉 방지를 유지하고 피해 기록·교리 알림을 공통 후처리에 연결 |
| MagicCircle.cs | HP 피해 결과 반환을 유지하고 공격자 저장·교리 직접 발행 제거 |
| MagicCircleAttack.cs | 결과 반환 API에 맞추고 공통 후처리 호출, 효과 적용과 HP 피해를 구분 |

## 검증

Unity 6000.3.5f2의 `.utmp/shared-combat-verification` 복사본에서 검사한다. 설치된 동일 패키지를 로컬 경로로 참조하며 원본 Packages는 변경하지 않는다.

- 컴파일: 통과.
- 최초 EditMode: 51/52. Leesc의 `InteractionRangeTests`가 콜라이더 내부 거리 0을 금지하는 잘못된 검사를 포함했다. 의도한 중심 거리와 표면 거리 비교로 테스트만 수정했다.
- 최초 그래픽 PlayMode: 50/52. 새 테스트 두 개의 기대값을 실제 프로필 감소량과 2페이즈 진입 후 HP에 맞췄다.
- 최종 EditMode: **52/52 통과**, 실패·건너뜀 0 — `Logs/PuriLeescEditModeFinal.xml`.
- 최종 그래픽 PlayMode: **53/53 통과**, 실패·건너뜀 0 — `Logs/PuriLeescPlayModeFinal.xml`.
- 원본과 검증 복사본의 추적 대상 Assets·ProjectSettings **413개 파일** SHA-256 일치, 누락·불일치 0. 원본 Packages 변경 없음.
- C#·asmdef·변경 문서의 공백 오류 검사 통과. Leesc에서 가져온 Unity 직렬화 파일의 기존 공백은 보존했다.

새 회귀 검사는 세 적 공격의 피해 기록·교리 1회 발행·오래된 ID 거부, 피격 즉시 회복 후 피해 기여 보존, 슬로우만 적용된 마법진, 실제 플레이어의 일반 적 믿음 판정, 보스에 대한 시작 범위·방향 및 빗나감, 늦게 진입한 대상의 적중 신호, 콤보 4타의 서로 다른 ID와 복수 콜라이더 중복 방지, 전진 도달 범위, 취소 후 지연 피해 차단을 포함한다. PlayerTEST·ShopTest의 기존 플레이어 전투 신호 보충 및 상점 모달 열기·닫기도 검사한다.

최종 결과와 주요 구현 소스 SHA-256은 [검증 요약](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Docs/PuriLeescMergeVerification/test-summary.json)에 기록했다. 자동 실행 검증이며 사람의 키보드 수동 플레이나 Profiler 성능 측정을 의미하지 않는다.
