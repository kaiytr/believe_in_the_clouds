using System;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-500)]
public sealed class LobbySceneController : MonoBehaviour
{
    [SerializeField] private bool isLobby = true;
    [SerializeField] private PlayerHealth player;
    [SerializeField] private LobbyBranchInteractable branchPrefab;
    [SerializeField] private Transform[] twoCandidateSlots = new Transform[2];
    [SerializeField] private Transform[] threeCandidateSlots = new Transform[3];
    [SerializeField] private FoundationInteractable foundationPrefab;
    [SerializeField] private Transform foundationAnchor;
    [SerializeField] private Sprite squareSprite;
    [SerializeField] private Sprite returnDoorSprite;
    [SerializeField] private Material worldMaterial;
    [SerializeField, Tooltip("Only attaches when this scene is the active run stage for its configured role.")]
    private bool runStageOnly;
    [SerializeField] private BranchRole runStageRole;
    [SerializeField, Tooltip("Horizontal player limits for a non-lobby stage.")]
    private float stageMinX = -10f;
    [SerializeField] private float stageMaxX = 10f;
    [SerializeField] private float stageMaxY = 8f;
    [SerializeField] private float lobbyMinX = -23f, lobbyMaxX = 31f;
    [SerializeField] private Vector2 lobbyTitlePosition = new Vector2(-15f, 2.3f);
    [SerializeField] private TemporaryStageExit authoredExit;
    [SerializeField] private bool showTestTools = true;
    public bool IsLobby => isLobby;
    public PlayerHealth Player => player;
    public bool ShowTestTools { get => showTestTools; set { showTestTools = value; if (tools != null) tools.SetActive(value); } }
    private LobbyFlowRuntime flow;
    private GameObject generated, tools;
    private string contentKey;
    private Text status, message;
    private TextMesh stageTitle, exitLabel;
    private SpriteRenderer exitBody;
    private Button complete, damage, death, grant, reload, fail, fresh;
    private bool playable = true;

    private void Start()
    {
        if (!isLobby && runStageOnly)
        {
            flow = LobbyFlowRuntime.Instance;
            if (flow == null || !flow.IsActiveRunStage(runStageRole))
            {
                enabled = false;
                return;
            }
        }
        else
        {
            flow = LobbyFlowRuntime.Ensure();
        }

        if (player == null) player = FindFirstObjectByType<PlayerHealth>();
        BuildUi(); flow.Attach(this);
    }
    public Vector2 ClampPosition(Vector2 position) => new Vector2(Mathf.Clamp(position.x, isLobby ? lobbyMinX : stageMinX, isLobby ? lobbyMaxX : stageMaxX),
        Mathf.Clamp(position.y, authoredExit != null ? -3f : -1.95f, stageMaxY));

    public void SetPlayable(bool value)
    {
        if (player == null || playable == value) return;
        playable = value;
        player.GetComponent<PlayerMove>().enabled = value;
        player.GetComponent<PlayerAttack>().enabled = value;
        player.GetComponent<PlayerDash>().enabled = value;
        if (!value && player.TryGetComponent(out Rigidbody2D body)) body.linearVelocity = new Vector2(0, body.linearVelocity.y);
    }

