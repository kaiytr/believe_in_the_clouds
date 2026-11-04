using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TitleScreen : MonoBehaviour
{
    public const string SceneName = "Title";
    private Button start;
    private Button tutorial;
    private Text message;
    private bool loading;
    private void Start()
    {
        Time.timeScale = 1f;
        EconomyRuntime.Ensure();
        Canvas canvas = DeathResultView.CreateCanvas("Title menu", 1000);
        canvas.transform.SetParent(transform, false);
        var background = DeathResultView.Box("Background", canvas.transform, new Color(.13f, .16f, .21f), 0, 0, 1, 1);
        var frame = DeathResultView.Box("Title frame", background, DeathResultView.Wood, .20f, .20f, .80f, .80f);
        var panel = DeathResultView.Box("Title panel", frame, DeathResultView.Paper, .008f, .014f, .992f, .986f);
        DeathResultView.Label(panel, "BELIEVE IN THE CLOUDS", 36, DeathResultView.Ink, .05f, .58f, .95f, .91f);
        start = DeathResultView.MenuButton(panel, "로비로 이동", .20f, .36f, .80f, .50f, () => Enter(LobbyFlowRuntime.LobbyScene));
        tutorial = DeathResultView.MenuButton(panel, "튜토리얼", .20f, .18f, .80f, .32f, () => Enter("Tutorial"));
        message = DeathResultView.Label(panel, "", 18, DeathResultView.Ink, .05f, .03f, .95f, .15f);
        UiEventSystemBoot.Ensure().SetSelectedGameObject(start.gameObject);
    }
    private void Enter(string sceneName)
    {
        if (loading) return;
        if (Currency.Service == null || !Currency.Service.IsReady)
        { message.text = "저장 데이터를 확인할 수 없습니다. 저장 파일을 확인해 주세요."; return; }
        try
        {
            loading = true; start.interactable = tutorial.interactable = false;
            Time.timeScale = 1f;
            SceneManager.LoadSceneAsync(sceneName);
        }
        catch (Exception ex) { loading = false; start.interactable = tutorial.interactable = true; message.text = ex.Message; }
    }
}
