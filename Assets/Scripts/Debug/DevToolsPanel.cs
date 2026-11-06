using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Developer tool panel, available in every scene. Hidden by default; F2 shows or hides it.
/// The layout is the prefab Resources/UI/DevTools, so it can be edited in the editor.
/// Buttons: +100 coins, 10 damage, death, and "reset the tutorial first-run flag and go to the title".
/// </summary>
public sealed class DevToolsPanel : MonoBehaviour
{
    private const KeyCode ToggleKey = KeyCode.F2;
    private const string PrefabPath = "UI/DevTools";

    private static DevToolsPanel instance;
    private GameObject panel;
    private DevToolsRefs refs;
    private readonly object modalToken = new object();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBoot()
    {
        if (instance != null) return;
        var go = new GameObject("[DevTools]");
        DontDestroyOnLoad(go);
        go.AddComponent<DevToolsPanel>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("DevTools prefab is missing: Resources/" + PrefabPath, this); enabled = false; return; }
        panel = Instantiate(prefab, transform);
        refs = panel.GetComponent<DevToolsRefs>();
        if (refs == null || !refs.IsComplete) { Debug.LogError("DevTools prefab needs a complete DevToolsRefs.", this); enabled = false; return; }

        foreach (UnityEngine.UI.Text text in panel.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            text.font = UiTheme.GetFont();
        refs.GrantCoins.onClick.AddListener(GrantCoins);
        refs.Damage.onClick.AddListener(Damage);
        refs.Death.onClick.AddListener(Die);
        refs.ResetAndGoToTitle.onClick.AddListener(ResetAndGoToTitle);
        panel.SetActive(false);
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey)) SetOpen(!panel.activeSelf);
        if (!panel.activeSelf) return;

        // Damage and death need a living player in the scene (not in the title, for example).
        PlayerHealth player = FindPlayer();
        bool hasPlayer = player != null && !player.IsDead;
        refs.Damage.interactable = hasPlayer;
        refs.Death.interactable = hasPlayer;
        refs.GrantCoins.interactable = Currency.IsReady;
    }

    // While open, game input is blocked so clicking a button does not also attack.
    private void SetOpen(bool open)
    {
        panel.SetActive(open);
        if (open) { UiEventSystemBoot.Ensure(); UiModal.Push(modalToken); }
        else UiModal.Pop(modalToken);
    }

    private void OnDisable() { UiModal.Pop(modalToken); }

    private static PlayerHealth FindPlayer() => FindFirstObjectByType<PlayerHealth>();

    private static void GrantCoins() => Currency.TryGrant(100, "dev.tools.grant");

    private static void Damage()
    {
        PlayerHealth player = FindPlayer();
        if (player != null && !player.IsDead) player.TakeDamage(10f);
    }

    private static void Die()
    {
        PlayerHealth player = FindPlayer();
        if (player != null && !player.IsDead) player.KillForTest();
    }

    // Same as the editor menu "Tools/테스트/튜토리얼 최초 실행 초기화", then back to the title.
    private void ResetAndGoToTitle()
    {
        PlayerPrefs.DeleteKey(TitleScreen.TutorialStartedKey);
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        SetOpen(false);
        SceneManager.LoadScene(TitleScreen.SceneName);
    }
}