    private void Update()
    {
        if (flow == null || flow.Scene != this) return;
        if (Input.GetKeyDown(KeyCode.F2) && (!runStageOnly || flow.IsPlayLoopTestMode))
            ShowTestTools = !ShowTestTools;
        LobbyRunData run = flow.State;
        if (run == null) { status.text = "저장 확인 중"; message.text = flow.Message; return; }
        bool matches = isLobby ? run.area == RunArea.Lobby : run.area == RunArea.TemporaryStage || run.area == RunArea.Complete;
        if (matches)
        {
            string key = isLobby ? run.batchId : run.stage.id;
            if (contentKey != key) { contentKey = key; BuildContent(run); }
        }
        status.text = isLobby ? $"로비  |  전투 클리어 {run.combatClears}회" + (run.combatClears >= 3 ? " · 보스 후보 해금" : " · 3회부터 보스 후보 등장")
            : run.area == RunArea.Complete ? "임시 보스 완료  |  런 완료 검증 · 정식 엔딩은 후속 연결"
            : run.hasStage ? $"{LobbyRules.RoleLabel(run.stage.role)} 임시 스테이지  |  " +
                (run.stage.completed ? "완료 · 출구에서 E로 복귀" : "역할 완료 전에는 복귀할 수 없습니다")
            : "스테이지 확인 중";
        message.text = flow.Message;
        bool ready = flow.InputReady;
        complete.interactable = ready && !isLobby && run.hasStage && !run.stage.completed;
        damage.interactable = ready;
        death.interactable = ready;
        grant.interactable = ready;
        fresh.interactable = !flow.IsTransitioning && !flow.Service.HasPendingDeath;
        reload.interactable = !flow.IsTransitioning && !flow.Service.HasPendingDeath && !flow.HasSaveFailure;
        fail.interactable = flow.FixtureStore != null;
        if (!isLobby && exitBody != null && run.hasStage)
        {
            bool available = run.stage.completed && run.stage.role != BranchRole.Boss;
            if (returnDoorSprite != null)
                exitBody.color = available ? Color.white : new Color(.55f, .55f, .55f);
            else
                exitBody.color = available ? new Color(.27f, .65f, .53f) : new Color(.22f, .25f, .3f);
            exitLabel.text = run.area == RunArea.Complete ? "검증 완료" : available ? "출구 · E" : "완료 후 출구 사용";
        }
    }

    private void BuildContent(LobbyRunData run)
    {
        if (generated != null) { generated.SetActive(false); Destroy(generated); }
        generated = new GameObject("Committed run view"); generated.transform.SetParent(transform);
        if (isLobby)
        {
            SpawnFoundation(generated.transform);
            SpawnBranches(run, generated.transform);
            WorldLabel(generated.transform, "Lobby title", "다음 목적지를 선택하세요", lobbyTitlePosition, new Color(.75f, .86f, .95f));
        }
        else
        {
            if (authoredExit != null)
            {
                exitBody = authoredExit.GetComponentInChildren<SpriteRenderer>();
                exitLabel = WorldLabel(generated.transform, "Exit label", "완료 후 출구 사용",
                    authoredExit.transform.position + Vector3.up * 2.25f, Color.white);
                return;
            }
            stageTitle = WorldLabel(generated.transform, "Stage role", LobbyRules.RoleLabel(run.stage.role) + " · 임시 스테이지",
                new Vector2(0, 1.8f), LobbyBranchInteractable.RoleColor(run.stage.role));
            GameObject exit = new GameObject("ReturnExit"); exit.transform.SetParent(generated.transform); exit.transform.position = new Vector3(8, -.7f);
            var box = exit.AddComponent<BoxCollider2D>(); box.isTrigger = true; box.size = new Vector2(2, 4.6f);
            var panel = new GameObject("Panel");
            panel.transform.SetParent(exit.transform, false);
            if (returnDoorSprite != null)
                panel.transform.localPosition = new Vector3(0f, box.offset.y - box.size.y * .5f, 0f);
            else
                panel.transform.localScale = new Vector3(box.size.x, box.size.y, 1f);
            exitBody = panel.AddComponent<SpriteRenderer>();
            exitBody.sprite = returnDoorSprite != null ? returnDoorSprite : squareSprite;
            exitBody.sharedMaterial = worldMaterial;
            exitBody.sortingOrder = -8;
            exit.AddComponent<TemporaryStageExit>();
            exitLabel = WorldLabel(exit.transform, "Exit label", "완료 후 출구 사용",
                new Vector2(0, returnDoorSprite != null ? 2.25f : 2.7f), Color.white);
        }
    }

    private void SpawnFoundation(Transform parent)
    {
        if (foundationPrefab == null || foundationAnchor == null)
        {
            Debug.LogError("LobbySceneController needs a Foundation prefab and anchor.", this);
            return;
        }

        Instantiate(foundationPrefab, foundationAnchor.position, foundationAnchor.rotation, parent);
    }

