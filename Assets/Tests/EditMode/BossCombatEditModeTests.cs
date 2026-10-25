using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class BossCombatEditModeTests
{
    private static BossAttackRecord Record() => new BossAttackRecord(3, 7, 12, 1, BossAttackKind.Melee,
        BossBeliefKind.AttackAndDodge, new AttackGeometry(Vector2.right, new Vector2(2f, 3f)), 14f, true);

    [TestCase(BossAttackEndReason.Completed, true, false, false, AttackOutcome.EvadedByPlayer)]
    [TestCase(BossAttackEndReason.Wall, true, false, false, AttackOutcome.EvadedByPlayer)]
    [TestCase(BossAttackEndReason.Cancelled, true, false, false, AttackOutcome.Cancelled)]
    [TestCase(BossAttackEndReason.TargetLost, true, false, false, AttackOutcome.Cancelled)]
    [TestCase(BossAttackEndReason.Completed, false, false, false, AttackOutcome.Missed)]
    [TestCase(BossAttackEndReason.Contact, true, true, false, AttackOutcome.Missed)]
    [TestCase(BossAttackEndReason.Contact, true, true, true, AttackOutcome.Hit)]
    public void OutcomesRequireActivationSpatialEscapeAndNoContact(BossAttackEndReason reason, bool activate, bool contact, bool damage, AttackOutcome expected)
    {
        var record = Record();
        record.Observe(true, Vector2.zero);
        record.Observe(false, Vector2.up * 2f);
        if (activate) record.Activate();
        if (contact) Assert.That(record.TryContact(), Is.True);
        if (damage) record.AcceptDamage();
        Assert.That(record.Finish(reason), Is.True);
        Assert.That(record.Outcome, Is.EqualTo(expected));
        Assert.That(record.Finish(BossAttackEndReason.Completed), Is.False, "Results are final and cannot reduce belief twice.");
        Assert.That(record.TryContact(), Is.False);
    }

    [Test]
    public void StandingStillAndNeverThreatenedTargetsCannotBeEvades()
    {
        var stationary = Record();
        stationary.Observe(true, Vector2.zero); stationary.Activate(); stationary.Observe(false, Vector2.zero);
        stationary.Finish(BossAttackEndReason.Completed);
        Assert.That(stationary.Outcome, Is.EqualTo(AttackOutcome.Missed));
        var outside = Record(); outside.Activate(); outside.Observe(false, Vector2.up * 8f); outside.Finish(BossAttackEndReason.Completed);
        Assert.That(outside.Outcome, Is.EqualTo(AttackOutcome.Missed));
    }

    [Test]
    public void WallBlocksWithoutPriorThreatButDoesNotEraseAnActualEscape()
    {
        var blocked = Record(); blocked.Activate(); blocked.Finish(BossAttackEndReason.Wall);
        Assert.That(blocked.Outcome, Is.EqualTo(AttackOutcome.Blocked));

        var escaped = Record();
        escaped.Observe(true, Vector2.zero); escaped.Observe(false, Vector2.up * 2f); escaped.Activate();
        escaped.Finish(BossAttackEndReason.Wall);
        Assert.That(escaped.Outcome, Is.EqualTo(AttackOutcome.EvadedByPlayer));
    }

    [Test]
    public void SharedGaugeKeepsVulnerableDeadlineAndRecoversOnlyOnce()
    {
        var gauge = new BossSharedBelief(100f);
        Assert.That(gauge.Reduce(20f, 10f, 5f), Is.False);
        Assert.That(gauge.Reduce(80f, 11f, 5f), Is.True);
        Assert.That(gauge.Reduce(20f, 14f, 5f), Is.False);
        Assert.That(gauge.Remaining(14f), Is.EqualTo(2f));
        Assert.That(gauge.Tick(15.9f, 1f), Is.False);
        Assert.That(gauge.Tick(16f, 1f), Is.True);
        Assert.That(gauge.Current, Is.EqualTo(100f));
        Assert.That(gauge.Tick(17f, 1f), Is.False);
    }

    [Test]
    public void WeightedChoiceAllowsRepeatsExcludesInvalidCandidatesAndHasNoFallbackAttack()
    {
        var pool = new[] { new BossPatternWeight(BossPatternKind.MeleeCombo, 1f, .5f), new BossPatternWeight(BossPatternKind.Projectile, 1f, .5f) };
        var counts = new Dictionary<BossPatternKind,int> { [BossPatternKind.MeleeCombo] = 3 };
        Assert.That(pool[0].EffectiveWeight(3), Is.EqualTo(.4f));
        Assert.That(BossPatternSelection.TryChoose(pool,counts,p => true,0f,out var chosen), Is.True);
        Assert.That(chosen, Is.EqualTo(BossPatternKind.MeleeCombo), "No forced alternation after the same attack.");
        Assert.That(BossPatternSelection.TryChoose(pool,counts,p => p != BossPatternKind.MeleeCombo,0f,out chosen), Is.True);
        Assert.That(chosen, Is.EqualTo(BossPatternKind.Projectile));
        Assert.That(BossPatternSelection.TryChoose(pool,counts,p => false,.5f,out chosen), Is.False);
    }

    [Test]
    public void GeometryUsesActualBodyAndExcludesRearAndAirAboveWave()
    {
        var target = new GameObject("Target"); var collider = target.AddComponent<CapsuleCollider2D>(); collider.size = new Vector2(1f,2f);
        try
        {
            var front = new AttackGeometry(new Vector2(1.73f,0f),new Vector2(2.6f,3f));
            target.transform.position = new Vector2(-1f,0f); Physics2D.SyncTransforms(); Assert.That(front.Overlaps(collider), Is.False);
            target.transform.position = new Vector2(1f,0f); Physics2D.SyncTransforms(); Assert.That(front.Overlaps(collider), Is.True);
            var wave = new AttackGeometry(new Vector2(1f,.3f),new Vector2(.6f,.6f));
            target.transform.position = new Vector2(1f,1.7f); Physics2D.SyncTransforms(); Assert.That(wave.Overlaps(collider), Is.False);
        }
        finally { Object.DestroyImmediate(target); }
    }

    [Test]
    public void RealPrefabAndEffectsHaveCompleteReferencesAndWorldUnitSprites()
    {
        var boss = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss/Boss.prefab").GetComponent<BossCombatController>();
        Assert.That(boss.ValidateEncounter(), Is.Empty);
        var data = boss.Settings;
        Assert.That(data.ValidateSettings(), Is.Empty);
        foreach (BossAttackVisual visual in new BossAttackVisual[] { data.WarningPrefab,data.SlashPrefab,data.ImpactPrefab,
            data.ProjectilePrefab.GetComponent<BossAttackVisual>(),data.WavePrefab.GetComponent<BossAttackVisual>(),data.SpikePrefab.GetComponent<BossAttackVisual>() })
        {
            Assert.That(visual.Renderer.sprite.bounds.size.x, Is.EqualTo(1f).Within(.001f), visual.name);
            Assert.That(visual.Renderer.sprite.bounds.size.y, Is.EqualTo(1f).Within(.001f), visual.name);
            Assert.That(visual.Renderer.sharedMaterial, Is.Not.Null);
        }
        var afterimageRenderer = data.DashPrefab.GetComponent<SpriteRenderer>();
        Assert.That(afterimageRenderer.sprite.bounds.size.x, Is.EqualTo(1f).Within(.001f));
        Assert.That(afterimageRenderer.sprite.bounds.size.y, Is.EqualTo(1f).Within(.001f));
        Assert.That(afterimageRenderer.sharedMaterial, Is.Not.Null);
        Assert.That(data.ProjectilePrefab.GetComponent<CircleCollider2D>().radius * 2f, Is.EqualTo(1f));
        Assert.That(data.WhiteCloudPrefab.GetComponent<BoxCollider2D>().usedByEffector, Is.True);
        Assert.That(data.WhiteCloudPrefab.GetComponent<PlatformEffector2D>().useOneWay, Is.True);
        Assert.That(data.BlackCloudPrefab.GetComponent<BoxCollider2D>().isTrigger, Is.True);
        var invalid = Object.Instantiate(data);
        try { invalid.WarningPrefab = null; invalid.BeliefRecoveryRatio = 0f; Assert.That(invalid.ValidateSettings(), Has.Some.Contains("WarningPrefab")); Assert.That(invalid.ValidateSettings().Count, Is.GreaterThanOrEqualTo(2)); }
        finally { Object.DestroyImmediate(invalid); }
    }

    [Test]
    public void BossTestContainsPrefabAndPreservesLatestPlayerPhysicsAndControls()
    {
        Scene source = EditorSceneManager.OpenScene("Assets/Scenes/PlayerTEST.unity",OpenSceneMode.Additive);
        Scene test = EditorSceneManager.OpenScene("Assets/Scenes/BossTest.unity",OpenSceneMode.Additive);
        try
        {
            GameObject original = Find(source,"Player"), clone = Find(test,"Player");
            Assert.That(original, Is.Not.Null); Assert.That(clone, Is.Not.Null);
            var a = new SerializedObject(original.GetComponent<PlayerMove>()); var b = new SerializedObject(clone.GetComponent<PlayerMove>());
            foreach (string field in new[] {"moveSpeed","jumpPower","groundCheckRadius"})
                Assert.That(b.FindProperty(field).floatValue, Is.EqualTo(a.FindProperty(field).floatValue),field);
            Assert.That(b.FindProperty("groundLayer").intValue, Is.EqualTo(a.FindProperty("groundLayer").intValue));
            Assert.That(((Transform)b.FindProperty("groundCheck").objectReferenceValue).localPosition,
                Is.EqualTo(((Transform)a.FindProperty("groundCheck").objectReferenceValue).localPosition));
            var ar = original.GetComponent<Rigidbody2D>(); var br = clone.GetComponent<Rigidbody2D>();
            Assert.That(br.gravityScale,Is.EqualTo(ar.gravityScale)); Assert.That(br.linearDamping,Is.EqualTo(ar.linearDamping));
            Assert.That(br.angularDamping,Is.EqualTo(ar.angularDamping)); Assert.That(br.interpolation,Is.EqualTo(ar.interpolation));
            Assert.That(br.constraints,Is.EqualTo(ar.constraints)); Assert.That(br.collisionDetectionMode,Is.EqualTo(ar.collisionDetectionMode));
            Assert.That(clone.GetComponent<CapsuleCollider2D>().size,Is.EqualTo(original.GetComponent<CapsuleCollider2D>().size));
            Assert.That(clone.GetComponent<CapsuleCollider2D>().offset,Is.EqualTo(original.GetComponent<CapsuleCollider2D>().offset));
            var boss = Find(test,"Boss"); Assert.That(PrefabUtility.IsPartOfPrefabInstance(boss),Is.True);
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(boss),Is.EqualTo("Assets/Prefabs/Boss/Boss.prefab"));
            Assert.That(boss.transform.localScale,Is.EqualTo(Vector3.one));
            Assert.That(Find(test,"BossTest tools").GetComponent<BossTestBootstrap>().Boss,Is.EqualTo(boss.GetComponent<BossCombatController>()));
        }
        finally { EditorSceneManager.CloseScene(test,true); EditorSceneManager.CloseScene(source,true); }
    }
    private static GameObject Find(Scene scene,string name)
    { foreach(var root in scene.GetRootGameObjects()) if(root.name==name) return root; return null; }
}
