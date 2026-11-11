using System;
using UnityEngine;

[DefaultExecutionOrder(-500)]
public sealed class LobbySceneController : MonoBehaviour
{
    [SerializeField] private bool isLobby = true;
    [SerializeField] private bool isStoryRoom;
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
    [SerializeField] private TemporaryStageExit authoredExit;
    [Tooltip("Played when the stage exit door rises out of the ground (same sound as revealing a lobby branch door).")]
    [SerializeField] private AudioClip exitRevealSound;
    [SerializeField, Range(0f, 1f)] private float exitRevealSoundVolume = 0.9f;
    public bool IsLobby => isLobby;
    public bool IsStoryRoom => isStoryRoom;
    public PlayerHealth Player => player;
    private LobbyFlowRuntime flow;
    private GameObject generated;
    private string contentKey;
    private SpriteRenderer exitBody;
    private TemporaryStageExit stageExit;
    private bool exitRevealStarted;
    private bool playable = true;

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerHealth>();

        if (!isLobby && !isStoryRoom && runStageOnly)
        {
            flow = LobbyFlowRuntime.Instance;

#if UNITY_EDITOR
            if (flow == null && gameObject.scene.name == LobbyFlowRuntime.ShopScene)
            {
                flow = LobbyFlowRuntime.Ensure();
                if (!flow.PrepareStandaloneShopTest(player))
                {
                    Debug.LogWarning("ShopTest를 직접 실행했지만 격리된 상점 테스트 런을 준비하지 못했습니다. 플레이어 및 재화 초기화 상태를 확인해 주세요.", this);
                    enabled = false;
                    return;
                }
            }
#endif

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

        flow.Attach(this);
    }
    public Vector2 ClampPosition(Vector2 position) => new Vector2(Mathf.Clamp(position.x, isLobby ? lobbyMinX : stageMinX, isLobby ? lobbyMaxX : stageMaxX),
        Mathf.Clamp(position.y, isStoryRoom ? -3f : authoredExit != null ? -3f : -1.95f, stageMaxY));

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
        if (isStoryRoom) return; // StoryRoomFlow owns authored NPC, altar and exit presentation.
        LobbyRunData run = flow.State;
        if (run == null) return;
        bool matches = isLobby ? run.area == RunArea.Lobby : run.area == RunArea.TemporaryStage || run.area == RunArea.Complete;
        if (matches)
        {
            string key = isLobby ? run.batchId : run.stage.id;
            if (contentKey != key) { contentKey = key; BuildContent(run); }
        }
        if (!isLobby && exitBody != null && run.hasStage)
        {
            bool isShop = run.stage.role == BranchRole.Shop;
            bool isCombat = run.stage.role == BranchRole.Combat;
            bool shouldExist = isShop || isCombat && run.stage.completed;
            if (isCombat && run.stage.completed && !exitRevealStarted)
                StartExitReveal();

            LobbyBranchRevealEffect reveal = stageExit != null
                ? stageExit.GetComponent<LobbyBranchRevealEffect>()
                : null;
            bool revealPlaying = reveal != null && reveal.IsPlaying;
            if (isShop)
            {
                exitBody.enabled = true;
            }
            else if (isCombat)
            {
                if (!shouldExist) exitBody.enabled = false;
                else if (exitRevealStarted && !revealPlaying)
                {
                    exitBody.enabled = true;
                }
            }
            else
            {
                exitBody.enabled = false;
            }

            bool available = isShop || isCombat && run.stage.completed;
            if (returnDoorSprite != null)
                exitBody.color = available ? Color.white : new Color(.55f, .55f, .55f);
            else
                exitBody.color = available ? new Color(.27f, .65f, .53f) : new Color(.22f, .25f, .3f);
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
        }
        else
        {
            if (authoredExit != null)
            {
                stageExit = authoredExit;
                exitBody = authoredExit.GetComponentInChildren<SpriteRenderer>();
                InitializeExitPresentation(run);
                return;
            }
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
            stageExit = exit.AddComponent<TemporaryStageExit>();
            InitializeExitPresentation(run);
        }
    }

    private void InitializeExitPresentation(LobbyRunData run)
    {
        exitRevealStarted = false;
        bool showImmediately = run != null && run.stage != null && run.stage.role == BranchRole.Shop;
        if (exitBody != null) exitBody.enabled = showImmediately;
    }

    private void StartExitReveal()
    {
        if (exitRevealStarted || stageExit == null || exitBody == null) return;
        exitRevealStarted = true;
        exitBody.enabled = true;

        var effect = stageExit.gameObject.AddComponent<LobbyBranchRevealEffect>();
        if (!effect.PlayRise(exitBody, exitBody.sprite, Color.white))
        {
            Destroy(effect);
            exitBody.enabled = true;
            return;
        }

        if (exitRevealSound != null)
        {
            AudioSource source = stageExit.GetComponent<AudioSource>();
            if (source == null) source = stageExit.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0f;
            source.PlayOneShot(exitRevealSound, exitRevealSoundVolume);
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

    private void OnDestroy() { if (flow != null) flow.Detach(this); }
}
