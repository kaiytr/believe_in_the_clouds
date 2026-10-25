using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BeliefSystemProjectSetup
{
    private const string DataFolder = "Assets/Data/Belief";
    private const string RearProfilePath = DataFolder + "/RearPositionBeliefProfile.asset";
    private const string DodgeProfilePath = DataFolder + "/DodgeAttackBeliefProfile.asset";
    private const string CatalogPath = DataFolder + "/BeliefProfileCatalog.asset";
    private const string EnemyScenePath = "Assets/Scenes/EnemyTest.unity";
    private const string BeliefScenePath = "Assets/Scenes/BeliefTest.unity";

    public static void BuildBeliefSystemAssetsAndScenes()
    {
        CreateBeliefAssets();
        foreach (string path in new[] { EnemyScenePath, BeliefScenePath })
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            ConfigureScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Belief profiles and prototype test scenes are configured.");
    }

    private static void CreateBeliefAssets()
    {
        EnsureFolder("Assets", "Data");
        EnsureFolder("Assets/Data", "Belief");

        BeliefProfile rear = LoadOrCreateProfile(RearProfilePath);
        SetProfile(rear, BeliefType.RearPosition, "플레이어는 내 뒤로 이동할 수 없다.", 25f, 0f);

        BeliefProfile dodge = LoadOrCreateProfile(DodgeProfilePath);
        SetProfile(dodge, BeliefType.DodgeAttack, "플레이어는 내 공격을 피할 수 없다.", 0f, 25f);

        var allProfiles = new List<BeliefProfile> { rear, dodge };
        var definitions = new (BeliefType type, string id, string phrase, float reduction)[]
        {
            (BeliefType.DogCounterattack, "DOG-01", "도망치는 사냥감은 약하다.", 100f),
            (BeliefType.DogUnerringAttack, "DOG-02", "내 공격은 반드시 적중한다.", 50f),
            (BeliefType.DogRearAttack, "DOG-03", "내가 보지 못하는 것은 나를 공격할 수 없다.", 40f),
            (BeliefType.BelieverConsecutiveHits, "BEL-01", "너는 연속으로 공격할 수 없다.", 25f),
            (BeliefType.BelieverUnerringStone, "BEL-02", "너는 무조건 내 돌에 맞는다.", 50f),
            (BeliefType.BelieverSafeDistance, "BEL-03", "나는 언제나 안전한 거리를 유지한다.", 50f),
            (BeliefType.ExecutiveEscapeCircle, "EXE-01", "내 마법진에서는 벗어날 수 없다.", 100f),
            (BeliefType.ExecutiveRearPosition, "EXE-02", "너는 내 뒤로 갈 수 없다.", 25f),
            (BeliefType.ExecutivePrayer, "EXE-03", "기도하는 동안 누구도 나를 방해할 수 없다.", 50f)
        };
        foreach (var definition in definitions)
        {
            BeliefProfile profile = LoadOrCreateProfile(DataFolder + "/" + definition.id + ".asset");
            SetProfile(profile, definition.type, definition.phrase, 0f, 0f, definition.reduction);
            allProfiles.Add(profile);
        }

        BeliefProfileCatalog catalog = AssetDatabase.LoadAssetAtPath<BeliefProfileCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<BeliefProfileCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        SerializedObject catalogData = new SerializedObject(catalog);
        catalogData.FindProperty("rearPositionProfile").objectReferenceValue = rear;
        catalogData.FindProperty("dodgeAttackProfile").objectReferenceValue = dodge;
        SerializedProperty profiles = catalogData.FindProperty("profiles");
        profiles.arraySize = allProfiles.Count;
        for (int i = 0; i < allProfiles.Count; i++)
            profiles.GetArrayElementAtIndex(i).objectReferenceValue = allProfiles[i];
        catalogData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    private static BeliefProfile LoadOrCreateProfile(string path)
    {
        BeliefProfile profile = AssetDatabase.LoadAssetAtPath<BeliefProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<BeliefProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }
        return profile;
    }

    private static void SetProfile(BeliefProfile profile, BeliefType type, string phrase,
        float reductionPerSecond, float reductionPerEvade, float reductionPerSuccess = 0f)
    {
        SerializedObject data = new SerializedObject(profile);
        data.FindProperty("type").enumValueIndex = (int)type;
        data.FindProperty("phrase").stringValue = phrase;
        data.FindProperty("maxBelief").floatValue = 100f;
        data.FindProperty("maintainedPlayerDamageMultiplier").floatValue = 0.6f;
        data.FindProperty("vulnerablePlayerDamageMultiplier").floatValue = 2f;
        data.FindProperty("vulnerableDuration").floatValue = 5f;
        data.FindProperty("recoveryRatio").floatValue = 1f;
        data.FindProperty("reductionPerSecond").floatValue = reductionPerSecond;
        data.FindProperty("reductionPerEvade").floatValue = reductionPerEvade;
        data.FindProperty("reductionPerSuccess").floatValue = reductionPerSuccess;
        data.FindProperty("timeWindow").floatValue = 2f;
        data.FindProperty("rearInterval").floatValue = 2f;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static void EnsureFolder(string parent, string folder)
    {
        string path = parent + "/" + folder;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folder);
    }

    private static void ConfigureScene(Scene scene)
    {
        GameObject player = FindRoot(scene, "TestPlayer");
        GameObject dog = FindRoot(scene, "Dog");
        GameObject believer = FindRoot(scene, "Believer");
        GameObject executive = FindRoot(scene, "Executive");
        BeliefProfileCatalog catalog = AssetDatabase.LoadAssetAtPath<BeliefProfileCatalog>(CatalogPath);

        if (player == null || dog == null || believer == null || executive == null || catalog == null)
            throw new System.InvalidOperationException("The prototype scene is missing a required root or belief catalog.");

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        Collider2D playerBody = player.GetComponent<Collider2D>();
        if (playerHealth == null || playerBody == null || playerBody.isTrigger)
            throw new System.InvalidOperationException("Player root requires PlayerHealth and a non-trigger body collider.");
        SetObjectReference(playerHealth, "bodyCollider", playerBody);

        ConfigureEnemy(dog, catalog);
        ConfigureEnemy(believer, catalog);
        ConfigureEnemy(executive, catalog);
        ConfigureDummyHitTool(scene, playerHealth, new[] {
            dog.GetComponent<EnemyUnit>(), believer.GetComponent<EnemyUnit>(), executive.GetComponent<EnemyUnit>() });

        player.transform.position = new Vector3(-4f, player.transform.position.y, player.transform.position.z);
        dog.transform.position = new Vector3(-1f, dog.transform.position.y, dog.transform.position.z);
        believer.transform.position = new Vector3(4f, believer.transform.position.y, believer.transform.position.z);
        executive.transform.position = new Vector3(6.5f, executive.transform.position.y, executive.transform.position.z);
        dog.SetActive(true);
        believer.SetActive(true);
        executive.SetActive(true);
    }

    private static void ConfigureDummyHitTool(Scene scene, PlayerHealth player, EnemyUnit[] enemies)
    {
        GameObject root = FindRoot(scene, "BeliefDummyData");
        if (root == null)
        {
            root = new GameObject("BeliefDummyData");
            SceneManager.MoveGameObjectToScene(root, scene);
        }
        var tool = root.GetComponent<BeliefDummyHitTool>();
        if (tool == null) tool = root.AddComponent<BeliefDummyHitTool>();
        var data = new SerializedObject(tool);
        data.FindProperty("player").objectReferenceValue = player;
        SerializedProperty targets = data.FindProperty("targets");
        targets.arraySize = enemies.Length;
        for (int i = 0; i < enemies.Length; i++)
            targets.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(root);
    }

    private static void ConfigureEnemy(GameObject enemy, BeliefProfileCatalog catalog)
    {
        bool wasActive = enemy.activeSelf;
        enemy.SetActive(false);

        EnemyUnit unit = enemy.GetComponent<EnemyUnit>();
        Collider2D body = enemy.GetComponent<Collider2D>();
        if (unit == null || body == null || body.isTrigger)
            throw new System.InvalidOperationException(enemy.name + " needs an EnemyUnit and root body collider.");
        SetObjectReference(unit, "bodyCollider", body);
        BeliefType[] roster = BeliefRules.CandidatesFor(unit);
        if (roster.Length != 3)
            throw new System.InvalidOperationException(enemy.name + " needs exactly three species-specific beliefs.");

        EnemyAttack[] attacks = enemy.GetComponents<EnemyAttack>();
        if (attacks.Length > 1)
        {
            EnemyAttack configuredAttack = null;
            foreach (EnemyAttack candidate in attacks)
            {
                if (HasRequiredAttackReferences(candidate))
                    configuredAttack = candidate;
            }

            if (configuredAttack == null)
                throw new System.InvalidOperationException(enemy.name + " has duplicate attacks but none is fully configured.");

            foreach (EnemyAttack candidate in attacks)
            {
                if (candidate != configuredAttack)
                    Object.DestroyImmediate(candidate, true);
            }
        }

        BeliefController controller = enemy.GetComponent<BeliefController>();
        if (controller == null)
            controller = enemy.AddComponent<BeliefController>();
        SerializedObject controllerData = new SerializedObject(controller);
        controllerData.FindProperty("beliefType").intValue = (int)roster[0];
        SerializedProperty candidates = controllerData.FindProperty("beliefTypeCandidates");
        candidates.arraySize = roster.Length;
        for (int i = 0; i < roster.Length; i++)
            candidates.GetArrayElementAtIndex(i).intValue = (int)roster[i];
        controllerData.FindProperty("profileCatalog").objectReferenceValue = catalog;
        controllerData.ApplyModifiedPropertiesWithoutUndo();

        BeliefCondition[] conditions = enemy.GetComponents<BeliefCondition>();
        BeliefCondition condition = null;
        foreach (BeliefCondition oldCondition in conditions)
        {
            if (oldCondition is ExpandedBeliefCondition && condition == null)
                condition = oldCondition;
            else
                Object.DestroyImmediate(oldCondition, true);
        }
        if (condition == null)
            condition = enemy.AddComponent<ExpandedBeliefCondition>();
        SetObjectReference(condition, "beliefController", controller);

        SetFloat(condition, "maxRearDistance", 4f);
        SetFloat(condition, "maxVerticalDifference", 1.5f);

        if (enemy.GetComponent<BeliefView>() == null)
            enemy.AddComponent<BeliefView>();

        if (wasActive)
            enemy.SetActive(true);
        EditorUtility.SetDirty(enemy);
    }

    private static bool HasRequiredAttackReferences(EnemyAttack attack)
    {
        SerializedObject data = new SerializedObject(attack);
        SerializedProperty indicator = data.FindProperty("dangerIndicatorPrefab");
        if (indicator == null || indicator.objectReferenceValue == null)
            return false;

        string requiredPrefabField = null;
        if (attack is ThrowStoneAttack)
            requiredPrefabField = "projectilePrefab";
        else if (attack is MagicCircleAttack)
            requiredPrefabField = "magicCirclePrefab";

        if (requiredPrefabField == null)
            return true;

        SerializedProperty prefab = data.FindProperty(requiredPrefabField);
        return prefab != null && prefab.objectReferenceValue != null;
    }

    private static GameObject FindRoot(Scene scene, string rootName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == rootName)
                return root;
        }
        return null;
    }

    private static void SetObjectReference(Object target, string field, Object value)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty property = data.FindProperty(field);
        if (property == null)
            throw new System.InvalidOperationException(target.name + " is missing serialized field " + field);
        property.objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetFloat(Object target, string field, float value)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty property = data.FindProperty(field);
        if (property == null)
            throw new System.InvalidOperationException(target.name + " is missing serialized field " + field);
        property.floatValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
