using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayLoopProjectSetup
{
    private const string SceneFolder = "Assets/Scenes/";
    private static readonly (string path, BranchRole role)[] RunScenes =
    {
        (SceneFolder + "ShopTest.unity", BranchRole.Shop),
        (SceneFolder + "DogCombatTest.unity", BranchRole.Combat),
        (SceneFolder + "BelieverCombatTest.unity", BranchRole.Combat),
        (SceneFolder + "ExecutiveCombatTest.unity", BranchRole.Combat),
        (SceneFolder + "TemporaryStage.unity", BranchRole.Event),
        (SceneFolder + "BossTest.unity", BranchRole.Boss)
    };

    [MenuItem("Tools/Lobby/Connect run content scenes")]
    public static void ConnectScenes()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (var entry in RunScenes)
                ConfigureScene(entry.path, entry.role);

            AddRunScenesToBuildSettings();
            AssetDatabase.SaveAssets();
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }

        Debug.Log("Lobby run roles now load their combat, shop, event, and boss scenes. Standalone test scenes remain usable.");
    }

    private static void ConfigureScene(string path, BranchRole role)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        PlayerHealth player = Object.FindFirstObjectByType<PlayerHealth>();
        if (player == null)
            throw new System.InvalidOperationException(path + " requires a PlayerHealth player.");

        if (player.GetComponent<PlayerDeathCoordinator>() == null)
            player.gameObject.AddComponent<PlayerDeathCoordinator>();

        LobbySceneController controller = Object.FindFirstObjectByType<LobbySceneController>();
        if (controller == null)
            controller = new GameObject("[Play Loop Stage]").AddComponent<LobbySceneController>();

        var controllerSettings = new SerializedObject(controller);
        controllerSettings.FindProperty("isLobby").boolValue = false;
        controllerSettings.FindProperty("runStageOnly").boolValue = true;
        controllerSettings.FindProperty("runStageRole").enumValueIndex = (int)role;
        controllerSettings.FindProperty("stageMinX").floatValue = role == BranchRole.Shop ? -8.5f : -10f;
        controllerSettings.FindProperty("stageMaxX").floatValue = role == BranchRole.Shop ? 8.5f : 10f;
        controllerSettings.FindProperty("player").objectReferenceValue = player;
        controllerSettings.FindProperty("squareSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Boss/UnitSquare.png");
        controllerSettings.FindProperty("worldMaterial").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Boss/BossUnlit.mat");
        controllerSettings.FindProperty("showTestTools").boolValue = true;
        controllerSettings.ApplyModifiedPropertiesWithoutUndo();

        RunStageCompletionWatcher completion = Object.FindFirstObjectByType<RunStageCompletionWatcher>();
        if (role == BranchRole.Combat || role == BranchRole.Shop || role == BranchRole.Boss)
        {
            if (completion == null)
                completion = new GameObject("[Run Stage Completion]").AddComponent<RunStageCompletionWatcher>();
            var completionSettings = new SerializedObject(completion);
            completionSettings.FindProperty("role").enumValueIndex = (int)role;
            completionSettings.ApplyModifiedPropertiesWithoutUndo();
        }
        else if (completion != null)
        {
            Object.DestroyImmediate(completion);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void AddRunScenesToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        foreach (var entry in RunScenes)
        {
            if (scenes.Any(scene => scene.path == entry.path)) continue;
            scenes.Add(new EditorBuildSettingsScene(entry.path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
