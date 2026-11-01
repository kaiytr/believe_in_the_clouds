using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ChestTestProjectSetup
{
    public const string ScenePath = "Assets/Scenes/ChestTest.unity";
    public const string PrefabPath = "Assets/Prefabs/Chest/Chest.prefab";
    private static Sprite Square => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Boss/UnitSquare.png");
    private static Material Material => AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Boss/BossUnlit.mat");

    [MenuItem("Tools/Chest/Build independent test scene")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Prefabs/Chest")); AssetDatabase.Refresh();
        GameObject prefab = BuildPrefab();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 0, -10);
        Camera view = camera.GetComponent<Camera>(); view.orthographic = true; view.orthographicSize = 6;
        view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.07f, .105f, .16f);
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StageTestProjectSetup.PlayerPath));
        player.name = "Player"; player.transform.position = new Vector3(-8, -1.95f, 0);
        GameObject floor = Visual("Ground", null, new Vector2(0, -3.5f), new Vector2(25, 1), new Color(.23f, .3f, .38f), -1);
        floor.layer = LayerMask.NameToLayer("Ground"); floor.AddComponent<BoxCollider2D>();
        for (int i = 0; i < 4; i++)
        {
            var chest = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            chest.name = "Chest " + (i + 1); chest.transform.position = new Vector3(-5 + i * 3.4f, -2.5f, 0);
            var serialized = new SerializedObject(chest.GetComponent<ChestInteractable>());
            serialized.FindProperty("spawnId").stringValue = "test-chest-" + (i + 1);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        GameObject notice = Visual("Notice", null, new Vector2(9, -2), new Vector2(.65f, 2), new Color(.23f, .5f, .6f), -5);
        notice.AddComponent<BoxCollider2D>().isTrigger = true; notice.AddComponent<ChestTestNotice>();
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StageTestProjectSetup.HudPath));
        new GameObject("[ChestTest]").AddComponent<ChestTestContext>();
        EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
        Debug.Log("ChestTest built: four chests, immediate-E notice, isolated memory profile.");
    }
    private static GameObject BuildPrefab()
    {
        var root = new GameObject("Chest");
        var collider = root.AddComponent<BoxCollider2D>(); collider.isTrigger = true; collider.size = new Vector2(1.2f, .9f);
        var view = root.AddComponent<ChestView>(); var chest = root.AddComponent<ChestInteractable>();
        GameObject border = Visual("Border", root.transform, Vector2.zero, new Vector2(1.3f, 1), new Color(.32f, .24f, .15f), -7);
        GameObject body = Visual("Body", root.transform, Vector2.zero, new Vector2(1.2f, .9f), new Color(.72f, .46f, .2f), -6);
        var labelRoot = new GameObject("State"); labelRoot.transform.SetParent(root.transform, false); labelRoot.transform.localPosition = new Vector3(0, .8f, 0);
        var label = labelRoot.AddComponent<TextMesh>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        label.text = "상자"; label.anchor = TextAnchor.MiddleCenter; label.fontSize = 48; label.characterSize = .045f;
        label.GetComponent<MeshRenderer>().sortingOrder = -5;
        var drop = new GameObject("DropPoint"); drop.transform.SetParent(root.transform, false); drop.transform.localPosition = new Vector3(0, .55f, 0);
        var settings = new SerializedObject(view);
        settings.FindProperty("body").objectReferenceValue = body.GetComponent<SpriteRenderer>();
        settings.FindProperty("border").objectReferenceValue = border.GetComponent<SpriteRenderer>();
        settings.FindProperty("stateLabel").objectReferenceValue = label; settings.ApplyModifiedPropertiesWithoutUndo();
        settings = new SerializedObject(chest); settings.FindProperty("dropPoint").objectReferenceValue = drop.transform; settings.ApplyModifiedPropertiesWithoutUndo();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); Object.DestroyImmediate(root); return prefab;
    }
    private static GameObject Visual(string name, Transform parent, Vector2 position, Vector2 size, Color color, int order)
    {
        var root = new GameObject(name); root.transform.SetParent(parent, false); root.transform.localPosition = position; root.transform.localScale = new Vector3(size.x, size.y, 1);
        var renderer = root.AddComponent<SpriteRenderer>(); renderer.sprite = Square; renderer.sharedMaterial = Material; renderer.color = color; renderer.sortingOrder = order; return root;
    }
}
