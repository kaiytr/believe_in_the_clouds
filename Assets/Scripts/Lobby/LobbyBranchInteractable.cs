using UnityEngine;

public sealed class LobbyBranchInteractable : InteractableBehaviour, ISecondaryInteractable, IWorldInteractionPrompt
{
    public string BranchId { get; private set; }
    public SpriteRenderer Body => body;
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private SpriteRenderer border;
    [SerializeField] private BoxCollider2D trigger;
    [SerializeField] private Sprite closedDoor;
    [SerializeField] private Sprite combatDoor;
    [SerializeField] private Sprite bossDoor;
    [SerializeField] private Sprite shopDoor;
    private LobbyBranchRevealEffect revealEffect;

    public bool IsRevealing => revealEffect != null && revealEffect.IsPlaying;

    public Transform PromptAnchor => transform;
    // The branch prompt sits in the middle of the door.
    public Vector3 PromptOffset => GetPromptOffsetAtArtCenter();
    public bool PromptCentered => true;

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
    }

    public void Initialize(string id)
    {
        ResolvePrefabReferences();
        BranchId = id;
    }

    private BranchCandidate Candidate => LobbyFlowRuntime.Instance?.State?.candidates.Find(item => item.id == BranchId);
    public override string InteractionPrompt
    {
        get
        {
            // One action per line; the prompt shows each as a keycap row.
            if (IsRevealing) return "공개 중…";
            BranchCandidate data = Candidate;
            if (data == null) return "확인 불가";
            if (data.revealed) return "E : 진입";
            return "E : 진입\nF : 정보 공개 (5코인)";
        }
    }

    public override bool CanInteract(GameObject interactor) => !IsRevealing && base.CanInteract(interactor) && Candidate != null &&
        LobbyFlowRuntime.Instance != null && LobbyFlowRuntime.Instance.InputReady && LobbyFlowRuntime.Instance.Scene.IsLobby;
    protected override void OnInteract(GameObject interactor) { LobbyFlowRuntime.Instance.Depart(BranchId); }
    public void InteractSecondary(GameObject interactor)
    {
        if (!CanInteract(interactor) || !IsInRange(interactor.transform.position) || Candidate.revealed) return;

        CurrencyResult result = LobbyFlowRuntime.Instance.Reveal(BranchId);
        BranchCandidate committed = Candidate;
        if (!result.Success || committed == null || !committed.revealed || body == null) return;

        revealEffect = gameObject.AddComponent<LobbyBranchRevealEffect>();
        Color tint = closedDoor != null ? Color.white : RoleColor(committed.role);
        if (!revealEffect.Play(body, border, null, DoorFor(committed), tint, LobbyRules.RoleLabel(committed.role)))
            Destroy(revealEffect);
    }

    private void Update()
    {
        BranchCandidate candidate = Candidate;
        if (candidate == null || body == null || IsRevealing) return;

        bool selected = InteractionController.Instance != null && InteractionController.Instance.Current == this &&
            LobbyFlowRuntime.Instance.InputReady && IsInRange(LobbyFlowRuntime.Instance.Scene.Player.transform.position);
        if (closedDoor != null)
        {
            Sprite door = DoorFor(candidate);
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

    private Sprite DoorFor(BranchCandidate candidate)
    {
        if (closedDoor == null) return body != null ? body.sprite : null;
        if (!candidate.revealed) return closedDoor;
        if (candidate.role == BranchRole.Combat && combatDoor != null) return combatDoor;
        if (candidate.role == BranchRole.Boss && bossDoor != null) return bossDoor;
        if (candidate.role == BranchRole.Shop && shopDoor != null) return shopDoor;
        return closedDoor;
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
