using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Assemble the prototype from the authored player, enemy and boss test scenes.</summary>
public static class StageTestProjectSetup
{
    public const string PlayerPath = "Assets/Prefabs/Player/Player.prefab";
    public const string HudPath = "Assets/Prefabs/UI/PlayerHud.prefab";
    public const string StagePath = "Assets/Scenes/StageTest.unity";
    private const string PlayerScene = "Assets/Scenes/PlayerTEST.unity";
    private const string BeliefScene = "Assets/Scenes/BeliefTest.unity";
    private const float FloorY = -3.35f;

    [MenuItem("Tools/Stage/Sync test players and build StageTest")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildSharedPlayerAndHud();
        RemoveLegacyBossPlayerHud();
        SyncTestScene(BeliefScene);
        SyncTestScene(BossProjectSetup.ScenePath);
        BuildStage();
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(scene => scene.path == StagePath))
            scenes.Add(new EditorBuildSettingsScene(StagePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("StageTest ready: shared PlayerTEST player/UI, three enemy species, boss and dash afterimages.");
    }

    private static void BuildSharedPlayerAndHud()
    {
        Scene scene = EditorSceneManager.OpenScene(PlayerScene, OpenSceneMode.Single);
        GameObject player = Player(scene);
        // The source scene contained two pose owners writing the same transform.
        foreach (PlayerBody duplicate in player.GetComponents<PlayerBody>().Skip(1))
            Object.DestroyImmediate(duplicate);
        Ensure<PlayerBody>(player);
        Ensure<PlayerHealth>(player);
        Ensure<PlayerCombatEvents>(player);
        Ensure<DoctrineStats>(player);
        Ensure<PlayerAttack>(player);
        PlayerDash dash = Ensure<PlayerDash>(player);
        Ensure<CombatAfterimageEmitter>(player);
        Ensure<PlayerHitReact>(player);
        Set(player.GetComponent<PlayerHealth>(), "bodyCollider", player.GetComponent<Collider2D>());
        Set(dash, "afterimagePrefab", AssetDatabase.LoadAssetAtPath<CombatAfterimage>(
            "Assets/Prefabs/Boss/Effects/BossDashTrail.prefab"));
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Player"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Player");
        PrefabUtility.SaveAsPrefabAssetAndConnect(player, PlayerPath, InteractionMode.AutomatedAction);

        DoctrineHud hud = Find<DoctrineHud>(scene);
        if (hud == null) throw new InvalidOperationException("PlayerTEST requires its authored DoctrineHud.");
        Set(hud, "player", null); // HUD resolves the current scene's player, including after restart.
        PrefabUtility.SaveAsPrefabAssetAndConnect(hud.gameObject, HudPath, InteractionMode.AutomatedAction);
        ConfigureCamera(scene, player.transform, false);
        EditorSceneManager.SaveScene(scene);
    }

