using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EconomyTestProjectSetup
{
    public const string ScenePath = ProjectScenes.EconomyTest;
    [MenuItem("Tools/Economy/Build independent test scene")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        const string settingsPath = "Assets/Resources/EconomySettings.asset";
        if (AssetDatabase.LoadAssetAtPath<EconomySettings>(settingsPath) == null)
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<EconomySettings>(), settingsPath);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 1, -10);
        var view = camera.GetComponent<Camera>(); view.orthographic = true; view.orthographicSize = 6;
        view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.14f, .19f, .26f);
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StageTestProjectSetup.PlayerPath));
        player.transform.position = new Vector3(1.5f, -1.5f, 0);
        var floor = new GameObject("Test Floor", typeof(BoxCollider2D));
        floor.layer = LayerMask.NameToLayer("Ground");
        floor.transform.position = new Vector3(0, -3.5f, 0); floor.GetComponent<BoxCollider2D>().size = new Vector2(200, 1);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StageTestProjectSetup.HudPath));
        new GameObject("Economy Test Fixture").AddComponent<TestEconomyContext>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("EconomyTest built. Fixture controls use an isolated store; actual progression is not connected.");
    }
}
