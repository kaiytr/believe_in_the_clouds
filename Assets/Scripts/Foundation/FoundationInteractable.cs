using UnityEngine;

// 재단 상호작용 지점.
// 상점과 같은 공통 규칙(등록/거리 판정/입력/안내 문구)을 그대로 쓴다.
// 여기서는 상호작용 발생 사실만 로그로 남긴다.
// 조합 화면은 나중에 붙인다.
public class FoundationInteractable : InteractableBehaviour
{
    [Header("재단")]
    [Tooltip("로그/UI에 표시할 재단 이름.")]
    [SerializeField] private string foundationName = "재단";

    public string FoundationName => string.IsNullOrEmpty(foundationName) ? InteractionName : foundationName;

    public override string InteractionPrompt
    {
        get
        {
            if (!string.IsNullOrEmpty(promptText) && promptText != DefaultPrompt)
            {
                return promptText;
            }

            return $"E : {FoundationName} 열기";
        }
    }

    protected override void OnInteract(GameObject interactor)
    {
        Debug.Log($"[상호작용] 상호 작용 완료 — {FoundationName} ({interactor?.name}, 인벤토리: {DescribeInventory()})");
    }

    private static string DescribeInventory()
    {
        DoctrineInventory inventory = DoctrineInventory.Instance;
        return inventory != null ? inventory.Describe() : "인벤토리 없음";
    }
}
