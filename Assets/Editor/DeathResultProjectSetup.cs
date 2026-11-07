using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DeathResultProjectSetup
{
    public const string TitlePath = ProjectScenes.Title;

    [MenuItem("Tools/Death Result/Create minimal title scene")]
    public static void BuildTitle()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("[Title]", typeof(TitleScreen));
            EditorSceneManager.SaveScene(scene, TitlePath);
            var scenes = EditorBuildSettings.scenes.Where(item => item.path != TitlePath).ToList();
            // Start builds at the title while keeping all existing gameplay entries.
            scenes.Insert(0, new EditorBuildSettingsScene(TitlePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (previous.Length > 0 && previous.All(item => !string.IsNullOrEmpty(item.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
