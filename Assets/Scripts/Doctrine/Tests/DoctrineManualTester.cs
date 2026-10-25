using UnityEngine;

// Manual reproduction harness run from the Unity Editor.
// Attach to an empty GameObject and run the inspector ContextMenu
// "Doctrine: Run All Checks" to print PASS/FAIL logs to the Console.
// Enable autoRunOnStart to run automatically on Play start.
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

        manager.EnsureSubscribed();
        manager.Clear();

        // 1. Create and activate one table doctrine.
        {
            Doctrine doctrine = new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal);
            bool ok = manager.TryActivate(doctrine, out string reason);
            Check(ok && manager.ActiveCount == 1, "1. Doctrine create/activate", reason, ref passed, ref failed);
            manager.Clear();
        }

        // 2. OnHit event -> table heal (+10% of 100 max = +10).
        {
            GameObject player = CreateCombatant("TestPlayer", SubjectType.Player, 100f);
            GameObject enemy = CreateCombatant("TestEnemy", SubjectType.Enemy, 100f);
            TestHealth health = player.GetComponent<TestHealth>();
            health.ForceHealth(50f);
            manager.TryActivate(
                new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal),
                player, out string _);
            health.TakeDamage(10f, enemy);
            Check(Mathf.Approximately(health.CurrentHealth, 50f),
                "2. OnHit+Heal on hit (expected HP 50)",
                "actual HP: " + health.CurrentHealth, ref passed, ref failed);
            Cleanup(player, enemy);
            manager.Clear();
        }

        // 3. Condition mismatch: no trigger.
        {
            GameObject player = CreateCombatant("TestPlayer", SubjectType.Player, 100f);
            GameObject enemy = CreateCombatant("TestEnemy", SubjectType.Enemy, 100f);
            TestHealth health = player.GetComponent<TestHealth>();
            health.ForceHealth(50f);
            manager.TryActivate(
                new Doctrine(SubjectType.Player, ConditionType.OnAttack, ResultType.Heal),
                player, out string _);
            health.TakeDamage(10f, enemy);
            Check(Mathf.Approximately(health.CurrentHealth, 40f),
                "3. Condition mismatch does not trigger (expected HP 40)",
                "actual HP: " + health.CurrentHealth, ref passed, ref failed);
            Cleanup(player, enemy);
            manager.Clear();
        }

        // 4. Two doctrines on the same condition both trigger (heal capped at trigger value here).
        {
            GameObject player = CreateCombatant("TestPlayer", SubjectType.Player, 100f);
            GameObject enemy = CreateCombatant("TestEnemy", SubjectType.Enemy, 100f);
            TestHealth health = player.GetComponent<TestHealth>();
            DoctrineStats stats = player.GetComponent<DoctrineStats>();
            health.ForceHealth(50f);
            float attackBefore = stats.EffectiveAttack;
            manager.TryActivate(
                new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal),
                player, out string _);
            manager.TryActivate(
                new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.IncreaseAttack),
                player, out string _);
            health.TakeDamage(10f, enemy);
            bool hpOk = Mathf.Approximately(health.CurrentHealth, 50f);
            bool atkOk = Mathf.Approximately(stats.EffectiveAttack, attackBefore * 1.2f);
            Check(hpOk && atkOk, "4. Two doctrines trigger together",
                $"HP:{health.CurrentHealth} ATK:{stats.EffectiveAttack}", ref passed, ref failed);
            Cleanup(player, enemy);
            manager.Clear();
        }

        // 5. Invalid doctrine rejected with reason.
        {
            Doctrine invalid = new Doctrine(SubjectType.None, ConditionType.None, ResultType.None);
            bool ok = manager.TryActivate(invalid, out string reason);
            Check(!ok && !string.IsNullOrEmpty(reason), "5. Invalid doctrine rejected",
                "reason: " + reason, ref passed, ref failed);
            manager.Clear();
        }

        // 6. No trigger after deactivation.
        {
            GameObject player = CreateCombatant("TestPlayer", SubjectType.Player, 100f);
            GameObject enemy = CreateCombatant("TestEnemy", SubjectType.Enemy, 100f);
            TestHealth health = player.GetComponent<TestHealth>();
            Doctrine doctrine = new Doctrine(
                SubjectType.Player, ConditionType.OnHit, ResultType.Heal);
            manager.TryActivate(doctrine, player, out string _);
            bool removed = manager.Deactivate(doctrine);
            health.ForceHealth(50f);
            health.TakeDamage(10f, enemy);
            Check(removed && Mathf.Approximately(health.CurrentHealth, 40f),
                "6. No trigger after deactivate (expected HP 40)",
                "actual HP: " + health.CurrentHealth, ref passed, ref failed);
            Cleanup(player, enemy);
            manager.Clear();
        }

        // 7. New table combo (All + LowHealth + IncreaseSpeed) via the common path.
        {
            GameObject player = CreateCombatant("TestPlayer", SubjectType.Player, 100f);
            DoctrineStats stats = player.GetComponent<DoctrineStats>();
            manager.TryActivate(
                new Doctrine(SubjectType.All, ConditionType.LowHealth, ResultType.IncreaseSpeed),
                out string _);
            DoctrineEventBus.Raise(ConditionType.LowHealth, player);
            Check(Mathf.Approximately(stats.SpeedMultiplier, 1.4f),
                "7. New combo via common path (expected speed 1.4)",
                "actual: " + stats.SpeedMultiplier, ref passed, ref failed);
            Cleanup(player);
            manager.Clear();
        }

        Debug.Log($"[DoctrineTest] done: PASS {passed}, FAIL {failed}.");
        LastPassed = passed;
        LastFailed = failed;
    }

    private GameObject CreateCombatant(string name, SubjectType identity, float maxHp)
    {
        GameObject go = new GameObject(name);
        DoctrineSubject subject = go.AddComponent<DoctrineSubject>();
        subject.Identity = identity;
        go.AddComponent<TestHealth>().SetMaxHealth(maxHp);
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
