# believe_in_the_clouds
겁 없는 10대 창업가의 뒤를 잇기 위해 자전거를 타고 무려 26km를 달려온 대한민국 자전거계의 전설 엄복동

## 재화 시스템 문서

다른 로컬에서 이어받을 때는 [개발 인수인계](Docs/CurrencySystem_Handoff.md)를 먼저 읽는다. 현재 문서의 Git 포함 여부와 함께 옮길 파일 목록도 확인한다.

재화 시스템 구현을 시작했으며 공통 API·경제 저장·상점 결제·코인 비행·사망 손실을 테스트 씬에 연결했다. 현재 구현과 검증 결과는 [구현 결과서](Docs/CurrencySystem_ImplementationStatus.md)를 기준으로 확인한다.

Unity에서 `Assets/Scenes/EconomyTest.unity`를 열고 Play하면 격리된 테스트 저장으로 재화를 검증할 수 있다. [테스트 가이드](Docs/CurrencySystem_TestGuide.md)에 조작법과 저장 경로가 있다.

로비 → 분기 선택 → 임시 스테이지 → 로비 복귀의 첫 플레이 루프를 연결했다. 실제 역할별 콘텐츠·재단·정식 보스와 엔딩은 후속 작업이다.

신규 시작은 0코인·초기 교리와 조각 지급 없음이다. 사망 시 교리를 전부 미장착으로 돌린 뒤 보유 조각 목록 전체에서 30%를 버림해 무작위 손실을 처리한다.

- [상세기획](Docs/CurrencySystem_Design.md): 확정된 플레이 규칙, 가격·등급, 사망 손실, 연출 테스트 초기값, 후속 콘텐츠 결정 보류
- [개발명세서](Docs/CurrencySystem_TechnicalSpec.md): 공통 API, 경제 저장, 전체 해제 후 조각 손실, 테스트 씬 검증, 후속 메인 루프 계약
- [개발자·AI 가이드](Docs/CurrencySystem_Guide.md): 향후 API 사용 예제와 기능 확장 시 지켜야 할 계약

## 분기 선택 로비

[로비 상세기획](Docs/LobbySystem_Design.md)을 기준으로 로비 기능과 임시 스테이지 왕복을 구현했다. Unity에서 `Assets/Scenes/Lobby.unity`를 열고 Play한다. 빌드의 시작 씬도 Lobby다.

첫 구현 범위는 가로로 긴 안전한 로비와 추적 카메라, E 출발·F 역할 공개, 저장 복원과 임시 스테이지 왕복 검증이다. 중도 귀환은 허용하지 않고 스테이지 완료 또는 사망 시에만 로비로 복귀한다. 재단은 추후 구현을 완료한 뒤 로비 내부 시설로 배치한다.

- [로비 테스트 가이드](Docs/LobbySystem_TestGuide.md): 실행 방법, 테스트 도구, 직접 확인할 항목
- [로비 구현 결과](Docs/LobbySystem_ImplementationStatus.md): 저장 구조, 자동 검증 결과와 후속 범위
