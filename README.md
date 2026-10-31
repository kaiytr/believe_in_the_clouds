# believe_in_the_clouds
겁 없는 10대 창업가의 뒤를 잇기 위해 자전거를 타고 무려 26km를 달려온 대한민국 자전거계의 전설 엄복동

## 재화 시스템 문서

다른 로컬에서 이어받을 때는 [개발 인수인계](Docs/CurrencySystem_Handoff.md)를 먼저 읽는다. 현재 문서의 Git 포함 여부와 함께 옮길 파일 목록도 확인한다.

재화 시스템 구현을 시작했으며 공통 API·경제 저장·상점 결제·코인 비행·사망 손실을 테스트 씬에 연결했다. 현재 구현과 검증 결과는 [구현 결과서](Docs/CurrencySystem_ImplementationStatus.md)를 기준으로 확인한다.

Unity에서 `Assets/Scenes/EconomyTest.unity`를 열고 Play하면 격리된 테스트 저장으로 재화를 검증할 수 있다. [테스트 가이드](Docs/CurrencySystem_TestGuide.md)에 조작법과 저장 경로가 있다.

메인 플레이 루프(로비 → 분기 선택 → 스테이지 → 로비 복귀 → … → 보스 → 엔딩)도 아직 구현되지 않았다. 이번 범위는 재화 시스템을 테스트 씬에서 독립 검증하는 것까지이며, 실제 메인 루프 연동은 후속 작업이다.

신규 시작은 0코인·초기 교리와 조각 지급 없음이다. 사망 시 교리를 전부 미장착으로 돌린 뒤 보유 조각 목록 전체에서 30%를 버림해 무작위 손실을 처리한다.

- [상세기획](Docs/CurrencySystem_Design.md): 확정된 플레이 규칙, 가격·등급, 사망 손실, 연출 테스트 초기값, 후속 콘텐츠 결정 보류
- [개발명세서](Docs/CurrencySystem_TechnicalSpec.md): 공통 API, 경제 저장, 전체 해제 후 조각 손실, 테스트 씬 검증, 후속 메인 루프 계약
- [개발자·AI 가이드](Docs/CurrencySystem_Guide.md): 향후 API 사용 예제와 기능 확장 시 지켜야 할 계약
