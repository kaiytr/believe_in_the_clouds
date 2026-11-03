using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class DogSpriteEditModeTests
{
    private const string Art = "Assets/Design/Enemy/Dog/";
    private const string VisualPrefab = "Assets/Prefabs/Enemy/Dog/DogVisual.prefab";

    [TestCase("DogIdle", 3)] [TestCase("DogWalk", 4)] [TestCase("DogAttack", 6)] [TestCase("DogDeath", 1)]
    public void SpriteSheetsUseCompleteAlignedFramesAndPointFiltering(string name, int count)
    {
        string path = Art + name + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        Assert.That(importer.mipmapEnabled, Is.False);
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
        Assert.That(frames, Has.Length.EqualTo(count));
        for (int index = 0; index < count; index++)
        {
            Assert.That(frames[index].rect, Is.EqualTo(new Rect(index * 61, 0, 61, 53)));
            Assert.That(frames[index].pivot, Is.EqualTo(new Vector2(30.5f, 2f)));
            Assert.That(frames[index].pixelsPerUnit, Is.EqualTo(53f));
        }
    }

    [TestCase(ProjectScenes.DogCombatTest)] [TestCase(ProjectScenes.StageTest)]
    [TestCase(ProjectScenes.EnemyTest)] [TestCase(ProjectScenes.BeliefTest)]
    public void AuthoredDogsUseTheSharedVisualWithSquarePixelsAndFeetAtTheBodyGroundLine(string path)
    {
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var enemies = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnemyUnit>(true)).ToArray();
            DogEnemy dog = enemies.OfType<DogEnemy>().Single();
            var visual = dog.GetComponentsInChildren<DogSpriteAnimator>(true).Single();
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(visual.gameObject), Is.EqualTo(VisualPrefab));
            var renderer = visual.GetComponent<SpriteRenderer>();
            Assert.That(renderer.sprite.name, Is.EqualTo("DogIdle_00"));
            Assert.That(renderer.color, Is.EqualTo(Color.white));
            Assert.That(renderer.flipX, Is.True);
            Assert.That(Mathf.Abs(visual.transform.lossyScale.x), Is.EqualTo(Mathf.Abs(visual.transform.lossyScale.y)).Within(.0001f));
            var body = dog.GetComponent<CapsuleCollider2D>();
            bool wasActive = dog.gameObject.activeSelf;
            try
            {
                dog.gameObject.SetActive(true); Physics2D.SyncTransforms();
                Assert.That(visual.transform.position.y, Is.EqualTo(body.bounds.min.y).Within(.0001f));
            }
            finally { dog.gameObject.SetActive(wasActive); }
            Assert.That(dog.GetComponent<SpriteRenderer>(), Is.Null, "The placeholder rectangle must not render behind the dog.");
            foreach (EnemyUnit other in enemies.Where(enemy => !(enemy is DogEnemy)))
                Assert.That(other.GetComponentsInChildren<DogSpriteAnimator>(true), Is.Empty, other.name);
        }
        finally
        {
            if (previous.Any(setup => setup.isLoaded && setup.isActive) &&
                previous.Where(setup => setup.isLoaded).All(setup => !string.IsNullOrEmpty(setup.path) && System.IO.File.Exists(setup.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
