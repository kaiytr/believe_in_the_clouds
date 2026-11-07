#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

// Batchmode 테스트 실행용 진입점.
// 사용법:
//   Unity -batchmode -projectPath <proj> -executeMethod DoctrineBatchRunner.Run -quit
// 수동 테스트 하네스(DoctrineManualTester)와 동일한 검사를 실행하고,
// 실패가 있으면 0이 아닌 코드로 종료한다.
public static class DoctrineBatchRunner
{
    public static void Run()
    {
        DoctrineManager manager = DoctrineManager.Instance;
        GameObject managerGo = null;
        if (manager == null)
        {
            managerGo = new GameObject("[DoctrineManager]");
            manager = managerGo.AddComponent<DoctrineManager>();
        }

        GameObject testerGo = new GameObject("[DoctrineBatchTester]");
        DoctrineManualTester tester = testerGo.AddComponent<DoctrineManualTester>();
        tester.RunAllChecks();

        int failed = tester.LastFailed;
        Debug.Log($"[DoctrineBatch] PASS={tester.LastPassed} FAIL={failed}.");

#if UNITY_EDITOR
        EditorApplication.Exit(failed == 0 ? 0 : 2);
#else
        Debug.LogError("[DoctrineBatch] UNITY_EDITOR가 아니라 종료 코드를 반환할 수 없습니다.");
#endif
    }
}
