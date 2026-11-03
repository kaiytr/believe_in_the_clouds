using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class DogVisualProjectSetup
{
    public const string ArtFolder = "Assets/Design/Enemy/Dog";
    public const string PrefabPath = "Assets/Prefabs/Enemy/Dog/DogVisual.prefab";
    private const int FrameWidth = 61, FrameHeight = 53;
    private static readonly string[] ScenePaths = {
        ProjectScenes.DogCombatTest, ProjectScenes.StageTest, ProjectScenes.EnemyTest, ProjectScenes.BeliefTest
    };

    [Serializable] private sealed class SceneResult { public string path; public int dogs; public bool gameplayPreserved, otherEnemiesPreserved; }
    [Serializable] private sealed class Report { public SceneResult[] scenes; }

    [MenuItem("Tools/Combat/Apply dog sprite assets")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Slice("DogIdle", 3); Slice("DogWalk", 4); Slice("DogAttack", 6); Slice("DogDeath", 1);
        BuildVisualPrefab();
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        var results = new List<SceneResult>();
        try
        {
            foreach (string path in ScenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                DogEnemy[] dogs = Components<DogEnemy>(scene);
                string beforeDogs = DogGameplay(dogs), beforeOthers = OtherEnemies(scene);
                foreach (DogEnemy dog in dogs) ConfigureDog(dog);
                bool sameDogs = beforeDogs == DogGameplay(dogs), sameOthers = beforeOthers == OtherEnemies(scene);
                if (!sameDogs || !sameOthers) throw new InvalidOperationException("Dog art setup changed gameplay or another enemy: " + path);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                results.Add(new SceneResult { path = path, dogs = dogs.Length, gameplayPreserved = sameDogs, otherEnemiesPreserved = sameOthers });
            }
            AssetDatabase.SaveAssets();
            string reportPath = Argument("-dogVisualReport") ?? Path.GetFullPath("Logs/DogVisualSetup/report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(new Report { scenes = results.ToArray() }, true));
            Debug.Log("Dog sprite assets applied to " + results.Sum(result => result.dogs) + " dogs. " + reportPath);
        }
        finally
        {
            if (previous.Any(setup => setup.isLoaded && setup.isActive) &&
                previous.Where(setup => setup.isLoaded).All(setup => !string.IsNullOrEmpty(setup.path) && File.Exists(setup.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    private static void Slice(string name, int count)
    {
        string path = ArtFolder + "/" + name + ".png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new InvalidOperationException("Missing dog image: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = FrameHeight;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();

        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var existing = provider.GetSpriteRects().ToDictionary(rect => rect.name);
        var rects = new SpriteRect[count];
        for (int index = 0; index < count; index++)
        {
            string frameName = name + "_" + index.ToString("00");
            rects[index] = new SpriteRect {
                name = frameName, rect = new Rect(index * FrameWidth, 0, FrameWidth, FrameHeight),
                alignment = SpriteAlignment.Custom, pivot = new Vector2(.5f, 2f / FrameHeight),
                spriteID = existing.TryGetValue(frameName, out SpriteRect previous) ? previous.spriteID : GUID.Generate()
            };
        }
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        int importedCount = Frames(name).Length;
        if (importedCount != count) throw new InvalidOperationException("Dog sheet slicing failed: " + path + " (expected " + count + ", imported " + importedCount + ")");
    }

    private static Sprite[] Frames(string name) => AssetDatabase.LoadAllAssetsAtPath(ArtFolder + "/" + name + ".png")
        .OfType<Sprite>().OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();

    private static void BuildVisualPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        AssetDatabase.Refresh();
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        GameObject root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("DogVisual");
        try
        {
            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = Frames("DogIdle")[0]; renderer.color = Color.white;
            renderer.flipX = true; // Source art faces left; positive enemy scale means facing right.
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Boss/BossUnlit.mat");
            DogSpriteAnimator animation = root.GetComponent<DogSpriteAnimator>();
            if (animation == null) animation = root.AddComponent<DogSpriteAnimator>();
            var serialized = new SerializedObject(animation);
            SetFrames(serialized, "idleFrames", Frames("DogIdle"));
            SetFrames(serialized, "walkFrames", Frames("DogWalk"));
            SetFrames(serialized, "attackFrames", Frames("DogAttack"));
            serialized.FindProperty("deathSprite").objectReferenceValue = Frames("DogDeath")[0];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
    }

    private static void SetFrames(SerializedObject serialized, string property, Sprite[] frames)
    {
        SerializedProperty array = serialized.FindProperty(property); array.arraySize = frames.Length;
        for (int index = 0; index < frames.Length; index++) array.GetArrayElementAtIndex(index).objectReferenceValue = frames[index];
    }

    private static void ConfigureDog(DogEnemy dog)
    {
        var body = dog.GetComponent<CapsuleCollider2D>();
        if (body == null) throw new InvalidOperationException("Dog requires its existing root CapsuleCollider2D.");
        SpriteRenderer placeholder = dog.GetComponent<SpriteRenderer>();
        DogSpriteAnimator animation = dog.GetComponentInChildren<DogSpriteAnimator>(true);
        if (animation == null)
            animation = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), dog.transform))
                .GetComponent<DogSpriteAnimator>();
        Transform visual = animation.transform;
        visual.localRotation = Quaternion.identity;
        // Preserve the original physics root while keeping square pixels and the feet on its ground line.
        Vector3 scale = dog.transform.lossyScale;
        float worldWidth = body.size.x * Mathf.Abs(scale.x), worldHeight = body.size.y * Mathf.Abs(scale.y);
        // Unity clamps a capsule's long axis to at least its diameter after scaling.
        if (body.direction == CapsuleDirection2D.Vertical) worldHeight = Mathf.Max(worldHeight, worldWidth);
        else worldWidth = Mathf.Max(worldWidth, worldHeight);
        visual.localPosition = new Vector3(body.offset.x,
            body.offset.y - worldHeight / Mathf.Max(.001f, Mathf.Abs(scale.y)) * .5f, 0f);
        float worldScale = worldWidth * FrameHeight / 51f;
        visual.localScale = new Vector3(worldScale / Mathf.Max(.001f, Mathf.Abs(scale.x)),
            worldScale / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1f);
        if (placeholder != null)
        {
            var renderer = animation.GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = placeholder.sortingLayerID; renderer.sortingOrder = placeholder.sortingOrder;
            Object.DestroyImmediate(placeholder);
        }
    }

    private static T[] Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    private static string DogGameplay(IEnumerable<DogEnemy> dogs) => string.Join("\n", dogs.Select(dog =>
        EditorJsonUtility.ToJson(dog) + EditorJsonUtility.ToJson(dog.GetComponent<DogChargeAttack>()) +
        EditorJsonUtility.ToJson(dog.GetComponent<Collider2D>()) + EditorJsonUtility.ToJson(dog.GetComponent<Rigidbody2D>()) +
        dog.transform.localPosition.ToString("R") + dog.transform.localScale.ToString("R")));

    private static string OtherEnemies(Scene scene) => string.Join("\n", Components<EnemyUnit>(scene)
        .Where(enemy => !(enemy is DogEnemy)).Select(enemy => EditorJsonUtility.ToJson(enemy) +
            EditorJsonUtility.ToJson(enemy.GetComponent<EnemyAttack>()) + EditorJsonUtility.ToJson(enemy.GetComponent<Collider2D>()) +
            EditorJsonUtility.ToJson(enemy.GetComponent<SpriteRenderer>())));

    private static string Argument(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