    private void SpawnBranches(LobbyRunData run, Transform parent)
    {
        if (branchPrefab == null)
        {
            Debug.LogError("LobbySceneController needs a Branch prefab.", this);
            return;
        }

        Transform[] slots = run.candidates.Count == 2 ? twoCandidateSlots : threeCandidateSlots;
        if (slots == null || slots.Length < run.candidates.Count)
        {
            Debug.LogError($"LobbySceneController has too few branch anchors for {run.candidates.Count} candidates.", this);
            return;
        }

        foreach (BranchCandidate candidate in run.candidates)
        {
            if (candidate.slot < 0 || candidate.slot >= slots.Length || slots[candidate.slot] == null)
            {
                Debug.LogError($"LobbySceneController is missing branch anchor {candidate.slot}.", this);
                continue;
            }

            Transform anchor = slots[candidate.slot];
            LobbyBranchInteractable branch = Instantiate(branchPrefab, anchor.position, anchor.rotation, parent);
            branch.Initialize(candidate.id);
        }
    }

    public static TextMesh WorldLabel(Transform parent, string name, string text, Vector2 position, Color color)
    {
        var root = new GameObject(name); root.transform.SetParent(parent, false); root.transform.localPosition = position;
        TextMesh label = root.AddComponent<TextMesh>(); label.text = text; label.fontSize = 48; label.characterSize = .055f;
        label.anchor = TextAnchor.MiddleCenter; label.color = color; label.font = UiTheme.GetFont();
        var renderer = root.GetComponent<MeshRenderer>(); renderer.sharedMaterial = label.font.material; renderer.sortingOrder = -1;
        return label;
    }

    private void BuildUi()
    {
        var root = new GameObject("Lobby verification UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 110;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        status = UiLabel(root.transform, "Progress", "로비 확인 중", 24, new Vector2(24, -20), new Vector2(1750, 42));
        tools = UiTheme.CreateImage("Test tools", root.transform, new Color(.06f, .1f, .17f, .94f));
        var rect = tools.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(24, -76); rect.sizeDelta = new Vector2(530, 320);
        UiLabel(tools.transform, "Test title", "테스트 도구  ·  F2 표시/숨기기", 18, new Vector2(16, -12), new Vector2(498, 30));
        fresh = UiButton(0, "새 메모리 테스트", () => flow.StartMemoryFixture());
        grant = UiButton(1, "+100 코인", () => flow.GrantForTest());
        complete = UiButton(2, "역할 완료", () => flow.CompleteStage());
        damage = UiButton(3, "피해 10", () => flow.DamageForTest());
        death = UiButton(4, "사망", () => flow.DieForTest());
        reload = UiButton(5, "저장 다시 불러오기", () => flow.ReloadForTest());
        fail = UiButton(6, "저장 실패 켜기/끄기", () => flow.ToggleSaveFailure());
        UiButton(7, "도구 숨기기", () => ShowTestTools = false);
        UiButton(8, "전체 플레이 루프 테스트", () => flow.StartPlayLoopTest());
        UiLabel(tools.transform, "Fixture note", "루프 테스트는 격리된 메모리 프로필을 사용합니다.", 15, new Vector2(16, -264), new Vector2(498, 30));
        message = UiLabel(root.transform, "Message", "", 18, new Vector2(24, -350), new Vector2(1100, 56));
        message.color = new Color(1, .85f, .5f);
        ShowTestTools = showTestTools && (!runStageOnly || flow.IsPlayLoopTestMode);
        UiEventSystemBoot.Ensure();
    }

    private Button UiButton(int index, string text, Action action)
    {
        var root = UiTheme.CreateImage("TestAction" + index, tools.transform, new Color(.22f, .32f, .45f));
        var rect = root.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(16 + index % 2 * 255, -52 - index / 2 * 40); rect.sizeDelta = new Vector2(243, 34);
        Image image = root.GetComponent<Image>(); image.raycastTarget = true;
        Button button = root.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => action());
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        Text label = UiTheme.CreateText("Label", root.transform, text, 16, Color.white); UiTheme.Stretch(label.rectTransform, 3);
        return button;
    }
    private static Text UiLabel(Transform parent, string name, string value, int size, Vector2 position, Vector2 dimensions)
    {
        Text text = UiTheme.CreateText(name, parent, value, size, Color.white, TextAnchor.UpperLeft);
        var rect = text.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = dimensions;
        return text;
    }
    private void OnDestroy() { if (flow != null) flow.Detach(this); }
}
