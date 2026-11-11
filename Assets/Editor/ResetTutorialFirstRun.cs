using UnityEditor;
using UnityEngine;

internal static class TutorialFirstRunReset
{
    [MenuItem("Tools/테스트/튜토리얼 최초 실행 초기화")]
    private static void ResetTutorialFirstRun()
    {
        PlayerPrefs.DeleteKey(TitleScreen.TutorialStartedKey);
        PlayerPrefs.SetInt(EconomyRuntime.TutorialPreviewKey, 1);
        PlayerPrefs.Save();
        Debug.Log("다음 Editor Play는 새 메모리 프로필로 튜토리얼을 시작합니다. 기존 저장 파일은 유지됩니다.");
    }
}
