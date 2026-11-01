using UnityEngine;
using UnityEngine.SceneManagement;

// 공통 상호작용 컨트롤러 (E 기본 동작, 선택적으로 F 보조 동작).
// 상점 전용이 아니다. 씬의 모든 IInteractable 중 가장 가까운 하나를 대상으로 삼는다.
//
// - 자동 부팅: 어떤 씬에서든 스스로 생성되고 DontDestroyOnLoad로 유지된다.
//   Tools 메뉴도, 씬에 미리 배치할 필요도 없다.
// - 안내 문구: 기본은 하단, 월드 앵커가 있는 대상은 대상 위를 따라간다.
// - 입력: E 기본 상호작용, ISecondaryInteractable에는 F. 모달에서는 E/ESC로 닫는다.
// - 확장: 새 상호작용 대상은 InteractableBehaviour(또는 IInteractable)만 구현하면
//   이 파일을 고치지 않아도 자동으로 참여한다.
//
// 이 클래스는 상점을 전혀 모른다. UI 패널을 여는 일은 각 기능 모듈
// (예: ShopScreen)이 Interacted 이벤트를 듣고 스스로 처리한다.
public class InteractionController : MonoBehaviour
{
    public static InteractionController Instance { get; private set; }

    [Header("입력")]
    [Tooltip("상호작용 키. 프로젝트 공통 규칙은 E.")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Tooltip("모달 UI를 닫는 키. E도 함께 동작한다.")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Header("안내 문구")]
    [Tooltip("화면 하단에서 띄울 높이.")]
    [SerializeField] private Vector2 promptOffset = new Vector2(0f, 250f);

    [Header("자동 탐색")]
    [Tooltip("씬 로드 시 이름에 이 문자열이 들어간 오브젝트를 상점으로 자동 등록한다.")]
    [SerializeField] private string autoRegisterShopHint = "Shop";

    [Tooltip("씬 로드 시 이름에 이 문자열이 들어간 오브젝트를 재단으로 자동 등록한다.")]
    [SerializeField] private string autoRegisterFoundationHint = "Foundation";

    [Header("진단")]
    [Tooltip("플레이 중 상태를 콘솔에 남긴다.")]
    [SerializeField] private bool logDiagnostics = false;

    private InteractionPrompt prompt;
    private InteractableBehaviour current;
    private float currentDistance;
    private GameObject player;
    private float nextPlayerSearchTime;
    private float nextDiagnosticTime;
    private InteractableBehaviour holdTarget;
    private IHoldInteractable hold;
    private float holdElapsed, holdDuration;
    private int sessionId;
    private bool requireKeyRelease = true, completionDispatched, hitInterrupted;
    private bool hasFocus = true;
    private float feedbackUntil;
    private bool feedbackSuccess;
    private string feedbackMessage;
    private InteractableBehaviour feedbackTarget;

    public float HoldProgress => holdTarget != null && holdDuration > 0 ? Mathf.Clamp01(holdElapsed / holdDuration) : 0;
    public InteractableBehaviour HoldingTarget => holdTarget;
    public bool UseManualInput { get; set; }
    public bool CanCompleteHold(InteractableBehaviour target, GameObject actor, int session) =>
        holdTarget == target && player == actor && sessionId == session && completionDispatched && holdElapsed >= holdDuration;

