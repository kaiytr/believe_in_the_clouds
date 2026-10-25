using UnityEngine;
using UnityEngine.SceneManagement;

// 공통 상호작용 컨트롤러 (E 키).
// 상점 전용이 아니다. 씬의 모든 IInteractable 중 가장 가까운 하나를 대상으로 삼는다.
//
// - 자동 부팅: 어떤 씬에서든 스스로 생성되고 DontDestroyOnLoad로 유지된다.
//   Tools 메뉴도, 씬에 미리 배치할 필요도 없다.
// - 안내 문구: 대상이 있으면 대상의 InteractionPrompt를 하단에 표시한다.
// - 입력: E 하나만 쓴다. 모달 UI가 열려 있으면 E/ESC는 "닫기"로 쓰인다.
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
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
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
        // 씬이 바뀌면 참조가 끊긴다.
        player = null;
        current = null;
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

    private void Update()
    {
        // 1) 모달 UI가 열려 있으면 월드 상호작용은 하지 않는다.
        if (UiModal.IsOpen)
        {
            prompt?.SetVisible(false);
            current = null;

            if (InteractionInput.WasPressed(closeKey) || InteractionInput.WasPressed(interactKey))
            {
                UiModal.RequestClose();
            }

            return;
        }

        ResolvePlayer();
        if (player == null)
        {
            prompt?.SetVisible(false);
            current = null;
            return;
        }

        // 2) 가장 가까운 상호작용 대상 탐색.
        //    상점만이 아니라 모든 IInteractable이 후보가 된다.
        current = FindNearestInteractable(player.transform.position, out currentDistance);

        bool inRange = current != null && currentDistance <= current.InteractionRadius;

        if (inRange)
        {
            prompt?.SetMessage(current.InteractionPrompt);
        }

        prompt?.SetVisible(inRange);

        if (!inRange)
        {
            // E를 눌렀는데 반응이 없을 때 원인을 콘솔에서 바로 알 수 있게 한다.
            if (InteractionInput.WasPressed(interactKey))
            {
                if (current == null)
                {
                    Debug.Log("[상호작용] 주변에 상호작용 대상이 없습니다.");
                }
                else
                {
                    Debug.Log($"[상호작용] '{current.InteractionName}'이(가) 너무 멉니다. " +
                        $"거리 {currentDistance:F2}m / 필요 {current.InteractionRadius:F2}m");
                }
            }

            LogDiagnostics();
            return;
        }

        if (!InteractionInput.WasPressed(interactKey))
        {
            return;
        }

        // 실제 상호작용. 대상이 자기 동작을 스스로 처리한다.
        string name = current.InteractionName;
        current.Interact(player);
        Debug.Log($"[상호작용] 상호 작용 완료 — {name} ({player.name}, {currentDistance:F2}m)");
    }

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
            if (candidateDistance < distance)
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
