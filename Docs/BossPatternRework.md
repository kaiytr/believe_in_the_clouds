# 보스 패턴 전면 수정 (세션 1, `codex/boss-patterns`)

2026-11-05 후속 요청 반영: 일반 공격과 믿음은 독립 실행하되, **믿음은 효과·예언 판정·파동이 끝날 때까지 한 종류만 유지**한다. 이전 믿음이 남아 있는 동안 다른 믿음을 동시에 발동시키지 않는다. 새 구름 에셋과 번개 표현은 `Docs/BossPresentation_2026-11-05.md`를 따른다.

기준 커밋 `3ac32d0`. 구현 커밋 `0913af3`(런타임·설정 에셋), 테스트 커밋 `dd2859c`. 이 문서는 보스 전용 소유 범위(`Assets/Scripts/Boss/**`, `Assets/Data/Boss/**`, 보스 테스트)의 변경 계약이다. 밸런스 수치는 최종 확정값이 아니다.

## 1. 구조: 일반 공격 레인과 믿음 레인

기존에는 일반 공격 3종과 믿음 3종을 한 선택기에서 골라 직렬로 실행했다. 이제 두 레인이 서로 다른 코루틴·패턴 ID·쿨타임·사용 이력을 가진다.

| 구분 | 일반 공격 레인 | 믿음 레인 |
|---|---|---|
| 풀 | 3타 콤보, 대시, 투사체 (1·2페이즈 동일) | 1페이즈: 공격 예언, 먹구름, 화상 강화 / 2페이즈: 공격·회피 예언, 공격 금지 예언, 충격파 |
| 선택 | 기존 `PhaseOnePool`/`PhaseTwoPool` 중 일반 공격 항목, 기존 가중치와 반복 완화 | 같은 풀의 믿음 항목, 별도 사용 이력 |
| 간격 | `PatternRecovery` 0.65초 + 공격 내부 `Recovery` 0.45초 (현재 값 유지) | `BeliefInitialDelay` / `BeliefCooldown` |
| 상태 | `IsRegularRunning`, `PatternId` | `IsBeliefProcessing`, `BeliefPatternId` |

풀 자산(`PhaseOnePool`/`PhaseTwoPool`)은 형식을 바꾸지 않았다. 실행 시 항목 종류(`BossPatternSelection.IsRegular`)로 두 레인에 나눈다. 풀 검증은 풀마다 일반 공격 1개 이상과 믿음 1개 이상을 요구한다.

### 새 설정 (`BossCombatSettings`, 저장 에셋에도 반영)

| 설정 | 초깃값 | 의미 |
|---|---|---|
| `BeliefInitialDelay` | 3초 | 전투 시작, 페이즈 전환, 취약 진입 등 **전체 취소 시점**부터 첫 믿음이 선택 가능해질 때까지의 대기 |
| `BeliefCooldown` | 3초 | 직전 믿음의 **효과와 판정이 모두 끝난 시점**부터 다음 믿음 선택까지. 화상 강화가 기록된 진행 공격·투사체도 끝나야 다른 믿음을 선택한다 |
| `ProphecyReactionTime` | 1초 (최소 1) | 관찰형 예언이 발동 후 최소로 보장하는 반응 시간 |
| `BurnWindow` | 10초 | 화상 강화 유지 시간 |
| `WhiteCloudCount` | 2 | 이동 발판 흰 구름 수 |
| `StormCloudSpeed` / `WhiteCloudSpeed` | 0.7~1.6 / 0.8~1.5 | 무작위 좌우 이동 속도 범위 (월드 단위/초) |
| `CloudTurnInterval` | 1.5~4초 | 방향·속도를 다시 뽑는 간격 |
| `StormCloudSize` | 11×2 | 먹구름 최소 크기. 실제 폭은 `max(이 값, 아레나폭/개수×StormCloudCoverage)` |
| `StormCloudCountRange` / `StormCloudCoverage` / `StormCloudWander` | 3~4 / 1.4 / 1.5 | 먹구름 개수, 폭 여유 배율, 칸 중심에서 벗어날 수 있는 거리. 구름마다 자기 칸 주변에서만 움직여 아레나 가로가 계속 채워진다 |
| `WhiteCloudVisualExtra` | 0.25 | 흰 구름 그림만 윗면 위로 더 그리는 높이(장식). 착지·보호 판정은 `CloudSize` 기준 |
| `CloudTopHeight` | 8 | 보스 카메라가 없는 전투의 먹구름 높이 기본값. 보스 아레나에서는 전투 화면 위쪽 여백 0.7유닛을 기준으로 배치 |
| `CloudSize` | 3×0.55 | 흰 구름 크기(세로는 그림 원본 비율에 가깝게) |
| `WhiteCloudTopHeight` | 2.66 | 흰 구름 윗면(착지면) 높이, 바닥 기준 |
| `WhiteCloudSurfaceThickness` | 0.14 | 단방향 착지 콜라이더 두께 |
| `ReferenceJumpApex` / `WhiteCloudLandingMargin` / `ReferencePlayerHeight` | 2.96 / 0.3 / 2 | 윗면 높이 검증용 기준값 |

