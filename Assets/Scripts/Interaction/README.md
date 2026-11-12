# 상호작용(E) 사용법

프로젝트 공통 규칙: **월드 기본 상호작용은 E 키**로 한다. 보조 동작이 필요한 대상은 `ISecondaryInteractable`을 구현하면 같은 대상에서 **F**를 사용한다. 로비 분기는 E 출발·F 역할 공개다.
특정 대상(상점)에 입력 처리를 묶지 않는다.

## 구조

```
Interaction/                      ← 공통 (대상이 무엇인지 모름)
  IInteractable.cs                계약: 이름/문구/반경/CanInteract/Interact
  ISecondaryInteractable.cs       선택적 F 보조 동작
  InteractableBehaviour.cs        등록·최근접 탐색·표면 기준 거리 판정
  InteractionController.cs        E/F 입력 1곳, 가장 가까운 대상 실행, 안내 문구
  InteractionInput.cs             입력 API 격리 (New Input System 전환 지점)
  InteractionPrompt.cs            대상 위 또는 하단 "E : ..." 표시
  UiModal.cs                      모달 열림 추적 + 닫기 요청 브로드캐스트

Shop/                             ← 기능 모듈 (상점만 앎)
  ShopInteractable.cs             상점 표시 정보
  ShopScreen.cs                   상점 패널 열기/닫기
  ShopPanelView.cs                패널 표시 전용
```

`InteractionController`는 `ShopInteractable`을 전혀 참조하지 않는다.
상점 UI는 `InteractableBehaviour.Interacted` 이벤트를 듣고 스스로 연다.
그래서 새 상호작용을 추가해도 공통 코드를 고칠 필요가 없다.

## 새 상호작용 추가하기

```csharp
// 예: 문
public class DoorInteractable : InteractableBehaviour
{
    [SerializeField] private string doorName = "문";

    // 문구를 직접 쓰고 싶으면 이렇게 재정의 (기본값은 "E : 상호작용")
    public override string InteractionPrompt => $"E : {doorName} 열기";

    protected override void OnInteract(GameObject interactor)
    {
        // 문 열기 로직
    }
}
```

씬에 붙이기만 하면 `InteractionController`가 자동으로 후보에 넣는다.
(스크립트를 붙이지 않아도 되게 하려면 `InteractionController.AutoRegisterShops`처럼
이름 기반 자동 등록을 추가하면 된다.)

## 거리 판정 규칙

거리는 **중심점이 아니라 콜라이더 표면** 기준이다.
`Shop`처럼 큰 오브젝트는 중심까지의 거리로 재면 손이 닿지 않기 때문이다.

- 콜라이더 2D/3D가 있으면 표면까지의 거리 (안쪽이면 0)
- 없으면 중심까지의 거리
- 기본 반경 `interactRadius = 2.5m` (대상 표면에서)

## 안내 위치

- 상자·상점·재단·로비 분기·스테이지 출구는 `IWorldInteractionPrompt`의 앵커를 사용해 **각 오브젝트 위에 안내를 부착**한다. 카메라 이동 뒤 위치를 갱신하며, 화면 밖 기준점이나 비활성 대상의 안내는 숨긴다.
- 상점·재단·분기·출구는 스프라이트(보이는 그림) 윗면에서 0.15 월드 유닛 위에 표시한다(`InteractableBehaviour.PromptArtGap`). 스프라이트가 없으면 콜라이더 윗면을 기준으로 한다.
- 상자는 E 1.5초 유지, 상점·재단·출구는 E 한 번, 분기는 E 출발·F 역할 공개다. 안내 위치와 입력 방식은 독립이다.
- 앵커를 제공하지 않는 다른 대상은 기존 하단 안내를 사용한다.

## 안내 문구 형식

`InteractionPrompt`는 문구를 **줄 단위**로 나눠 한 줄마다 키 네모칸 + 문구로 세로로 쌓아 보여준다 (상자 안내와 같은 모양, 단 진행 표시는 없다).
`"K : 문구"` 형식인 줄은 네모칸에 `K`를 넣고, 그 외 줄은 네모칸 없이 가운데 정렬 문구로 나온다.

```csharp
public override string InteractionPrompt => "E : 진입\nF : 정보 공개 (5코인)";
```

문구는 최대한 짧게 쓴다. 상자의 꾹 누르기(`IHoldInteractable`)만 진행 표시가 있는 별도 안내를 쓴다.

## 모달 UI와 입력

- 모달(상점 패널)이 열려 있으면 월드 상호작용은 실행되지 않는다.
- E/F는 같은 최근접 대상과 거리 판정을 공유한다. 둘을 같은 프레임에 누르면 E를 우선한다. 보조 동작 구현에서도 범위·상태를 검사한다.
- 대신 E 또는 ESC가 "닫기 요청"이 된다.
- 모달이 열린 동안 플레이어 이동/점프/대시/공격은 `UiModal.IsOpen`으로 막힌다.

## 콘솔 로그

| 상황 | 로그 |
| --- | --- |
| 상호작용 성공 | `[상호작용] 상호 작용 완료 — 상점 (Player, 0.42m)` |
| 대상이 반경 밖 | `[상호작용] '상점'이(가) 너무 멉니다. 거리 3.10m / 필요 2.50m` |
| 주변에 대상 없음 | `[상호작용] 주변에 상호작용 대상이 없습니다.` |
| 상점 자동 등록 | `[상호작용] 'Shop' 오브젝트를 상점으로 자동 등록했습니다.` |

## 입력 API 주의

`ProjectSettings/activeInputHandler`가 **Both(2)** 라 구 `Input` API를 쓰고 있다.
New 전용으로 바꾸면 `InteractionInput.cs` 한 곳만 Input System 버전으로 교체하면 된다.
(플레이어 이동/대시/공격도 같은 구 API를 쓰므로 그때 함께 정리해야 한다.)
