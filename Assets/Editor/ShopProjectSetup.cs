using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShopProjectSetup
{
    public const string ScenePath = ProjectScenes.ShopTest;
    public const string PrefabPath = "Assets/Prefabs/Shop/Shop.prefab";

    [MenuItem("Tools/Shop/Build Shop prefab and update ShopTest")]
    public static void BuildShopPrefabAndScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsurePrefabFolder();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject shop = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Shop");
        if (shop == null)
            throw new System.InvalidOperationException("ShopTest requires a root object named Shop.");

        PlayerHealth player = Object.FindFirstObjectByType<PlayerHealth>();
        if (player == null)
            throw new System.InvalidOperationException("ShopTest requires a PlayerHealth player.");
        ConfigureCamera(scene, player.transform);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            ConfigureShop(shop);
            prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(shop, PrefabPath, InteractionMode.AutomatedAction);
        }
        else
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ConfigureShop(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!PrefabUtility.IsPartOfPrefabInstance(shop))
            {
                Vector3 position = shop.transform.position;
                Quaternion rotation = shop.transform.rotation;
                Vector3 scale = shop.transform.localScale;
                Object.DestroyImmediate(shop);
                shop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                shop.name = "Shop";
                shop.transform.SetPositionAndRotation(position, rotation);
                shop.transform.localScale = scale;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("ShopTest now uses a pass-through Shop prefab.");
    }

    private static void ConfigureShop(GameObject root)
    {
        root.name = "Shop";
        BoxCollider2D collider = root.GetComponent<BoxCollider2D>();
        if (collider == null) collider = root.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;

        ShopInteractable interactable = root.GetComponent<ShopInteractable>();
        if (interactable == null) interactable = root.AddComponent<ShopInteractable>();
        var settings = new SerializedObject(interactable);
        settings.FindProperty("shopName").stringValue = "상점";
        settings.FindProperty("displayName").stringValue = "상점";
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureCamera(Scene scene, Transform target)
    {
        GameObject cameraObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Main Camera");
        if (cameraObject == null || cameraObject.GetComponent<Camera>() == null)
            throw new System.InvalidOperationException("ShopTest requires a Main Camera with a Camera component.");

        LobbyCameraFollow follow = cameraObject.GetComponent<LobbyCameraFollow>();
        if (follow == null) follow = cameraObject.AddComponent<LobbyCameraFollow>();
        follow.Target = target;
        follow.MinX = -100f;
        follow.MaxX = 100f;
    }

    private static void EnsurePrefabFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Shop"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Shop");
    }
}
