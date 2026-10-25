# Puri 작업 범위 코드 리뷰

- 작성일: 2026-10-25
- 기준: `Puri` 브랜치, HEAD `a56c5f0` 및 현재 미커밋 변경
- 선별 근거: 현재 Git 사용자 `xzwpuri`의 커밋·줄별 작성 이력과 최근 동일 프로젝트 작업 기록
- 대상: 기존 코드 리뷰 11건 중 본인 작업 범위에 해당하는 9건 — P2 7건, P3 2건
- 상태: 9개 항목 구현 반영. 수정 결과와 검증은 [공통 전투 정리 기록](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Docs/CommonCombatRefactor_2026-10-25.md)을 참조한다.
- 이 문서의 문제 설명은 수정 전 정적 리뷰 기록이다. 아래 소스 링크는 수정 후 파일 위치로 갱신했다. 최종 검증 결과는 수정 기록에서 관리한다.

기존 리뷰의 번호를 유지했다. 교리 전용 피해 인터페이스와 교리 이동 배율을 다룬 기존 2·3번은 다른 작성자의 구현에 해당해 제외했다. 보스·일반 적의 공용 타격 정보처럼 여러 기능에 걸친 항목은 본인이 구현한 수신·판정 경로의 개선 범위만 기재했다. 미커밋 잔상 변경은 커밋 작성자로 귀속하지 않고, 최근 본인 요청으로 진행한 작업 범위로 포함했다.

| 기존 번호 | 우선순위 | 항목 | 본인 작업 근거 |
| --- | --- | --- | --- |
| 1 | P2 | 일반 적·보스 타격 식별 처리 불일치 | `8f114b5`, `dc43ddc`의 타격 수신 코드 |
| 4 | P2 | 공격 취소 처리 중복과 돌진 정리 누락 | `b52f2c9` 및 일반 적 공격 코드의 작성 이력 |
| 5 | P2 | 반복 물리 검색과 결과 배열 할당 | `b52f2c9`, `dc43ddc`의 형상 검사·물리 유틸리티 |
| 6 | P2 | 숨겨진 디버그 주석 UI 생성·갱신 | `dc43ddc`의 보스 표시 코드 |
| 7 | P2 | 적 생성마다 OS 폰트 탐색·생성 | `b52f2c9`의 믿음 UI 코드 |
| 8 | P2 | 돌진 잔상의 과도한 생성·파괴 빈도 | `dc43ddc`의 보스 돌진 및 최근 미커밋 공용 잔상 작업 |
| 9 | P2 | 보스용 플레이어 공격 기록 누적 | `6580ad8`, `dc43ddc`의 공격 신호 어댑터 |
| 10 | P3 | 일반 적·보스의 동일 UI 레이아웃 중복 | `b52f2c9`, `dc43ddc`의 UI 구성 코드 |
| 11 | P3 | 패턴 추첨의 물리 적격 검사 반복 | `dc43ddc`의 패턴 선택 코드 |

P2는 기능 일관성·상태 정리·반복 비용 때문에 우선 개선할 항목이며, P3는 이후 정리할 유지보수·중복 실행 항목이다. 실제 프레임 시간이나 메모리 증가량을 측정한 우선순위는 아니다.

## 1. [P2] 일반 적과 보스의 타격 식별 처리 불일치

**위치**

- [BossCombatController.cs:205](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Boss/BossCombatController.cs:205)
- [EnemyUnit.cs:330](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Enemy/Core/EnemyUnit.cs:330)
- [PlayerCombatEvents.cs:38](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Combat/PlayerCombatEvents.cs:38)

**수정 전 문제와 영향**

일반 적은 `DamageInfo.PlayerAttack`의 공격 ID와 `HitIndex`로 중복 타격을 거부한다. 보스의 `TakeDamage`는 이 정보를 읽지 않으며, 별도 `BossPlayerCombatEvents.TryApplyHitAndPublish` 경로를 사용할 때만 외부에서 중복을 막는다. 공용 `IDamageable` 경로로 동일한 타격 정보를 두 번 전달하면 일반 적과 보스의 피해 수락 횟수가 달라진다. 일반 적용 타격 정보만 전달하는 공격은 보스 믿음 판정용 공격 시도·적중 신호도 발생시키지 않는다.

코드상 확인 예시는 전투 중 HP 500, 유지 피해 배율 0.6인 보스에게 동일한 `PlayerAttackContext`에서 만든 기본 피해 10의 `DamageInfo`를 두 번 전달하는 경우다. 보스 HP가 494, 488로 두 차례 감소하는 경로가 있다. 이 예시는 Unity에서 재현 실행한 결과가 아니다.

