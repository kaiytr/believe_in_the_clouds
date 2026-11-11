using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TitleScreen : MonoBehaviour
{
    public const string SceneName = "Title";
    public const string TutorialStartedKey = "believe_in_the_clouds.tutorial_started";
    private const string BackgroundResource = "UI/Title/TitleBackground";
    private const float ArtworkWidth = 1920f;
    private const float ArtworkHeight = 1080f;

    [Header("BGM")]
    [SerializeField] private AudioClip titleBgm;
    [SerializeField, Range(0f, 1f)] private float titleBgmVolume = 0.6f;

    private Button start;
    private Button quit;
    private Text message;
    private AudioSource bgmSource;
    private bool loading;

    private void Start()
    {
        Time.timeScale = 1f;
        EconomyRuntime.Ensure();
        PlayTitleBgm();

        Canvas canvas = DeathResultView.CreateCanvas("Title menu", 1000);
        canvas.transform.SetParent(transform, false);
        CreateBackground(canvas.transform);

        RectTransform menu = CreateButtonLayout(canvas.transform);
        start = CreateArtworkButton(menu, "시작하기", "TitleStartButton", "TitleStartButtonPressed",
            new Rect(782f, 171f, 357f, 78f), StartGame);
        quit = CreateArtworkButton(menu, "종료하기", "TitleQuitButton", "TitleQuitButtonPressed",
            new Rect(782f, 67f, 357f, 78f), QuitGame);
        message = DeathResultView.Label(menu, "", 17, DeathResultView.Paper,
            .20f, .255f, .80f, .31f);

        UiEventSystemBoot.Ensure().SetSelectedGameObject(start.gameObject);
    }

    // 타이틀 화면에서는 이 씬 전용 AudioSource로 BGM을 반복 재생한다.
    // 씬을 떠나면 오브젝트와 함께 정리되므로 다른 씬으로 번지지 않는다.
    private void PlayTitleBgm()
    {
        if (titleBgm == null)
        {
            Debug.LogWarning("타이틀 BGM 클립이 연결되지 않았습니다.", this);
            return;
        }

        EnsureAudioListener();

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f; // 2D
        bgmSource.volume = titleBgmVolume;
        bgmSource.clip = titleBgm;
        bgmSource.Play();
    }

    // 타이틀 씬에는 카메라가 없어 AudioListener도 없다. 소리가 들리려면 하나 필요하다.
    private static void EnsureAudioListener()
    {
        if (AudioListener.pause)
            AudioListener.pause = false;

        if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() != null)
            return;

        var listener = new GameObject("Title Audio Listener", typeof(AudioListener));
        listener.transform.SetParent(null, false);
    }

    private static RectTransform CreateButtonLayout(Transform parent)
    {
        var layout = new GameObject("Title button layout", typeof(RectTransform), typeof(AspectRatioFitter));
        RectTransform rect = layout.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = rect.sizeDelta = Vector2.zero;

        AspectRatioFitter fitter = layout.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = ArtworkWidth / ArtworkHeight;
        return rect;
    }

    private static Button CreateArtworkButton(Transform parent, string label, string normalResource,
        string pressedResource, Rect bounds, Action action)
    {
        float x0 = bounds.xMin / ArtworkWidth, y0 = bounds.yMin / ArtworkHeight;
        float x1 = bounds.xMax / ArtworkWidth, y1 = bounds.yMax / ArtworkHeight;
        Sprite normal = Resources.Load<Sprite>("UI/Title/" + normalResource);
        if (normal == null)
        {
            Debug.LogError("타이틀 버튼 이미지를 불러오지 못했습니다: " + normalResource);
            return DeathResultView.MenuButton(parent, label, x0, y0, x1, y1, action);
        }

        Sprite pressed = Resources.Load<Sprite>("UI/Title/" + pressedResource);
        var artwork = new GameObject(label + " artwork", typeof(RectTransform), typeof(Image));
        artwork.transform.SetParent(parent, false);
        DeathResultView.Place(artwork.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);
        Image image = artwork.GetComponent<Image>();
        image.sprite = normal;
        image.raycastTarget = false;

        // 원본의 투명 여백은 보존하고 실제 버튼 테두리 안에서만 입력을 받는다.
        RectTransform hitArea = DeathResultView.Box(label + " hit area", parent, Color.clear, x0, y0, x1, y1);
        hitArea.GetComponent<Image>().raycastTarget = true;
        Button button = hitArea.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = normal,
            selectedSprite = normal,
            pressedSprite = pressed != null ? pressed : normal,
            disabledSprite = normal
        };
        button.onClick.AddListener(() => action());
        return button;
    }

    private static void CreateBackground(Transform parent)
    {
        Sprite backgroundSprite = Resources.Load<Sprite>(BackgroundResource);
        if (backgroundSprite == null)
        {
            RectTransform fallback = DeathResultView.Box("Missing title background", parent,
                new Color(.13f, .16f, .21f), 0f, 0f, 1f, 1f);
            fallback.GetComponent<Image>().raycastTarget = false;
            Debug.LogError("타이틀 배경 이미지를 불러오지 못했습니다: " + BackgroundResource);
            return;
        }

        RectTransform viewport = new GameObject("Title background viewport", typeof(RectTransform))
            .GetComponent<RectTransform>();
        viewport.SetParent(parent, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = viewport.offsetMax = Vector2.zero;

        GameObject artwork = new GameObject("Title background artwork", typeof(RectTransform),
            typeof(Image), typeof(AspectRatioFitter));
        RectTransform artworkRect = artwork.GetComponent<RectTransform>();
        artworkRect.SetParent(viewport, false);
        artworkRect.anchorMin = artworkRect.anchorMax = new Vector2(.5f, .5f);
        artworkRect.anchoredPosition = Vector2.zero;
        artworkRect.sizeDelta = Vector2.zero;

        Image image = artwork.GetComponent<Image>();
        image.sprite = backgroundSprite;
        image.raycastTarget = false;

        AspectRatioFitter fitter = artwork.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = backgroundSprite.rect.width / backgroundSprite.rect.height;
    }

    private void StartGame()
    {
        if (loading) return;
        if (Currency.Service == null || !Currency.Service.IsReady)
        {
            message.text = "저장 데이터를 확인할 수 없습니다. 저장 파일을 확인해 주세요.";
            return;
        }

        bool firstStart = PlayerPrefs.GetInt(TutorialStartedKey, 0) == 0;
        bool enterTutorial = Currency.Service.TutorialState == TutorialProgress.NotCompleted;
        string destination = enterTutorial ? "Tutorial" : LobbyFlowRuntime.LobbyScene;
        try
        {
            loading = true;
            start.interactable = quit.interactable = false;
            Time.timeScale = 1f;

            PlayerPrefs.SetInt(TutorialStartedKey, 1);
            PlayerPrefs.Save();

            if (SceneManager.LoadSceneAsync(destination) == null)
                throw new InvalidOperationException("씬을 불러올 수 없습니다: " + destination);
        }
        catch (Exception ex)
        {
            if (firstStart)
            {
                PlayerPrefs.DeleteKey(TutorialStartedKey);
                PlayerPrefs.Save();
            }
            loading = false;
            start.interactable = quit.interactable = true;
            message.text = ex.Message;
        }
    }

    private void QuitGame()
    {
        if (loading) return;
        loading = true;
        start.interactable = quit.interactable = false;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
