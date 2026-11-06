using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CutsceneOwnedAssetBuilder
{
    private const string DataRoot = "Assets/Data/Cutscenes";
    private const string PrefabRoot = "Assets/Prefabs/Cutscenes";
    private const string StonePrefabPath = "Assets/Prefabs/Enemy/Believer/StoneProjectile.prefab";

    [MenuItem("Tools/Cutscenes/Create Owned Samples")]
    public static void CreateOwnedSamples()
    {
        if (!Application.isBatchMode)
        {
            if (!EditorUtility.DisplayDialog("Create Cutscene Samples",
                    "Create missing cutscene assets? Current scenes will be saved first.",
                    "Continue", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
        }

        EnsureFolder("Assets/Data", "Cutscenes");
        EnsureFolder("Assets/Prefabs", "Cutscenes");
        EnsureFolder("Assets/Art", "Cutscenes");
        EnsureFolder("Assets/Editor", "Cutscenes");

        GameObject stone = AssetDatabase.LoadAssetAtPath<GameObject>(StonePrefabPath);
        if (stone == null)
        {
            Debug.LogError("Cannot create tutorial data: believer stone visual prefab is missing at " + StonePrefabPath);
            return;
        }

        CutsceneDefinition boss = CreateDefinition("BossIntroduction", "boss.introduction", new[]
        {
            CutsceneStep.Say("boss", "오랜만이야."),
            CutsceneStep.Say("boss", "세상 구경은 즐거웠어?"),
            CutsceneStep.Say("player", "...."),
            CutsceneStep.Choose("player", "무슨 소리인지 모르겠는데", "네가 저것들의 수장이야?"),
            CutsceneStep.Say("boss", "...아하."),
            CutsceneStep.Say("boss", "결국 너도, 세속에 물들어버린 거구나."),
            CutsceneStep.Say("boss", "......"),
            CutsceneStep.Say("boss", "자, 기도하자."),
            CutsceneStep.Say("boss", "구름은 걷히지 않을 테니까.")
        });

        CutsceneDefinition intro = CreateDefinition("TutorialIntro", "tutorial.intro", new[]
        {
            CutsceneStep.Move("player", "introPlayer", 0f),
            CutsceneStep.Say("player", "윽... 여긴..."),
            CutsceneStep.Say("player", "분명히... 어떤 무리에게 잡혀왔던 것 까지는 기억나는데."),
            CutsceneStep.Say("player", "…일단 이곳을 빠져나야겠어.")
        });

        CutsceneDefinition encounter = CreateDefinition("TutorialBelieverEncounter",
            "tutorial.believer.encounter", new[]
        {
            CutsceneStep.Move("player", "playerTalk", 0f),
            CutsceneStep.Move("believer", "believerStart", 0f),
            CutsceneStep.Say("believer", "...!!"),
            CutsceneStep.Say("believer", "그분께서 깨어나셨다!"),
            CutsceneStep.Move("believer", "believerNear", 2.4f),
            CutsceneStep.Say("believer", "..."),
            CutsceneStep.Say("believer", "뭐야, 가짜잖아?"),
            CutsceneStep.BackAway("believer", "believerThrow", "player", 2.4f),
            CutsceneStep.ThrowHarmlessly("believer", "playerTalk", stone, 0.62f, 0.55f),
            CutsceneStep.Say("player", "…!?"),
            CutsceneStep.External("tutorial.believer.first-stone.tip")
        });

        CutsceneDefinition afterDeath = CreateDefinition("TutorialAfterBelieverDeath",
            "tutorial.believer.after-death", new[]
        {
            CutsceneStep.Say("player", "…"),
            CutsceneStep.Say("player", "죄송하게 됐습니다.")
        });

        CreateRunnerPrefab(boss);
        CreateActorPrefab();
        CreateEntryTriggerPrefab();
        CreateTutorialRigPrefab(intro, encounter, afterDeath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Created missing cutscene-owned definitions and prefabs.");
    }

    private static CutsceneDefinition CreateDefinition(string fileName, string id, CutsceneStep[] steps)
    {
        string path = DataRoot + "/" + fileName + ".asset";
        CutsceneDefinition asset = AssetDatabase.LoadAssetAtPath<CutsceneDefinition>(path);
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<CutsceneDefinition>();
        asset.Configure(id, steps);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void CreateRunnerPrefab(CutsceneDefinition defaultDefinition)
    {
        string path = PrefabRoot + "/CutsceneRunner.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            return;

        GameObject root = new GameObject("CutsceneRunner");
        CutsceneBubbleView view = root.AddComponent<CutsceneBubbleView>();
        CutsceneRunner runner = root.AddComponent<CutsceneRunner>();
        runner.SetDefaultDefinition(defaultDefinition);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void CreateActorPrefab()
    {
        string path = PrefabRoot + "/CutsceneActor.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            return;

        GameObject root = new GameObject("CutsceneActor");
        root.AddComponent<CutsceneCharacter>().Configure("화자", null);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void CreateEntryTriggerPrefab()
    {
        string path = PrefabRoot + "/CutsceneEntryTrigger.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            return;

        GameObject root = new GameObject("CutsceneEntryTrigger");
        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(2.6f, 3.5f);
        root.AddComponent<CutsceneTrigger>();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void CreateTutorialRigPrefab(CutsceneDefinition intro,
        CutsceneDefinition encounter, CutsceneDefinition afterDeath)
    {
        string path = PrefabRoot + "/TutorialCutsceneRig.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            return;

        GameObject root = new GameObject("TutorialCutsceneRig");
        root.AddComponent<CutsceneBubbleView>();
        CutsceneRunner runner = root.AddComponent<CutsceneRunner>();
        CutsceneDeathWatcher watcher = root.AddComponent<CutsceneDeathWatcher>();
        CutsceneTutorialFlow flow = root.AddComponent<CutsceneTutorialFlow>();
        flow.Configure(runner, intro, encounter, afterDeath, null, null, watcher);
        runner.SetDefaultDefinition(intro);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
