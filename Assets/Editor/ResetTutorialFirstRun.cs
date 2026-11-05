using UnityEditor;
using UnityEngine;

internal static class TutorialFirstRunReset
{
    private const string TutorialStartedKey = "believe_in_the_clouds.tutorial_started";

    [MenuItem("Tools/테스트/튜토리얼 최초 실행 초기화")]
    private static void ResetTutorialFirstRun()
    {
        PlayerPrefs.DeleteKey(TutorialStartedKey);
        PlayerPrefs.Save();
        Debug.Log("튜토리얼 최초 진입 상태를 초기화했습니다.");
    }
}