    public InteractableBehaviour Current => current;
    public bool HasTarget => current != null;
    public KeyCode InteractKey => interactKey;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBoot()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject go = new GameObject("[Interaction]");
        DontDestroyOnLoad(go);
        go.AddComponent<InteractionController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        DoctrineEventBus.EventRaised += HandleCombatEvent;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        DoctrineEventBus.EventRaised -= HandleCombatEvent;
        CancelHold();
    }

    private void Start()
    {
        EnsurePrompt();
        AutoRegisterInteractables();
        ResolvePlayer();
        LogSceneSummary("Start");
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CancelHold();
        feedbackUntil = 0;
        // 씬이 바뀌면 참조가 끊긴다.
        player = null;
        current = null;
        nextPlayerSearchTime = 0;
        hitInterrupted = false;
        EnsurePrompt();
        AutoRegisterInteractables();
        LogSceneSummary($"씬 로드({scene.name})");
    }

    private void EnsurePrompt()
    {
        if (prompt == null)
        {
            prompt = InteractionPrompt.BuildRuntime(promptOffset);
            DontDestroyOnLoad(prompt.gameObject);
        }
    }

    // 씬에 이름으로 배치된 오브젝트를 상호작용 지점으로 승격한다.
    // 상점("Shop")과 재단("Foundation")을 자동 등록한다.
    private void AutoRegisterInteractables()
    {
        AutoRegisterByHint(autoRegisterShopHint, typeof(ShopInteractable), "상점");
        AutoRegisterByHint(autoRegisterFoundationHint, typeof(FoundationInteractable), "재단");
    }

    private void AutoRegisterByHint(string hint, System.Type componentType, string label)
    {
        if (string.IsNullOrEmpty(hint) || componentType == null)
        {
            return;
        }

        Transform[] all = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.parent != null)
            {
                continue;
            }

            // 런타임 UI([...])는 이름에 힌트가 들어가도 제외한다.
            if (t.name.Length > 0 && t.name[0] == '[')
            {
                continue;
            }

            if (t.name.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (t.GetComponent<Camera>() != null
                || t.GetComponent<Light>() != null
                || t.GetComponent<Canvas>() != null
                || t.GetComponent<InteractionController>() != null)
            {
                continue;
            }

            if (t.GetComponent(componentType) == null)
            {
                t.gameObject.AddComponent(componentType);
                Debug.Log($"[상호작용] '{t.name}' 오브젝트를 {label}으로 자동 등록했습니다.");
            }
        }
    }

    // 씬에 "Shop" 이름으로 배치된 오브젝트를 상점으로 승격한다.
    // (구버전 호환: 내부적으로 AutoRegisterByHint를 쓴다)
    private void AutoRegisterShops()
    {
        AutoRegisterByHint(autoRegisterShopHint, typeof(ShopInteractable), "상점");
    }

    private void LateUpdate()
    {
        if (!UseManualInput) ProcessInput(InteractionInput.Capture(interactKey, closeKey, player), Time.deltaTime);
    }

    // Uses the same path for captured runtime input and deterministic integration input.
    public void ProcessInput(InteractionFrame input, float deltaTime)
    {
        EnsurePrompt();
        if (!input.Held) requireKeyRelease = false;
        if (UiModal.IsOpen)
        {
            CancelHold();
            prompt?.SetVisible(false);
            current = null;
            if (input.ClosePressed || input.Pressed) UiModal.RequestClose();
            return;
        }
        ResolvePlayer();
        PlayerHealth health = player != null ? player.GetComponent<PlayerHealth>() : null;
        if (player == null || health != null && health.IsDead || Time.timeScale <= 0 || !hasFocus)
        {
            CancelHold();
            prompt?.SetVisible(false);
            current = null;
            return;
        }
        bool interrupted = input.ActionPressed || input.ClosePressed || hitInterrupted || HoldActionsBlocked();
        hitInterrupted = false;
        if (hold != null)
        {
            if (!input.Held || interrupted || holdTarget == null || !holdTarget.CanInteract(player) ||
                holdTarget.DistanceTo(player.transform.position) > holdTarget.InteractionRadius)
                CancelHold();
            else
            {
                PositionPrompt(holdTarget);
                holdElapsed = Mathf.Min(holdDuration, holdElapsed + Mathf.Max(0, deltaTime));
                prompt.SetHoldMessage(interactKey.ToString(), hold.HoldLabel, HoldProgress);
                prompt.SetVisible(true);
                if (holdElapsed >= holdDuration)
                {
                    completionDispatched = true;
                    feedbackTarget = holdTarget;
                    HoldInteractionResult result;
                    try { result = hold.CompleteHold(player, sessionId); }
                    finally { ClearHold(); }
                    feedbackSuccess = result.Success;
                    feedbackMessage = result.Message;
                    feedbackUntil = Time.unscaledTime + (result.Success ? .1f : 2f);
                    if (result.Success) prompt.SetHoldMessage(interactKey.ToString(), result.Message ?? "완료", 1);
                    else prompt.SetMessage(result.Message);
                }
                return;
            }
        }
        if (Time.unscaledTime < feedbackUntil && !input.Pressed)
        {
            if (feedbackTarget == null) { prompt.SetVisible(false); return; }
            PositionPrompt(feedbackTarget);
            if (feedbackSuccess) prompt.SetHoldMessage(interactKey.ToString(), feedbackMessage ?? "완료", 1);
            else prompt.SetMessage(feedbackMessage);
            prompt.SetVisible(true);
            return;
        }
        current = FindNearestInteractable(player.transform.position, out currentDistance);
        PositionPrompt(current);
        prompt.SetVisible(current != null);
        if (current == null) { LogDiagnostics(); return; }
        if (current is IHoldInteractable promptHold)
            prompt.SetHoldMessage(interactKey.ToString(), promptHold.HoldLabel, 0);
        else prompt.SetMessage(current.InteractionPrompt);
        if (requireKeyRelease) return;
        if (!input.Pressed)
        {
            if (input.SecondaryPressed && current is ISecondaryInteractable secondary)
                secondary.InteractSecondary(player);
            return;
        }
        requireKeyRelease = true;
        if (current is IHoldInteractable targetHold)
        {
            if (interrupted || !input.Held || targetHold.HoldDuration <= 0 ||
                float.IsNaN(targetHold.HoldDuration) || float.IsInfinity(targetHold.HoldDuration)) return;
            holdTarget = current; hold = targetHold; holdElapsed = 0;
            holdDuration = targetHold.HoldDuration; completionDispatched = false; sessionId++;
            hold.BeginHold(player, sessionId);
        }
        else current.Interact(player);
    }

    private void PositionPrompt(InteractableBehaviour target)
    {
        if (target is IWorldInteractionPrompt anchor) prompt.Follow(anchor.PromptAnchor, anchor.PromptOffset);
        else prompt.Dock();
    }

    private bool HoldActionsBlocked()
    {
        PlayerMove move = player.GetComponent<PlayerMove>();
        PlayerAttack attack = player.GetComponent<PlayerAttack>();
        PlayerDash dash = player.GetComponent<PlayerDash>();
        return move != null && (!move.IsGrounded || move.ControlLocked) ||
            attack != null && attack.IsAttacking || dash != null && dash.IsDashing;
    }

    public void CancelHold()
    {
        if (holdTarget != null) hold?.CancelHold(player, sessionId);
        ClearHold();
        prompt?.SetVisible(false);
    }

    private void ClearHold()
    {
        hold = null; holdTarget = null; holdElapsed = 0; holdDuration = 0;
        completionDispatched = false; requireKeyRelease = true;
    }

    private void HandleCombatEvent(DoctrineEventData evt)
    {
        if (evt.SubjectObject != player || evt.Condition != ConditionType.OnHit) return;
        hitInterrupted = true;
        CancelHold();
    }

    private void OnApplicationFocus(bool focused)
    {
        hasFocus = focused || Application.isBatchMode;
        if (!focused) CancelHold();
    }

    private void OnApplicationPause(bool paused) { if (paused) CancelHold(); }

    private InteractableBehaviour FindNearestInteractable(Vector3 position, out float distance)
    {
        InteractableBehaviour nearest = null;
        distance = float.MaxValue;

        var all = InteractableBehaviour.Instances;
        for (int i = all.Count - 1; i >= 0; i--)
        {
            InteractableBehaviour candidate = all[i];
            if (candidate == null)
            {
                continue;
            }

            if (!candidate.CanInteract(player))
            {
                continue;
            }

            float candidateDistance = candidate.DistanceTo(position);
            if (candidateDistance > candidate.InteractionRadius) continue;
            if (candidateDistance < distance || Mathf.Approximately(candidateDistance, distance) && candidate == current)
            {
                distance = candidateDistance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private void ResolvePlayer()
    {
        if (player != null && player.activeInHierarchy)
        {
            return;
        }

        if (Time.unscaledTime < nextPlayerSearchTime)
        {
            return;
        }

        nextPlayerSearchTime = Time.unscaledTime + 0.5f;

        PlayerHealth health = Object.FindFirstObjectByType<PlayerHealth>();
        if (health != null)
        {
            player = health.gameObject;
            return;
        }

        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null)
        {
            player = tagged;
        }
    }

    // 대상이 없을 때 "왜 안 되는지"를 알 수 있게 주기적으로 상태를 남긴다.
    private void LogDiagnostics()
    {
        if (!logDiagnostics || Time.unscaledTime < nextDiagnosticTime)
        {
            return;
        }

        nextDiagnosticTime = Time.unscaledTime + 2f;

        if (current != null)
        {
            Debug.Log($"[상호작용] 가장 가까운 대상: {current.InteractionName} " +
                $"({currentDistance:F2}m / 필요 {current.InteractionRadius:F2}m)");
            return;
        }

        int registered = InteractableBehaviour.Instances.Count;
        Debug.Log($"[상호작용] 등록된 대상 {registered}개. " +
            "씬에 상호작용 오브젝트(예: 이름에 Shop)가 있는지 확인하세요.");
    }

    private void LogSceneSummary(string label)
    {
        int registered = InteractableBehaviour.Instances.Count;
        GameObject found = player;
        Debug.Log($"[상호작용] {label}: 등록된 대상 {registered}개, " +
            $"플레이어 {(found != null ? found.name : "미발견")}");
    }
}
