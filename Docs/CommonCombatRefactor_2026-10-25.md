# 일반 적·보스 공통 전투 로직 정리

- 기준일: 2026-10-25
- 대상: Puri 코드 리뷰의 9개 항목(기존 번호 1·4·5·6·7·8·9·10·11)
- 상태: 9개 항목 구현 반영 및 Unity 회귀 검증 완료
- 보존: 기존 미커밋 변경, 취약 중 AI·공격 지속, 보스 패턴·페이즈 규칙, 평면 경고와 테두리 없는 잔상

## 반영 결과

| 기존 번호 | 수정 내용 | 공통 경로 / 보스에 남긴 역할 |
| --- | --- | --- |
| 1 | 공격 ID·타격 순번의 중복 판정을 공격 컨텍스트로 통합. 직접 DamageInfo 수신과 어댑터 적중이 같은 기록을 사용한다. | PlayerAttackContext와 PlayerCombatEvents 공용. 보스는 전투 세대 유효성 및 믿음 신호 해석 담당. |
| 4 | 준비 취소·사망/대상 소실·비활성화가 CancelAttack 한 경로를 사용한다. 개 돌진 플래그·속도·충돌·잔상도 같은 정리 훅에서 회수한다. | EnemyAttack의 공통 취소 처리와 OnAttackCancelled. 재활성화 시 플레이어 충돌 무시도 복구. |
| 5 | Overlap/Cast/Raycast 결과를 성장 가능한 List 버퍼로 재사용하고 대상 레이어를 필터링한다. 근접·가시의 위협 관찰과 적중이 같은 검색 결과를 사용한다. | AttackGeometry·CombatPhysics 공용. BossCombatPhysics에는 보스 장애물 제외와 지면 경로 규칙을 유지. |
| 6 | 숨김 상태에서는 경고·구름의 World cue UI를 생성하거나 시간 문자열을 구성하지 않는다. F1 이벤트로 현재 상태를 즉시 표시한다. | 실제 DangerIndicator는 유지. 보스 디버그 주석만 지연 생성. |
| 7 | OS 폰트 목록과 생성 폰트를 공유·캐시하고 런타임 세션 종료 시 정리한다. | CombatUiResources를 일반 적·보스·디버그가 사용. |
| 8 | 0.05초 간격의 공통 잔상 방출과 개체 소유의 재사용 풀을 적용했다. 외곽선 자식을 프리팹과 생성 도구에서 제거했다. | CombatAfterimageEmitter·CombatAfterimage 공용. 취소 시 활성 잔상 회수, 개체 제거 시 풀 제거. |
| 9 | 플레이어 컴포넌트에 계속 쌓이던 attempts/appliedHitIds를 제거했다. 공격 컨텍스트가 자신의 타격·대상 기록만 소유하고 Dispose로 종료한다. | PlayerCombatEvents는 신호 전달만 담당. 테스트 입력도 사용 후 Dispose, 더미 도구는 재전송용 마지막 공격 하나만 유지. |
| 10 | 일반 적 런타임 UI 빌더와 보스 UI 빌더의 체력·믿음·말풍선 레이아웃을 하나의 프리팹과 뷰로 통합했다. | CombatantHudView 공용. BeliefView와 BossGameplayHud는 데이터 연결만 담당하며 보스는 플레이어 HP 표시를 추가. |
| 11 | 추첨 한 번에서 후보별 적격 판정을 한 번만 수행하고 가중치 버퍼·콜백을 재사용한다. 실행 불가 재탐색은 0.1초 간격으로 제한했다. | 보스 패턴 선택 규칙은 유지. 별도 공격·가중치 프레임워크를 추가하지 않았다. |

## 주요 파일

- [공용 공격 정보](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Combat/PlayerAttackContext.cs)
- [공용 플레이어 전투 신호](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Combat/PlayerCombatEvents.cs)
- [공용 형상](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Combat/AttackGeometry.cs) / [물리 검색](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Combat/CombatPhysics.cs)
- [공용 표시 뷰](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Combat/CombatantHudView.cs) / [표시 프리팹](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Resources/Combat/CombatantHud.prefab)
- [공용 폰트·리소스](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Combat/CombatUiResources.cs)
- [공용 잔상 방출·재사용](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Scripts/Combat/CombatAfterimageEmitter.cs)
- [공용 에셋 갱신 도구](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Assets/Editor/CommonCombatProjectSetup.cs)

BossPlayerCombatEvents는 PlayerCombatEvents로 옮겼고 기존 스크립트 GUID는 유지했다. BossTest의 직렬화된 클래스 이름과 테스트·설정 도구 참조도 갱신했다. BossAttackGeometry는 공용 AttackGeometry로 옮겼다. 보스의 여러 타격·지면 파동·페이즈 진행은 기존 단일 공격용 EnemyUnit에 강제로 결합하지 않았다.

## 공격 정보 사용 계약

공격 하나마다 컨텍스트를 만들고, 실제 다단 타격은 hitIndex를 증가시킨다. 같은 공격을 여러 콜라이더에서 검사할 때는 같은 컨텍스트와 타격 순번을 전달한다.

