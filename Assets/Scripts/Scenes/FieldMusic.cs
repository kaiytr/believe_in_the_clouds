using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Background music of the ordinary maps (tutorial, lobby, shop, combat stages). One persistent player keeps the
/// track running across those scene changes instead of restarting it each time. It fades out in the scenes that
/// have their own audio: the title, the boss stage and the ending.
/// </summary>
public sealed class FieldMusic : MonoBehaviour
{
    private const string ClipPath = "Sounds/FieldBgm";
    private const float Volume = .5f, FadeSeconds = .8f;
    private static readonly string[] SilentScenes = { "Title", "Boss", "Ending" };

    private static FieldMusic instance;
    private AudioSource source;
    private float targetVolume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("[Field Music]");
        DontDestroyOnLoad(go);
        go.AddComponent<FieldMusic>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        AudioClip clip = Resources.Load<AudioClip>(ClipPath);
        if (clip == null) { Debug.LogError("Field music clip is missing: Resources/" + ClipPath, this); enabled = false; return; }
        source = gameObject.AddComponent<AudioSource>();
        source.clip = clip; source.loop = true; source.playOnAwake = false;
        source.spatialBlend = 0f; source.volume = 0f;

        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply(scene);

    private void Apply(Scene scene)
    {
        targetVolume = System.Array.IndexOf(SilentScenes, scene.name) >= 0 ? 0f : Volume;
        if (targetVolume > 0f && !source.isPlaying) source.Play();
    }

    private void Update()
    {
        if (source == null) return;
        source.volume = Mathf.MoveTowards(source.volume, targetVolume, Volume / FadeSeconds * Time.unscaledDeltaTime);
        if (targetVolume <= 0f && source.volume <= 0f && source.isPlaying) source.Stop();
    }
}
