using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BeliefSystemEditModeTests
{
    [Test]
    public void RearRangeUsesColliderBoundsAndIncludesBothLimits()
    {
        Bounds enemy = new Bounds(Vector3.zero, new Vector3(1f, 1f, 1f));
        Bounds playerAtLimit = new Bounds(new Vector3(-2f, 1.5f, 0f), new Vector3(1f, 1f, 1f));
        Bounds playerBeyondLimit = new Bounds(new Vector3(-2.01f, 1.5f, 0f), new Vector3(1f, 1f, 1f));
        Bounds playerInFront = new Bounds(new Vector3(2f, 0f, 0f), new Vector3(1f, 1f, 1f));
        Bounds sameCenter = new Bounds(new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 1f));

        Assert.That(RearPositionBeliefCondition.IsWithinRearRange(enemy, playerAtLimit, 1f, 1f, 1.5f), Is.True);
        Assert.That(RearPositionBeliefCondition.IsWithinRearRange(enemy, playerBeyondLimit, 1f, 1f, 1.5f), Is.False);
        Assert.That(RearPositionBeliefCondition.IsWithinRearRange(enemy, playerInFront, 1f, 1f, 1.5f), Is.False);
        Assert.That(RearPositionBeliefCondition.IsWithinRearRange(enemy, sameCenter, 1f, 1f, 1.5f), Is.False);

        Bounds playerBehindLeftFacingEnemy = new Bounds(new Vector3(2f, 0f, 0f), new Vector3(1f, 1f, 1f));
        Assert.That(RearPositionBeliefCondition.IsWithinRearRange(enemy, playerBehindLeftFacingEnemy, -1f, 1f, 0f), Is.True);
    }

    [Test]
    public void ProfileCatalogResolvesBothTypesAndDefaultsToRearProfile()
    {
        BeliefProfile rear = ScriptableObject.CreateInstance<BeliefProfile>();
        BeliefProfile dodge = ScriptableObject.CreateInstance<BeliefProfile>();
        BeliefProfileCatalog catalog = ScriptableObject.CreateInstance<BeliefProfileCatalog>();

        try
        {
            SetField(rear, "type", BeliefType.RearPosition);
            SetField(rear, "phrase", "rear");
            SetField(rear, "reductionPerSecond", 25f);
            SetField(dodge, "type", BeliefType.DodgeAttack);
            SetField(dodge, "phrase", "dodge");
            SetField(dodge, "reductionPerEvade", 25f);
            SetField(catalog, "rearPositionProfile", rear);
            SetField(catalog, "dodgeAttackProfile", dodge);

            Assert.That(catalog.IsValid(out string error), Is.True, error);
            Assert.That(catalog.Resolve(BeliefType.RearPosition), Is.SameAs(rear));
            Assert.That(catalog.Resolve(BeliefType.DodgeAttack), Is.SameAs(dodge));
            Assert.That(catalog.Resolve(BeliefType.Unassigned), Is.SameAs(rear));
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(rear);
            Object.DestroyImmediate(dodge);
        }
    }

    [Test]
    public void DamageInfoCarriesAmountAndExplicitSource()
    {
        DamageInfo info = new DamageInfo(12.5f, DamageSource.PlayerOwned);

        Assert.That(info.Amount, Is.EqualTo(12.5f));
        Assert.That(info.Source, Is.EqualTo(DamageSource.PlayerOwned));
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing serialized field {fieldName}");
        field.SetValue(target, value);
    }
}
