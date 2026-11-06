using UnityEngine;
using UnityEngine.UI;

public sealed class EndingSceneController : MonoBehaviour
{
    private void Start()
    {
        Time.timeScale = 1f;
        EnsureCamera();

        Canvas canvas = DeathResultView.CreateCanvas("Temporary Ending", 1000);
        canvas.transform.SetParent(transform, false);
        RectTransform backdrop = DeathResultView.Box("Ending backdrop", canvas.transform,
            new Color(.035f, .045f, .075f, 1f), 0f, 0f, 1f, 1f);
        backdrop.GetComponent<Image>().raycastTarget = false;

        RectTransform frame = DeathResultView.Box("Ending frame", backdrop, DeathResultView.Ink,
            .27f, .22f, .73f, .78f);
        RectTransform panel = DeathResultView.Box("Ending panel", frame, DeathResultView.Wood,
            .012f, .02f, .988f, .98f);

        DeathResultView.Label(panel, "− THE END −", 48, DeathResultView.Paper,
            .08f, .68f, .92f, .88f);
        DeathResultView.Label(panel, "보스를 쓰러뜨렸습니다", 30, DeathResultView.Paper,
            .08f, .45f, .92f, .62f);
        DeathResultView.Label(panel, "이 이야기는 계속됩니다", 20, DeathResultView.Paper,
            .08f, .32f, .92f, .43f);

        Button quit = DeathResultView.MenuButton(panel, "게임 종료",
            .32f, .10f, .68f, .25f, QuitGame);
        UiEventSystemBoot.Ensure().SetSelectedGameObject(quit.gameObject);
    }

    private static void EnsureCamera()
    {
        if (Camera.main != null) return;

        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .045f, .075f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
