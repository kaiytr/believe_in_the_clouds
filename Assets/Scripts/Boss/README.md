# 보스 전투 코드

보스 패턴 구조, 믿음 규칙, 상시 구름과 번개, 컷씬 연결 지점은 [`Docs/BossPatternRework.md`](../../../Docs/BossPatternRework.md)에 정리했다. 이 폴더의 요약:

- `BossCombatController`: 전투 상태, 공유 믿음, 일반 공격 레인과 믿음 레인(각자 코루틴·패턴 ID·쿨타임), 예언, 전체 취소.
- 믿음은 한 종류만 유지한다. 이전 믿음의 효과와 판정이 끝난 뒤 쿨타임을 기다린다. 일반 공격은 계속 독립 실행한다.
- `BossAttackExecutor`: 3타 콤보, 대시, 투사체(플레이어 높이와 무관하게 조준), 충격파 본체 동작과 파동 추적.
- `BossCloudField` / `BossCloudPlatform` / `BossCloudLightning`: 상시 먹구름 2~3개, 이동 흰 구름 발판 2개, 이동 구름 위치를 따라가는 위험 사각형과 단발 번개.
- `BossLightningStrikeVisual`: 흰색·푸른색의 계단형 도트 낙뢰와 잔광. 피해 판정은 기존 Lightning에만 있다.
- `BossEncounterTrigger`: 아레나 진입 봉쇄. `AutoBeginEncounter=false`면 진입은 봉쇄와 구름 준비만 하고 전투는 `Boss.BeginEncounter()`로 시작한다.
- 설정은 `Assets/Data/Boss/BossCombatSettings.asset` 한 곳.

Unity 6000.3.5f2 / Test Framework 1.6.0. 보스 테스트: `BossCombatEditModeTests`, `BossPatternReworkEditModeTests`, `BossCombatPlayModeTests`, `BossPatternReworkPlayModeTests`, `BossStagePlayModeTests`.
