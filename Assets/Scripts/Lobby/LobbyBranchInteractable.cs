using UnityEngine;

public sealed class LobbyBranchInteractable : InteractableBehaviour, ISecondaryInteractable
{
    public string BranchId { get; private set; }
    public SpriteRenderer Body { get; private set; }
    private SpriteRenderer border;
    private TextMesh label;
    private Color displayed;

    public void Initialize(string id, Sprite sprite, Material material)
    {
        BranchId = id; interactRadius = 1.4f;
        displayName = "분기";
        var trigger = gameObject.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true; trigger.size = new Vector2(2.8f, 4.6f);
        Body = CreatePanel("Panel", sprite, material, new Vector2(2.8f, 4.6f), -8);
        border = CreatePanel("Frame", sprite, material, new Vector2(3f, 4.8f), -9);
        label = LobbySceneController.WorldLabel(transform, "Role", "?", new Vector2(0, 2.8f), Color.white);
    }

    private SpriteRenderer CreatePanel(string name, Sprite sprite, Material material, Vector2 size, int order)
    {
        var panel = new GameObject(name); panel.transform.SetParent(transform, false);
        panel.transform.localScale = new Vector3(size.x, size.y, 1);
        var renderer = panel.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite; renderer.sharedMaterial = material; renderer.sortingOrder = order;
        return renderer;
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
        if (candidate == null || Body == null) return;
        Color color = candidate.revealed ? RoleColor(candidate.role) : new Color(.36f, .39f, .45f);
        if (displayed != color)
        {
            displayed = color; Body.color = color;
            label.text = candidate.revealed ? LobbyRules.RoleLabel(candidate.role) : "?";
        }
        bool selected = InteractionController.Instance != null && InteractionController.Instance.Current == this &&
            LobbyFlowRuntime.Instance.InputReady && IsInRange(LobbyFlowRuntime.Instance.Scene.Player.transform.position);
        border.color = selected ? new Color(1f, .9f, .55f) : new Color(.19f, .23f, .3f);
    }

    public static Color RoleColor(BranchRole role)
    {
        switch (role)
        {
            case BranchRole.Combat: return new Color(.76f, .28f, .31f);
            case BranchRole.Shop: return new Color(.76f, .6f, .22f);
            case BranchRole.Event: return new Color(.28f, .55f, .78f);
            default: return new Color(.57f, .3f, .72f);
        }
    }
}
