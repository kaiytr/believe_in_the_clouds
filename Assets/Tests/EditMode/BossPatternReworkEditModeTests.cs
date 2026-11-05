using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Boss pattern rework: settings contract and lane selection (no scene needed).</summary>
public sealed class BossPatternReworkEditModeTests
{
    private const string SettingsPath = "Assets/Data/Boss/BossCombatSettings.asset";

    [Test]
    public void SavedSettingsDocumentLaneTimingAndValidate()
    {
        var data = AssetDatabase.LoadAssetAtPath<BossCombatSettings>(SettingsPath);
        Assert.That(data.ValidateSettings(), Is.Empty);
        Assert.That(data.BeliefInitialDelay, Is.EqualTo(3f));
        Assert.That(data.BeliefCooldown, Is.EqualTo(3f));
        Assert.That(data.ProphecyReactionTime, Is.GreaterThanOrEqualTo(1f));
        Assert.That(data.BurnWindow, Is.EqualTo(10f));
        Assert.That(data.StormCloudCountRange, Is.EqualTo(new Vector2Int(3, 4)));
        Assert.That(data.WhiteCloudCount, Is.EqualTo(2));
        Assert.That(data.PatternRecovery, Is.EqualTo(.65f), "Regular attack spacing keeps its current value.");
    }

    [Test]
    public void WhiteCloudTopIsReachableByTheMeasuredBaseJump()
    {
        var data = AssetDatabase.LoadAssetAtPath<BossCombatSettings>(SettingsPath);
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        float jumpPower = new SerializedObject(player.GetComponent<PlayerMove>()).FindProperty("jumpPower").floatValue;
        float gravity = -Physics2D.gravity.y * player.GetComponent<Rigidbody2D>().gravityScale;
        float step = 0.02f, velocity = jumpPower, height = 0f, apex = 0f;
        // Same semi-implicit stepping as the physics engine: v += g*dt, then y += v*dt.
        for (int i = 0; i < 200 && (velocity > 0f || height > 0f); i++)
        {
            velocity -= gravity * step;
            height += velocity * step;
            apex = Mathf.Max(apex, height);
        }
        Assert.That(apex, Is.EqualTo(data.ReferenceJumpApex).Within(.02f), "ReferenceJumpApex must match PlayerMove.");
        Assert.That(data.WhiteCloudTopHeight, Is.LessThanOrEqualTo(apex - data.WhiteCloudLandingMargin + .001f));
        Assert.That(data.WhiteCloudTopHeight - data.CloudSize.y, Is.GreaterThanOrEqualTo(data.ReferencePlayerHeight),
            "The shelter under the cloud must still fit the body.");
    }

    [Test]
    public void RegularAndBeliefLanesSelectFromTheirOwnEntriesWithSeparateHistories()
    {
        foreach (bool phaseTwo in new[] { false, true })
        {
            BossPatternWeight[] pool = BossCombatSettings.DefaultPool(phaseTwo);
            var regularUses = new Dictionary<BossPatternKind, int>();
            var beliefUses = new Dictionary<BossPatternKind, int> { [phaseTwo ? BossPatternKind.NoAttackBelief : BossPatternKind.CloudBelief] = 5 };
            for (float roll = 0f; roll < 1f; roll += .1f)
            {
                Assert.That(BossPatternSelection.TryChooseLane(pool, true, regularUses, p => true, roll, out var regular), Is.True);
                Assert.That(BossPatternSelection.IsRegular(regular), Is.True);
                Assert.That(BossPatternSelection.TryChooseLane(pool, false, beliefUses, p => true, roll, out var belief), Is.True);
                Assert.That(BossPatternSelection.IsRegular(belief), Is.False);
                Assert.That(BossPatternSelection.AllowedInPhase(belief, phaseTwo ? BossPhase.Two : BossPhase.One), Is.True);
            }
            // A belief's own repeat penalty never touches the regular history and vice versa.
            Assert.That(regularUses, Is.Empty);
            Assert.That(BossPatternSelection.TryChooseLane(pool, false, beliefUses, p => false, .5f, out _), Is.False);
        }
    }

    [Test]
    public void ProphecyAndAttackContextKeepDecisionsOnTheirOwnObjects()
    {
        var prophecy = new BossProphecy(BossBeliefKind.AttackAndDodge, 2f);
        var combo = new BossRegularAttackContext(4, BossPatternKind.MeleeCombo, burnEnhanced: true, dodge: prophecy);
        var record = new BossAttackRecord(1, 4, 9, 1, BossAttackKind.Melee, combo.DisplayBelief,
            new AttackGeometry(Vector2.zero, Vector2.one), 6f, true, combo);
        Assert.That(record.BurnEnhanced, Is.True, "Recorded at attack start, not read from live belief state.");
        Assert.That(combo.DisplayBelief, Is.EqualTo(BossBeliefKind.AttackAndDodge));
        var plain = new BossAttackRecord(1, 5, 10, 0, BossAttackKind.Lightning, BossBeliefKind.Cloud,
            new AttackGeometry(Vector2.zero, Vector2.one), 18f, false);
        Assert.That(plain.BurnEnhanced, Is.False);
    }
}
