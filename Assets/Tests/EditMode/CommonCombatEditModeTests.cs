using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class CommonCombatEditModeTests
{
    [Test]
    public void WeightedChoiceInspectsEachCandidateOnlyOnce()
    {
        var pool=new[] { new BossPatternWeight(BossPatternKind.MeleeCombo,1f,.5f),
            new BossPatternWeight(BossPatternKind.Dash,2f,.5f), new BossPatternWeight(BossPatternKind.Projectile,1f,.5f) };
        var calls=new Dictionary<BossPatternKind,int>();
        bool Eligible(BossPatternKind kind) { calls[kind]=calls.TryGetValue(kind,out int count)?count+1:1; return kind!=BossPatternKind.Dash; }
        Assert.That(BossPatternSelection.TryChoose(pool,new Dictionary<BossPatternKind,int>(),Eligible,.9f,out var chosen,new List<float>()),Is.True);
        Assert.That(chosen,Is.EqualTo(BossPatternKind.Projectile));
        Assert.That(calls.Count,Is.EqualTo(3));
        foreach(int count in calls.Values) Assert.That(count,Is.EqualTo(1));
    }
    [Test]
    public void SharedQueriesKeepTargetsBeyondTheInitialBufferCapacityAndRespectRotation()
    {
        var objects=new List<GameObject>();
        try
        {
            for(int i=0;i<80;i++)
            {
                var actor=new GameObject("Crowded query fixture"); objects.Add(actor);
                actor.transform.position=new Vector3(1000f+(i%8)*.02f,1000f,0f);
                actor.AddComponent<BoxCollider2D>().size=Vector2.one*.1f;
            }
            Collider2D target=objects[79].GetComponent<Collider2D>();
            Physics2D.SyncTransforms();
            Assert.That(new AttackGeometry(new Vector2(1000f,1000f),Vector2.one).Overlaps(target),Is.True);
            Assert.That(CombatPhysics.SweptTarget(target,new Vector2(998f,1000f),Vector2.one*.2f,Vector2.right,3f,false),Is.True);
            var rotated=new AttackGeometry(new Vector2(1000f,1000f),new Vector2(4f,.1f),90f);
            target.transform.position=new Vector3(1000f,1001f,0f); Physics2D.SyncTransforms();
            Assert.That(rotated.Overlaps(target),Is.True);
            target.transform.position=new Vector3(1001f,1000f,0f); Physics2D.SyncTransforms();
            Assert.That(rotated.Overlaps(target),Is.False);
        }
        finally { foreach(var actor in objects) Object.DestroyImmediate(actor); }
    }
    [Test]
    public void BossAndOrdinaryEnemiesUseTheSameHudAssetAndCachedKoreanFont()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<CombatantHudView>("Assets/Resources/Combat/CombatantHud.prefab");
        Assert.That(prefab,Is.Not.Null); Assert.That(prefab.IsConfigured,Is.True);
        var boss=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss/Boss.prefab");
        var view=boss.GetComponent<BossGameplayHud>().WorldView;
        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(view),Is.EqualTo(prefab));
        Assert.That(CombatUiResources.HudPrefab,Is.EqualTo(prefab));
        var font=CombatUiResources.KoreanFont(); Assert.That(font,Is.Not.Null);
        Assert.That(CombatUiResources.KoreanFont("Malgun Gothic"),Is.SameAs(font));
    }
}
