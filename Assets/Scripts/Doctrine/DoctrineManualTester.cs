using UnityEngine;

// Unity Editor에서 실행하는 재현형 수동 테스트 하네스.
// Test Runner를 열기 어려운 경우 이 컴포넌트를 빈 GameObject에 붙이고
// 인스펙터 ContextMenu "Doctrine: Run All Checks"를 실행하면
// 명령서의 테스트 기준 7항목에 대한 PASS/FAIL 로그가 Console에 출력된다.
// autoRunOnStart를 켜면 Play 시작 시 자동으로 실행된다.
public class DoctrineManualTester : MonoBehaviour
{
    public bool autoRunOnStart = false;

    public int LastPassed { get; private set; }
    public int LastFailed { get; private set; }

    private void Start()
    {
        if (autoRunOnStart)
        {
            RunAllChecks();
        }
    }

    [ContextMenu("Doctrine: Run All Checks")]
    public void RunAllChecks()
    {
        int passed = 0;
        int failed = 0;

        DoctrineManager manager = DoctrineManager.Instance;
        if (manager == null)
        {
            GameObject managerGo = new GameObject("[DoctrineManager]");
            manager = managerGo.AddComponent<DoctrineManager>();
        }

        // AddComponent 메시지가 동작하지 않는 실행 환경에서도 구독 보장.
        manager.EnsureSubscribed();
        manager.Clear();

        // 1. 교리 하나를 생성하고 활성화할 수 있다.
        {
            Doctrine doctrine = new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal, 30f);
            bool ok = manager.TryActivate(doctrine, out string reason);
            Check(ok && manager.ActiveCount == 1, "1. 교리 생성/활성화", reason, ref passed, ref failed);
            manager.Clear();
        }

        // 2. 피격 이벤트 -> OnHit 교리 실행.
        {
            GameObject player = CreateCombatant("TestPlayer", SubjectType.Player, 100f);
            GameObject enemy = CreateCombatant("TestEnemy", SubjectType.Enemy, 100f);
            Health health = player.GetComponent<Health>();
            health.ForceHealth(50f);
            manager.TryActivate(
                new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal, 30f),
                player, out string _);
            health.TakeDamage(10f, enemy);
            Check(Mathf.Approximately(health.CurrentHealth, 70f),
                "2. 피격 시 OnHit+Heal 발동 (기대 HP 70)",
                "실제 HP: " + health.CurrentHealth, ref passed, ref failed);
            Cleanup(player, enemy);
            manager.Clear();
        }

        // 3. 조건 불일치 교리 미실행.
        {
            GameObject player = CreateCombatant("TestPlayer", SubjectType.Player, 100f);
            GameObject enemy = CreateCombatant("TestEnemy", SubjectType.Enemy, 100f);
            Health health = player.GetComponent<Health>();
            health.ForceHealth(50f);
            manager.TryActivate(
                new Doctrine(SubjectType.Player, ConditionType.OnAttack, ResultType.Heal, 30f),
                player, out string _);
            health.TakeDamage(10f, enemy);
            Check(Mathf.Approximately(health.CurrentHealth, 40f),
                "3. 조건 불일치 교리 미실행 (기대 HP 40)",
                "실제 HP: " + health.CurrentHealth, ref passed, ref failed);
            Cleanup(player, enemy);
            manager.Clear();
        }

        // 4. 동일 조건 두 교리 동시 발동.
        {
            GameObject player = CreateCombatant("TestPlayer", SubjectType.Player, 100f);
            GameObject enemy = CreateCombatant("TestEnemy", SubjectType.Enemy, 100f);
            Health health = player.GetComponent<Health>();
            DoctrineStats stats = player.GetComponent<DoctrineStats>();
            health.ForceHealth(50f);
            float attackBefore = stats.EffectiveAttack;
            manager.TryActivate(
                new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal, 20f),
                player, out string _);
            manager.TryActivate(
                new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.IncreaseAttack, 1.5f, 0f),
                player, out string _);
            health.TakeDamage(10f, enemy);
            bool hpOk = Mathf.Approximately(health.CurrentHealth, 60f);
            bool atkOk = Mathf.Approximately(stats.EffectiveAttack, attackBefore * 1.5f);
            Check(hpOk && atkOk, "4. 두 교리 동시 발동",
                $"HP:{health.CurrentHealth} ATK:{stats.EffectiveAttack}", ref passed, ref failed);
            Cleanup(player, enemy);
            manager.Clear();
        }

        // 5. 잘못된 교리는 거부 + 이유 반환.
        {
            Doctrine invalid = new Doctrine(SubjectType.None, ConditionType.None, ResultType.None);
            bool ok = manager.TryActivate(invalid, out string reason);
            Check(!ok && !string.IsNullOrEmpty(reason), "5. 잘못된 교리 거부",
                "이유: " + reason, ref passed, ref failed);
            manager.Clear();
        }

        // 6. 해제 후 미발동.
        {
            GameObject player = CreateCombatant("TestPlayer", SubjectType.Player, 100f);
            GameObject enemy = CreateCombatant("TestEnemy", SubjectType.Enemy, 100f);
            Health health = player.GetComponent<Health>();
            Doctrine doctrine = new Doctrine(
                SubjectType.Player, ConditionType.OnHit, ResultType.Heal, 30f);
            manager.TryActivate(doctrine, player, out string _);
            bool removed = manager.Deactivate(doctrine);
            health.ForceHealth(50f);
            health.TakeDamage(10f, enemy);
            Check(removed && Mathf.Approximately(health.CurrentHealth, 40f),
                "6. 해제 후 미발동 (기대 HP 40)",
                "실제 HP: " + health.CurrentHealth, ref passed, ref failed);
            Cleanup(player, enemy);
            manager.Clear();
        }

        // 7. 신규 조합(All + OnDodge + IncreaseSpeed)이 코드 수정 없이 공통 경로로 동작.
        {
            GameObject player = CreateCombatant("TestPlayer", SubjectType.Player, 100f);
            DoctrineStats stats = player.GetComponent<DoctrineStats>();
            manager.TryActivate(
                new Doctrine(SubjectType.All, ConditionType.OnDodge, ResultType.IncreaseSpeed, 2f, 0f),
                out string _);
            DoctrineEventBus.Raise(ConditionType.OnDodge, player);
            Check(Mathf.Approximately(stats.SpeedMultiplier, 2f),
                "7. 신규 조합 공통 경로 발동 (기대 속도배율 2)",
                "실제 배율: " + stats.SpeedMultiplier, ref passed, ref failed);
            Cleanup(player);
            manager.Clear();
        }

        Debug.Log($"[DoctrineTest] 완료: PASS {passed}, FAIL {failed}.");
        LastPassed = passed;
        LastFailed = failed;
    }

    private GameObject CreateCombatant(string name, SubjectType identity, float maxHp)
    {
        GameObject go = new GameObject(name);
        DoctrineSubject subject = go.AddComponent<DoctrineSubject>();
        subject.Identity = identity;
        go.AddComponent<Health>().SetMaxHealth(maxHp);
        go.AddComponent<DoctrineStats>();
        return go;
    }

    private void Cleanup(params GameObject[] objects)
    {
        foreach (GameObject go in objects)
        {
            if (go == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }
    }

    private void Check(bool condition, string name, string detail, ref int passed, ref int failed)
    {
        if (condition)
        {
            passed++;
            Debug.Log($"[DoctrineTest] PASS: {name} ({detail})");
        }
        else
        {
            failed++;
            Debug.LogError($"[DoctrineTest] FAIL: {name} ({detail})");
        }
    }
}
