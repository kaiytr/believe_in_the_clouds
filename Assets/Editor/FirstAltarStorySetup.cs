using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Rebuilds only this feature's owned assets/scene. Never rewrites the shared player or 1.0 scenes.
public static class FirstAltarStorySetup
{
    public const string DefinitionPath = "Assets/Data/Story/FirstAltarStoryDefinition.asset";
    private const string Terrain = "Assets/Art/LevelTiles/PlatformerTerrain.png";
    private const float FloorY = -2.4f;

    [MenuItem("Tools/Story/Rebuild first altar story scene")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string directory in new[] { "Assets/Data/Story", "Assets/Scenes/Story", "Assets/Prefabs/Story", "Assets/Art/Story", "Assets/Sounds/Story" })
                Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
            FirstAltarStoryDefinition definition = LoadOrCreate<FirstAltarStoryDefinition>(DefinitionPath);
            CutsceneDefinition death = LoadOrCreate<CutsceneDefinition>("Assets/Data/Story/FirstAltarDeath.asset");
            death.Configure(FirstAltarStoryDefinition.DeathStageId,
                new[] { CutsceneStep.External(FirstAltarStoryDefinition.DeathStageId) });
            definition.deathSequence = death;
            definition.rainLoop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Boss/보스전-비.mp3");
            definition.impactClip = MakeImpact();
            EditorUtility.SetDirty(death); EditorUtility.SetDirty(definition);
            Material unlit = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Boss/BossUnlit.mat");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject hero = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ProjectSceneSynchronization.PlayerPath));
            hero.transform.position = new Vector3(-10, -1.15f, 0);
            PlayerHealth player = hero.GetComponent<PlayerHealth>();
            PrefabUtility.RecordPrefabInstancePropertyModifications(hero.transform);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ProjectSceneSynchronization.HudPath));

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera"; cameraObject.transform.position = new Vector3(-3, 0, -10);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 5.6f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .035f, .05f);
            LobbyCameraFollow follow = cameraObject.AddComponent<LobbyCameraFollow>();
            follow.Target = hero.transform; follow.MinX = -14; follow.MaxX = 14;
            Light2D light = new GameObject("Room ambient light").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global; light.intensity = .55f;

            var room = new GameObject("Altar room architecture");
            SpriteRenderer wall = SpriteObject("Stone wall", room.transform, TerrainSprite("InteriorWall"), unlit,
                new Vector3(0, 1.4f), new Vector3(1, 1, 1), new Color(.38f, .39f, .46f), -30);
            wall.drawMode = SpriteDrawMode.Tiled; wall.size = new Vector2(28, 8);
            SpriteRenderer floor = SpriteObject("Stone floor", room.transform, TerrainSprite("Stone0_0"), unlit,
                new Vector3(0, FloorY - 1.5f), Vector3.one, new Color(.50f, .46f, .49f), 1);
            floor.drawMode = SpriteDrawMode.Tiled; floor.size = new Vector2(28, 3);
            Collider("Ground", room.transform, new Vector2(0, FloorY - .5f), new Vector2(28, 1));
            Collider("Left wall", room.transform, new Vector2(-14, 3), new Vector2(1, 18));
            Collider("Right wall", room.transform, new Vector2(14, 3), new Vector2(1, 18));
            foreach (float x in new[] { -12f, -6f, 0f, 6f, 12f })
                SpriteObject("Stone pillar", room.transform, TerrainSprite("Pillar"), unlit,
                    new Vector3(x, .8f), new Vector3(1.4f, 2.3f, 1), new Color(.6f, .54f, .57f), -20);
            foreach (float x in new[] { -9f, -3f, 3f, 9f })
                SpriteObject("Dark window", room.transform, TerrainSprite("Window"), unlit,
                    new Vector3(x, 1.9f), Vector3.one, new Color(.48f, .50f, .64f), -21);
            Sprite[] torchFrames = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/LevelTiles/InteriorTorch.png").OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
            foreach (float x in new[] { -7f, 7f })
            {
                SpriteRenderer torch = SpriteObject("Wall torch", room.transform, torchFrames[0], unlit,
                    new Vector3(x, .6f), Vector3.one, Color.white, -15);
                torch.gameObject.AddComponent<SpriteFrameAnimator>().Configure(torchFrames, 8);
            }

            Sprite[] idle = AssetDatabase.LoadAllAssetsAtPath("Assets/Design/Enemy/Believer/BelieverIdle.png")
                .OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
            Sprite dead = AssetDatabase.LoadAllAssetsAtPath("Assets/Design/Enemy/Believer/BelieverDeath.png").OfType<Sprite>().First();
            var npc = new GameObject("StoryBeliever");
            // Tutorial/Combat: parent scale (1,2) * visual scale (13/6,13/12) = uniform 13/6.
            const float believerScale = 13f / 6f;
            SpriteRenderer body = SpriteObject("Believer body", npc.transform, idle[0], unlit, Vector3.zero,
                new Vector3(believerScale, believerScale, 1), Color.white, 4);
            body.flipX = true; // The source believer art faces left; the altar is on its right.
            body.transform.localPosition = new Vector3(0, -idle[0].bounds.min.y * believerScale, 0);
            var head = new GameObject("Prayer head anchor"); head.transform.SetParent(npc.transform, false);
            head.transform.localPosition = new Vector3(0, idle[0].bounds.size.y * believerScale + .2f, 0);
            CutsceneCharacter speaker = npc.AddComponent<CutsceneCharacter>(); speaker.Configure("신도", head.transform);
            StoryBelieverView believer = npc.AddComponent<StoryBelieverView>(); believer.Configure(body, idle, dead);
            GameObject npcPrefab = PrefabUtility.SaveAsPrefabAsset(npc, "Assets/Prefabs/Story/StoryBeliever.prefab");
            Object.DestroyImmediate(npc);
            npc = (GameObject)PrefabUtility.InstantiatePrefab(npcPrefab); npc.transform.position = new Vector3(1, FloorY, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(npc.transform);
            speaker = npc.GetComponent<CutsceneCharacter>(); believer = npc.GetComponent<StoryBelieverView>();

            GameObject altar = new GameObject("StoryAltar");
            Sprite altarArt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Design/World/FoundationAltar.png");
            SpriteRenderer altarBody = SpriteObject("Altar artwork", altar.transform, altarArt, unlit, Vector3.zero,
                Vector3.one, Color.white, 2);
            altarBody.transform.localPosition = new Vector3(0, -altarArt.bounds.min.y, 0);
            BoxCollider2D altarCollider = altar.AddComponent<BoxCollider2D>(); altarCollider.isTrigger = true;
            altarCollider.size = new Vector2(2.3f, 2.8f); altarCollider.offset = new Vector2(0, 1.4f);
            altar.AddComponent<StoryAltarInteractable>();
            GameObject altarPrefab = PrefabUtility.SaveAsPrefabAsset(altar, "Assets/Prefabs/Story/StoryAltar.prefab");
            Object.DestroyImmediate(altar);
            altar = (GameObject)PrefabUtility.InstantiatePrefab(altarPrefab); altar.transform.position = new Vector3(4, FloorY, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(altar.transform);

            GameObject exit = new GameObject("StoryRoomExit");
            Sprite door = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/LobbyBranch.prefab").GetComponentInChildren<SpriteRenderer>().sprite;
            SpriteRenderer exitBody = SpriteObject("Return door", exit.transform, door, unlit, Vector3.zero, Vector3.one, Color.white, 0);
            exitBody.transform.localPosition = new Vector3(0, -door.bounds.min.y, 0);
            BoxCollider2D exitCollider = exit.AddComponent<BoxCollider2D>(); exitCollider.isTrigger = true;
            exitCollider.size = new Vector2(1.8f, 3.5f); exitCollider.offset = new Vector2(0, 1.7f);
            exit.AddComponent<StoryRoomExit>();
            GameObject exitPrefab = PrefabUtility.SaveAsPrefabAsset(exit, "Assets/Prefabs/Story/StoryRoomExit.prefab");
            Object.DestroyImmediate(exit);
            exit = (GameObject)PrefabUtility.InstantiatePrefab(exitPrefab); exit.transform.position = new Vector3(11.5f, FloorY, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(exit.transform);

            var blood = new GameObject("Altar blood floor");
            Sprite disc = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Boss/UnitDisc.png");
            var random = new System.Random(1711);
            for (int i = 0; i < 32; i++)
            {
                float x = 1.4f + (float)random.NextDouble() * 4.6f;
                float width = .12f + (float)random.NextDouble() * 1.5f;
                SpriteRenderer stain = SpriteObject("Blood stain", blood.transform, disc, unlit,
                    new Vector3(x, FloorY + .06f + (float)random.NextDouble() * .16f),
                    new Vector3(width, .035f + (float)random.NextDouble() * .15f, 1),
                    new Color(.30f + (float)random.NextDouble() * .15f, .012f, .035f), 3);
                stain.transform.localRotation = Quaternion.Euler(0, 0, -10 + (float)random.NextDouble() * 20);
            }
            blood.SetActive(false);

            var controllerObject = new GameObject("Story room run binding");
            LobbySceneController controller = controllerObject.AddComponent<LobbySceneController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("isLobby").boolValue = false; serialized.FindProperty("isStoryRoom").boolValue = true;
            serialized.FindProperty("player").objectReferenceValue = player;
            serialized.FindProperty("stageMinX").floatValue = -13.4f; serialized.FindProperty("stageMaxX").floatValue = 13.4f;
            serialized.FindProperty("stageMaxY").floatValue = 7f; serialized.ApplyModifiedPropertiesWithoutUndo();
            CutsceneRunner runner = controllerObject.AddComponent<CutsceneRunner>();
            controllerObject.AddComponent<StoryRoomFlow>().Configure(definition, player, speaker, believer, blood, runner);

            EditorSceneManager.SaveScene(scene, ProjectScenes.FirstAltarStory);
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(entry => entry.path != ProjectScenes.FirstAltarStory)
                .Concat(new[] { new EditorBuildSettingsScene(ProjectScenes.FirstAltarStory, true) }).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("First altar story scene and owned assets created: " + ProjectScenes.FirstAltarStory);
        }
        finally
        {
            if (!Application.isBatchMode && previous.Any(entry => entry.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
        return asset;
    }

    private static Sprite TerrainSprite(string name) => AssetDatabase.LoadAllAssetsAtPath(Terrain).OfType<Sprite>().First(sprite => sprite.name == name);
    private static SpriteRenderer SpriteObject(string label, Transform parent, Sprite sprite, Material material,
        Vector3 position, Vector3 scale, Color color, int order)
    {
        var child = new GameObject(label); child.transform.SetParent(parent, false);
        child.transform.localPosition = position; child.transform.localScale = scale;
        SpriteRenderer view = child.AddComponent<SpriteRenderer>();
        view.sprite = sprite; view.sharedMaterial = material; view.color = color; view.sortingOrder = order;
        return view;
    }
    private static void Collider(string label, Transform parent, Vector2 position, Vector2 size)
    {
        var child = new GameObject(label); child.transform.SetParent(parent, false); child.transform.localPosition = position;
        child.layer = Mathf.Max(0, LayerMask.NameToLayer("Ground"));
        child.AddComponent<BoxCollider2D>().size = size;
    }
    private static AudioClip MakeImpact()
    {
        const string path = "Assets/Sounds/Story/AltarImpact.wav";
        if (!File.Exists(path))
        {
            const int rate = 22050, count = 11025;
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
                writer.Write((short)2); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                var random = new System.Random(411);
                float noise = 0;
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)rate;
                    noise = noise * .88f + ((float)random.NextDouble() * 2 - 1) * .12f;
                    float envelope = Mathf.Min(1, t * 500) * Mathf.Exp(-12 * t);
                    float sample = (Mathf.Sin(2 * Mathf.PI * (85 * t - 45 * t * t)) * .78f + noise * .5f) * envelope;
                    writer.Write((short)(Mathf.Clamp(sample, -1, 1) * 30000));
                }
            }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