**개선 요구사항**

보스 수신 경로도 공용 공격 ID·타격 순번을 검사하게 하고, 보스 전용 공격 신호를 같은 타격 정보와 연결한다. 기존 보스 패턴·페이즈·공유 믿음 제어는 유지한다. 플레이어 공격 조작이나 무기 기능을 추가할 필요는 없다.

**완료 확인**

- 같은 공격 ID·타격 순번의 재전송을 일반 적과 보스가 모두 거부한다.
- 다른 타격 순번은 별도 타격으로 처리한다.
- 보스의 시도·적중 신호가 중복 피해나 믿음 중복 감소를 만들지 않는다.
- 취소·페이즈 전환·재시작 이전 공격의 무효화 계약을 유지한다.

## 4. [P2] 공격 취소 처리 중복과 돌진 정리 누락

**위치**

- [EnemyAttack.cs:246](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Enemy/Core/EnemyAttack.cs:246)
- [EnemyAttack.cs:250](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Enemy/Core/EnemyAttack.cs:250)
- [EnemyAttack.cs:276](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Enemy/Core/EnemyAttack.cs:276)
- [DogChargeAttack.cs:249](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Enemy/Dog/DogChargeAttack.cs:249)

**수정 전 문제와 영향**

`CancelWindup`, `CancelForOwnerDeath`, `OnDisable`이 코루틴 중지·경고 제거·결과 확정을 각각 구현한다. 이 중 `CancelForOwnerDeath`만 파생 공격 정리 훅을 호출한다. 개가 돌진 중 비활성화되면 코루틴은 중단되지만 `isCharging`, 수평 속도, 플레이어와의 물리 충돌 설정을 정리하는 코드가 실행되지 않는다. 재활성화 또는 컴포넌트 비활성화 시 공격 상태와 실제 물리 상태가 어긋날 수 있다.

**개선 요구사항**

취소 사유를 받는 공통 종료 처리와 파생 공격 정리 훅을 하나로 통합한다. 쿨다운과 AI 복귀처럼 사유마다 다른 부분만 별도로 결정한다. 현재 취약 상태의 행동 규칙은 유지한다.

**완료 확인**

- 준비·돌진·회복 각각에서 사망, 대상 소실, 개체 비활성화, 공격 컴포넌트 비활성화를 확인한다.
- 돌진 플래그·수평 속도·충돌 설정이 일관되게 정리된다.
- 취소 결과를 한 번만 보고하고 재활성화 후 정상 공격을 시작한다.

## 5. [P2] 반복 물리 검색과 결과 배열 할당

**위치**

- [BossAttackRecord.cs:28](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Boss/BossAttackRecord.cs:28)
- [EnemyAttack.cs:270](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Enemy/Core/EnemyAttack.cs:270)
- [BossCombatPhysics.cs:26](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Boss/BossCombatPhysics.cs:26)
- [BossAttackExecutor.cs:88](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Boss/BossAttackExecutor.cs:88)

**수정 전 문제와 영향**

대상 플레이어 콜라이더를 알고 있지만 `OverlapBoxAll`·`OverlapCircleAll`로 주변 검색 결과 배열을 만든 뒤 해당 대상 하나를 찾는다. 돌진·투사체·지면 파동도 반복적인 `CastAll`·`RaycastAll`을 사용한다. 보스 근접 활성 구간은 위협 관찰과 적중 검사에서 동일 형상을 같은 물리 틱에 두 번 검색한다. 검색과 배열 할당이 공격 수·검사 빈도에 비례해 늘어난다.

**개선 요구사항**

플레이어와 장애물의 검색 마스크를 구분하고 결과 버퍼를 재사용한다. 같은 틱의 동일 형상 검사 결과를 위협 기록과 적중 판정이 공유하도록 한다. 회전 사각형·원형·이동 구간의 정확도를 유지하며, 버퍼 포화 시 누락 처리도 정한다.

**완료 확인**

- 근접 앞·뒤·공중 경계와 돌진·투사체의 얇은 벽 차단이 동일하게 동작한다.
- 지면 파동의 경로와 회피 결과를 유지한다.
- 같은 전투 조건에서 물리 쿼리 호출 수와 GC Alloc을 전후 비교한다.

## 6. [P2] 숨겨진 디버그 주석 UI 생성·갱신

**위치**

