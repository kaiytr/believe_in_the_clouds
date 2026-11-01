using UnityEngine;
using UnityEngine.SceneManagement;

// 재단 테스트용 조각 시드. FoundationTest 씬에서만 동작한다.
// 플레이 시작 시 인벤토리가 비어 있으면 테스트용 조각 6개를 넣어준다.
// 격리된 메모리 프로필만 사용해 일반 저장 데이터에 영향을 주지 않는다.
// 상점 구매 흐름을 타지 않고도 재단 왼쪽 목록/선택을 확인할 수 있다.
// 합성 기능 테스트가 끝나면 이 파일은 삭제하면 된다.
public static class FoundationDebugSeed
{
    private const string TargetScene = "FoundationTest";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Seed()
    {
        if (SceneManager.GetActiveScene().name != TargetScene)
        {
            return;
        }

        EconomyRuntime economy = EconomyRuntime.Instance;
        if (economy == null)
        {
            Debug.LogWarning("[재단테스트] EconomyRuntime을 찾을 수 없어 격리 프로필을 사용할 수 없습니다.");
            return;
        }

        economy.UseFoundationTestProfile();

        DoctrineInventory inventory = DoctrineInventory.Instance;
        if (inventory == null)
        {
            inventory = new GameObject("[DoctrineInventory]").AddComponent<DoctrineInventory>();
        }

        if (inventory.TotalCount > 0)
        {
            Debug.Log($"[재단테스트] 시드 생략 (이미 보유 중) → {inventory.Describe()}");
            return;
        }

        // 주어 2 / 조건 2 / 결과 2 — 각 탭에 2장씩 보이게 한다.
        inventory.AddSubject(SubjectType.Player);
        inventory.AddSubject(SubjectType.Enemy);
        inventory.AddCondition(ConditionType.OnHit);
        inventory.AddCondition(ConditionType.LowHealth);
        inventory.AddResult(ResultType.Heal);
        inventory.AddResult(ResultType.Explode);

        Debug.Log($"[재단테스트] 시드 완료 → {inventory.Describe()}");
    }
}
