using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Side-view ruined sanctuary assembled from the project's existing stone atlas.
public static class BossStageSceneryProjectSetup
{
    public const string PrefabPath = "Assets/Prefabs/Environment/BossRuinedSanctuary.prefab";
    public const float SceneMinX = -42f, SceneMaxX = 48f;
    public const float ArenaMinX = -12f, ArenaMaxX = 33f;
    public const float PlayerSpawnX = -38f;
    private const float SceneWidth = SceneMaxX - SceneMinX;
    private const float SceneCenter = (SceneMinX + SceneMaxX) * .5f;
    private const string LayoutName = "Boss stage (90 world units)";
    private const string RootName = "Boss ruined sanctuary";
    private const string Atlas = "Assets/Art/LevelTiles/PlatformerTerrain.png";
    private const float Floor = -3.35f;
    private static Sprite[] stone;
    private static Material material;
    private static Sprite square, disc;

    [MenuItem("Tools/Boss/Apply ruined sanctuary stage")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            BuildPrefab();
            ApplyScene(ProjectScenes.Boss, ArenaMinX, ArenaMaxX, true);
            GameObject encounter = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Boss/BossEncounter.prefab");
            try
            {
                BossArenaProjectSetup.Configure(encounter.GetComponentInChildren<BossEncounterTrigger>(),
                    encounter.GetComponentInChildren<BossCombatController>(), ArenaMinX, ArenaMaxX, Floor, null);
                PrefabUtility.SaveAsPrefabAsset(encounter, "Assets/Prefabs/Boss/BossEncounter.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(encounter); }
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
        Debug.Log("Boss stage expanded to 90 units with a 30-unit approach, 45-unit arena and 15-unit far side. Original camera tracking retained.");
    }

    public static void Decorate(Scene scene)
    {
        GameObject old = scene.GetRootGameObjects().FirstOrDefault(root => root.name == RootName);
        if (old != null) Object.DestroyImmediate(old);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { BuildPrefab(); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); }
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = RootName;
        // The replacement floor is visual only; layout authoring owns the actual colliders.
        foreach (SpriteRenderer renderer in scene.GetRootGameObjects()
                     .Where(root => root.name == "Arena (36 world units)" || root.name == LayoutName)
                     .SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true)))
            if (renderer.gameObject.name == "Main ground" || renderer.gameObject.name == "Left wall" ||
                renderer.gameObject.name == "Right wall") renderer.enabled = false;
    }

    private static void ApplyScene(string path, float minX, float maxX, bool fixedExploration)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        BossEncounterTrigger entry = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<BossEncounterTrigger>(true)).Single();
        BossCombatController boss = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<BossCombatController>(true)).Single();
        Camera camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>())
            .First(item => item.CompareTag("MainCamera"));
        BossArenaProjectSetup.Configure(entry, boss, minX, maxX, Floor, camera, fixedExploration);
        // Both authored scenes use horizontal exploration, including StageTest's virtual camera.
        var cameraData = new SerializedObject(entry.GetComponent<BossArenaCamera>());
        cameraData.FindProperty("explorationFixedY").boolValue = true;
        cameraData.ApplyModifiedPropertiesWithoutUndo();
        if (fixedExploration)
        {
            ConfigureLayout(scene, camera);
            Decorate(scene);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void ConfigureLayout(Scene scene, Camera camera)
    {
        GameObject layout = scene.GetRootGameObjects().First(root =>
            root.name == "Arena (36 world units)" || root.name == LayoutName);
        layout.name = LayoutName;
        Transform floor = layout.transform.Find("Main ground");
        floor.position = new Vector3(SceneCenter, Floor - .5f, 0);
        floor.localScale = new Vector3(SceneWidth, 1, 1);
        layout.transform.Find("Left wall").position = new Vector3(SceneMinX - .25f, Floor + 4.5f, 0);
        layout.transform.Find("Right wall").position = new Vector3(SceneMaxX + .25f, Floor + 4.5f, 0);

        PlayerHealth player = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PlayerHealth>(true)).Single();
        player.transform.position = new Vector3(PlayerSpawnX, Floor + 1.05f, 0);
        LobbyCameraFollow follow = camera.GetComponent<LobbyCameraFollow>();
        follow.MinX = SceneMinX;
        follow.MaxX = SceneMaxX;
        camera.transform.position = new Vector3(Mathf.Clamp(PlayerSpawnX + 2,
            SceneMinX + camera.orthographicSize * camera.aspect,
            SceneMaxX - camera.orthographicSize * camera.aspect), 0, -10);
        BossArenaCamera arenaCamera = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<BossArenaCamera>(true)).Single();
        var cameraData = new SerializedObject(arenaCamera);
        cameraData.FindProperty("explorationXLimits").vector2Value = new Vector2(SceneMinX, SceneMaxX);
        cameraData.ApplyModifiedPropertiesWithoutUndo();
        foreach (LobbySceneController flow in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<LobbySceneController>(true)))
        {
            var data = new SerializedObject(flow);
            data.FindProperty("stageMinX").floatValue = SceneMinX + 1;
            data.FindProperty("stageMaxX").floatValue = SceneMaxX - 1;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void BuildPrefab()
    {
        stone = AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().ToArray();
        material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Boss/BossUnlit.mat");
        square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Boss/UnitSquare.png");
        disc = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Boss/UnitDisc.png");
        GameObject root = new GameObject(RootName);
        try
        {
            Transform sky = Group(root.transform, "Overcast sky and distant ruins");
            SpritePart(sky, "Overcast horizon", square, new Vector2(SceneCenter, 8), new Vector2(SceneWidth + 40, 70),
                new Color(.25f, .29f, .31f), -60);
            for (int i = 0; i < 17; i++)
                SpritePart(sky, "Cloud bank", disc, new Vector2(SceneMinX - 7 + i * 6.5f, 13.4f + Mathf.Sin(i * 2.1f)),
                    new Vector2(12, 4.5f), new Color(.15f, .19f, .22f), -55);
            for (int i = 0; i < 16; i++)
            {
                float x = SceneMinX - 7 + i * 7;
                float height = 8 + Mathf.Sin(i * 2.8f) * 2;
                Tiled(sky, "Distant masonry", "InteriorWall", new Vector2(x, Floor + height * .5f),
                    new Vector2(5.8f, height), new Color(.37f, .44f, .48f), -50);
                Column(sky, x - 2.5f, Floor, height + .8f, .8f, new Color(.38f, .44f, .47f), -49);
                SpritePart(sky, "Distant arch", Stone("InteriorArcade"), new Vector2(x, Floor + height - .2f),
                    new Vector2(5.8f, 3), new Color(.39f, .45f, .48f), -48);
            }

            Transform hall = Group(root.transform, "Broken nave");
            Color masonry = new Color(.67f, .69f, .67f);
            float[] columns = { -43, -35.5f, -28, -20.5f, -13, -4, 5, 12.5f, 20, 27.5f, 35, 42.5f, 50 };
            float[] heights = { 17, 13.8f, 16, 11.8f, 14, 10.7f, 9.1f, 13.8f, 17, 10, 14, 12.5f, 17 };
            for (int i = 0; i < columns.Length; i++)
                Column(hall, columns[i], Floor, heights[i], 1.35f, masonry, -30);
            // A few surviving bays frame a large opening above the central arena.
            foreach (float x in new[] { -39.25f, -31.75f, -24.25f, -16.75f, 16.25f, 38.75f, 46.25f })
            {
                SpritePart(hall, "Surviving stone arch", Stone("InteriorArcade"), new Vector2(x, 10.1f),
                    new Vector2(7.5f, 4), masonry, -31);
                Tiled(hall, "Broken roof edge", "InteriorCornice", new Vector2(x, 13),
                    new Vector2(7.5f, .8f), masonry, -29);
            }
            foreach (float x in new[] { -7.7f, 8.7f })
                SpritePart(hall, "Broken arch shoulder", Stone("InteriorLedgeLeft"), new Vector2(x, 7.8f),
                    new Vector2(2.5f, 1.3f), masonry, -29).transform.rotation = Quaternion.Euler(0, 0, x < 0 ? -14 : 18);
            Sprite banner = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/LevelTiles/InteriorDecor.png")
                .OfType<Sprite>().Single(item => item.name == "InteriorBanner");
            foreach (float x in new[] { -35.5f, -20.5f, 12.5f, 35f, 42.5f })
                SpritePart(hall, "Weathered hanging cloth", banner, new Vector2(x, 5.8f), new Vector2(1, 3.8f),
                    new Color(.4f, .39f, .44f), -28);

            Transform rubble = Group(root.transform, "Fallen columns and rubble behind combat plane");
            FallenColumn(rubble, -14.1f, 3.7f, -66);
            FallenColumn(rubble, 15.5f, 4.8f, 72);
            FallenColumn(rubble, 8.4f, 2.5f, 82);
            FallenColumn(rubble, -30f, 3.3f, 70);
            FallenColumn(rubble, 39f, 3.7f, -74);
            for (int i = 0; i < 92; i++)
            {
                float x = SceneMinX - 1 + i * 1.03f;
                float height = .16f + Mathf.Abs(Mathf.Sin(i * 7.3f)) * .3f;
                SpritePart(rubble, "Stone fragment", Stone("InteriorGround" + i % 4 + "_" + i % 3),
                    new Vector2(x, Floor + height * .5f), new Vector2(.3f + height, height), masonry, -15)
                    .transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(i * 3.1f) * 24);
            }

            Transform ground = Group(root.transform, "Continuous stone combat floor");
            Tiled(ground, "Foundation masonry", "InteriorWall", new Vector2(SceneCenter, Floor - 4),
                new Vector2(SceneWidth + 8, 8), new Color(.87f, .85f, .79f), -3);
            Tiled(ground, "Worn stone floor lip", "InteriorCornice", new Vector2(SceneCenter, Floor - .15f),
                new Vector2(SceneWidth + 8, .3f), new Color(.94f, .92f, .84f), -2);
            SpritePart(ground, "Foundation shadow", square, new Vector2(SceneCenter, Floor - 16), new Vector2(SceneWidth + 40, 18),
                new Color(.065f, .08f, .09f), -1);

            Rain(root.transform, "Distant rainfall", -12, 400, .025f, .3f);
            Rain(root.transform, "Foreground rainfall", 3, 100, .018f, .18f);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void Column(Transform parent, float x, float floor, float height, float width, Color tint, int order)
    {
        Tiled(parent, "Fluted column shaft", "InteriorColumnShaft", new Vector2(x, floor + height * .5f),
            new Vector2(width, height - .6f), tint, order);
        SpritePart(parent, "Column base", Stone("InteriorColumnBase"), new Vector2(x, floor + .35f),
            new Vector2(width + .65f, .7f), tint, order + 1);
        SpritePart(parent, "Column capital", Stone("InteriorColumnCapital"), new Vector2(x, floor + height - .35f),
            new Vector2(width + .7f, .7f), tint, order + 1);
    }

    private static void FallenColumn(Transform parent, float x, float length, float angle)
    {
        Transform fallen = Group(parent, "Fallen column");
        fallen.localPosition = new Vector3(x, Floor + .5f, 0);
        fallen.localRotation = Quaternion.Euler(0, 0, angle);
        Tiled(fallen, "Broken shaft", "InteriorColumnShaft", new Vector2(0, length * .5f), new Vector2(.8f, length),
            new Color(.7f, .72f, .68f), -19);
    }

    private static void Rain(Transform parent, string name, int order, float rate, float width, float opacity)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = new Vector3(SceneCenter, Floor + 19, 0);
        ParticleSystem rain = obj.AddComponent<ParticleSystem>();
        rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = rain.main;
        main.duration = 2;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = 1.06f;
        main.startSpeed = 0;
        main.startSize3D = true;
        main.startSizeX = width;
        main.startSizeY = .48f;
        main.startSizeZ = 1;
        main.startRotation = -.1f;
        main.startColor = new Color(.67f, .76f, .8f, opacity);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 900;
        var emission = rain.emission;
        emission.rateOverTime = rate;
        var shape = rain.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(SceneWidth + 10, .1f, .1f);
        var velocity = rain.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = -1.8f;
        velocity.y = -18;
        velocity.z = 0;
        var renderer = rain.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
        rain.useAutoRandomSeed = false;
        rain.randomSeed = (uint)(order + 100);
    }

    private static Transform Group(Transform parent, string name)
    {
        Transform result = new GameObject(name).transform;
        result.SetParent(parent, false);
        return result;
    }

    private static Sprite Stone(string name) => stone.Single(item => item.name == name);

    private static SpriteRenderer Tiled(Transform parent, string name, string sprite, Vector2 position,
        Vector2 size, Color color, int order)
    {
        SpriteRenderer result = SpritePart(parent, name, Stone(sprite), position, size, color, order);
        result.transform.localScale = Vector3.one;
        result.drawMode = SpriteDrawMode.Tiled;
        result.size = size;
        return result;
    }

    private static SpriteRenderer SpritePart(Transform parent, string name, Sprite sprite, Vector2 position,
        Vector2 size, Color color, int order)
    {
        Transform part = Group(parent, name);
        part.localPosition = position;
        part.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
        SpriteRenderer renderer = part.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }
}