삭제한 설정: `StareDuration`, `SurpriseWindup`(응시 제거), `BurnPreparation`(화상 준비 없음), `CloudEdgePadding`, `ShelterHeight`(흰 구름은 먹구름 안이 아니라 독립 이동 발판). `CloudWarning`은 `[Obsolete]` 미사용 필드로만 남겼다. 소유 범위 밖 `StageIntegrationPlayModeTests`가 아직 이 이름을 읽어 테스트 어셈블리가 컴파일되도록 하기 위한 것이다.

흰 구름 윗면 높이는 `PlayerMove`(jumpPower 15.64, 중력 9.81×4, 물리 0.02초)로 기본 점프 정점을 계산해 정했다(발 기준 2.961). 윗면 2.66은 정점−착지 여유 0.3 이하이고, 구름 밑면 2.11은 몸 높이 2 위라 구름 아래 보호 구역에 몸 전체가 들어간다. 위에 서도 몸(4.66)이 먹구름 밑면 아래다(대체 높이 8 기준 6). `ValidateSettings`와 EditMode 테스트가 이 관계를 검사한다.

## 2. 믿음 규칙

예언은 `BossProphecy`로 보스가 보관하고, 일반 공격은 시작 시 `BossRegularAttackContext`(3타 콤보 전체가 하나)에 화상 강화 여부와 연결된 회피 예언을 **한 번 기록**한다.

- **화상 강화**: 발동 즉시 `BurnWindow`(10초). 응시도 강제 일반 공격도 없다. 유지 중 새로 시작하는 일반 공격만 강화되며, 진행 중이던 공격은 시작 때 기록한 값을 유지한다. 실제 피해가 승인된 타격에서만 기존 `BossBurnEffect`로 화상을 건다. 강화 공격을 회피하면 기존대로 타격마다 믿음 감소. 강화 중에는 재발동되지 않는다.
- **공격 예언 (1페이즈)**: 플레이어의 공격 시도를 관찰한다. 발동 `ProphecyReactionTime` 이후 처음 시작하는 일반 공격의 직전에 닫으며, 그 안에 공격이 없었으면 믿음 감소. 그 전에 시작된 일반 공격은 닫는 기준이 아니다. 같은 종류가 미해결이면 다시 발동하지 않는다.
- **공격 금지 예언 (2페이즈)**: 같은 관찰 구간. 구간 중 공격하면 믿음을 한 번만 감소(여러 타격·중복 이벤트로 중복 감소 없음). 공격 신호는 기존 `PlayerCombatEvents` 신호(`AttackSignalMode`)를 쓰며 UI 클릭은 세지 않는다. 신호는 플레이어 스윙의 시작 범위에 보스가 있을 때만 발행되는 기존 규칙 그대로다.
- **공격·회피 예언 (2페이즈)**: 다음에 시작하는 일반 공격에 연결. 콤보 전체가 끝난 뒤 한 번 확정한다. 적중이 있으면 유지, 실제 위협을 벗어난 회피가 있으면 감소, 취소는 보상 없음. 실제 위협이 한 번도 없었던 공격(멀리 있음, 장애물로 막힘)은 예언을 소모하지 않고 다음 일반 공격을 기다린다.
- **충격파 (2페이즈)**: 선택되면 즉시 예약되어 새 일반 공격 시작을 막고, 진행 중인 일반 공격(콤보 전체)이 끝나면 보스 본체를 확보해 아래 휘두르기 + 충격파를 낸다. 일반 공격을 덧붙이지 않는다. 본체 동작(예고, 휘두르기, 회복) 동안만 새 일반 공격이 막히며, 구름은 계속 움직인다. 다른 믿음은 충격파 추적까지 끝난 뒤 선택한다. 파동 추적은 실행기가 따로 소유해 본체가 풀려도 이어진다. 예약은 중복되지 않는다.
- **먹구름**: 아래 3절.

