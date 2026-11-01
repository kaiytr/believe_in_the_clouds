using UnityEngine;

// 재단 상호작용 지점.
// 상점과 같은 공통 규칙(등록/거리 판정/입력/안내 문구)을 그대로 쓴다.
// 조합 화면은 FoundationScreen이 상호작용 이벤트를 받아 연다.
public class FoundationInteractable : InteractableBehaviour, IWorldInteractionPrompt
{
    private void Awake()
    {
        SetInteractionCollidersToTriggers();

        TextMesh[] labels = GetComponentsInChildren<TextMesh>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i].font == null)
            {
                labels[i].font = UiTheme.GetFont();
            }

            if (labels[i].font != null && labels[i].TryGetComponent(out MeshRenderer textRenderer))
            {
                textRenderer.sharedMaterial = labels[i].font.material;
            }
        }
    }

    [Header("재단")]
    [Tooltip("로그/UI에 표시할 재단 이름.")]
    [SerializeField] private string foundationName = "재단";
    [Tooltip("오브젝트 위 상호작용 안내와 콜라이더 윗면 사이의 월드 거리.")]
    [SerializeField, Min(0f)] private float promptGap = .8f;

    public string FoundationName => string.IsNullOrEmpty(foundationName) ? InteractionName : foundationName;
    public Transform PromptAnchor => transform;
    public Vector3 PromptOffset => GetPromptOffsetAboveCollider(promptGap);

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
