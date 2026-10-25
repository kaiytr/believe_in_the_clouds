using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Doctrine system EditMode tests, aligned to the fixed table:
// Subjects 4 / Conditions 4 / Results 4.
// Heal = 10% max HP (20s cooldown), IncreaseSpeed = x1.4 persistent,
// IncreaseAttack = x1.2 for 5s, Explode = 30% of exploder max HP to all sides incl. self.
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
        TestHealth health = go.AddComponent<TestHealth>();
        health.SetMaxHealth(maxHp);
        go.AddComponent<DoctrineStats>();
        spawned.Add(go);
        return go;
    }

    [Test]
    public void Activate_SingleDoctrine_Succeeds()
    {
        Doctrine doctrine = new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal);

        bool ok = manager.TryActivate(doctrine, out string reason);

        Assert.IsTrue(ok, "Activation failed: " + reason);
        Assert.AreEqual(1, manager.ActiveCount);
    }

    [Test]
    public void PlayerHit_TriggersOnHitHeal_TableRatio()
    {
        GameObject player = CreateCombatant("Player", SubjectType.Player, 100f);
        TestHealth health = player.GetComponent<TestHealth>();
        health.ForceHealth(50f);
        GameObject enemy = CreateCombatant("Enemy", SubjectType.Enemy, 100f);

        Assert.IsTrue(manager.TryActivate(
            new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal),
            player, out string reason), reason);

        health.TakeDamage(10f, enemy);

        Assert.AreEqual(50f, health.CurrentHealth, 0.001f);
    }

    [Test]
    public void Heal_SecondTriggerWithinCooldown_DoesNothing()
    {
        GameObject player = CreateCombatant("Player", SubjectType.Player, 100f);
        TestHealth health = player.GetComponent<TestHealth>();
        health.ForceHealth(20f);

        Assert.IsTrue(manager.TryActivate(
            new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal),
            player, out string reason), reason);

        health.TakeDamage(5f, null);
        float afterFirst = health.CurrentHealth;

        health.TakeDamage(5f, null);

        Assert.AreEqual(afterFirst - 5f, health.CurrentHealth, 0.001f);
    }

    [Test]
    public void ConditionMismatch_DoesNotTrigger()
    {
        GameObject player = CreateCombatant("Player", SubjectType.Player, 100f);
        TestHealth health = player.GetComponent<TestHealth>();
        health.ForceHealth(50f);
        GameObject enemy = CreateCombatant("Enemy", SubjectType.Enemy, 100f);

        Assert.IsTrue(manager.TryActivate(
            new Doctrine(SubjectType.Player, ConditionType.OnAttack, ResultType.Heal),
            player, out string reason), reason);

        health.TakeDamage(10f, enemy);

        Assert.AreEqual(40f, health.CurrentHealth, 0.001f);
    }

    [Test]
    public void TwoMatchingDoctrines_BothTrigger()
    {
        GameObject player = CreateCombatant("Player", SubjectType.Player, 100f);
        TestHealth health = player.GetComponent<TestHealth>();
        DoctrineStats stats = player.GetComponent<DoctrineStats>();
        health.ForceHealth(50f);
        float attackBefore = stats.EffectiveAttack;
        GameObject enemy = CreateCombatant("Enemy", SubjectType.Enemy, 100f);

        Assert.IsTrue(manager.TryActivate(
            new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal),
            player, out string r1), r1);
        Assert.IsTrue(manager.TryActivate(
            new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.IncreaseAttack),
            player, out string r2), r2);

        health.TakeDamage(10f, enemy);

        Assert.AreEqual(50f, health.CurrentHealth, 0.001f);
        Assert.AreEqual(attackBefore * 1.2f, stats.EffectiveAttack, 0.001f);
    }

    [Test]
    public void InvalidDoctrine_RejectedWithReason()
    {
        Doctrine invalid = new Doctrine(SubjectType.None, ConditionType.None, ResultType.None);

        bool ok = manager.TryActivate(invalid, out string reason);

        Assert.IsFalse(ok);
        Assert.IsFalse(string.IsNullOrEmpty(reason), "Failure reason is empty.");
    }

    [Test]
    public void DoctrineBoundToTargetWithoutHealth_RejectedWithReason()
    {
        GameObject bare = new GameObject("Bare");
        spawned.Add(bare);
        DoctrineSubject subject = bare.AddComponent<DoctrineSubject>();
        subject.Identity = SubjectType.Player;

        bool ok = manager.TryActivate(
            new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal),
            bare, out string reason);

        Assert.IsFalse(ok);
        Assert.IsFalse(string.IsNullOrEmpty(reason));
    }

    [Test]
    public void DeactivatedDoctrine_NoLongerTriggers()
    {
        GameObject player = CreateCombatant("Player", SubjectType.Player, 100f);
        TestHealth health = player.GetComponent<TestHealth>();
        GameObject enemy = CreateCombatant("Enemy", SubjectType.Enemy, 100f);
        Doctrine doctrine = new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal);

        Assert.IsTrue(manager.TryActivate(doctrine, player, out string reason), reason);
        Assert.IsTrue(manager.Deactivate(doctrine));

        health.ForceHealth(50f);
        health.TakeDamage(10f, enemy);

        Assert.AreEqual(40f, health.CurrentHealth, 0.001f);
        Assert.AreEqual(0, manager.ActiveCount);
    }

    // All + LowHealth + IncreaseSpeed: table x1.4 persistent, no code change needed.
    [Test]
    public void NewCombination_WorksWithoutCodeChange()
    {
        GameObject player = CreateCombatant("Player", SubjectType.Player, 100f);
        DoctrineStats stats = player.GetComponent<DoctrineStats>();

        Assert.IsTrue(manager.TryActivate(
            new Doctrine(SubjectType.All, ConditionType.LowHealth, ResultType.IncreaseSpeed),
            out string reason), reason);

        DoctrineEventBus.Raise(ConditionType.LowHealth, player);

        Assert.AreEqual(1.4f, stats.SpeedMultiplier, 0.001f);
    }

    [Test]
    public void MatchesRule_AllMatchesEverySubject()
    {
        Doctrine all = new Doctrine(SubjectType.All, ConditionType.OnHit, ResultType.Heal);
        Doctrine playerOnly = new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal);

        Assert.IsTrue(DoctrineManager.MatchesRule(all, SubjectType.Enemy, ConditionType.OnHit));
        Assert.IsFalse(DoctrineManager.MatchesRule(playerOnly, SubjectType.Enemy, ConditionType.OnHit));
        Assert.IsTrue(DoctrineManager.MatchesRule(playerOnly, SubjectType.Player, ConditionType.OnHit));
    }

    // Table rule: friends and foes alike, so self is included (exclude = null).
    [Test]
    public void Explode_DamagesSelfAndOthers()
    {
        GameObject self = CreateCombatant("Self", SubjectType.Player, 100f);
        GameObject other = CreateCombatant("Other", SubjectType.Enemy, 100f);
        TestHealth selfHealth = self.GetComponent<TestHealth>();
        TestHealth otherHealth = other.GetComponent<TestHealth>();

        List<IHealth> targets = new List<IHealth> { selfHealth, otherHealth };

        bool ok = DoctrineEffectExecutor.ApplyExplodeDamage(targets, 30f, null, null);

        Assert.IsTrue(ok);
        Assert.AreEqual(70f, selfHealth.CurrentHealth, 0.001f);
        Assert.AreEqual(70f, otherHealth.CurrentHealth, 0.001f);
    }

    [Test]
    public void Explode_HasTableCooldownOfFiveSeconds()
    {
        Doctrine explode = new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Explode);
        Doctrine heal = new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal);
        Doctrine speed = new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.IncreaseSpeed);

        Assert.AreEqual(5f, DoctrineManager.GetCooldownSeconds(explode), 0.001f);
        Assert.AreEqual(20f, DoctrineManager.GetCooldownSeconds(heal), 0.001f);
        Assert.AreEqual(0f, DoctrineManager.GetCooldownSeconds(speed), 0.001f);
    }

    [Test]
    public void Explode_CooldownOverride_Wins()
    {
        Doctrine explode = new Doctrine(
            SubjectType.Player, ConditionType.OnHit, ResultType.Explode, 0f, 0f, 0f, 2f);

        Assert.AreEqual(2f, DoctrineManager.GetCooldownSeconds(explode), 0.001f);
    }

    [Test]
    public void LowHealth_EventRaisedOnThresholdCross()
    {
        GameObject player = CreateCombatant("Player", SubjectType.Player, 100f);
        TestHealth health = player.GetComponent<TestHealth>();
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
