using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Darkens the stage and lights it from its sources: wall torches in the normal stages and
/// god rays through a broken ceiling in the boss stage. Built at scene load so no scene edits are needed.
/// </summary>
public static class StageAtmosphere
{
    private const string TorchName = "Wall Torch";
    private const string RootName = "Stage atmosphere";
    private static readonly Color NormalAmbient = new Color(.5f, .55f, .7f);
    private const float NormalAmbientIntensity = .5f;
    private static readonly Color BossAmbient = new Color(.52f, .56f, .62f);
    private const float BossAmbientIntensity = .4f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply(scene);

    private static void Apply(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == RootName) return;

        bool boss = scene.name == LobbyFlowRuntime.BossTestScene;
        var torches = new System.Collections.Generic.List<Transform>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == TorchName) torches.Add(child);
        if (!boss && torches.Count == 0) return;

        var atmosphere = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(atmosphere, scene);
        var ambient = atmosphere.AddComponent<Light2D>();
        ambient.lightType = Light2D.LightType.Global;
        ambient.color = boss ? BossAmbient : NormalAmbient;
        ambient.intensity = boss ? BossAmbientIntensity : NormalAmbientIntensity;

        foreach (Transform torch in torches)
            if (torch.GetComponent<TorchLight>() == null) torch.gameObject.AddComponent<TorchLight>();
        if (boss) atmosphere.AddComponent<BossLightShafts>();
    }
}