믿음 감소 규칙(`ReduceBelief`)과 취약 진입 시 취소는 기존과 같다(취약 중에도 다음 패턴 선택 계속).

### 취소 규칙

전체 취소(`CancelCombat`)는 사망, 비활성화, 대상 소실, 페이즈 전환, 취약 진입, 테스트 취소에서 두 레인과 모든 예언, 화상 강화 창, 번개, 충격파 예약, 진행 공격·예고를 함께 정리한다. 취소된 공격은 `Cancelled`로 끝나 회피 성공이 아니다. 한 레인의 정상 종료나 한 레인 안의 예외는 다른 레인의 공격·판정을 지우지 않는다(예외만 두 레인을 모두 정리). 구름은 페이즈·취약에서 유지되고 사망, 비활성화, 아레나 해제에서만 지워진다.

HUD: 일반 공격의 예고 큐(제목·힌트·타이머)와 믿음의 말풍선은 분리했다. 현재 유지 중인 믿음 한 종류의 문구만 표시한다. 믿음 레인은 큐와 본체 모션을 건드리지 않고, 충격파만 본체를 확보한 동안 큐를 사용한다. 보스 몸체에 상태 틴트를 덧씌우지 않는다.

## 3. 상시 구름과 번개

`BossCloudField`(보스 오브젝트에 런타임 부착)가 먹구름 2~3개와 흰 구름 2개를 가진다.

- 먹구름: 장식, 보스 전투 카메라 프레임의 위쪽에서 0.7유닛 아래에 배치한다. 전투 카메라 중심은 기존보다 1유닛 낮다. 아레나 카메라가 없는 전투에서는 월드 `CloudTopHeight`를 쓴다. 흰 구름은 단방향 착지 발판(키네마틱 `Rigidbody2D` + 얇은 `PlatformEffector2D`, `PlatformLayer`). 둘 다 아레나(`PatternXLimits`) 안에서 무작위 좌우 이동. 겹쳐도 된다.
- 공용 `PlayerMove`는 수정하지 않았다. 흰 구름이 위에 선 플레이어를 직접 태운다(발이 윗면에 닿고 상승 속도가 아닐 때 이동량을 그대로 더함). 아래에서 올라올 때 지면 판정이 얇은 면을 바닥으로 읽는 것을 막으려고, 몸이 윗면 위에 있을 때만 면의 레이어를 `PlatformLayer`로 바꾼다. 보스 몸체는 면과 충돌을 무시하고, 보스 장애물 판정은 구름을 제외한다.
- 번개: `CloudBelief` 발동 시 현재 구름 위치 기준 위험 사각형(먹구름 범위의 합집합에서 흰 구름 아래 보호 구역을 뺀 것)을 예고하고, 그 사각형은 매 프레임 구름을 따라간다. `CloudDuration`(3초) 뒤 그 순간의 같은 사각형으로 한 번 판정한다. 예고와 판정은 같은 기하(`BossCloudField.ComputeDangerRects`)를 쓴다.
- 보호: 몸 전체가 하나의 흰 구름 아래 보호 구역(바닥~구름 밑면)에 들어와야 한다(기존 `Contains` 기준). 흰 구름 위는 보호되지 않는다. 먹구름이 겹쳐도 보호는 무효화되지 않고, 번개 한 번당 `BossAttackRecord` 하나라서 피해·피격 이벤트·믿음 결과도 한 번이다.
- 보상: 보호 구역에 있었거나, 위험에 들어 있다가 벗어난 경우에만 믿음 감소. 처음부터 범위 밖이면 보상 없음.
- 번개 후에도 구름은 남는다. 번개 시각 효과는 `LightningDuration` 뒤 지워진다.

## 4. 공개 연결 지점 (컷씬 통합용)

```csharp
BossCombatController boss;
boss.PrepareArenaVisuals();     // 구름만 준비. 멱등. 전투·접촉 피해·패턴·번개 없음. 반환: 준비됨 여부
boss.ReleaseArenaVisuals();     // 구름·번개 제거
boss.BeginEncounter();          // 기존 전투 시작 진입점 (한 번만 유효)
boss.Clouds                     // BossCloudField: StormClouds, WhiteClouds, SetMotionEnabled(bool)

BossEncounterTrigger arena;
arena.AutoBeginEncounter = false;   // 기본 true. 직렬화 필드 autoBeginEncounter
arena.ArenaEntered                   // 몸 전체 진입, 봉쇄, 구름 준비 직후 한 번
arena.HoldingForIntro                // 봉쇄·구름 준비는 끝났고 전투는 아직 시작 전
```

