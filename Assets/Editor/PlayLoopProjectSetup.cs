using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayLoopProjectSetup
{
    private static readonly (string path, BranchRole role)[] RunScenes =
    {
        (ProjectScenes.ShopTest, BranchRole.Shop),
        (ProjectScenes.DogCombatTest, BranchRole.Combat),
        (ProjectScenes.BelieverCombatTest, BranchRole.Combat),
        (ProjectScenes.ExecutiveCombatTest, BranchRole.Combat),
        (ProjectScenes.TemporaryStage, BranchRole.Event),
        (ProjectScenes.BossTest, BranchRole.Boss)
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
        ProjectSceneSynchronization.SynchronizeScene(scene);
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
        controllerSettings.FindProperty("stageMinX").floatValue = role == BranchRole.Shop ? -8.5f : role == BranchRole.Boss ? -17f : -10f;
        controllerSettings.FindProperty("stageMaxX").floatValue = role == BranchRole.Shop ? 8.5f : role == BranchRole.Boss ? 17f : 10f;
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
        EditorBuildSettings.scenes = ProjectScenes.GameplayPaths
            .Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
    }
}