    private static void RemoveLegacyBossPlayerHud()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(BossProjectSetup.BossPath);
        try
        {
            Transform legacy = contents.transform.Find("Gameplay HUD/Player gameplay HUD");
            if (legacy != null) Object.DestroyImmediate(legacy.gameObject);
            PrefabUtility.SaveAsPrefabAsset(contents, BossProjectSetup.BossPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static void SyncTestScene(string path)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        GameObject oldPlayer = Player(scene);
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath), scene);
        player.name = oldPlayer.name;
        player.transform.position = oldPlayer.transform.position;
        ReplaceSceneReferences(scene, oldPlayer, player);
        Object.DestroyImmediate(oldPlayer);
        foreach (DoctrineHud oldHud in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DoctrineHud>(true)))
            Object.DestroyImmediate(oldHud.gameObject);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath), scene);
        EditorSceneManager.SaveScene(scene);
    }

    // Preserve dummy-tool, camera and other serialized references when replacing the player root.
    private static void ReplaceSceneReferences(Scene scene, GameObject source, GameObject replacement)
    {
        var map = new Dictionary<Object, Object>();
        foreach (Transform oldTransform in source.GetComponentsInChildren<Transform>(true))
        {
            string path = AnimationUtility.CalculateTransformPath(oldTransform, source.transform);
            Transform target = path.Length == 0 ? replacement.transform : replacement.transform.Find(path);
            if (target == null) continue;
            map[oldTransform.gameObject] = target.gameObject;
            foreach (Component component in oldTransform.GetComponents<Component>())
                if (component != null && target.GetComponent(component.GetType()) is Component counterpart)
                    map[component] = counterpart;
        }
        foreach (Component component in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)))
        {
            if (component == null || component.transform.IsChildOf(source.transform)) continue;
            var data = new SerializedObject(component);
            SerializedProperty property = data.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue != null && map.TryGetValue(property.objectReferenceValue, out Object target))
                    property.objectReferenceValue = target;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void BuildStage()
    {
        Scene stage = EditorSceneManager.OpenScene(PlayerScene, OpenSceneMode.Single);
        // Save under the new path before changing the layout of the source scene.
        EditorSceneManager.SaveScene(stage, StagePath);
        foreach (GameObject root in stage.GetRootGameObjects())
            if (root.name == "Ground") Object.DestroyImmediate(root);
        GameObject player = Player(stage);
        player.transform.position = new Vector3(-63f, FloorY + 1.05f, 0f);
        ConfigureCamera(stage, player.transform, true);
        Camera camera = Find<Camera>(stage);
        camera.backgroundColor = new Color(.075f, .1f, .16f);

        var ground = new GameObject("Ground - stage sections").transform;
        Solid(ground, "01 Spawn ground", new Vector2(-61f, FloorY - 1f), new Vector2(14f, 2f), new Color(.22f,.37f,.38f));
        Solid(ground, "02 Dog ground", new Vector2(-48f, FloorY - 1f), new Vector2(12f, 2f), new Color(.29f,.34f,.43f));
        Solid(ground, "03 Believer ground", new Vector2(-36f, FloorY - 1f), new Vector2(12f, 2f), new Color(.32f,.3f,.43f));
        Solid(ground, "04 Executive ground", new Vector2(-24f, FloorY - 1f), new Vector2(12f, 2f), new Color(.33f,.29f,.37f));
        Solid(ground, "05 Boss arena ground", new Vector2(0f, FloorY - 1f), new Vector2(36f, 2f), new Color(.38f,.33f,.25f));
        Solid(ground, "Left boundary", new Vector2(-68.5f, 2f), new Vector2(1f, 15f), new Color(.18f,.24f,.32f));
        Solid(ground, "Right boundary", new Vector2(18.5f, 2f), new Vector2(1f, 15f), new Color(.18f,.24f,.32f));
        Platform(ground, "Jump practice platform", new Vector2(-57f, FloorY + 1.6f), new Vector2(4f,.35f));
        Platform(ground, "Upper path A", new Vector2(-43f, FloorY + 1.6f), new Vector2(3f,.35f));
        Platform(ground, "Upper path B", new Vector2(-39f, FloorY + 2.5f), new Vector2(3f,.35f));
        Platform(ground, "Executive overlook", new Vector2(-25f, FloorY + 1.8f), new Vector2(4f,.35f));

        var scenery = new GameObject("Stage landmarks").transform;
        Visual(scenery, "Backdrop", new Vector2(-25f,2f),new Vector2(110f,35f),new Color(.09f,.13f,.2f),-30);
        for (int i = 0; i < 11; i++)
            Visual(scenery, "Distant pillar " + i,new Vector2(-66f + i*8f,1f),new Vector2(1.3f,12f),new Color(.12f,.17f,.24f),-29);
        Label(scenery,"START  >",new Vector2(-63f,1.3f),new Color(.5f,.9f,.85f));
        Label(scenery,"01  DOG",new Vector2(-49f,2.5f),new Color(.8f,.86f,1f));
        Label(scenery,"02  BELIEVER",new Vector2(-37f,2.5f),new Color(.8f,.75f,1f));
        Label(scenery,"03  EXECUTIVE",new Vector2(-25f,2.5f),new Color(1f,.75f,.8f));
        Label(scenery,"BOSS ARENA",new Vector2(0f,4f),new Color(1f,.85f,.5f));
        Visual(scenery,"Arena threshold",new Vector2(-17.5f,FloorY+3f),new Vector2(.16f,6f),new Color(1f,.8f,.4f,.45f),-1);

        Scene enemies = EditorSceneManager.OpenScene(BeliefScene, OpenSceneMode.Additive);
        try
        {
            EnemySection(stage, enemies, "Dog", -49f, -55f);
            EnemySection(stage, enemies, "Believer", -37f, -43f);
            EnemySection(stage, enemies, "Executive", -25f, -31f);
        }
        finally { EditorSceneManager.CloseScene(enemies, true); }

        Scene bossScene = EditorSceneManager.OpenScene(BossProjectSetup.ScenePath, OpenSceneMode.Additive);
        BossCombatController boss;
        try
        {
            GameObject clone = Object.Instantiate(Find<BossCombatController>(bossScene).gameObject);
            clone.name = "Boss";
            SceneManager.MoveGameObjectToScene(clone, stage);
            boss = clone.GetComponent<BossCombatController>();
            clone.SetActive(false);
        }
        finally { EditorSceneManager.CloseScene(bossScene, true); }
        SceneManager.SetActiveScene(stage);
        GameObject entrance = new GameObject("Boss arena entry");
        entrance.transform.position = new Vector2(0f,2f);
        entrance.AddComponent<BoxCollider2D>().size = new Vector2(34f,16f);
        entrance.GetComponent<BoxCollider2D>().isTrigger = true;
        Set(entrance.AddComponent<BossEncounterTrigger>(),"boss",boss);
        BuildInstructions(player.GetComponent<PlayerHealth>(),boss);
        EditorSceneManager.SaveScene(stage);
    }

    private static void EnemySection(Scene stage, Scene source, string name, float x, float entryX)
    {
        GameObject original = source.GetRootGameObjects().First(root => root.name == name);
        GameObject enemy = Object.Instantiate(original);
        enemy.name = name;
        SceneManager.MoveGameObjectToScene(enemy, stage);
        Collider2D collider = enemy.GetComponent<Collider2D>();
        enemy.transform.position = new Vector3(x, FloorY + collider.bounds.extents.y - collider.offset.y * enemy.transform.lossyScale.y + .05f,0f);
        enemy.SetActive(false);
        var entry = new GameObject(name + " encounter entry");
        SceneManager.MoveGameObjectToScene(entry, stage);
        entry.transform.position = new Vector2((entryX + x + 3f)*.5f,2f);
        entry.AddComponent<BoxCollider2D>().size = new Vector2(x + 3f - entryX,16f);
        entry.GetComponent<BoxCollider2D>().isTrigger = true;
        var data = new SerializedObject(entry.AddComponent<StageEnemyActivation>());
        var list = data.FindProperty("enemies"); list.arraySize = 1;
        list.GetArrayElementAtIndex(0).objectReferenceValue = enemy;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureCamera(Scene scene, Transform player, bool stage)
    {
        foreach (MonoBehaviour component in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)))
        {
            if (component == null) continue;
            var data = new SerializedObject(component);
            if (component.GetType().Name == "CinemachineCamera")
            {
                data.FindProperty("Target.TrackingTarget").objectReferenceValue = player;
                if (stage) data.FindProperty("Lens.OrthographicSize").floatValue = 6.5f;
            }
            if (stage && component.GetType().Name == "CinemachineFollow")
            {
                data.FindProperty("FollowOffset").vector3Value = new Vector3(1.5f,2.2f,-10f);
                data.FindProperty("TrackerSettings.PositionDamping").vector3Value = new Vector3(.2f,.2f,0f);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        if (stage) Find<Camera>(scene).transform.position = player.position + new Vector3(1.5f,2.2f,-10f);
    }

    private static void BuildInstructions(PlayerHealth player, BossCombatController boss)
    {
        var root = new GameObject("StageTest tools",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f,1080f); scaler.matchWidthOrHeight = .5f;
        var text = new GameObject("Controls and progress",typeof(RectTransform),typeof(Text)).GetComponent<Text>();
        text.transform.SetParent(root.transform,false);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 22;
        text.color = new Color(.87f,.92f,1f); text.alignment = TextAnchor.UpperLeft; text.raycastTarget = false;
        text.text = "STAGE TEST";
        text.rectTransform.anchorMin = new Vector2(0f,1f); text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.pivot = new Vector2(0f,1f); text.rectTransform.sizeDelta = new Vector2(-48f,76f);
        text.rectTransform.anchoredPosition = new Vector2(24f,-20f);
        var controller = root.AddComponent<StageTestController>();
        Set(controller,"player",player); Set(controller,"boss",boss); Set(controller,"instructions",text);
    }

    private static void Platform(Transform parent,string name,Vector2 position,Vector2 size)
    {
        GameObject platform = Solid(parent,name,position,size,new Color(.4f,.53f,.62f));
        platform.AddComponent<PlatformEffector2D>().useOneWay = true;
        platform.GetComponent<BoxCollider2D>().usedByEffector = true;
    }
    private static GameObject Solid(Transform parent,string name,Vector2 position,Vector2 size,Color color)
    {
        GameObject solid = Visual(parent,name,position,size,color,0);
        solid.layer = LayerMask.NameToLayer("Ground");
        solid.AddComponent<BoxCollider2D>();
        return solid;
    }
    private static GameObject Visual(Transform parent,string name,Vector2 position,Vector2 size,Color color,int order)
    {
        var root = new GameObject(name); root.transform.SetParent(parent,false);
        root.transform.position = position; root.transform.localScale = new Vector3(size.x,size.y,1f);
        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Boss/UnitSquare.png");
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Boss/BossUnlit.mat");
        renderer.color = color; renderer.sortingOrder = order;
        return root;
    }
    private static void Label(Transform parent,string title,Vector2 position,Color color)
    {
        var root = new GameObject(title); root.transform.SetParent(parent,false); root.transform.position = position;
        var label = root.AddComponent<TextMesh>(); label.text = title; label.fontSize = 60; label.characterSize = .08f;
        label.anchor = TextAnchor.MiddleCenter; label.color = color;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        root.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        root.GetComponent<MeshRenderer>().sortingOrder = -2;
    }
    private static GameObject Player(Scene scene) => scene.GetRootGameObjects().First(root => root.CompareTag("Player"));
    private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();
    private static T Ensure<T>(GameObject root) where T : Component => root.GetComponent<T>() ?? root.AddComponent<T>();
    private static void Set(Object target,string field,Object value)
    {
        var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
