using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CutsceneSceneInstaller
{
    private const string TutorialScenePath = "Assets/Scenes/Gameplay/Designed/Tutorial.unity";
    private const string TutorialRigPath = "Assets/Prefabs/Cutscenes/TutorialCutsceneRig.prefab";
    private const string BossEncounterPrefabPath = "Assets/Prefabs/Boss/BossEncounter.prefab";
    private const string RunnerPrefabPath = "Assets/Prefabs/Cutscenes/CutsceneRunner.prefab";
    private const string CutsceneDataRoot = "Assets/Data/Cutscenes/";

    [MenuItem("Tools/Cutscenes/Install Tutorial Flow")]
    public static void InstallTutorialFlow()
    {
        if (!EditorUtility.DisplayDialog("Install Tutorial Cutscenes",
                "This modifies only Tutorial.unity. Continue?", "Install", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);
        Transform anchors = GameObject.Find("TutorialCutsceneAnchors")?.transform;
        PlayerHealth playerHealth = FindPlayerHealth();
        GameObject storyObject = GameObject.Find("TutorialStoryBeliever");
        BelieverEnemy believerEnemy = storyObject != null ? storyObject.GetComponent<BelieverEnemy>() : null;

        Transform introPlayer = FindAnchor(anchors, "IntroPlayer");
        Transform encounter = FindAnchor(anchors, "BelieverEncounter");
        Transform playerTalk = FindAnchor(anchors, "PlayerTalk");
        Transform believerStart = FindAnchor(anchors, "BelieverStart");
        Transform believerNear = FindAnchor(anchors, "BelieverNear");
        Transform believerThrow = FindAnchor(anchors, "BelieverThrow");
        GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TutorialRigPath);

        if (anchors == null || playerHealth == null || believerEnemy == null || rigPrefab == null ||
            introPlayer == null || encounter == null || playerTalk == null ||
            believerStart == null || believerNear == null || believerThrow == null)
        {
            Debug.LogError("Tutorial cutscene install stopped before changes. Required anchors, PlayerHealth, " +
                           "TutorialStoryBeliever/BelieverEnemy, or TutorialCutsceneRig prefab is missing.");
            return;
        }

        CutsceneCharacter player = EnsureCharacter(playerHealth.gameObject, "플레이어");
        CutsceneCharacter believer = EnsureCharacter(believerEnemy.gameObject, "신도");

        GameObject rig = GameObject.Find("TutorialCutsceneRig");
        if (rig == null)
        {
            rig = PrefabUtility.InstantiatePrefab(rigPrefab, scene) as GameObject;
            if (rig == null)
            {
                Debug.LogError("TutorialCutsceneRig prefab could not be instantiated.");
                return;
            }
            Undo.RegisterCreatedObjectUndo(rig, "Install Tutorial Cutscene Rig");
            rig.name = "TutorialCutsceneRig";
        }

        CutsceneRunner runner = rig.GetComponent<CutsceneRunner>();
        CutsceneBubbleView view = rig.GetComponent<CutsceneBubbleView>();
        CutsceneDeathWatcher watcher = rig.GetComponent<CutsceneDeathWatcher>();
        CutsceneTutorialFlow flow = rig.GetComponent<CutsceneTutorialFlow>();
        CutsceneDefinition intro = LoadDefinition("TutorialIntro");
        CutsceneDefinition encounterDefinition = LoadDefinition("TutorialBelieverEncounter");
        CutsceneDefinition afterDeath = LoadDefinition("TutorialAfterBelieverDeath");

        if (runner == null || view == null || watcher == null || flow == null ||
            intro == null || encounterDefinition == null || afterDeath == null)
        {
            Debug.LogError("Tutorial rig or sequence data is incomplete. No scene changes were saved.");
            return;
        }

        runner.SetDefaultDefinition(intro);
        runner.SetActorBindings(
            new CutsceneActorBinding("player", player),
            new CutsceneActorBinding("believer", believer));
        runner.SetAnchorBindings(
            new CutsceneAnchorBinding("introPlayer", introPlayer),
            new CutsceneAnchorBinding("playerTalk", playerTalk),
            new CutsceneAnchorBinding("believerStart", believerStart),
            new CutsceneAnchorBinding("believerNear", believerNear),
            new CutsceneAnchorBinding("believerThrow", believerThrow));
        flow.Configure(runner, intro, encounterDefinition, afterDeath, player, believer, watcher);
        flow.Runner.SetAnchorBindings(
            new CutsceneAnchorBinding("introPlayer", introPlayer),
            new CutsceneAnchorBinding("playerTalk", playerTalk),
            new CutsceneAnchorBinding("believerStart", believerStart),
            new CutsceneAnchorBinding("believerNear", believerNear),
            new CutsceneAnchorBinding("believerThrow", believerThrow));
        EnsureEncounterTrigger(encounter, runner, encounterDefinition);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, TutorialScenePath);
        Debug.Log("Installed the cutscene rig in " + TutorialScenePath);
    }

    [MenuItem("Tools/Cutscenes/Install Boss Introduction")]
    public static void InstallBossIntroduction()
    {
        if (!EditorUtility.DisplayDialog("Install Boss Introduction",
                "This modifies only BossEncounter.prefab. Continue?", "Install", "Cancel"))
            return;

        GameObject contents = PrefabUtility.LoadPrefabContents(BossEncounterPrefabPath);
        if (contents == null)
        {
            Debug.LogError("Could not load " + BossEncounterPrefabPath);
            return;
        }

        try
        {
            BossEncounterTrigger oldTrigger = contents.GetComponentInChildren<BossEncounterTrigger>(true);
            CutsceneBossEncounterCoordinator coordinator =
                contents.GetComponentInChildren<CutsceneBossEncounterCoordinator>(true);
            BossCombatController boss = oldTrigger != null
                ? ReadBoss(oldTrigger)
                : coordinator != null ? coordinator.EncounterBoss : null;
            BoxCollider2D entry = oldTrigger != null ? oldTrigger.GetComponent<BoxCollider2D>()
                : coordinator != null ? coordinator.EntryCollider : null;
            Collider2D[] gates = oldTrigger != null ? ReadGates(oldTrigger)
                : coordinator != null ? coordinator.Gates : null;
            BossArenaCamera camera = oldTrigger != null ? ReadCamera(oldTrigger)
                : coordinator != null ? coordinator.ArenaCamera : null;

            if (boss == null || entry == null || gates == null)
            {
                Debug.LogError("Boss install stopped: BossEncounterTrigger refs to Boss, entry collider, or gates are missing.");
                return;
            }

            CutsceneDefinition intro = LoadDefinition("BossIntroduction");
            GameObject runnerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RunnerPrefabPath);
            if (intro == null || runnerPrefab == null)
            {
                Debug.LogError("Boss intro data or CutsceneRunner prefab is missing.");
                return;
            }

            SerializedObject bossObject = new SerializedObject(boss);
            SerializedProperty startOnAwake = bossObject.FindProperty("startOnAwake");
            if (startOnAwake == null)
            {
                Debug.LogError("BossCombatController.startOnAwake was not found; refusing to install an auto-starting intro.");
                return;
            }
            startOnAwake.boolValue = false;
            bossObject.ApplyModifiedPropertiesWithoutUndo();

            CutsceneCharacter bossCharacter = EnsureCharacter(boss.gameObject, "보스");
            Transform runnerTransform = contents.transform.Find("BossIntroductionCutscene");
            CutsceneRunner runner = runnerTransform != null
                ? runnerTransform.GetComponent<CutsceneRunner>() : null;
            if (runner == null)
            {
                GameObject runnerInstance = PrefabUtility.InstantiatePrefab(runnerPrefab, contents.scene) as GameObject;
                if (runnerInstance == null)
                {
                    Debug.LogError("CutsceneRunner prefab could not be instantiated.");
                    return;
                }
                runnerInstance.name = "BossIntroductionCutscene";
                runnerInstance.transform.SetParent(contents.transform, false);
                runner = runnerInstance.GetComponent<CutsceneRunner>();
            }

            runner.SetDefaultDefinition(intro);
            runner.SetActorBindings(new CutsceneActorBinding("boss", bossCharacter));

            if (coordinator == null)
                coordinator = Undo.AddComponent<CutsceneBossEncounterCoordinator>(contents);
            coordinator.Configure(boss, entry, gates, camera, runner, intro);

            if (oldTrigger != null)
                Object.DestroyImmediate(oldTrigger, true);

            PrefabUtility.SaveAsPrefabAsset(contents, BossEncounterPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Installed the boss introduction handoff in " + BossEncounterPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void EnsureEncounterTrigger(Transform anchor, CutsceneRunner runner,
        CutsceneDefinition definition)
    {
        Transform triggerTransform = anchor.Find("CutsceneEncounterTrigger");
        if (triggerTransform == null)
        {
            GameObject triggerObject = new GameObject("CutsceneEncounterTrigger", typeof(BoxCollider2D));
            Undo.RegisterCreatedObjectUndo(triggerObject, "Create Tutorial Cutscene Trigger");
            triggerTransform = triggerObject.transform;
            triggerTransform.SetParent(anchor, false);
        }

        BoxCollider2D collider = triggerTransform.GetComponent<BoxCollider2D>();
        if (collider == null)
            collider = Undo.AddComponent<BoxCollider2D>(triggerTransform.gameObject);
        collider.isTrigger = true;
        collider.size = new Vector2(2.6f, 3.5f);

        CutsceneTrigger trigger = triggerTransform.GetComponent<CutsceneTrigger>();
        if (trigger == null)
            trigger = Undo.AddComponent<CutsceneTrigger>(triggerTransform.gameObject);
        trigger.Configure(runner, definition, true, true);
    }

    private static CutsceneCharacter EnsureCharacter(GameObject actor, string label)
    {
        CutsceneCharacter character = actor.GetComponent<CutsceneCharacter>();
        if (character == null)
            character = Undo.AddComponent<CutsceneCharacter>(actor);
        character.Configure(label, null);
        return character;
    }

    private static PlayerHealth FindPlayerHealth()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.GetComponent<PlayerHealth>() : null;
    }

    private static Transform FindAnchor(Transform root, string name)
    {
        if (root == null)
            return null;
        Transform direct = root.Find(name);
        return direct != null ? direct : root.Find("TutorialCutsceneAnchors/" + name);
    }

    private static CutsceneDefinition LoadDefinition(string name) =>
        AssetDatabase.LoadAssetAtPath<CutsceneDefinition>(CutsceneDataRoot + name + ".asset");

    private static BossCombatController ReadBoss(BossEncounterTrigger trigger)
    {
        SerializedProperty property = new SerializedObject(trigger).FindProperty("boss");
        return property != null ? property.objectReferenceValue as BossCombatController : null;
    }

    private static BossArenaCamera ReadCamera(BossEncounterTrigger trigger)
    {
        SerializedProperty property = new SerializedObject(trigger).FindProperty("arenaCamera");
        return property != null ? property.objectReferenceValue as BossArenaCamera : null;
    }

    private static Collider2D[] ReadGates(BossEncounterTrigger trigger)
    {
        SerializedProperty property = new SerializedObject(trigger).FindProperty("gates");
        if (property == null || !property.isArray)
            return null;
        Collider2D[] result = new Collider2D[property.arraySize];
        for (int i = 0; i < property.arraySize; i++)
            result[i] = property.GetArrayElementAtIndex(i).objectReferenceValue as Collider2D;
        return result;
    }
}
