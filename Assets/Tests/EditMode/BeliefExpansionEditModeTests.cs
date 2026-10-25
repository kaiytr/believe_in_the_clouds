using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BeliefExpansionEditModeTests
{
    private static EnemyAttackResult Result(AttackOutcome outcome, float time = 0f,
        bool damaged = false, bool normal = true, bool launched = true) =>
        new EnemyAttackResult(1, outcome, launched, damaged, normal, time);

    [Test]
    public void CounterattackIncludesTwoSecondsConsumesOnceAndUsesLatestEvade()
    {
        var state = new BeliefChallengeState(BeliefRule.Counterattack, 100f);
        Assert.That(state.Hit(0f, false, false), Is.Zero);
        Assert.That(state.ResolveAttack(Result(AttackOutcome.EvadedByPlayer, 10f)), Is.Zero);
        Assert.That(state.Hit(12f, false, false), Is.EqualTo(100f));
        Assert.That(state.Hit(12f, false, false), Is.Zero);
        state.ResolveAttack(Result(AttackOutcome.EvadedByPlayer, 20f));
        Assert.That(state.Hit(22.001f, false, false), Is.Zero);
        state.ResolveAttack(Result(AttackOutcome.EvadedByPlayer, 30f));
        state.ResolveAttack(Result(AttackOutcome.EvadedByPlayer, 31f));
        Assert.That(state.Hit(33f, false, false), Is.EqualTo(100f));
        state.ResolveAttack(Result(AttackOutcome.EvadedByPlayer, 40f));
        state.Reset();
        Assert.That(state.Hit(40f, false, false), Is.Zero);
    }

    [TestCase(AttackOutcome.Missed, false, true, true, 50f)]
    [TestCase(AttackOutcome.EvadedByPlayer, false, true, true, 50f)]
    [TestCase(AttackOutcome.Hit, true, true, true, 0f)]
    [TestCase(AttackOutcome.Hit, false, true, true, 50f)] // harmful effect without HP damage
    [TestCase(AttackOutcome.Blocked, false, false, true, 0f)]
    [TestCase(AttackOutcome.Cancelled, false, false, false, 0f)]
    [TestCase(AttackOutcome.Missed, false, false, true, 0f)] // abnormal destruction
    [TestCase(AttackOutcome.Missed, false, true, false, 0f)]
    public void NoHpDamageRequiresAnActuallyLaunchedNormallyFinishedAttack(
        AttackOutcome outcome, bool hpDamage, bool normal, bool launched, float expected)
    {
        var state = new BeliefChallengeState(BeliefRule.NoHpDamage, 50f);
        Assert.That(state.ResolveAttack(Result(outcome, damaged: hpDamage, normal: normal, launched: launched)),
            Is.EqualTo(expected));
    }

    [Test]
    public void ConsecutiveHitsUseAdjacentIntervalsAndResetOnTimeoutOrLifecycle()
    {
        var state = new BeliefChallengeState(BeliefRule.ConsecutiveHits, 25f);
        Assert.That(state.Hit(0f, false, false), Is.Zero);
        float total = 0f;
        for (int i = 1; i <= 4; i++) total += state.Hit(i * 2f, false, false);
        Assert.That(total, Is.EqualTo(100f), "The chain can last longer than two seconds overall.");
        Assert.That(state.Hit(10.001f, false, false), Is.Zero);
        Assert.That(state.Hit(12.001f, false, false), Is.EqualTo(25f));
        state.Reset();
        Assert.That(state.Hit(12.01f, false, false), Is.Zero);
    }

    [Test]
    public void RetreatEntryAllowsOneSuccessPerRetreatAndRearmsForANewRetreat()
    {
        var state = new BeliefChallengeState(BeliefRule.RetreatPunish, 50f);
        state.ObserveRetreat(false, true, 0f);
        Assert.That(state.Hit(0f, false, false), Is.Zero);
        state.ObserveRetreat(true, false, 1f);
        state.ObserveRetreat(true, true, 2f);
        Assert.That(state.Hit(4f, false, false), Is.EqualTo(50f));
        state.ObserveRetreat(true, false, 4f);
        state.ObserveRetreat(true, true, 5f);
        Assert.That(state.Hit(5f, false, false), Is.Zero);
        state.ObserveRetreat(false, true, 6f);
        state.ObserveRetreat(true, true, 7f);
        Assert.That(state.Hit(9.001f, false, false), Is.Zero);
        state.ObserveRetreat(true, true, 10f);
        Assert.That(state.Hit(10f, false, false), Is.Zero, "Standing inside must not renew the window.");
        state.Reset();
        state.ObserveRetreat(true, true, 11f);
        Assert.That(state.Hit(11f, false, false), Is.EqualTo(50f));
    }

    [Test]
    public void RearDwellIsDiscretePreservesWholeIntervalsAndDiscardsPartialTimeOnExit()
    {
        var state = new BeliefChallengeState(BeliefRule.RearDwell, 25f);
        Assert.That(state.TickRear(true, 1.5f), Is.Zero);
        Assert.That(state.TickRear(true, 0f), Is.Zero);
        Assert.That(state.TickRear(true, .5f), Is.EqualTo(25f));
        Assert.That(state.TickRear(true, 6f), Is.EqualTo(75f));
        Assert.That(state.TickRear(true, 1.5f), Is.Zero);
        Assert.That(state.TickRear(false, .1f), Is.Zero);
        Assert.That(state.TickRear(true, .5f), Is.Zero);
        state.Reset();
        Assert.That(state.TickRear(true, 2f), Is.EqualTo(25f));
    }

    [Test]
    public void RearHitsPrayerAndCircleEscapeUseTheirOwnConditions()
    {
        var rear = new BeliefChallengeState(BeliefRule.RearHit, 40f);
        Assert.That(rear.Hit(0f, false, true), Is.Zero);
        Assert.That(rear.Hit(0f, true, false), Is.EqualTo(40f));
        var prayer = new BeliefChallengeState(BeliefRule.PrayerHit, 50f);
        Assert.That(prayer.Hit(0f, true, false), Is.Zero);
        Assert.That(prayer.Hit(0f, false, true), Is.EqualTo(50f));
        var circle = new BeliefChallengeState(BeliefRule.EscapeCircle, 100f);
        Assert.That(circle.ResolveAttack(Result(AttackOutcome.Missed)), Is.Zero);
        Assert.That(circle.ResolveAttack(Result(AttackOutcome.Hit, damaged: true)), Is.Zero);
        Assert.That(circle.ResolveAttack(Result(AttackOutcome.EvadedByPlayer)), Is.EqualTo(100f));
    }

    [Test]
    public void CatalogContainsNineExpandedContentsWithEightRulesAndPreservesV1Numbers()
    {
        Assert.That((int)BeliefType.RearPosition, Is.EqualTo(1));
        Assert.That((int)BeliefType.DodgeAttack, Is.EqualTo(2));
        var catalog = AssetDatabase.LoadAssetAtPath<BeliefProfileCatalog>("Assets/Data/Belief/BeliefProfileCatalog.asset");
        Assert.That(catalog.IsValid(out string error), Is.True, error);
        var rules = new HashSet<BeliefRule>();
        int count = 0;
        foreach (BeliefProfile profile in catalog.Profiles)
        {
            Assert.That(catalog.Resolve(profile.Type), Is.SameAs(profile));
            if (!BeliefRules.IsExpanded(profile.Type)) continue;
            count++;
            rules.Add(profile.Rule);
            Assert.That(profile.TimeWindow, Is.EqualTo(2f));
            Assert.That(profile.RearInterval, Is.EqualTo(2f));
        }
        Assert.That(count, Is.EqualTo(9));
        Assert.That(rules.Count, Is.EqualTo(8));
        Assert.That(catalog.Resolve(BeliefType.DogCounterattack).ReductionPerSuccess, Is.EqualTo(100f));
        Assert.That(catalog.Resolve(BeliefType.DogRearAttack).ReductionPerSuccess, Is.EqualTo(40f));
        Assert.That(catalog.Resolve(BeliefType.ExecutiveRearPosition).ReductionPerSuccess, Is.EqualTo(25f));
        Assert.That(catalog.Resolve(BeliefType.DogUnerringAttack).Rule,
            Is.EqualTo(catalog.Resolve(BeliefType.BelieverUnerringStone).Rule));
    }

    [Test]
    public void CatalogRejectsDuplicateContentAndProfilesRejectMissingReduction()
    {
        var source = AssetDatabase.LoadAssetAtPath<BeliefProfileCatalog>("Assets/Data/Belief/BeliefProfileCatalog.asset");
        var catalog = Object.Instantiate(source);
        var profile = Object.Instantiate(source.Resolve(BeliefType.DogCounterattack));
        try
        {
            typeof(BeliefProfileCatalog).GetField("profiles", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(catalog, new List<BeliefProfile> { profile, profile });
            Assert.That(catalog.IsValid(out _), Is.False);
            typeof(BeliefProfile).GetField("reductionPerSuccess", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(profile, 0f);
            Assert.That(profile.IsValid(out _), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(profile);
        }
    }
}
