using UnityEditor;
using UnityEngine;

internal static class TutorialFirstRunReset
{
    [MenuItem("Tools/테스트/튜토리얼 최초 실행 초기화")]
    private static void ResetTutorialFirstRun()
    {
        PlayerPrefs.DeleteKey(TitleScreen.TutorialStartedKey);
        PlayerPrefs.Save();
        Debug.Log("튜토리얼 최초 진입 상태를 초기화했습니다.");
    }
}
