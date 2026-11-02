using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EnemyCombatTestSceneSetup
{
    private const string SourceScenePath = ProjectScenes.EnemyTest;
    private const float CombatFloorWidth = 36f;
    private const float CombatWallWidth = 0.5f;
    private const float CombatWallHeight = 10f;
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
    private const string PlayerHudPrefabPath = "Assets/Prefabs/UI/PlayerHud.prefab";
    private static readonly (string enemyName, string scenePath)[] Scenes =
    {
        ("Dog", ProjectScenes.DogCombatTest),
        ("Believer", ProjectScenes.BelieverCombatTest),
        ("Executive", ProjectScenes.ExecutiveCombatTest)
    };
    private static readonly string[] EnemyNames = { "Dog", "Believer", "Executive" };

    [MenuItem("Tools/Combat/Build one-enemy combat test scenes")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        foreach (var entry in Scenes)
            BuildScene(entry.enemyName, entry.scenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("DogCombatTest, BelieverCombatTest and ExecutiveCombatTest are ready; EnemyTest remains unchanged.");
    }

    private static void BuildScene(string enemyName, string outputPath)
    {
        Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        GameObject selectedEnemy = Root(scene, enemyName);
        GameObject oldPlayer = Root(scene, "TestPlayer");
        GameObject dummyRoot = Root(scene, "BeliefDummyData");
        BeliefDummyHitTool hitTool = dummyRoot != null ? dummyRoot.GetComponent<BeliefDummyHitTool>() : null;
        if (selectedEnemy == null || oldPlayer == null || hitTool == null)
            throw new System.InvalidOperationException("EnemyTest requires TestPlayer, BeliefDummyData and " + enemyName + ".");

        foreach (string otherName in EnemyNames.Where(name => name != enemyName))
        {
            GameObject other = Root(scene, otherName);
            if (other != null) Object.DestroyImmediate(other);
        }

        selectedEnemy.SetActive(true);
        selectedEnemy.transform.position = new Vector3(5f, selectedEnemy.transform.position.y, selectedEnemy.transform.position.z);

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerHudPrefabPath);
        if (playerPrefab == null || hudPrefab == null)
            throw new System.InvalidOperationException("The shared Player and PlayerHud prefabs must exist before building combat test scenes.");

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        player.name = "Player";
        player.transform.position = new Vector3(-5f, -1.95f, 0f);
        ReplaceSceneReferences(scene, oldPlayer, player);
        Object.DestroyImmediate(oldPlayer);
        if (player.GetComponent<PlayerDeathCoordinator>() == null)
            player.AddComponent<PlayerDeathCoordinator>();
        ConfigureCamera(scene, player.transform);
        PrefabUtility.InstantiatePrefab(hudPrefab, scene);

        GameObject ground = Root(scene, "Ground");
        if (ground == null)
            throw new System.InvalidOperationException("EnemyTest requires a Ground root.");
        Vector3 groundScale = ground.transform.localScale;
        ground.transform.localScale = new Vector3(CombatFloorWidth, groundScale.y, groundScale.z);
        ground.transform.position = new Vector3(ground.transform.position.x, -3.5f, ground.transform.position.z);
        ConfigureBoundaryWalls(scene, ground);

        EnemyUnit enemyUnit = selectedEnemy.GetComponent<EnemyUnit>();
        if (enemyUnit == null)
            throw new System.InvalidOperationException(enemyName + " root requires an EnemyUnit component.");
        var hitToolSettings = new SerializedObject(hitTool);
        SerializedProperty targets = hitToolSettings.FindProperty("targets");
        targets.arraySize = 1;
        targets.GetArrayElementAtIndex(0).objectReferenceValue = enemyUnit;
        hitToolSettings.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, outputPath);
    }

    private static GameObject Root(Scene scene, string name) =>
        scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);

    private static void ConfigureBoundaryWalls(Scene scene, GameObject ground)
    {
        Sprite wallSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Boss/UnitSquare.png");
        Material wallMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Boss/BossUnlit.mat");
        if (wallSprite == null || wallMaterial == null)
            throw new System.InvalidOperationException("Combat boundary walls require UnitSquare.png and BossUnlit.mat.");

        GameObject group = new GameObject("Combat Boundary Walls");
        SceneManager.MoveGameObjectToScene(group, scene);
        float halfFloor = CombatFloorWidth * 0.5f;
        float wallCenterY = ground.transform.position.y + CombatWallHeight * 0.5f;
        CreateBoundaryWall(group.transform, ground.layer, "Left wall", -halfFloor - CombatWallWidth * 0.5f,
            wallCenterY, wallSprite, wallMaterial);
        CreateBoundaryWall(group.transform, ground.layer, "Right wall", halfFloor + CombatWallWidth * 0.5f,
            wallCenterY, wallSprite, wallMaterial);
    }

    private static void CreateBoundaryWall(Transform parent, int layer, string name, float x, float y,
        Sprite sprite, Material material)
    {
        GameObject wall = new GameObject(name);
        wall.layer = layer;
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = new Vector3(x, y, 0f);
        wall.transform.localScale = new Vector3(CombatWallWidth, CombatWallHeight, 1f);

        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.isTrigger = false;

        SpriteRenderer renderer = wall.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.color = new Color(.22f, .28f, .38f, 1f);
    }

    private static void ConfigureCamera(Scene scene, Transform player)
    {
        GameObject mainCameraObject = Root(scene, "Main Camera");
        Camera mainCamera = mainCameraObject != null ? mainCameraObject.GetComponent<Camera>() : null;
        if (mainCamera == null)
            throw new System.InvalidOperationException("EnemyTest requires a Main Camera with a Camera component.");

        System.Type brainType = CinemachineType("CinemachineBrain");
        if (mainCameraObject.GetComponent(brainType) == null)
            mainCameraObject.AddComponent(brainType);

        GameObject cameraRig = new GameObject("CinemachineCamera");
        SceneManager.MoveGameObjectToScene(cameraRig, scene);
        Vector3 followOffset = new Vector3(1.5f, 2.2f, -10f);
        cameraRig.transform.position = player.position + followOffset;

        Component follow = cameraRig.AddComponent(CinemachineType("CinemachineFollow"));
        var followSettings = new SerializedObject(follow);
        followSettings.FindProperty("FollowOffset").vector3Value = followOffset;
        followSettings.FindProperty("TrackerSettings.PositionDamping").vector3Value = new Vector3(.2f, .2f, 0f);
        followSettings.ApplyModifiedPropertiesWithoutUndo();

        Component virtualCamera = cameraRig.AddComponent(CinemachineType("CinemachineCamera"));
        var cameraSettings = new SerializedObject(virtualCamera);
        cameraSettings.FindProperty("Target.TrackingTarget").objectReferenceValue = player;
        cameraSettings.FindProperty("Lens.OrthographicSize").floatValue = 6.5f;
        cameraSettings.ApplyModifiedPropertiesWithoutUndo();
        mainCamera.transform.position = cameraRig.transform.position;
    }

    private static System.Type CinemachineType(string className)
    {
        System.Type type = System.Type.GetType("Unity.Cinemachine." + className + ", Unity.Cinemachine");
        if (type == null)
            throw new System.InvalidOperationException("Cinemachine component not found: " + className);
        return type;
    }

    private static void ReplaceSceneReferences(Scene scene, GameObject source, GameObject replacement)
    {
        var map = new Dictionary<Object, Object>();
        foreach (Transform oldTransform in source.GetComponentsInChildren<Transform>(true))
        {
            string path = AnimationUtility.CalculateTransformPath(oldTransform, source.transform);
            Transform target = path.Length == 0 ? replacement.transform : replacement.transform.Find(path);
            if (target == null) continue;

            map[oldTransform.gameObject] = target.gameObject;
            foreach (Component oldComponent in oldTransform.GetComponents<Component>())
                if (oldComponent != null && target.GetComponent(oldComponent.GetType()) is Component newComponent)
                    map[oldComponent] = newComponent;
        }

        foreach (Component component in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)))
        {
            if (component == null || component.transform.IsChildOf(source.transform)) continue;
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue != null && map.TryGetValue(property.objectReferenceValue, out Object target))
                    property.objectReferenceValue = target;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
