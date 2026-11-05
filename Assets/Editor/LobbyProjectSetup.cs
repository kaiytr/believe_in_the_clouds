using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LobbyProjectSetup
{
    public const string LobbyPath = ProjectScenes.Lobby;
    public const string LobbyPrefabFolder = "Assets/Prefabs/Lobby";
    public const string BranchPrefabPath = LobbyPrefabFolder + "/LobbyBranch.prefab";
    public const string FoundationPrefabPath = LobbyPrefabFolder + "/Foundation.prefab";
    private static Sprite Square => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Boss/UnitSquare.png");
    private static Material Material => AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Boss/BossUnlit.mat");

    [MenuItem("Tools/Lobby/Build lobby")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureLobbyPrefabs();
        BuildScene();
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.path != LobbyPath &&
            scene.path != "Assets/Scenes/Gameplay/TemporaryStage.unity").ToList();
        int titleIndex = scenes.FindIndex(scene => scene.path == ProjectScenes.Title);
        scenes.Insert(titleIndex >= 0 ? titleIndex + 1 : 0, new EditorBuildSettingsScene(LobbyPath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Lobby built; removed event stage excluded. Existing scene registrations are preserved.");
    }

    [MenuItem("Tools/Lobby/Configure existing lobby prefab layout")]
    public static void ConfigureExistingLobbyPrefabLayout()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureLobbyPrefabs();
        Scene scene = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);
        LobbySceneController controller = Object.FindFirstObjectByType<LobbySceneController>();
        if (controller == null)
        {
            Debug.LogError("LobbySceneController was not found in Lobby.unity.");
            return;
        }

        ConfigureLobbyLayout(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Lobby prefab assets and editable layout anchors are ready.");
    }

    private static void BuildScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StageTestProjectSetup.PlayerPath));
        player.name = "Player"; player.transform.position = new Vector3(-12, -1.95f, 0);
        var cameraRoot = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(LobbyCameraFollow));
        cameraRoot.tag = "MainCamera"; cameraRoot.transform.position = new Vector3(-13, 0, -10);
        Camera camera = cameraRoot.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 6;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .105f, .16f);
        var follow = cameraRoot.GetComponent<LobbyCameraFollow>(); follow.Target = player.transform;
        follow.MinX = -24; follow.MaxX = 32;

        GameObject scenery = new GameObject("Room");
        Visual(scenery.transform, "Backdrop", new Vector2(4, 1), new Vector2(60, 24), new Color(.085f, .13f, .2f), -30);
        int groundLayer = LayerMask.NameToLayer("Ground");
        GameObject ground = Visual(scenery.transform, "Ground", new Vector2(4, -3.5f), new Vector2(58, 1), new Color(.24f, .32f, .4f), 0);
        ground.layer = groundLayer; ground.AddComponent<BoxCollider2D>();
        foreach (float x in new[] { -24f, 32f })
        {
            GameObject wall = Visual(scenery.transform, "Room boundary", new Vector2(x, 4), new Vector2(1, 15), new Color(.16f, .23f, .3f), -5);
            wall.layer = groundLayer; wall.AddComponent<BoxCollider2D>();
        }
        for (int i = 0; i < 7; i++)
            Visual(scenery.transform, "Background pillar " + i, new Vector2(-20 + i * 8, 1), new Vector2(.3f, 12), new Color(.105f, .16f, .24f), -25);

        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StageTestProjectSetup.HudPath));
        var controller = new GameObject("[LobbyScene]").AddComponent<LobbySceneController>();
        var settings = new SerializedObject(controller);
        settings.FindProperty("isLobby").boolValue = true;
        settings.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerHealth>();
        settings.FindProperty("squareSprite").objectReferenceValue = Square;
        settings.FindProperty("worldMaterial").objectReferenceValue = Material;
        settings.ApplyModifiedPropertiesWithoutUndo();
        ConfigureLobbyLayout(controller);
        EditorSceneManager.SaveScene(scene, LobbyPath);
    }

    private static void EnsureLobbyPrefabs()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Prefabs/Lobby"));
        AssetDatabase.Refresh();

        if (AssetDatabase.LoadAssetAtPath<GameObject>(BranchPrefabPath) == null)
        {
            CreateBranchPrefab();
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(FoundationPrefabPath) == null)
        {
            CreateFoundationPrefab();
        }
    }

    private static void CreateBranchPrefab()
    {
        GameObject root = new GameObject("LobbyBranch");
        BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(2.8f, 4.6f);
        LobbyBranchInteractable interactable = root.AddComponent<LobbyBranchInteractable>();

        SpriteRenderer body = Visual(root.transform, "Panel", Vector2.zero, new Vector2(2.8f, 4.6f),
            new Color(.36f, .39f, .45f), -8).GetComponent<SpriteRenderer>();
        SpriteRenderer border = Visual(root.transform, "Frame", Vector2.zero, new Vector2(3f, 4.8f),
            new Color(.19f, .23f, .3f), -9).GetComponent<SpriteRenderer>();
        TextMesh label = WorldLabelPrefab(root.transform, "Role", "?", new Vector2(0f, 2.8f), Color.white);

        var settings = new SerializedObject(interactable);
        settings.FindProperty("body").objectReferenceValue = body;
        settings.FindProperty("border").objectReferenceValue = border;
        settings.FindProperty("label").objectReferenceValue = label;
        settings.FindProperty("trigger").objectReferenceValue = trigger;
        settings.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, BranchPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.ImportAsset(BranchPrefabPath);
    }

    private static void CreateFoundationPrefab()
    {
        GameObject root = new GameObject("Foundation");
        BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(3.2f, 4.6f);
        FoundationInteractable interactable = root.AddComponent<FoundationInteractable>();

        Visual(root.transform, "Foundation body", Vector2.zero, new Vector2(2.8f, 4.6f),
            new Color(.36f, .39f, .45f), -8);
        WorldLabelPrefab(root.transform, "Foundation label", "교리 재단", new Vector2(0f, 2.8f),
            new Color(.82f, .74f, .97f));

        var settings = new SerializedObject(interactable);
        settings.FindProperty("foundationName").stringValue = "재단";
        settings.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, FoundationPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.ImportAsset(FoundationPrefabPath);
    }

    private static void ConfigureLobbyLayout(LobbySceneController controller)
    {
        Transform oldLayout = controller.transform.Find("Lobby Layout Anchors");
        if (oldLayout != null)
        {
            Object.DestroyImmediate(oldLayout.gameObject);
        }

        GameObject layout = new GameObject("Lobby Layout Anchors");
        layout.transform.SetParent(controller.transform, false);

        Transform foundationAnchor = Anchor(layout.transform, "Foundation Anchor", new Vector3(-7f, -.7f, 0f));
        Transform twoLayout = Anchor(layout.transform, "Two Candidate Layout", Vector3.zero);
        Transform[] twoSlots =
        {
            Anchor(twoLayout, "Branch Slot 1", new Vector3(10f, -.7f, 0f)),
            Anchor(twoLayout, "Branch Slot 2", new Vector3(20f, -.7f, 0f))
        };
        Transform threeLayout = Anchor(layout.transform, "Three Candidate Layout", Vector3.zero);
        Transform[] threeSlots =
        {
            Anchor(threeLayout, "Branch Slot 1", new Vector3(7f, -.7f, 0f)),
            Anchor(threeLayout, "Branch Slot 2", new Vector3(15f, -.7f, 0f)),
            Anchor(threeLayout, "Branch Slot 3", new Vector3(23f, -.7f, 0f))
        };

        GameObject branchAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BranchPrefabPath);
        GameObject foundationAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FoundationPrefabPath);
        var settings = new SerializedObject(controller);
        settings.FindProperty("branchPrefab").objectReferenceValue = branchAsset.GetComponent<LobbyBranchInteractable>();
        settings.FindProperty("foundationPrefab").objectReferenceValue = foundationAsset.GetComponent<FoundationInteractable>();
        settings.FindProperty("foundationAnchor").objectReferenceValue = foundationAnchor;
        SetTransformArray(settings.FindProperty("twoCandidateSlots"), twoSlots);
        SetTransformArray(settings.FindProperty("threeCandidateSlots"), threeSlots);
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform Anchor(Transform parent, string name, Vector3 position)
    {
        GameObject anchor = new GameObject(name);
        anchor.transform.SetParent(parent, false);
        anchor.transform.localPosition = position;
        return anchor.transform;
    }

    private static void SetTransformArray(SerializedProperty property, Transform[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static TextMesh WorldLabelPrefab(Transform parent, string name, string value, Vector2 position, Color color)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = position;
        TextMesh label = root.AddComponent<TextMesh>();
        label.text = value;
        label.fontSize = 48;
        label.characterSize = .055f;
        label.anchor = TextAnchor.MiddleCenter;
        label.color = color;
        root.GetComponent<MeshRenderer>().sortingOrder = -1;
        return label;
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