```csharp
using var attack = new PlayerAttackContext(player.gameObject, PlayerAttackKind.Direct);
attack.Begin(target); // 공격 시작 시 대상·전투 세대를 고정하고 시도 신호 발행
attack.TryHit(target, 10f, hitIndex: 0);
// 같은 target과 hitIndex 0의 재전송은 거부된다.
attack.TryHit(target, 10f, hitIndex: 1); // 실제 두 번째 타격
```

일반 적·보스 모두 IDamageable 대상에 대해 위 경로를 사용한다. 직접 수신 경로에서는 `receiver.TakeDamage(attack.Damage(amount, hitIndex))`를 사용할 수 있다. 수신 코드가 중복 차단과 실제 피해 수락 뒤 적중 신호를 처리한다. 세대 고정이 필요한 지연 공격은 발동 시점에 Begin을 호출한다. 공격 완료·취소 시 Dispose하며, 지속 공격은 마지막 실제 타격까지 컨텍스트를 유지한다.

이전 더미 어댑터와 비슷하게 `PlayerCombatEvents.PublishAttackAttempt(targetGameObject)`도 컨텍스트를 반환한다. 반환한 컨텍스트를 TryApplyHitAndPublish에 전달할 수 있다. 별도의 실제 피해 경로를 사용하는 어댑터에만 PublishHitConfirmed를 사용한다. 피해 시도/적중 신호 자체는 피해를 추가하지 않는다. DamageOverTime·Summon은 적중형 믿음 신호를 발행하지 않는다.

## 프리팹과 설정 도구

- 일반 적은 Resources/Combat/CombatantHud를 생성하고 보스는 같은 프리팹 인스턴스를 사용한다.
- 체력·믿음·말풍선 색과 배치는 공용 뷰·프리팹에서 한 번 관리한다. 캐릭터 방향·크기에 대한 보정과 작은 창의 최소 가독성도 공유한다.
- `Tools > Combat > Upgrade shared HUD and afterimages`는 공용 HUD를 준비하고 보스 표시를 다시 연결하며 잔상 외곽선을 제거한다. BossTest 씬을 재생성하지 않는다.
- 잔상은 방출 간격 기준 최대 초당 20회이며, 수명이 끝나면 재사용한다. 실제 Instantiate 횟수는 풀 워밍업 후 방출 횟수와 함께 증가하지 않는다. 개체별 비활성 잔상 보관 상한은 16개다.

## 검증

Unity 6000.3.5f2를 사용했다. 원본 프로젝트의 UnityLockfile이 다른 프로세스에서 사용 중이어서 .utmp/shared-combat-verification 복사본에서 실행했다. 복사본은 원본에 설치된 동일 패키지를 로컬 경로로 참조한다. 원본 Packages 설정과 편집 세션은 유지했다.

- EditMode 전체: 46/46 통과, 실패·건너뜀 0 — Logs/SharedCombatEditMode.xml.
- 최초 그래픽 PlayMode 전체: 43/44 통과. 기존 일반 적 통합 테스트가 수동 씬의 비활성 적 및 앞서 끝난 공격 결과를 전제로 하던 문제를 발견했다.
- 해당 테스트는 실제 적 세 종류를 준비하고 공격 시나리오를 순차 실행하도록 수정했으며, 대상 재검증 1/1 통과 — Logs/SharedCombatLegacyFixture.xml.
- 최종 그래픽 PlayMode 전체: 44/44 통과, 실패·건너뜀 0 — Logs/SharedCombatPlayModeFinal.xml.

추가 검사는 공용 컨텍스트의 직접/어댑터 중복 타격 차단·다단 타격·보스 이전 세대 거부, 돌진 중 공격 컴포넌트와 개체 비활성화 후 정리·재사용, 80개 콜라이더 검색 및 회전 형상, 공용 HUD·폰트 참조, 숨김 상태 World cue 0개와 F1 표시 복원, 잔상 재사용 및 취소 회수, 후보별 적격 콜백 1회를 확인한다. 잔상 검사는 24개 방출 구간에서 프리팹 신규 생성 증가가 6개 이하임을 검사한다.

실제 프리팹 렌더 캡처에서 1024×576의 일반 적 긴 한국어 문구와 보스 구름·기본 UI를 확인했다. 화면은 자동 테스트에서 상태를 고정해 촬영했으며 사람의 키보드 플레이 완료를 의미하지 않는다. 전후 전체 프레임 시간·GC Alloc의 Profiler 수치를 측정한 결과는 포함하지 않는다.

소스·공용 HUD·보스 프리팹·잔상 프리팹·설정·테스트 씬 등 103개 파일이 최종 검증 복사본과 동일함을 SHA-256으로 확인했다. [검증 요약과 해시](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Docs/CommonCombatVerification/test-summary.json)를 함께 남겼다.

화면 확인: [보스 기본 UI](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Docs/CommonCombatVerification/verified-boss-gameplay-1024x576.png), [일반 적 긴 문구](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Docs/CommonCombatVerification/verified-enemy-phrases-1024x576.png), [보스 디버그 UI](C:/Users/1-3/Documents/UnityProjects/believe_in_the_clouds/Docs/CommonCombatVerification/verified-boss-debug-1024x576.png).
