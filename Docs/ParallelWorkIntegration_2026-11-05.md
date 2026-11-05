# 병렬 작업 통합 기록 — 2026-11-05

## 반영한 작업

공통 기준은 3ac32d0e458a7f8f5662f1c236106a9951e91db6이다. Puri에서 아래 세 작업을 병합했고 Git 파일 충돌은 없었다.

| 작업 | 전달 커밋 | 병합 커밋 |
| --- | --- | --- |
| 보스 패턴 | ad8ca1a9f4f7b3e65c8010c2f5ce45a495bbb9d3 | e8e5868 |
| 컷씬 말풍선 | 2ee2bae64045f7f211f6b7d7007cf83862ff33e9 | abdf115 |
| 제작 레벨 | 45c38e82f8d012cf5d9e6609c5f9f9461e8ef593 | 1996641 |

레벨은 최초 보고의 Mac bundle 대신 origin/codex/level-layouts에 올라온 동일한 최종 커밋을 fetch해 반영했다. 다른 작업 브랜치·Leesc·main의 내용은 추가 병합하지 않았다.

## 연결 및 수정

- 기존 BossEncounterTrigger를 유지했다. 초기 컷씬 설치 도구가 이 컴포넌트를 제거하던 방식을 바꿔, 아레나 범위·구름·봉쇄·카메라·사망 해제를 계속 같은 컴포넌트가 소유하게 했다.
- AutoBeginEncounter=false와 ArenaEntered를 이용해 몸 전체 진입 → 봉쇄/구름 준비 → 도입 컷씬 → 정상 종료 시 BeginEncounter 1회 순서를 연결했다.
- 도입 취소는 CancelIntro로 전투 전 구름과 봉쇄를 정리한다. 씬 종료 중 늦게 온 완료 신호로 전투를 시작하지 않는다.
- 실제 BossTest 씬은 BossEncounter 전체 인스턴스가 아니므로 실제 씬과 재사용 프리팹에 각각 연결했다.
- Tutorial의 기존 앵커·TutorialStoryBeliever를 사용해 시작 독백, 조우/첫 돌, 같은 신도 사망 후 독백을 연결했다.
- 서사 신도는 조우 전부터 물리·AI·공격·접촉 피해를 대기시키고, 조우 연출과 외부 단계 정상 종료 뒤 기존 전투 상태를 복원한다. 기존 신도·믿음 종류·보상을 새로 복제하거나 변경하지 않았다.
- 정지된 배우도 연출 이동 때 걷기 프레임을 표시하도록 이동 속도를 전달했다.
- 첫 돌은 기존 시각 전용 복제 방식으로 피해 없이 지나간다.
- 공격/대시 중 컷씬을 시작할 때 취소된 행동의 이동 잠금이 다시 복원되지 않게 잠금 저장 순서를 수정했다. 공격 중 처치 → 후속 독백 → 조작 복귀를 추가 확인했다.
- Tip UI는 만들지 않았다. tutorial.believer.first-stone.tip 연결 지점은 유지하며 제공자가 없으면 실제 전투로 통과한다.
- ShopTest의 엑셀 AB열에 기존 TemporaryStageExit와 일반 복귀 문 에셋을 사용하는 출구를 배치하고 authoredExit에 연결했다. 기존 ReturnToLobby·상점 완료 조건을 재사용한다.
- 보스 재생성 도구에 컷씬 설치 연결을 추가했다. 패턴 전용 테스트는 도입을 명시적으로 제외하고, 실제 도입은 통합 테스트에서 확인한다.
- StageIntegration의 구형 번개 기대값을 새 상시 구름/단발 번개/취소 후 구름 유지 규칙으로 갱신했다.

## 설치 도구

ParallelWorkIntegrationSetup.Install은 BossEncounter 프리팹, BossTest, Tutorial, ShopTest의 위 연결을 설치한다. 기존 컷씬/트리거/상점 출구를 재사용한다. 보스·튜토리얼 설치를 반복 적용한 결과 중복 없이 최종 테스트를 통과했다.

원본 Unity 에디터가 열려 있어 .utmp/BossStageVerification의 캐시된 복사본에서 Unity 6000.3.5f2로 설치·컴파일·선별 확인을 수행했다. 설치된 위 3개 씬과 BossEncounter 프리팹만 원본에 반영했다. 기존 에셋 GUID와 다른 씬·공용 프리팹은 유지했다.

## 실제 확인 결과

- Unity 런타임·에디터·테스트 어셈블리 컴파일 성공.
- BossPatternReworkEditModeTests 4/4 통과.
- 직접 관련된 PlayMode 6/6 통과:
  - 일반 공격 중 믿음 발동과 화상 비소급.
  - 흰 구름 아래 통과·착지·탑승 이동.
  - 보스 도입 중 전투 대기·실제 아레나 범위의 구름·정상 종료 1회 시작·사망 후 봉쇄 해제.
  - 보스 도입 중 씬 이탈·늦은 완료 신호·모달 정리.
  - 튜토리얼 신도 대기 → 이동/무피해 첫 돌 → Tip 미연결 통과 → 실제 전투 → 같은 신도 사망 후 대사.
  - StageTest의 보호/노출/취소와 상시 구름 유지.
- 입력 잠금 저장 순서를 보완한 뒤 해당 튜토리얼 케이스 1개를 추가 재실행해 공격 중 처치 후에도 이동 잠금이 해제됨을 확인.
- Tools/level_layout_rework.py --check 통과: 9개 제작 씬의 기존 배치·적 수·타일 폭·앵커·점프 연결.
- 설치 전후 4개 에셋의 기존 Transform 96개 위치·크기·회전 유지 확인.
- git diff --check 통과.

원본 결과는 Logs/ParallelIntegration/PlayMode.xml, EditMode.xml, TutorialLock.xml, scene-transform-audit.json과 해당 Unity 로그에 있다. Logs와 검증용 프로젝트는 Git에 포함하지 않는다.

## 확인하지 않은 범위

전체 EditMode/PlayMode, 전체 빌드, 모든 보스 패턴·전체 플레이 루프는 실행하지 않았다. 마우스로 전체 대사/선택지를 진행하는 수동 플레이, 화면비별 말풍선 외형과 최종 난이도는 별도 플레이 확인이 필요하다. 보스·컷씬 기존 세션의 나머지 미실행 테스트를 전부 통과했다고 주장하지 않는다.

Tip UI 구현은 후속 작업이다. Docs/CutsceneTipIntegrationGuide.md를 따라 연결한다.

원격 푸시는 이번 통합 작업에서 실행하지 않았다. 최종 작업 브랜치는 Puri다.