최종 흐름: 몸 전체 진입 → 봉쇄와 구름 준비(`ArenaEntered`) → 도입 컷씬 → 정상 종료 → `boss.BeginEncounter()` 한 번 → 전투. `AutoBeginEncounter`가 true(기본)면 기존처럼 진입 즉시 시작한다. 아레나에 바인딩된 보스는 `Start`의 `startOnAwake`로 자동 시작하지 않고, 접촉 피해는 `BeginEncounter` 전까지 억제된다. 컷씬 중 구름을 멈추고 싶으면 `boss.Clouds.SetMotionEnabled(false)`.

## 5. 직렬화 사본의 반영 범위

- `BossCombatSettings.asset`이 유일한 설정 원본이다. `Boss.prefab`, `BossEncounter.prefab`(Boss 프리팹 인스턴스), `BossTest.unity`의 보스(프리팹 인스턴스), 소유 범위 밖 `StageTest.unity`의 보스가 모두 이 에셋을 참조한다. 새 설정은 에셋 한 곳에만 있으며 씬과 프리팹을 다시 저장하지 않았다(YAML에 오버라이드 없음 확인).
- 새 컴포넌트(`BossCloudField`, 구름 면/리지드바디, 번개)는 런타임에 붙여서 프리팹 직렬화를 건드리지 않는다. `WhiteCloud`/`BlackCloud` 프리팹은 그대로(장식용 트리거 콜라이더)이다.
- `BossEncounterTrigger.autoBeginEncounter`는 새 직렬화 필드이며 기존 인스턴스에서는 기본값 true로 읽힌다.
- 공통 동기화 도구(`ProjectSceneSynchronization`, `StageTestProjectSetup`)는 실행하지 않았다. `StageTest`의 아레나는 보스 프리팹과 설정 에셋을 따라 새 동작을 받는다.

## 6. 확인한 것과 하지 않은 것

실행하지 않음: Unity 에디터가 이 프로젝트를 이미 열어 두고 있어 배치 모드 테스트를 돌릴 수 없고, 전체 EditMode/PlayMode 묶음도 지시에 따라 돌리지 않았다. **PlayMode 동작(구름 착지·탑승, 번개, 레인 상호작용)은 에디터에서 실제로 실행해 보지 않았다.**

실제로 한 확인:
- Unity와 같은 참조 DLL로 Roslyn 컴파일: 런타임 어셈블리 전체, PlayMode 테스트 어셈블리 전체, 보스 EditMode 테스트 두 파일 오류 없음(`StageIntegrationPlayModeTests`의 `CloudWarning` 사용에 대한 `[Obsolete]` 경고 1건만 있음).
- 점프 정점 계산(발 기준 2.961)을 Python과 EditMode 테스트 코드로 같은 방식으로 재현.
- 저장 에셋 YAML을 새 설정 이름에 맞춰 수정하고 삭제 항목의 잔여 참조(`grep`)가 없음을 확인.

추가·수정한 테스트(실행 전):
- `BossPatternReworkEditModeTests`: 저장 설정 계약, 흰 구름 윗면이 실제 `PlayerMove` 값에서 계산한 점프 정점 안인지, 두 레인의 선택·사용 이력 분리, 공격 기록의 화상 스냅샷.
- `BossPatternReworkPlayModeTests`(9개): 진행 중 일반 공격에 믿음 비소급, 1초 관찰 구간, 금지 예언 1회 감소, 콤보 전체 1회 회피 판정, 충격파 대기·예약·본체 확보, 다른 높이의 투사체, 구름 멱등·유지·해제, 겹친 먹구름 단발 피해와 보호, 구름 탑승과 아래에서의 지면 판정, 도입 대기 후 `BeginEncounter` 1회.
- `BossCombatEditModeTests`/`BossCombatPlayModeTests`: 사라진 응시·화상 준비·구형 번개 시나리오를 삭제하거나 새 API로 바꿈. 구 `FastProjectileAndDashStopAtThinWall...`은 벽 뒤의 플레이어를 노리는 투사체가 새 규칙(위협이 없으면 발사하지 않음)과 맞는지 에디터 실행으로 확인이 필요하다.

