using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// The shared prefabs own player combat and HUD settings across gameplay and development scenes.
public static class ProjectSceneSynchronization
{
    public const string PlayerPath = "Assets/Prefabs/Player/Player.prefab";
    public const string HudPath = "Assets/Prefabs/UI/PlayerHud.prefab";

    [Serializable]
    public sealed class SceneAudit
    {
        public string path, playerPrefab;
        public int players, huds, attackOverrides, missingScripts;
        public bool sharedHud, horizontalCameras, playerDoctrineIdentity;
        public bool valid;
    }

    [Serializable]
    private sealed class Report
    {
        public SceneAudit[] before, after;
        public string[] changedScenes;
    }

    [MenuItem("Tools/Project/Sync shared player and UI in all scenes")]
    public static void SynchronizeAll()
    {
        Run(true);
    }

    [MenuItem("Tools/Project/Audit shared scene setup")]
    public static void AuditAll()
    {
        Run(false);
    }

    private static void Run(bool synchronize)
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        var before = new List<SceneAudit>();
        var after = new List<SceneAudit>();
        var changed = new List<string>();
        try
        {
            foreach (string path in ProjectScenes.AllPaths)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                before.Add(Audit(scene));
                if (synchronize && SynchronizeScene(scene))
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    changed.Add(path);
                }
                after.Add(Audit(scene));
            }
            string output = CommandArgument("-sceneSyncReport") ?? Path.GetFullPath("Logs/SceneSynchronization/audit.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(new Report {
                before = before.ToArray(), after = after.ToArray(), changedScenes = changed.ToArray()
            }, true));
            if (synchronize && after.Any(item => !item.valid))
                throw new InvalidOperationException("Shared scene synchronization failed. See " + output);
            Debug.Log($"Shared scene audit: {after.Count(item => item.valid)}/{after.Count} valid, {changed.Count} updated. {output}");
        }
        finally
        {
            if (previous.Any(item => item.isLoaded && item.isActive) && previous.Where(item => item.isLoaded).All(item => !string.IsNullOrEmpty(item.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    public static bool SynchronizeScene(Scene scene)
    {
        bool changed = false;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
        if (prefab == null || hudPrefab == null) throw new InvalidOperationException("Shared Player and PlayerHud prefabs are required.");
        foreach (PlayerHealth health in Components<PlayerHealth>(scene).Where(item => item.CompareTag("Player")).ToArray())
        {
            GameObject player = health.gameObject;
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player) != PlayerPath)
            {
                Physics2D.SyncTransforms();
                Collider2D oldBody = player.GetComponent<Collider2D>();
                float feet = oldBody != null ? oldBody.bounds.min.y : player.transform.position.y;
                GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                replacement.name = player.name;
                replacement.transform.SetPositionAndRotation(player.transform.position, player.transform.rotation);
                replacement.transform.SetSiblingIndex(player.transform.GetSiblingIndex());
                if (player.GetComponent<PlayerDeathCoordinator>() != null && replacement.GetComponent<PlayerDeathCoordinator>() == null)
                    replacement.AddComponent<PlayerDeathCoordinator>();
                Physics2D.SyncTransforms();
                if (oldBody != null)
                    replacement.transform.position += Vector3.up * (feet - replacement.GetComponent<Collider2D>().bounds.min.y);
                RemapReferences(scene, player, replacement);
                Object.DestroyImmediate(player);
                player = replacement;
                changed = true;
            }
            PlayerAttack attack = player.GetComponent<PlayerAttack>();
            if (AttackOverrides(player) > 0)
            {
                PrefabUtility.RevertObjectOverride(attack, InteractionMode.AutomatedAction);
                changed = true;
            }
        }

        DoctrineHud[] huds = Components<DoctrineHud>(scene).ToArray();
        DoctrineHud keeper = huds.FirstOrDefault(item => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item.gameObject) == HudPath);
        if (keeper == null)
        {
            keeper = ((GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, scene)).GetComponent<DoctrineHud>();
            changed = true;
        }
        foreach (DoctrineHud old in huds.Where(item => item != keeper))
        {
            RemapReferences(scene, old.gameObject, keeper.gameObject);
            Object.DestroyImmediate(old.gameObject);
            changed = true;
        }
        MonoBehaviour[] cameras = Components<MonoBehaviour>(scene).Where(item => item != null && item.GetType().Name == "CinemachineCamera").ToArray();
        foreach (MonoBehaviour camera in cameras)
        {
            if (camera.GetComponent<CinemachineHorizontalFollow>() != null) continue;
            camera.gameObject.AddComponent<CinemachineHorizontalFollow>();
            changed = true;
        }
        if (cameras.Length > 0)
            foreach (BossArenaCamera arena in Components<BossArenaCamera>(scene))
            {
                var data = new SerializedObject(arena);
                SerializedProperty fixedY = data.FindProperty("explorationFixedY");
                if (fixedY.boolValue) continue;
                fixedY.boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
        return changed;
    }

    public static SceneAudit Audit(Scene scene)
    {
        PlayerHealth[] players = Components<PlayerHealth>(scene).Where(item => item.CompareTag("Player")).ToArray();
        DoctrineHud[] huds = Components<DoctrineHud>(scene).ToArray();
        var result = new SceneAudit {
            path = scene.path, players = players.Length, huds = huds.Length,
            playerPrefab = players.Length == 1 ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(players[0].gameObject) : "",
            sharedHud = huds.Length == 1 && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(huds[0].gameObject) == HudPath,
            playerDoctrineIdentity = players.Length == 1 && players[0].GetComponent<DoctrineSubject>() != null &&
                players[0].GetComponent<DoctrineSubject>().Identity == SubjectType.Player,
            attackOverrides = players.Sum(item => AttackOverrides(item.gameObject)),
            missingScripts = Components<MonoBehaviour>(scene).Count(item => item == null),
            horizontalCameras = Components<MonoBehaviour>(scene).Where(item => item != null && item.GetType().Name == "CinemachineCamera")
                .All(item => item.GetComponent<CinemachineHorizontalFollow>() != null)
        };
        result.valid = result.players == 1 && result.playerPrefab == PlayerPath &&
            players[0].GetComponent<PlayerAttack>() != null && result.sharedHud && result.attackOverrides == 0 &&
            result.missingScripts == 0 && result.horizontalCameras && result.playerDoctrineIdentity;
        return result;
    }

    private static int AttackOverrides(GameObject player)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(player)) return 0;
        return (PrefabUtility.GetPropertyModifications(player) ?? Array.Empty<PropertyModification>())
            .Count(item => item.target is PlayerAttack);
    }

    private static IEnumerable<T> Components<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

    private static void RemapReferences(Scene scene, GameObject oldRoot, GameObject replacement)
    {
        var map = new Dictionary<Object, Object>();
        foreach (Transform old in oldRoot.GetComponentsInChildren<Transform>(true))
        {
            string path = AnimationUtility.CalculateTransformPath(old, oldRoot.transform);
            Transform target = path.Length == 0 ? replacement.transform : replacement.transform.Find(path);
            if (target == null) continue;
            map[old.gameObject] = target.gameObject;
            foreach (Component component in old.GetComponents<Component>())
            {
                if (component == null) continue;
                Component counterpart = target.GetComponent(component.GetType());
                if (counterpart == null && component is Collider2D) counterpart = target.GetComponent<Collider2D>();
                if (counterpart != null) map[component] = counterpart;
            }
        }
        foreach (Component component in Components<Component>(scene))
        {
            if (component == null || component.transform.IsChildOf(oldRoot.transform)) continue;
            var data = new SerializedObject(component);
            SerializedProperty property = data.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null &&
                    map.TryGetValue(property.objectReferenceValue, out Object target)) property.objectReferenceValue = target;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static string CommandArgument(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, name);
        return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
    }
}
