using UnityEngine;

public sealed class LobbyBranchInteractable : InteractableBehaviour, ISecondaryInteractable, IWorldInteractionPrompt
{
    public string BranchId { get; private set; }
    public SpriteRenderer Body => body;
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private SpriteRenderer border;
    [SerializeField] private TextMesh label;
    [SerializeField] private BoxCollider2D trigger;
    [SerializeField] private Sprite closedDoor;
    [SerializeField] private Sprite combatDoor;
    [SerializeField] private Sprite shopDoor;
    [Tooltip("분기 위 안내와 콜라이더 윗면 사이의 월드 거리.")]
    [SerializeField, Min(0f)] private float promptGap = .8f;

    public Transform PromptAnchor => transform;
    public Vector3 PromptOffset => GetPromptOffsetAboveCollider(promptGap);

    private void Awake()
    {
        ResolvePrefabReferences();
        interactRadius = 1.4f;
        displayName = "분기";

        if (trigger == null)
        {
            trigger = GetComponent<BoxCollider2D>();
        }
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<BoxCollider2D>();
        }

        trigger.isTrigger = true;
        trigger.size = new Vector2(2.8f, 4.6f);

        if (label != null && label.font == null)
        {
            label.font = UiTheme.GetFont();
            if (label.font != null && label.TryGetComponent(out MeshRenderer textRenderer))
            {
                textRenderer.sharedMaterial = label.font.material;
            }
        }
    }

    public void Initialize(string id)
    {
        ResolvePrefabReferences();
        BranchId = id;
        if (label != null)
        {
            label.text = "?";
        }
    }

    private BranchCandidate Candidate => LobbyFlowRuntime.Instance?.State?.candidates.Find(item => item.id == BranchId);
    public override string InteractionPrompt
    {
        get
        {
            BranchCandidate data = Candidate;
            if (data == null) return "분기를 확인할 수 없습니다.";
            if (data.revealed) return "E : " + LobbyRules.RoleLabel(data.role) + " 출발 · 공개됨";
            return Currency.Balance >= LobbyRules.RevealPrice
                ? "E : 출발   |   F : 역할 공개 (5코인)"
                : "E : 출발   |   F : 역할 공개 (5코인 · 코인 부족)";
        }
    }

    public override bool CanInteract(GameObject interactor) => base.CanInteract(interactor) && Candidate != null &&
        LobbyFlowRuntime.Instance != null && LobbyFlowRuntime.Instance.InputReady && LobbyFlowRuntime.Instance.Scene.IsLobby;
    protected override void OnInteract(GameObject interactor) { LobbyFlowRuntime.Instance.Depart(BranchId); }
    public void InteractSecondary(GameObject interactor)
    {
        if (CanInteract(interactor) && IsInRange(interactor.transform.position)) LobbyFlowRuntime.Instance.Reveal(BranchId);
    }

    private void Update()
    {
        BranchCandidate candidate = Candidate;
        if (candidate == null || body == null) return;

        string roleLabel = candidate.revealed ? LobbyRules.RoleLabel(candidate.role) : "?";
        if (label != null && label.text != roleLabel) label.text = roleLabel;

        bool selected = InteractionController.Instance != null && InteractionController.Instance.Current == this &&
            LobbyFlowRuntime.Instance.InputReady && IsInRange(LobbyFlowRuntime.Instance.Scene.Player.transform.position);
        if (closedDoor != null)
        {
            Sprite door = closedDoor;
            if (candidate.revealed)
            {
                if (candidate.role == BranchRole.Combat && combatDoor != null) door = combatDoor;
                else if (candidate.role == BranchRole.Shop && shopDoor != null) door = shopDoor;
            }
            if (body.sprite != door) body.sprite = door;
            Color tint = candidate.revealed ? Color.white : new Color(.88f, .88f, .88f);
            body.color = selected ? Color.Lerp(tint, new Color(1f, .9f, .55f), .3f) : tint;
            return;
        }

        body.color = candidate.revealed ? RoleColor(candidate.role) : new Color(.36f, .39f, .45f);
        if (border != null)
        {
            border.color = selected ? new Color(1f, .9f, .55f) : new Color(.19f, .23f, .3f);
        }
    }

    private void ResolvePrefabReferences()
    {
        if (body == null)
        {
            body = transform.Find("Panel")?.GetComponent<SpriteRenderer>();
        }
        if (border == null)
        {
            border = transform.Find("Frame")?.GetComponent<SpriteRenderer>();
        }
        if (label == null)
        {
            label = transform.Find("Role")?.GetComponent<TextMesh>();
        }
    }

    public static Color RoleColor(BranchRole role)
    {
        switch (role)
        {
            case BranchRole.Combat: return new Color(.76f, .28f, .31f);
            case BranchRole.Shop: return new Color(.76f, .6f, .22f);
            default: return new Color(.57f, .3f, .72f);
        }
    }
}
