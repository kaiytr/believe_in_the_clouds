using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TitleScreen : MonoBehaviour
{
    public const string SceneName = "Title";
    private const string TutorialStartedKey = "believe_in_the_clouds.tutorial_started";
    private const string BackgroundResource = "UI/Title/TitleBackground";

    private Button start;
    private Button quit;
    private Text message;
    private bool loading;

    private void Start()
    {
        Time.timeScale = 1f;
        EconomyRuntime.Ensure();

        Canvas canvas = DeathResultView.CreateCanvas("Title menu", 1000);
        canvas.transform.SetParent(transform, false);
        CreateBackground(canvas.transform);

        RectTransform menuPanel = DeathResultView.Box("Title menu backing", canvas.transform,
            new Color(.035f, .035f, .045f, .64f), .42f, .035f, .58f, .195f);
        start = DeathResultView.MenuButton(menuPanel, "시작하기", .08f, .56f, .92f, .90f, StartGame);
        quit = DeathResultView.MenuButton(menuPanel, "종료하기", .08f, .10f, .92f, .44f, QuitGame);
        message = DeathResultView.Label(canvas.transform, "", 17, DeathResultView.Paper,
            .25f, .20f, .75f, .245f);

        UiEventSystemBoot.Ensure().SetSelectedGameObject(start.gameObject);
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
        bool hasExistingRun = Currency.Service.Snapshot().hasRun;
        bool enterTutorial = firstStart && !hasExistingRun;
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
