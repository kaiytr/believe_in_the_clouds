using UnityEngine;

[RequireComponent(typeof(BoxCollider2D), typeof(ChestView))]
public sealed class ChestInteractable : InteractableBehaviour, IHoldInteractable, IWorldInteractionPrompt
{
    [SerializeField] private string spawnId = "chest-01";
    [SerializeField] private Transform dropPoint;
    [SerializeField] private Vector3 promptOffset = new Vector3(0, 1.65f, 0);
    private string stageId;
    private GameObject actor;
    private int session;
    private ChestView view;
    private bool opened;

    public string SpawnId => spawnId;
    public string StageId => stageId;
    public bool IsOpened => opened;
    public float HoldDuration => ChestRules.HoldSeconds;
    public string HoldLabel => "길게 눌러 상자 열기";
    public Transform PromptAnchor => transform;
    public Vector3 PromptOffset => promptOffset;
    public event System.Action<ChestOpenResult> Opened;
    public override float InteractionRadius => ChestRules.InteractionRadius;
    public override string InteractionName => "상자";
    public override string InteractionPrompt => "E : 길게 눌러 상자 열기";

    protected override void OnEnable()
    {
        base.OnEnable();
        Currency.Changed += HandleChanged;
        GetComponent<BoxCollider2D>().isTrigger = true;
        view = GetComponent<ChestView>();
    }
    protected override void OnDisable()
    {
        Currency.Changed -= HandleChanged;
        actor = null;
        base.OnDisable();
    }
    private void Start()
    {
        if (string.IsNullOrEmpty(stageId)) stageId = Currency.Service?.RunSnapshot()?.stage?.id;
        RefreshState();
    }
    public void Bind(string stageInstanceId)
    {
        actor = null; stageId = stageInstanceId; RefreshState();
    }
    private void HandleChanged(CurrencyChange change) => RefreshState();
    public void RefreshState()
    {
        opened = Currency.Service?.GetChestState(stageId, spawnId)?.opened ?? false;
        if (view != null) view.SetState(opened, false);
    }
    private void Update()
    {
        if (view != null) view.SetState(opened, !opened && InteractionController.Instance != null &&
            InteractionController.Instance.Current == this);
    }
    public override bool CanInteract(GameObject interactor)
    {
        if (!base.CanInteract(interactor) || opened || Currency.Service == null ||
            !Currency.Service.CanUseChestStage(stageId) || Currency.Service.GetChestState(stageId, spawnId) == null) return false;
        LobbyFlowRuntime flow = LobbyFlowRuntime.Instance;
        if (flow != null && flow.Scene != null && !flow.InputReady) return false;
        PlayerHealth health = interactor.GetComponent<PlayerHealth>();
        return health != null && !health.IsDead;
    }

    // E-down dispatch alone must never bypass the hold controller.
    protected override void OnInteract(GameObject interactor) { }
    public void BeginHold(GameObject interactor, int sessionId) { actor = interactor; session = sessionId; }
    public void CancelHold(GameObject interactor, int sessionId) { if (actor == interactor && session == sessionId) actor = null; }
    public HoldInteractionResult CompleteHold(GameObject interactor, int sessionId)
    {
        if (actor != interactor || session != sessionId || !CanInteract(interactor) ||
            DistanceTo(interactor.transform.position) > InteractionRadius || InteractionController.Instance == null ||
            !InteractionController.Instance.CanCompleteHold(this, interactor, sessionId))
            return new HoldInteractionResult(false, "상자를 열 수 없습니다.");
        actor = null;
        ChestOpenResult result = Currency.Service.TryOpenChest(stageId, spawnId,
            dropPoint != null ? dropPoint.position : transform.position, Camera.main);
        if (!result.Success) return new HoldInteractionResult(false, ErrorText(result.Error));
        RefreshState();
        if (!result.IsReplay)
        {
            view?.ShowReward(result.Reward);
            if (Opened != null)
                foreach (System.Action<ChestOpenResult> listener in Opened.GetInvocationList())
                    try { listener(result); } catch (System.Exception ex) { Debug.LogException(ex); }
        }
        return new HoldInteractionResult(true, "열림");
    }

    private static string ErrorText(EconomyError error)
    {
        switch (error)
        {
            case EconomyError.SaveFailed: return "보상을 저장하지 못했습니다. 다시 시도하세요.";
            case EconomyError.ItemLimit: return "해당 교리 조각의 보유 한도에 도달했습니다.";
            case EconomyError.Busy: return "다른 처리가 진행 중입니다. 다시 시도하세요.";
            default: return "지금은 상자를 열 수 없습니다.";
        }
    }
}
