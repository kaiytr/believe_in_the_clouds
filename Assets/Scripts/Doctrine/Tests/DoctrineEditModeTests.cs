using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Doctrine 시스템 EditMode 테스트. 명령서의 테스트 기준 7항목을 검증한다.
// Unity Editor에서 Window > General > Test Runner > EditMode로 실행한다.
// duration 0 = 영구 버프를 사용하므로 EditMode(코루틴 미진행)에서도 판정 가능하다.
[TestFixture]
public class DoctrineEditModeTests
{
    private DoctrineManager manager;
    private readonly List<GameObject> spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        DoctrineEventBus.RemoveAllListeners();
        GameObject go = new GameObject("[TestDoctrineManager]");
        manager = go.AddComponent<DoctrineManager>();
        // AddComponent 메시지가 동작하지 않는 실행 환경에서도 구독 보장.
        manager.EnsureSubscribed();
        spawned.Add(go);
    }

    [TearDown]
    public void TearDown()
    {
        if (manager != null)
        {
            manager.Clear();
        }

        foreach (GameObject go in spawned)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        spawned.Clear();
        DoctrineEventBus.RemoveAllListeners();
    }

    private GameObject CreateCombatant(string name, SubjectType identity, float maxHp)
    {
        GameObject go = new GameObject(name);
        DoctrineSubject subject = go.AddComponent<DoctrineSubject>();
        subject.Identity = identity;
        Health health = go.AddComponent<Health>();
        health.SetMaxHealth(maxHp);
        go.AddComponent<DoctrineStats>();
        spawned.Add(go);
        return go;
    }

    // 1. 교리 하나를 생성하고 활성화할 수 있다.
    [Test]
    public void Activate_SingleDoctrine_Succeeds()
    {
        Doctrine doctrine = new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal, 30f);

        bool ok = manager.TryActivate(doctrine, out string reason);

        Assert.IsTrue(ok, "활성화 실패: " + reason);
        Assert.AreEqual(1, manager.ActiveCount);
    }

    // 2. 피격 이벤트가 발생하면 OnHit 조건을 가진 교리가 실행된다.
    [Test]
    public void PlayerHit_TriggersOnHitHeal()
    {
        GameObject player = CreateCombatant("Player", SubjectType.Player, 100f);
        Health health = player.GetComponent<Health>();
        health.ForceHealth(50f);
        GameObject enemy = CreateCombatant("Enemy", SubjectType.Enemy, 100f);

        Assert.IsTrue(manager.TryActivate(
            new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal, 30f),
            player, out string reason), reason);

        health.TakeDamage(10f, enemy);

        Assert.AreEqual(70f, health.CurrentHealth, 0.001f);
    }

    // 3. 조건이 일치하지 않는 교리는 실행되지 않는다.
    [Test]
    public void ConditionMismatch_DoesNotTrigger()
    {
        GameObject player = CreateCombatant("Player", SubjectType.Player, 100f);
        Health health = player.GetComponent<Health>();
        health.ForceHealth(50f);
        GameObject enemy = CreateCombatant("Enemy", SubjectType.Enemy, 100f);

        Assert.IsTrue(manager.TryActivate(
            new Doctrine(SubjectType.Player, ConditionType.OnAttack, ResultType.Heal, 30f),
            player, out string reason), reason);

        health.TakeDamage(10f, enemy);

        Assert.AreEqual(40f, health.CurrentHealth, 0.001f);
    }

    // 4. 두 교리의 조건이 일치하면 두 효과가 모두 실행된다.
    [Test]
    public void TwoMatchingDoctrines_BothTrigger()
    {
        GameObject player = CreateCombatant("Player", SubjectType.Player, 100f);
        Health health = player.GetComponent<Health>();
        DoctrineStats stats = player.GetComponent<DoctrineStats>();
        health.ForceHealth(50f);
        float attackBefore = stats.EffectiveAttack;
        GameObject enemy = CreateCombatant("Enemy", SubjectType.Enemy, 100f);

        Assert.IsTrue(manager.TryActivate(
            new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal, 20f),
            player, out string r1), r1);
        Assert.IsTrue(manager.TryActivate(
            new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.IncreaseAttack, 1.5f, 0f),
            player, out string r2), r2);

        health.TakeDamage(10f, enemy);

        Assert.AreEqual(60f, health.CurrentHealth, 0.001f);
        Assert.AreEqual(attackBefore * 1.5f, stats.EffectiveAttack, 0.001f);
    }

    // 5. 잘못된 교리는 활성화되지 않고 실패 이유를 확인할 수 있다.
    [Test]
    public void InvalidDoctrine_RejectedWithReason()
    {
        Doctrine invalid = new Doctrine(SubjectType.None, ConditionType.None, ResultType.None);

        bool ok = manager.TryActivate(invalid, out string reason);

        Assert.IsFalse(ok);
        Assert.IsFalse(string.IsNullOrEmpty(reason), "실패 이유가 비어 있다.");
    }

    // 5-2. 효과 실행에 필요한 컴포넌트가 없는 대상 바인딩도 거부된다.
    [Test]
    public void DoctrineBoundToTargetWithoutHealth_RejectedWithReason()
    {
        GameObject bare = new GameObject("Bare");
        spawned.Add(bare);
        DoctrineSubject subject = bare.AddComponent<DoctrineSubject>();
        subject.Identity = SubjectType.Player;

        bool ok = manager.TryActivate(
            new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal, 10f),
            bare, out string reason);

        Assert.IsFalse(ok);
        Assert.IsFalse(string.IsNullOrEmpty(reason));
    }

    // 6. 교리를 해제하면 이후 이벤트에서 해당 교리가 실행되지 않는다.
    [Test]
    public void DeactivatedDoctrine_NoLongerTriggers()
    {
        GameObject player = CreateCombatant("Player", SubjectType.Player, 100f);
        Health health = player.GetComponent<Health>();
        GameObject enemy = CreateCombatant("Enemy", SubjectType.Enemy, 100f);
        Doctrine doctrine = new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal, 30f);

        Assert.IsTrue(manager.TryActivate(doctrine, player, out string reason), reason);
        Assert.IsTrue(manager.Deactivate(doctrine));

        health.ForceHealth(50f);
        health.TakeDamage(10f, enemy);

        Assert.AreEqual(40f, health.CurrentHealth, 0.001f);
        Assert.AreEqual(0, manager.ActiveCount);
    }

    // 7. 새 교리 조합이 기존 전투/이동 코드 수정 없이 공통 경로로 동작한다.
    //    (이전 테스트에서 쓰지 않은 All + OnDodge + IncreaseSpeed 조합 사용)
    [Test]
    public void NewCombination_WorksWithoutCodeChange()
    {
        GameObject player = CreateCombatant("Player", SubjectType.Player, 100f);
        DoctrineStats stats = player.GetComponent<DoctrineStats>();

        Assert.IsTrue(manager.TryActivate(
            new Doctrine(SubjectType.All, ConditionType.OnDodge, ResultType.IncreaseSpeed, 2f, 0f),
            out string reason), reason);

        DoctrineEventBus.Raise(ConditionType.OnDodge, player);

        Assert.AreEqual(2f, stats.SpeedMultiplier, 0.001f);
    }

    // Subject 매칭 규칙: All은 모두 일치, 타입 불일치는 제외.
    [Test]
    public void MatchesRule_AllMatchesEverySubject()
    {
        Doctrine all = new Doctrine(SubjectType.All, ConditionType.OnHit, ResultType.Heal);
        Doctrine playerOnly = new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal);

        Assert.IsTrue(DoctrineManager.MatchesRule(all, SubjectType.Enemy, ConditionType.OnHit));
        Assert.IsFalse(DoctrineManager.MatchesRule(playerOnly, SubjectType.Enemy, ConditionType.OnHit));
        Assert.IsTrue(DoctrineManager.MatchesRule(playerOnly, SubjectType.Player, ConditionType.OnHit));
    }

    // Explode의 피해 적용부가 지정 대상을 제외하고 주변에 피해를 준다.
    [Test]
    public void Explode_AppliesDamageToOthersExcludingSelf()
    {
        GameObject self = CreateCombatant("Self", SubjectType.Player, 100f);
        GameObject other = CreateCombatant("Other", SubjectType.Enemy, 100f);
        Health selfHealth = self.GetComponent<Health>();
        Health otherHealth = other.GetComponent<Health>();

        List<Health> targets = new List<Health> { selfHealth, otherHealth };

        bool ok = DoctrineEffectExecutor.ApplyExplodeDamage(targets, 25f, null, selfHealth);

        Assert.IsTrue(ok);
        Assert.AreEqual(100f, selfHealth.CurrentHealth, 0.001f);
        Assert.AreEqual(75f, otherHealth.CurrentHealth, 0.001f);
    }

    // LowHealth 이벤트가 체력 임계값 돌파 시 발생한다.
    [Test]
    public void LowHealth_EventRaisedOnThresholdCross()
    {
        GameObject player = CreateCombatant("Player", SubjectType.Player, 100f);
        Health health = player.GetComponent<Health>();
        int lowHealthCount = 0;
        DoctrineEventBus.EventRaised += evt =>
        {
            if (evt.Condition == ConditionType.LowHealth && evt.SubjectObject == player)
            {
                lowHealthCount++;
            }
        };

        health.TakeDamage(75f);

        Assert.AreEqual(1, lowHealthCount);
    }
}
