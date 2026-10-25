using UnityEngine;
using UnityEngine.SceneManagement;

// 씬에 직접 배치하지 않아도 어떤 씬을 열든 교리 시스템과 하단 HUD가 항상 존재하도록 보장한다.
// Tools 메뉴를 실행할 필요가 없다: 플레이 모드 진입과 씬 전환 때마다
// DoctrineManager/DoctrineHud가 없으면 자동으로 만들고, DontDestroyOnLoad로 씬 전환에도 유지한다.
// 씬에 이미 배치해 두었다면 그 오브젝트(인스펙터 설정 포함)를 그대로 승격해 쓴다.
public static class DoctrineHudBoot
{
    private const string HudObjectName = "[DoctrineHud]";
    private const string ManagerObjectName = "[DoctrineManager]";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBoot()
    {
        // 도메인 리로드를 끈 상태에서도 중복 구독되지 않도록 해제 후 구독한다.
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        EnsureManagerExists();
        EnsureHudExists();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureManagerExists();
        EnsureHudExists();
    }

    // 교리 실행 엔진. 없으면 만들어서 반환한다. 이미 있으면 그대로 반환한다.
    public static DoctrineManager EnsureManagerExists()
    {
        if (DoctrineManager.Instance != null)
        {
            // 씬에 배치된 매니저도 씬 전환에서 살아남게 한다.
            Object.DontDestroyOnLoad(DoctrineManager.Instance.gameObject);
            return DoctrineManager.Instance;
        }

        DoctrineManager existing = Object.FindFirstObjectByType<DoctrineManager>();
        if (existing != null)
        {
            Object.DontDestroyOnLoad(existing.gameObject);
            return existing;
        }

        GameObject managerObject = new GameObject(ManagerObjectName);
        Object.DontDestroyOnLoad(managerObject);
        return managerObject.AddComponent<DoctrineManager>();
    }

    // HUD가 없으면 만들어서 반환한다. 이미 있으면 그대로 반환한다.
    public static DoctrineHud EnsureHudExists()
    {
        DoctrineHud hud = Object.FindFirstObjectByType<DoctrineHud>();
        if (hud != null)
        {
            // 씬에 배치된 HUD도 씬 전환에서 살아남게 한다.
            Object.DontDestroyOnLoad(hud.gameObject);
            return hud;
        }

        GameObject hudObject = new GameObject(HudObjectName, typeof(RectTransform));
        Object.DontDestroyOnLoad(hudObject);
        return hudObject.AddComponent<DoctrineHud>();
    }
}
