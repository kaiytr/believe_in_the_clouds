using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LobbyProjectSetup
{
    public const string LobbyPath = "Assets/Scenes/Lobby.unity";
    public const string StagePath = "Assets/Scenes/TemporaryStage.unity";
    private static Sprite Square => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Boss/UnitSquare.png");
    private static Material Material => AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Boss/BossUnlit.mat");

    [MenuItem("Tools/Lobby/Build lobby and temporary stage")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene(false); BuildScene(true);
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.path != LobbyPath && scene.path != StagePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(LobbyPath, true));
        scenes.Add(new EditorBuildSettingsScene(StagePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Lobby and TemporaryStage built; Lobby is the prototype build entry. Existing test scenes are preserved.");
    }

    private static void BuildScene(bool lobby)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StageTestProjectSetup.PlayerPath));
        player.name = "Player"; player.transform.position = new Vector3(lobby ? -18 : -7, -1.95f, 0);
        var cameraRoot = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(LobbyCameraFollow));
        cameraRoot.tag = "MainCamera"; cameraRoot.transform.position = new Vector3(lobby ? -13 : -2, 0, -10);
        Camera camera = cameraRoot.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 6;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .105f, .16f);
        var follow = cameraRoot.GetComponent<LobbyCameraFollow>(); follow.Target = player.transform;
        follow.MinX = lobby ? -24 : -14; follow.MaxX = lobby ? 32 : 14;

        GameObject scenery = new GameObject("Room");
        Visual(scenery.transform, "Backdrop", new Vector2(lobby ? 4 : 0, 1), new Vector2(lobby ? 60 : 30, 24), new Color(.085f, .13f, .2f), -30);
        int groundLayer = LayerMask.NameToLayer("Ground");
        GameObject ground = Visual(scenery.transform, "Ground", new Vector2(lobby ? 4 : 0, -3.5f), new Vector2(lobby ? 58 : 30, 1), new Color(.24f, .32f, .4f), 0);
        ground.layer = groundLayer; ground.AddComponent<BoxCollider2D>();
        foreach (float x in new[] { lobby ? -24f : -12f, lobby ? 32f : 12f })
        {
            GameObject wall = Visual(scenery.transform, "Room boundary", new Vector2(x, 4), new Vector2(1, 15), new Color(.16f, .23f, .3f), -5);
            wall.layer = groundLayer; wall.AddComponent<BoxCollider2D>();
        }
        if (lobby)
            for (int i = 0; i < 7; i++)
                Visual(scenery.transform, "Background pillar " + i, new Vector2(-20 + i * 8, 1), new Vector2(.3f, 12), new Color(.105f, .16f, .24f), -25);

        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StageTestProjectSetup.HudPath));
        var controller = new GameObject(lobby ? "[LobbyScene]" : "[TemporaryStageScene]").AddComponent<LobbySceneController>();
        var settings = new SerializedObject(controller);
        settings.FindProperty("isLobby").boolValue = lobby;
        settings.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerHealth>();
        settings.FindProperty("squareSprite").objectReferenceValue = Square;
        settings.FindProperty("worldMaterial").objectReferenceValue = Material;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene, lobby ? LobbyPath : StagePath);
    }

    private static GameObject Visual(Transform parent, string name, Vector2 position, Vector2 size, Color color, int order)
    {
        var root = new GameObject(name); root.transform.SetParent(parent); root.transform.position = position;
        root.transform.localScale = new Vector3(size.x, size.y, 1);
        var renderer = root.AddComponent<SpriteRenderer>(); renderer.sprite = Square; renderer.sharedMaterial = Material;
        renderer.color = color; renderer.sortingOrder = order;
        return root;
    }
}