## 7. 통합 담당이 할 일과 제한

- `Assets/Tests/PlayMode/StageIntegrationPlayModeTests.cs`의 `StageCloudShelterProtectsThenExposedPlayerIsHitAndCancellationLeavesNoStrike`는 옛 번개 방식(구름 2개, 취소 후 구름 없음)을 가정해 새 동작에서 실패한다. 소유 범위 밖이라 수정하지 않았다. 새 방식은 `boss.Clouds`, `boss.Clouds.Lightning`을 쓰고, 취소 뒤에도 구름은 남는다.
- 컷씬: 위 4절 흐름대로 `AutoBeginEncounter=false`를 BossTest/StageTest 아레나에 설정하고 `ArenaEntered`에서 컷씬을 시작한다(해당 씬 저장은 통합 담당). 컷씬 중 플레이어 입력 잠금은 컷씬 쪽 책임이다.
- 밸런스: 믿음 간격 3초, 번개 3초, 먹구름 8폭 등은 모두 설정에서 조절한다.
- 알려진 제한: 얇은 단방향 면은 몸이 면보다 약간 위로 올라온 순간부터 지면 판정을 받는다(점프로 면을 통과하는 중 한 틱 겹칠 수 있음). 먹구름은 모두 같은 높이다. 충격파는 파동이 도달하는 동안 일반 공격 루프를 막지 않는다(본체 해제 후 이어짐).

## 8. 병렬 작업 통합 후 보완 (2026-11-05)

위 개별 세션의 미실행 기록 이후 통합에서 새 설정 EditMode 4개와 직접 관련된 PlayMode 6개를 실제 실행해 통과했다. 구형 StageIntegration 번개 테스트를 갱신했고, 실제 BossTest와 BossEncounter에 기존 BossEncounterTrigger를 유지하는 컷씬 연결을 적용했다. 도입 취소용 CancelIntro API를 추가했다. StageTest는 도입 없이 기존 자동 진입을 유지한다.

전체 보스 테스트를 실행한 것은 아니다. 실행한 케이스와 남은 수동 확인은 Docs/ParallelWorkIntegration_2026-11-05.md를 따른다. 컷씬 연결의 최종 기준은 Docs/CutsceneIntegrationGuide.md다.

## 8. 후속 수정 (`codex/boss-polish`)

- **구름 크기**: 흰 구름 세로 0.42→0.55, 먹구름 8×1.4→11×2. 흰 구름이 두꺼워진 만큼 착지 여유를 0.5→0.3으로 줄여 윗면을 2.66으로 올렸다(밑면이 몸 높이 2 위에 있어야 보호가 성립한다). 먹구름 시작 위치는 개수만큼 칸을 나눠 배치한다.
- **화살표 제거**: `DangerIndicator`의 방향 화살표 기능 전체와 호출(보스 대시·투사체, 신자 돌 던지기)을 삭제했다. 프리팹에 남은 방향 관련 직렬화 값은 무시된다.
- **대시 예고**: 보스는 몸 앞쪽 끝에서 시작해 `DashDistance`만큼만 그린다(뒤쪽과 몸 아래를 덮지 않음). 개 돌진 예고도 콜라이더 앞쪽 끝에서 시작하고, 위협 판정도 같은 사각형을 쓴다.
- **연속 베기**: 보스의 판정과 칼날 이펙트는 플레이어와 완전히 같다(변경 없음, 이펙트도 플레이어의 `PlaySwing` 그대로). 붉은 예고만 그 이펙트가 그리는 부채꼴(`DownFanImpactEffect.SwingFan`, 이펙트와 같은 중심 각도·반경·반각)로 바꿨다(`BossFanWarning`). 참고: 판정 히트박스(사각형)는 이 부채꼴보다 앞쪽으로 더 길어(1타 약 1.7 대 1.2) 부채꼴 밖 가장자리에서도 맞을 수 있다.
- **컷씬 말풍선**: `CutsceneBubbleView`가 전체 대사/선택지 목록에 맞춰 폭(300~560 설계 단위), 안쪽 여백 좌우 44·아래 58(꼬리 포함)·위 36과 높이를 정한다. 타이핑 중에는 크기가 변하지 않는다(전체 문장 기준). 선택지는 가장 긴 항목에 폭을 맞추고, 길면 줄바꿈하며 항목 높이가 늘어난다.
