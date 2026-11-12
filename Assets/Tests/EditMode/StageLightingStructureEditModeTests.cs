using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class StageLightingStructureEditModeTests
{
    [Test]
    public void StoryAndCombatScenesSerializeWorldArtWithoutRuntimeLighting()
    {
        foreach (string path in new[] { ProjectScenes.FirstAltarStory, ProjectScenes.CombatLevelPath(1) })
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                Component[] components = roots.SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
                Assert.That(components.Where(component => component != null && component.GetType().Name == "Light2D"),
                    Is.Empty, path + " must use StageAtmosphere when play begins.");
                Assert.That(components.OfType<TorchLight>(), Is.Empty, path + " must let StageAtmosphere attach TorchLight.");
                Assert.That(roots.Any(root => root.name == "Stage atmosphere" || root.name == "Room ambient light"), Is.False);
                Assert.That(components.OfType<Transform>().Count(transform => transform.name == "Wall Torch"), Is.GreaterThan(0));
            }
            finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
        }
    }

    [Test]
    public void StoryRoomUsesSharedStageMaterialsAndTorchAnimation()
    {
        Scene scene = SceneManager.GetSceneByPath(ProjectScenes.FirstAltarStory);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ProjectScenes.FirstAltarStory, OpenSceneMode.Additive);
        try
        {
            GameObject[] roots = scene.GetRootGameObjects();
            Assert.That(roots.Any(root => root.name == "Altar room architecture"), Is.True);
            Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<StoryRoomFlow>(true)).Count(), Is.EqualTo(1));
            Material scenery = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LevelTiles/InteriorScenery.mat");
            Material terrain = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LevelTiles/InteriorTerrain.mat");
            Transform torchTemplate = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/InteriorHallBay.prefab")
                .GetComponentsInChildren<Transform>(true).Single(transform => transform.name == "Wall Torch");
            var templateAnimation = new SerializedObject(torchTemplate.GetComponent<SpriteFrameAnimator>());
            foreach (SpriteRenderer view in roots.SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true)))
            {
                if (new[] { "Stone wall", "Stone pillar", "Dark window", "Stone floor" }.Contains(view.name))
                {
                    Assert.That(view.sharedMaterial, Is.SameAs(view.name == "Stone floor" ? terrain : scenery), view.name);
                    Assert.That(view.color, Is.EqualTo(Color.white), view.name + " must not bake darkness into its tint.");
                }
                Assert.That(AssetDatabase.GetAssetPath(view.sharedMaterial).StartsWith("Assets/Art/Story/StoryLit", StringComparison.Ordinal), Is.False);
                if (view.name != "Wall Torch") continue;
                Assert.That(view.sharedMaterial, Is.SameAs(torchTemplate.GetComponent<SpriteRenderer>().sharedMaterial));
                Assert.That(view.sprite, Is.SameAs(torchTemplate.GetComponent<SpriteRenderer>().sprite));
                var animation = new SerializedObject(view.GetComponent<SpriteFrameAnimator>());
                SerializedProperty frames = animation.FindProperty("frames");
                SerializedProperty sourceFrames = templateAnimation.FindProperty("frames");
                Assert.That(frames.arraySize, Is.EqualTo(sourceFrames.arraySize));
                for (int i = 0; i < frames.arraySize; i++)
                    Assert.That(frames.GetArrayElementAtIndex(i).objectReferenceValue,
                        Is.SameAs(sourceFrames.GetArrayElementAtIndex(i).objectReferenceValue));
            }
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }
}