- [BossAttackVisual.cs:64](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Boss/BossAttackVisual.cs:64)
- [BossWorldCue.cs:14](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Boss/BossWorldCue.cs:14)
- [BossWorldCue.cs:44](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Boss/BossWorldCue.cs:44)
- [BossCloudPlatform.cs:28](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Boss/BossCloudPlatform.cs:28)

**수정 전 문제와 영향**

공격 경고와 구름이 디버그 표시 여부와 무관하게 주석용 Canvas·Text·Image를 만든다. 이후 시간 문자열·라벨·막대를 계속 갱신하고 마지막에 `Canvas.enabled`만 끈다. 기본 플레이 화면에서도 숨겨진 디버그 표시를 위한 오브젝트 생성과 문자열 할당이 발생한다.

**개선 요구사항**

현재 주석 상태를 저장하되, 디버그 표시를 켰을 때 UI를 생성하고 갱신한다. F1 전환 직후 진행 중인 공격과 구름의 최신 값을 표시하도록 한다.

**완료 확인**

- 디버그를 켜지 않은 플레이에서는 주석 UI 생성·문자열 갱신을 생략한다.
- 경고·구름·가시 진행 도중 F1을 켜도 최신 남은 시간과 위치를 즉시 표시한다.
- 전환으로 공격 ID·피해·믿음·위험 범위가 바뀌지 않는다.

## 7. [P2] 적 생성마다 OS 폰트 탐색·생성

**위치**

- [BeliefView.cs:19](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Belief/BeliefView.cs:19)
- [BeliefView.cs:19](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Belief/BeliefView.cs:19)

**수정 전 문제와 영향**

각 적의 믿음 UI를 만들 때 OS 설치 폰트 목록을 다시 조회하고 같은 동적 폰트를 생성한다. 공용 캐시나 생성한 폰트의 정리 경로가 없다. 동일한 문구 스타일을 개체마다 준비해 생성 비용과 폰트 객체를 늘린다. 보스는 별도 정적 캐시를 사용하고 있어 폰트 준비 방식도 중복되어 있다.

**개선 요구사항**

일반 적과 보스가 공용 폰트 에셋 또는 한 번 생성한 폰트를 공유하게 한다. OS 폰트 fallback을 유지한다면 탐색 결과와 생성 폰트의 수명을 한 곳에서 관리한다.

**완료 확인**

- 여러 적을 생성해도 동일 폰트의 반복 탐색·생성을 피한다.
- 긴 한국어 문구와 기존 두 화면 크기의 가독성을 유지한다.
- 반복 생성·제거 후 폰트 객체 수가 계속 증가하지 않는지 확인한다.

## 8. [P2] 돌진 잔상의 과도한 생성·파괴 빈도

**위치**

- [DogChargeAttack.cs:125](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Enemy/Dog/DogChargeAttack.cs:125)
- [DogChargeAttack.cs:245](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Enemy/Dog/DogChargeAttack.cs:245)
- [BossAttackExecutor.cs:123](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Boss/BossAttackExecutor.cs:123)
- [CombatAfterimage.cs:37](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Combat/CombatAfterimage.cs:37)

**수정 전 문제와 영향**

개와 보스가 물리 틱마다 잔상 프리팹을 생성하고 짧은 수명 뒤 파괴한다. 현재 물리 간격 0.02초 기준으로 돌진 중 개체 하나당 초당 최대 50회 생성·파괴한다. 현재 코드에서는 양쪽 모두 `drawOutline:false`지만, 프리팹의 외곽선 자식 오브젝트는 함께 생성된다. 동시 돌진 개체 수가 늘어나면 같은 비용이 반복된다.

**개선 요구사항**

잔상 생성을 별도 시간 또는 이동 거리 간격으로 조절하고, 짧은 수명에 맞춘 소규모 재사용 풀을 적용한다. 외곽선을 사용하지 않는 잔상은 해당 자식 생성을 생략할 수 있게 한다.

**완료 확인**

- 개와 보스의 스프라이트 잔상·페이드·테두리 없는 표시를 유지한다.
- 페이즈 전환·취소·사망 시 잔상이 정상 회수된다.
- 동시 돌진 조건에서 생성·파괴 횟수와 GC Alloc을 전후 비교한다.

## 9. [P2] 보스용 플레이어 공격 기록 누적

**위치**

- [PlayerCombatEvents.cs:19](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Combat/PlayerCombatEvents.cs:19)
- [PlayerCombatEvents.cs:23](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Combat/PlayerCombatEvents.cs:23)
- [PlayerCombatEvents.cs:38](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Combat/PlayerCombatEvents.cs:38)

**수정 전 문제와 영향**

