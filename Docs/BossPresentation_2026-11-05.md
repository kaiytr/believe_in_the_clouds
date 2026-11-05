# 보스 믿음·구름·말풍선 표현 수정 (2026-11-05)

## 변경 내용

- 컷씬 말풍선은 기존 Resources/UI/Hud/EnemyBeliefSpeechBubble.png를 사용한다. HudArtworkResources의 기존 영역 정보를 재사용하고, 같은 원본 텍스처에서 테두리·꼬리 분할 정보만 설정한다. 별도 배경 텍스처를 그리지 않는다.
- 말풍선 아래쪽 꼬리를 화자 머리 위에 맞추고, 기존 에셋에 맞춰 글자와 선택지 색을 회색 계열로 정리했다.
- 일반 공격과 믿음의 실행 흐름은 독립적이지만, 믿음은 한 종류만 유지한다. 예언 판정, 화상 강화와 강화가 기록된 진행 공격, 번개, 충격파 추적이 끝나야 다음 믿음을 선택한다.
- 믿음 쿨타임 3초는 발동 처리 종료가 아니라 실제 효과·판정 종료 뒤에 시작한다. 일반 공격은 이 대기와 무관하게 계속 실행할 수 있다.
- 먹구름 꼭대기 높이를 바닥 기준 6 → 8유닛으로 올렸다.
- 첨부한 c7bc6659aea4171f.png를 Assets/Design/Boss/BossStormCloud.png로, 8332622272e03ff7.png를 BossWhiteCloud.png로 복사했다. 원본 PNG 픽셀은 수정하지 않았다.
- 구름 색에 추가 틴트를 곱하지 않고 원본 색을 사용한다. 먹구름은 분할 표시, 흰 구름은 가장자리를 유지한 반복 표시로 발판 폭에 맞춘다.
- 구름 루트 배율은 1을 유지하고 SpriteRenderer.size로 표시 크기를 맞춘다. 흰 구름의 실제 폭·착지 높이·보호 구역은 기존 설정을 사용한다.
- 새 PNG는 2D 스프라이트·Point 필터·무압축으로 임포트한다. TextureShape를 명시해 큐브맵으로 들어가지 않도록 했다.
- 번개는 기존 노란 LineRenderer 줄 대신 BossLightningStrikeVisual의 흰색/푸른색 계단형 낙뢰, 짧은 재발광·잔광과 착지점 빛 조각으로 바꿨다.
- 번개 시각 효과에는 콜라이더와 피해 처리가 없다. 기존 3초 예고·위험 사각형·한 번의 피해·흰 구름 아래 보호 판정은 BossCloudLightning이 계속 담당한다.
- 낙뢰 시각 효과도 흰 구름을 만나면 위에서 멈춘다. 보호 구역 아래로 낙뢰 폭과 가지가 새지 않도록 여유를 둔다.

## 설정과 재생성

설정 원본은 Assets/Data/Boss/BossCombatSettings.asset이다.
CloudTopHeight=8, LightningSpacing=2, LightningWidth=0.16을 적용했다.

후속 화면 조정: 보스 전투 카메라 중심을 기존보다 1유닛 낮추고, 먹구름 윗면은 플레이어 높이에 따른 전투 카메라 확대까지 계산한 화면 위쪽 0.7유닛 아래에 둔다. 컷씬 말풍선은 기존 믿음 말풍선 에셋의 비율과 화면상 크기를 따라가며, 화자 이름 없이 대사와 선택지만 표시한다.

BossPresentationAssetSetup.Apply가 새 구름 이미지 임포트와 BlackCloud/WhiteCloud 프리팹만 갱신한다. BossProjectSetup의 구름 재생성도 같은 이미지와 표시 방식을 사용한다.

구름 프리팹 검증은 원본 이미지가 1×1 유닛인지 강제하지 않고, 루트 배율과 SpriteRenderer의 조절 가능한 표시 크기를 확인한다. 다른 기존 효과의 단위 스프라이트 검증은 유지한다.

## 확인

Unity 6000.3.5f2의 검증용 복사본에서 직접 관련된 PlayMode 4개를 확인했다.
- OnlyOneBeliefRemainsActiveAndCooldownStartsAfterItsEffectEnds
- OverlappingStormsStrikeOnceAndAWhiteCloudShieldsTheWholeBody
- WhiteCloudCarriesWhoeverStandsOnItAndPassesFromBelow
- BubbleFollowsSpeakerAnchorAndStaysInsideScreenSafeArea

초기 구름 임포트의 큐브맵 문제를 수정한 뒤 영향을 받은 보스 케이스만 재확인했다. 흰 구름 반복 표시 보완 뒤에는 착지/탑승/표시 크기 케이스 하나만 추가 확인했다. 전체 EditMode·PlayMode나 전체 빌드는 실행하지 않았다.

실제 Unity 정지 렌더링으로 새 구름과 낙뢰 모양도 확인했다. Logs/BossPresentation/Captures/cloud-lightning.png는 위치를 배치한 표현 확인용 정지 미리보기이며 자동 전투 전체를 촬영한 영상이 아니다. 말풍선은 런타임 테스트에서 원본 텍스처 재사용과 머리 앵커·화면 경계를 확인했다.

원본 결과는 Logs/BossPresentation의 PlayMode.xml, Boss-Retry.xml, WhiteCloud-Final.xml에 있다. 전체 패턴 밸런스와 대사 클릭 흐름을 다시 전부 확인한 것은 아니다.
