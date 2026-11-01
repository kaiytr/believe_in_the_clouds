using System;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-500)]
public sealed class LobbySceneController : MonoBehaviour
{
    [SerializeField] private bool isLobby = true;
    [SerializeField] private PlayerHealth player;
    [SerializeField] private Sprite squareSprite;
    [SerializeField] private Material worldMaterial;
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
        if (player == null) player = FindFirstObjectByType<PlayerHealth>();
        flow = LobbyFlowRuntime.Ensure();
        BuildUi(); flow.Attach(this);
    }
    public Vector2 ClampPosition(Vector2 position) => new Vector2(Mathf.Clamp(position.x, isLobby ? -23f : -10f, isLobby ? 31f : 10f),
        Mathf.Clamp(position.y, -1.95f, 8f));

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
        if (Input.GetKeyDown(KeyCode.F2)) ShowTestTools = !ShowTestTools;
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
            : run.hasStage ? $"{LobbyRules.RoleLabel(run.stage.role)} 임시 스테이지  |  " + (run.stage.completed ? "완료 · 출구에서 E로 복귀" : "역할 완료 전에는 복귀할 수 없습니다")
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
            foreach (BranchCandidate candidate in run.candidates)
            {
                var gate = new GameObject("Branch_" + (candidate.slot + 1)); gate.transform.SetParent(generated.transform);
                gate.transform.position = new Vector3(run.candidates.Count == 2 ? 10 + candidate.slot * 10 : 7 + candidate.slot * 8, -.7f);
                gate.AddComponent<LobbyBranchInteractable>().Initialize(candidate.id, squareSprite, worldMaterial);
            }
            WorldLabel(generated.transform, "Lobby title", "다음 목적지를 선택하세요", new Vector2(-15, 2.3f), new Color(.75f, .86f, .95f));
            WorldLabel(generated.transform, "Future altar space", "재단 예정 공간", new Vector2(-7, -1.3f), new Color(.55f, .62f, .7f));
        }
        else
        {
            stageTitle = WorldLabel(generated.transform, "Stage role", LobbyRules.RoleLabel(run.stage.role) + " · 임시 스테이지",
                new Vector2(0, 1.8f), LobbyBranchInteractable.RoleColor(run.stage.role));
            GameObject exit = new GameObject("ReturnExit"); exit.transform.SetParent(generated.transform); exit.transform.position = new Vector3(8, -.7f);
            var box = exit.AddComponent<BoxCollider2D>(); box.isTrigger = true; box.size = new Vector2(2, 4.6f);
            var panel = new GameObject("Panel"); panel.transform.SetParent(exit.transform, false); panel.transform.localScale = new Vector3(2, 4.6f, 1);
            exitBody = panel.AddComponent<SpriteRenderer>(); exitBody.sprite = squareSprite; exitBody.sharedMaterial = worldMaterial; exitBody.sortingOrder = -8;
            exit.AddComponent<TemporaryStageExit>();
            exitLabel = WorldLabel(exit.transform, "Exit label", "완료 후 출구 사용", new Vector2(0, 2.7f), Color.white);
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
        rect.anchoredPosition = new Vector2(24, -76); rect.sizeDelta = new Vector2(530, 260);
        UiLabel(tools.transform, "Test title", "테스트 도구  ·  F2 표시/숨기기", 18, new Vector2(16, -12), new Vector2(498, 30));
        fresh = UiButton(0, "새 메모리 테스트", () => flow.StartMemoryFixture());
        grant = UiButton(1, "+100 코인", () => flow.GrantForTest());
        complete = UiButton(2, "역할 완료", () => flow.CompleteStage());
        damage = UiButton(3, "피해 10", () => flow.DamageForTest());
        death = UiButton(4, "사망", () => flow.DieForTest());
        reload = UiButton(5, "저장 다시 불러오기", () => flow.ReloadForTest());
        fail = UiButton(6, "저장 실패 켜기/끄기", () => flow.ToggleSaveFailure());
        UiButton(7, "도구 숨기기", () => ShowTestTools = false);
        UiLabel(tools.transform, "Fixture note", "저장 실패 주입은 새 메모리 테스트에서 사용합니다.", 15, new Vector2(16, -218), new Vector2(498, 30));
        message = UiLabel(root.transform, "Message", "", 18, new Vector2(24, -350), new Vector2(1100, 56));
        message.color = new Color(1, .85f, .5f);
        ShowTestTools = showTestTools; UiEventSystemBoot.Ensure();
    }

    private Button UiButton(int index, string text, Action action)
    {
        var root = UiTheme.CreateImage("TestAction" + index, tools.transform, new Color(.22f, .32f, .45f));
        var rect = root.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(16 + index % 2 * 255, -52 - index / 2 * 40); rect.sizeDelta = new Vector2(243, 34);
        Image image = root.GetComponent<Image>(); image.raycastTarget = true;
        Button button = root.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => action());
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