공격 시도는 `attempts`, 피해 적용 시도는 `appliedHitIds`에 계속 추가된다. 완료·취소·전투 세대 변경 때 삭제하지 않고 테스트 도구의 초기화에서만 비운다. 이전 세대 검사는 기록 사용만 거부하므로 오래된 ID와 대상 참조는 남는다. 컴포넌트가 오래 유지되는 환경에서는 공격 수에 비례해 저장량이 늘어난다.

**개선 요구사항**

공격 완료·취소·전투 종료에 기록 회수를 연결한다. 회수한 ID의 재전송은 거부해 기존 중복·이전 세대 차단을 유지한다. 일반 적 개체 수명 동안의 중복 방지 기록이나 보스의 최대 64개 결과 이력을 일괄 삭제하는 방식은 피한다.

**완료 확인**

- 다수의 시도·적중·실패 후 종료된 기록이 회수된다.
- 완료·취소된 ID의 재전송과 이전 전투 ID를 거부한다.
- 테스트 도구 없이 재시작해도 이전 기록이 계속 누적되지 않는다.

## 10. [P3] 일반 적·보스의 동일 UI 레이아웃 중복

**위치**

- [BeliefView.cs:13](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Belief/BeliefView.cs:13)
- [BeliefView.cs:13](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Belief/BeliefView.cs:13)
- [BossProjectSetup.cs:299](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Editor/BossProjectSetup.cs:299)
- [BossGameplayHud.cs:30](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Boss/BossGameplayHud.cs:30)

**수정 전 문제와 영향**

일반 적의 런타임 UI 생성과 보스의 에디터 UI 생성에 말풍선·꼬리·믿음 게이지·HP 게이지의 크기·색·앵커가 중복되어 있다. 같은 스타일을 변경할 때 두 구현을 각각 수정해야 한다. 화면 크기별 최소 가독성 보정은 보스에만 있어 표시 동작도 달라질 수 있다.

**개선 요구사항**

공용 표시 프리팹과 HP·믿음·문구를 받는 작은 뷰를 재사용한다. 일반 적과 보스의 데이터 연결만 각각 유지하고, 디버그 HUD의 별도 역할은 보존한다.

**완료 확인**

- 한 곳의 스타일 수정이 일반 적과 보스에 함께 반영된다.
- 긴 한국어 문구, 방향 전환, 빈 말풍선 숨김, 작은 창 가독성을 유지한다.
- 기존 UI 갱신·재생성 도구에서 중복 오브젝트가 생기지 않는다.

## 11. [P3] 패턴 추첨의 물리 적격 검사 반복

**위치**

- [BossCombatSettings.cs:193](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Boss/BossCombatSettings.cs:193)
- [BossCombatSettings.cs:200](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Boss/BossCombatSettings.cs:200)
- [BossCombatController.cs:433](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Boss/BossCombatController.cs:433)

**수정 전 문제와 영향**

`TryChoose`는 총 가중치 계산 시 적격 콜백을 호출하고 추첨 과정에서 다시 호출한다. 해당 콜백은 근접 Overlap, 돌진·투사체 Cast, 지면 Raycast를 수행한다. 응시 후 연결 공격이나 강화 중 유효 후보가 없으면 매 프레임 같은 선택을 재시도한다.

**개선 요구사항**

한 번의 추첨에서 유효 후보와 유효 가중치를 한 번만 계산해 재사용한다. 실행 불가 대기 상태의 재탐색에는 짧은 간격 또는 위치·상태 변화 조건을 적용한다.

**완료 확인**

- 동일 입력의 가중치 선택·반복 패널티·페이즈 제한을 유지한다.
- 막힌 경로를 제외하고 플레이어 이동 후 유효한 연결 공격을 선택한다.
- 한 추첨 안에서 같은 후보의 물리 적격 검사를 중복 실행하지 않는다.

## 작업 순서와 보존할 설계

권장 순서는 타격 식별·공격 취소의 일관성 확보(1·4), 반복 검색·UI·폰트·잔상·기록 수명 정리(5·6·7·8·9), 레이아웃·선택 중복 정리(10·11)다.

보스 전용 패턴 제어와 공유 믿음, 공격 세대·타격 ID, 믿음 콘텐츠와 판정 규칙의 분리는 필요한 동작을 담당한다. 기존 믿음 enum 값과 에셋 GUID도 유지해야 한다. 취약 진입 시 진행 공격을 취소한 뒤 취약 중에는 AI와 공격이 계속되는 현재 규칙, 테두리 없는 돌진 잔상, 플레이어 공격 기능 없이 더미 데이터로 검증하는 흐름을 보존한다.
