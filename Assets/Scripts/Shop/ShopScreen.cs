using UnityEngine;

// 상점 화면 모듈.
// "상점 상호작용이 발생하면 패널을 열고, E/ESC로 닫는다"만 담당한다.
// 어떤 씬에서든 자동 생성되며, 상점 오브젝트가 없는 씬에서는 패널을 띄우지 않는다.
//
// 상점 전용 로직과 공통 상호작용 로직을 분리해 두었기 때문에,
// 나중에 다른 상호작용(문/NPC/제단)을 추가해도 이 파일은 건드리지 않는다.
public class ShopScreen : MonoBehaviour
{
    public static ShopScreen Instance { get; private set; }

    private ShopPanelView panel;
    private ShopInteractable openShop;

    public bool IsOpen => panel != null && panel.IsOpen;
    public ShopInteractable OpenShop => openShop;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBoot()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject go = new GameObject("[ShopScreen]");
        DontDestroyOnLoad(go);
        go.AddComponent<ShopScreen>();
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
        EnsurePanel();
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
        InteractableBehaviour.Interacted += HandleInteracted;
        UiModal.CloseRequested += HandleCloseRequested;
    }

    private void OnDisable()
    {
        InteractableBehaviour.Interacted -= HandleInteracted;
        UiModal.CloseRequested -= HandleCloseRequested;
    }

    private void EnsurePanel()
    {
        if (panel == null)
        {
            panel = ShopPanelView.BuildRuntime();
            DontDestroyOnLoad(panel.gameObject);
        }
    }

    private void HandleInteracted(InteractableBehaviour target, GameObject interactor)
    {
        if (target is not ShopInteractable shop)
        {
            return;
        }

        // 같은 상점을 다시 상호작용하면 닫기(토글).
        if (openShop == shop && IsOpen)
        {
            Close();
            return;
        }

        Open(shop, interactor);
    }

    private void HandleCloseRequested()
    {
        if (IsOpen)
        {
            Close();
        }
    }

    public void Open(ShopInteractable shop, GameObject interactor)
    {
        if (shop == null)
        {
            return;
        }

        EnsurePanel();
        openShop = shop;
        panel.Open(shop.ShopName);
        UiModal.Push(this);
        Debug.Log($"[상점] {shop.ShopName} 열림 ({interactor?.name})");
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        panel.Close();
        UiModal.Pop(this);

        ShopInteractable closed = openShop;
        openShop = null;
        Debug.Log($"[상점] {closed?.ShopName} 닫힘");
    }
}
