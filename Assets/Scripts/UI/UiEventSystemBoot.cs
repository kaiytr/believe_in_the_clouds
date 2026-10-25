using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// uGUI 클릭을 받으려면 씬에 EventSystem이 반드시 있어야 한다.
// 이 프로젝트의 씬에는 하나도 없어서 상점 카드가 클릭에 반응하지 않았다.
// 씬마다 배치하지 않아도 되도록 여기서 자동으로 만들고 유지한다.
//
// 입력 모듈: 이 프로젝트의 activeInputHandler는 Both(2)라 구 Input 기반인
// StandaloneInputModule을 쓴다. New 전용으로 바꾸면 InputSystemUIInputModule로
// 교체해야 한다(TryAddInputSystemModule 참고).
public static class UiEventSystemBoot
{
    private static GameObject booted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBoot()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Ensure();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Ensure();
    }

    /// <summary>EventSystem이 없으면 만든다. 이미 있으면 그대로 둔다.</summary>
    public static EventSystem Ensure()
    {
        EventSystem existing = Object.FindFirstObjectByType<EventSystem>();
        if (existing != null)
        {
            Object.DontDestroyOnLoad(existing.gameObject);
            return existing;
        }

        if (booted != null)
        {
            return booted.GetComponent<EventSystem>();
        }

        booted = new GameObject("[EventSystem]");
        Object.DontDestroyOnLoad(booted);

        EventSystem eventSystem = booted.AddComponent<EventSystem>();
        AddInputModule(booted);
        return eventSystem;
    }

    private static void AddInputModule(GameObject target)
    {
        // 구 Input API(Both/Old) 환경에서는 StandaloneInputModule이 가장 안전하다.
        if (target.GetComponent<BaseInputModule>() == null)
        {
            target.AddComponent<StandaloneInputModule>();
        }
    }
}
