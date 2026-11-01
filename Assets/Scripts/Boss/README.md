# 보스 번개 대피 패턴

CloudBelief는 위쪽의 큰 먹구름과 그 아래 작은 흰 구름을 함께 만든다. 흰 구름은 충돌 발판이 아니다. 빨간 사각형은 번개 공격 구역, 청록색 사각형은 흰 구름 아래 보호 구역이다. 청록색 채움의 실제 월드 좌표와 판정 좌표는 같은 `AttackGeometry`를 사용한다.

`CloudWarning` 0.5초의 구름 생성 시간과 `CloudDuration` 3초의 대피 시간이 지나면 한 번만 번개가 발동한다. 발동 프레임에 Physics2D 변환을 동기화하고 실제 PlayerHealth.BodyCollider의 전체 bounds가 청록색 범위에 포함되는지 확인한다. 경계는 포함하며 부동소수점 오차 0.0001만 허용한다. 몸 일부가 범위 밖이거나 흰 구름 위에 있으면 보호되지 않는다. 발동 전에 한 번 들어갔다 나오는 것은 성공이 아니다.

대피 성공은 기존 공용 믿음 게이지를 20 감소시킨다. 공격 구역 밖에서는 피해를 받지 않지만 보호 구역 대피 성공 보상은 받지 않는다. 무적 접촉은 대피 성공으로 계산하지 않는다. 번개 피해는 18이고 발동 순간에만 판정한다. `LightningDuration` 0.45초는 연출 유지 시간이며 추가 피해 시간이 아니다.

설정은 `Assets/Data/Boss/BossCombatSettings.asset`에서 조절한다. 먹구름 크기는 12×1.4, 꼭대기는 지면에서 6, 흰 구름 크기는 3×0.7, 보호 높이는 2.8, 흰 구름의 가로 이동은 +2이다. CloudSpacing은 음수도 가능하다. 번개 선 간격 0.8과 두께 0.08도 설정에 있다. 먹구름 폭은 아레나 폭으로 제한되며 보호 구역이 들어가지 못하면 실행을 중단한다. 생성 시 지면과 영역을 고정하므로 플레이어를 추적하지 않는다. 높낮이가 다른 StageTest 지형에 통합할 때는 대피 위치의 실제 바닥과 접근 경로를 확인한다.

패턴 취소, 보스 비활성화/사망, 페이즈 전환, 방어력 저하, 플레이어 사망, 씬 언로드는 기존 generation과 소유 이펙트 루트 정리를 사용한다. 새 번개는 예약 콜백이나 충돌 이벤트로 피해를 가하지 않는다. 가시 구현은 독립적인 기존 진단/수명 테스트용으로 남지만 구름에서 호출하지 않는다.

충격파는 코드 기본값과 저장된 설정 모두 6 → 9 월드 단위/초로 변경했다. 7 단위 이동 시간은 약 1.17 → 0.78초다. 예고 0.65초와 크기 0.6×0.6은 유지한다. 이동 구간 전체 swept 판정과 양쪽 파동이 공유하는 단일 BossAttackRecord의 접촉 제한을 그대로 사용한다. PlayMode 테스트는 실제 점프 회피, 지상 피격, 속도 200에서 폭 0.08 콜라이더의 충돌 및 중복 피해 방지를 확인한다.

전용 프리팹 갱신 메뉴: Tools/Boss/Update cloud lightning prefabs only. 씬이나 공용 UI를 재생성하지 않는다. 기존 구름 프리팹 GUID와 루트 컴포넌트 참조는 유지한다.

Unity 6000.3.5f2 / Test Framework 1.6.0. 전용 worktree에서 다음 명령으로 실행한다. 그래픽 캡처 테스트의 출력은 무시되는 Logs/BossCombatVerification에 저장한다.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Unity.exe' -batchmode -nographics -projectPath 'C:/Users/PC/Documents/GitHub/believe_boss' -runTests -testPlatform EditMode -testFilter BossCombatEditModeTests -testResults 'C:/Users/PC/Documents/GitHub/believe_boss/Logs/BossValidation/editmode.xml' -logFile 'C:/Users/PC/Documents/GitHub/believe_boss/Logs/BossValidation/editmode.log'
& 'C:/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Unity.exe' -batchmode -projectPath 'C:/Users/PC/Documents/GitHub/believe_boss' -runTests -testPlatform PlayMode -testFilter BossCombatPlayModeTests -testResults 'C:/Users/PC/Documents/GitHub/believe_boss/Logs/BossValidation/playmode.xml' -logFile 'C:/Users/PC/Documents/GitHub/believe_boss/Logs/BossValidation/playmode.log'
```

StageTest 소유 세션은 최신 보스 스크립트, BossCombatSettings, WhiteCloud/BlackCloud 프리팹 변경을 함께 통합해야 한다. 기본 씬 인스턴스가 동일한 보스 설정 에셋을 참조하면 씬 변경 없이 적용된다. 다른 설정 사본이나 프리팹 override가 있다면 새 설정과 속도 9를 반영한다. StageTest 씬 및 생성 코드는 이 작업에서 수정하지 않는다.

작업 기준: Puri / ac9dc7f0a8e80085ed4ef329b12050ce6ca19343. 전용 worktree는 C:/Users/PC/Documents/GitHub/believe_boss, 브랜치는 codex/boss-patterns이다.

2026-11-01 최종 검증: BossCombatEditModeTests 24/24, 그래픽을 켠 BossCombatPlayModeTests 40/40 통과. 1600×900 및 1024×576 캡처를 열어 먹구름/흰 구름, 보호 영역, 카운트다운, 번개를 확인했다. 결과 XML과 로그는 Logs/BossValidation, 캡처는 Logs/BossCombatVerification에 남겼다. StageTest 플레이 검증은 실행하지 않았다.
