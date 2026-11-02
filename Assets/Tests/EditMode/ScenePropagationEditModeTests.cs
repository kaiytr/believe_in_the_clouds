using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class ScenePropagationEditModeTests
{
    private const string PlayerPath = "Assets/Prefabs/Player/Player.prefab";
    private const string HudPath = "Assets/Prefabs/UI/PlayerHud.prefab";
    public static IEnumerable<string> ScenePaths => ProjectScenes.AllPaths;

    [TestCaseSource(nameof(ScenePaths))]
    public void EveryProjectSceneUsesTheSharedCombatSettingsAndHud(string path)
    {
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var players = roots.SelectMany(root => root.GetComponentsInChildren<PlayerHealth>(true))
                .Where(item => item.CompareTag("Player")).ToArray();
            Assert.That(players, Has.Length.EqualTo(1), path);
            var player = players[0];
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player.gameObject), Is.EqualTo(PlayerPath));
            Assert.That(player.BodyCollider, Is.SameAs(player.GetComponent<Collider2D>()));
            Assert.That(player.GetComponent<DoctrineSubject>(), Is.Not.Null);
            Assert.That(player.GetComponent<DoctrineSubject>().Identity, Is.EqualTo(SubjectType.Player));
            var attack = player.GetComponent<PlayerAttack>();
            Assert.That(attack, Is.Not.Null);
            var actual = new SerializedObject(attack);
            var expected = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath).GetComponent<PlayerAttack>());
            foreach (string field in new[] { "downFanRadius", "downFanHalfAngle", "downFanEnemyKnockDown", "downFanEnemyKnockOut", "downFanPlayerBounce", "finishHitStop" })
                Assert.That(actual.FindProperty(field).floatValue, Is.EqualTo(expected.FindProperty(field).floatValue), field);
            Assert.That(actual.FindProperty("downFanOriginOffset").vector2Value, Is.EqualTo(expected.FindProperty("downFanOriginOffset").vector2Value));
            Assert.That(actual.FindProperty("airAttack.hitboxOffset").vector2Value, Is.EqualTo(Vector2.zero));
            Assert.That(actual.FindProperty("airAttack.name").stringValue, Is.EqualTo("공중: 아래 부채꼴 내리찍기"));
            Assert.That(actual.FindProperty("comboSteps").arraySize, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
                foreach (string field in new[] { "damage", "windupTime", "recoveryTime", "lungeDistance" })
                {
                    string property = $"comboSteps.Array.data[{i}].{field}";
                    Assert.That(actual.FindProperty(property).floatValue, Is.EqualTo(expected.FindProperty(property).floatValue), property);
                }
            var huds = roots.SelectMany(root => root.GetComponentsInChildren<DoctrineHud>(true)).ToArray();
            Assert.That(huds, Has.Length.EqualTo(1));
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(huds[0].gameObject), Is.EqualTo(HudPath));
            foreach (var component in roots.SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                Assert.That(component, Is.Not.Null, "Missing scene script");
                if (component is LobbyCameraFollow follow) Assert.That(follow.Target, Is.SameAs(player.transform));
                if (component.GetType().Name == "CinemachineCamera")
                {
                    Assert.That(component.GetComponent<CinemachineHorizontalFollow>(), Is.Not.Null);
                    Assert.That(new SerializedObject(component).FindProperty("Target.TrackingTarget").objectReferenceValue, Is.SameAs(player.transform));
                }
                if (component is BeliefDummyHitTool)
                    Assert.That(new SerializedObject(component).FindProperty("player").objectReferenceValue, Is.SameAs(player));
            }
        }
        finally
        {
            if (previous.Any(item => item.isLoaded && item.isActive) && previous.Where(item => item.isLoaded).All(item => !string.IsNullOrEmpty(item.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
