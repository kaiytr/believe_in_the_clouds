using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 적 공격 사운드(공격준비시전 / 공격시전 / 공격피격)를 씬에 일괄 배정한다.
// Assets/Sounds/Enemy/<종류>/<종류>-<단계>.mp3 파일명 규칙을 그대로 따른다.
public static class EnemyAttackSoundSetup
{
    private const string SoundRoot = "Assets/Sounds/Enemy";

    private static readonly (string enemy, string attackClass)[] Enemies =
    {
        ("개", nameof(DogChargeAttack)),
        ("신자", nameof(ThrowStoneAttack)),
        ("간부", nameof(MagicCircleAttack))
    };

    [MenuItem("Tools/Audio/Apply enemy attack sounds")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var clips = LoadClips();
        foreach (var (enemy, attackClass) in Enemies)
        {
            Debug.Log($"{enemy}: 준비={Name(clips, enemy, "공격준비시전")}, " +
                      $"시전={Name(clips, enemy, "공격시전")}, 피격={Name(clips, enemy, "공격피격")}");
        }

        int patched = 0;
        foreach (string path in ProjectScenes.AllPaths)
        {
            if (!File.Exists(path)) continue;
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool dirty = false;

            foreach (var (enemy, attackClass) in Enemies)
            {
                var windup = Find(clips, enemy, "공격준비시전");
                var cast = Find(clips, enemy, "공격시전");
                var hit = Find(clips, enemy, "공격피격");
                if (windup == null && cast == null && hit == null) continue;

                foreach (MonoBehaviour behaviour in scene.GetRootGameObjects()
                             .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)))
                {
                    if (behaviour == null || behaviour.GetType().Name != attackClass) continue;

                    var serialized = new SerializedObject(behaviour);
                    if (Assign(serialized, "attackWindupSound", windup) |
                        Assign(serialized, "attackCastSound", cast) |
                        Assign(serialized, "attackHitSound", hit))
                    {
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        dirty = true;
                        patched++;
                    }
                }
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Enemy attack sounds applied to {patched} attack component(s).");
    }

    private static Dictionary<string, AudioClip> LoadClips()
    {
        var clips = new Dictionary<string, AudioClip>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SoundRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) clips[Path.GetFileNameWithoutExtension(path)] = clip;
        }
        return clips;
    }

    private static AudioClip Find(Dictionary<string, AudioClip> clips, string enemy, string stage) =>
        clips.TryGetValue($"{enemy}-{stage}", out AudioClip clip) ? clip : null;

    private static string Name(Dictionary<string, AudioClip> clips, string enemy, string stage) =>
        Find(clips, enemy, stage) != null ? $"{enemy}-{stage}" : "(없음)";

    // 이미 같은 클립이면 건드리지 않는다. null 클립은 기존 값을 지우지 않는다.
    private static bool Assign(SerializedObject serialized, string propertyName, AudioClip clip)
    {
        if (clip == null) return false;
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == clip) return false;
        property.objectReferenceValue = clip;
        return true;
    }
}
