using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayLoopProjectSetup
{
    private static readonly (string path, BranchRole role)[] RunScenes =
    {
        // The designed combat levels are authored by hand and are not touched here.
        (ProjectScenes.Shop, BranchRole.Shop),
        (ProjectScenes.Boss, BranchRole.Boss)
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

        Debug.Log("Shop and boss scenes are connected to the lobby run and the build settings now list every scene of the play loop.");
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
        controllerSettings.FindProperty("runStageRole").intValue = (int)role;
        controllerSettings.FindProperty("stageMinX").floatValue = role == BranchRole.Shop ? -8.5f : role == BranchRole.Boss ? -17f : -10f;
        controllerSettings.FindProperty("stageMaxX").floatValue = role == BranchRole.Shop ? 8.5f : role == BranchRole.Boss ? 17f : 10f;
        controllerSettings.FindProperty("player").objectReferenceValue = player;
        controllerSettings.FindProperty("squareSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Boss/UnitSquare.png");
        controllerSettings.FindProperty("returnDoorSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Design/Lobby/BranchClosed.png");
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
            completionSettings.FindProperty("role").intValue = (int)role;
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
        EditorBuildSettings.scenes = new[] { ProjectScenes.Title }.Concat(ProjectScenes.GameplayPaths)
            .Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
    }
}
