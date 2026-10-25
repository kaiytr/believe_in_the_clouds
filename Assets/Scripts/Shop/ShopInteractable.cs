using UnityEngine;

// 상점 상호작용 지점.
// 공통 규칙(등록/거리 판정/입력/안내 문구)은 InteractableBehaviour가 담당하고,
// 여기서는 "상점"의 표시 정보만 정의한다.
//
// 씬에 직접 붙이지 않아도 된다: InteractionController가 씬 로드 시
// 이름에 "Shop"이 들어간 오브젝트를 찾아 자동으로 이 컴포넌트를 붙인다.
//
// 패널을 여는 일은 ShopScreen이 Interacted 이벤트를 듣고 처리한다.
// (상호작용 대상은 UI를 직접 알지 않는다)
public class ShopInteractable : InteractableBehaviour
{
    [Header("상점")]
    [Tooltip("UI 제목으로 쓸 상점 이름.")]
    [SerializeField] private string shopName = "상점";

    public string ShopName => string.IsNullOrEmpty(shopName) ? InteractionName : shopName;

    // 문구를 직접 지정하지 않았으면 상점 이름으로 만든다.
    public override string InteractionPrompt
    {
        get
        {
            if (!string.IsNullOrEmpty(promptText) && promptText != DefaultPrompt)
            {
                return promptText;
            }

            return $"E : {ShopName} 열기";
        }
    }

    protected override void OnInteract(GameObject interactor)
    {
        // 실제 UI/구매 처리는 ShopScreen이 Interacted 이벤트로 받는다.
    }
}
