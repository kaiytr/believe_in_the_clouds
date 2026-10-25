using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BeliefSystemPlayModeTests
{
    private readonly List<Object> createdObjects = new List<Object>();
    private readonly Dictionary<string, GameObject> sceneActors = new Dictionary<string, GameObject>();

    [UnityTest]
    public IEnumerator BeliefIsPerEnemyDamageDependsOnSourceAndVulnerabilityRecoversOnce()
    {
        BeliefProfile rear = CreateProfile(BeliefType.RearPosition, 25f, 0f);
        BeliefProfile dodge = CreateProfile(BeliefType.DodgeAttack, 0f, 25f);
        BeliefProfileCatalog catalog = ScriptableObject.CreateInstance<BeliefProfileCatalog>();
        createdObjects.Add(catalog);
        SetField(catalog, "rearPositionProfile", rear);
        SetField(catalog, "dodgeAttackProfile", dodge);

        BeliefController first = CreateController(catalog);
        BeliefController second = CreateController(catalog);
        yield return null;

        Assert.That(first.CurrentBelief, Is.EqualTo(100f));
        Assert.That(second.CurrentBelief, Is.EqualTo(100f));

        first.ReduceBelief(25f);
        Assert.That(first.CurrentBelief, Is.EqualTo(75f));
        Assert.That(second.CurrentBelief, Is.EqualTo(100f));
        Assert.That(first.ApplyIncomingDamageModifier(new DamageInfo(10f, DamageSource.PlayerOwned)), Is.EqualTo(6f).Within(0.001f));
        Assert.That(first.ApplyIncomingDamageModifier(new DamageInfo(10f, DamageSource.Enemy)), Is.EqualTo(10f));
        Assert.That(first.ApplyIncomingDamageModifier(new DamageInfo(10f, DamageSource.Environment)), Is.EqualTo(10f));

        first.ReduceBelief(75f);
        second.ReduceBelief(100f);
        Assert.That(first.CurrentState, Is.EqualTo(BeliefState.Vulnerable));
        Assert.That(first.CurrentBelief, Is.Zero);
        Assert.That(first.ApplyIncomingDamageModifier(new DamageInfo(10f, DamageSource.PlayerOwned)), Is.EqualTo(20f));
        Assert.That(first.ReduceBelief(25f), Is.False);
        second.StopBeliefSystem();

        yield return new WaitForSeconds(5.1f);

        Assert.That(first.CurrentState, Is.EqualTo(BeliefState.Maintained));
        Assert.That(first.CurrentBelief, Is.EqualTo(100f));
        Assert.That(second.CurrentState, Is.EqualTo(BeliefState.Vulnerable));
        Assert.That(second.CurrentBelief, Is.Zero);
    }

    [UnityTest]
    public IEnumerator RealPrototypeSceneUsesActualDogStoneAndCircleAttacksForEvades()
    {
        sceneActors.Clear();
        SceneManager.sceneLoaded += CacheAndPauseEnemyAI;
        AsyncOperation load = LoadSceneForPlayMode(
            "Assets/Scenes/BeliefTest.unity",
            new LoadSceneParameters(LoadSceneMode.Single));
        while (!load.isDone)
            yield return null;
        SceneManager.sceneLoaded -= CacheAndPauseEnemyAI;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        GameObject dog = GetCachedActor("Dog");
        GameObject believer = GetCachedActor("Believer");
        GameObject executive = GetCachedActor("Executive");
        Assert.That(player, Is.Not.Null);
        Assert.That(dog, Is.Not.Null);
        Assert.That(believer, Is.Not.Null);
        Assert.That(executive, Is.Not.Null);
        Assert.That(dog.activeInHierarchy && believer.activeInHierarchy && executive.activeInHierarchy, Is.True);

        EnemyUnit dogUnit = dog.GetComponent<EnemyUnit>();
        EnemyUnit believerUnit = believer.GetComponent<EnemyUnit>();
        EnemyUnit executiveUnit = executive.GetComponent<EnemyUnit>();
        DogChargeAttack dogAttack = dog.GetComponent<DogChargeAttack>();
        ThrowStoneAttack stoneAttack = believer.GetComponent<ThrowStoneAttack>();
        MagicCircleAttack circleAttack = executive.GetComponent<MagicCircleAttack>();
        Assert.That(dogUnit.Belief.Type, Is.EqualTo(BeliefType.RearPosition));
        Assert.That(believerUnit.Belief.Type, Is.EqualTo(BeliefType.DodgeAttack));
        Assert.That(executiveUnit.Belief.Type, Is.EqualTo(BeliefType.DodgeAttack));

        AttackOutcome dogOutcome = AttackOutcome.Missed;
        AttackOutcome stoneOutcome = AttackOutcome.Missed;
        AttackOutcome circleOutcome = AttackOutcome.Missed;
        bool gotDogOutcome = false;
        bool gotStoneOutcome = false;
        bool gotCircleOutcome = false;
        dogAttack.OutcomeReported += (attack, outcome) => { dogOutcome = outcome; gotDogOutcome = true; };
        stoneAttack.OutcomeReported += (attack, outcome) => { stoneOutcome = outcome; gotStoneOutcome = true; };
        circleAttack.OutcomeReported += (attack, outcome) => { circleOutcome = outcome; gotCircleOutcome = true; };

        dogUnit.enabled = true;
        believerUnit.enabled = true;
        executiveUnit.enabled = true;
        yield return null;
        // Configure all real actors, then run their attack scenarios in stages.
        // Otherwise an early stone result can satisfy the later wait before the intended evade.
        believerUnit.enabled = executiveUnit.enabled = false;
        stoneAttack.CancelForOwnerDeath(); circleAttack.CancelForOwnerDeath();
        believerUnit.LockMovement(); executiveUnit.LockMovement();
        Assert.That(dog.GetComponent<BeliefView>().WorldView, Is.Not.Null);
        UnityEngine.UI.Text beliefPhrase = dog.GetComponentInChildren<UnityEngine.UI.Text>(true);
        Assert.That(beliefPhrase, Is.Not.Null);
        Assert.That(beliefPhrase.text, Is.EqualTo(dogUnit.Belief.Profile.Phrase));
        Transform gaugeFillTransform = dog.GetComponent<BeliefView>().WorldView.BeliefFill.transform;
        Assert.That(gaugeFillTransform, Is.Not.Null);
        Assert.That(gaugeFillTransform.GetComponent<UnityEngine.UI.Image>().fillAmount,
            Is.EqualTo(dogUnit.Belief.NormalizedBelief).Within(0.001f));

        yield return WaitForCondition(() => dogAttack.IsAttacking, 2f);
        Assert.That(dogAttack.IsAttacking, Is.True, "Dog should warn immediately from the configured start position.");

        yield return WaitForCondition(() => dogAttack.WasPlayerThreatenedDuringWarning, 1f);
        Assert.That(dogAttack.WasPlayerThreatenedDuringWarning, Is.True);

        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        playerBody.position = new Vector2(1f, playerBody.position.y);
        playerBody.linearVelocity = new Vector2(0f, playerBody.linearVelocity.y);
        Physics2D.SyncTransforms();

        yield return WaitForCondition(() => gotDogOutcome, 3f);
        Assert.That(gotDogOutcome, Is.True,
            $"Dog charge should resolve after the player leaves its corridor " +
            $"(charging={dogAttack.IsCharging}, attacking={dogAttack.IsAttacking}, vulnerable={dogUnit.IsVulnerable}, " +
            $"belief={dogUnit.Belief.CurrentBelief:F2}, timeScale={Time.timeScale:F2}, delta={Time.deltaTime:F4}, state={dogUnit.CurrentState}, " +
            $"dogX={dog.transform.position.x:F2}, playerX={player.transform.position.x:F2}, warned={dogAttack.WasPlayerThreatenedDuringWarning}).");
        Assert.That(dogOutcome, Is.EqualTo(AttackOutcome.EvadedByPlayer));
        float beliefAfterEvadedCharge = dogUnit.Belief.CurrentBelief;
        Assert.That(beliefAfterEvadedCharge, Is.GreaterThan(70f),
            "The rear-position belief should not receive the 25-point dodge reduction.");

        playerBody.position = new Vector2(-3f, playerBody.position.y);
        Physics2D.SyncTransforms();
        yield return new WaitForSeconds(0.4f);
        Bounds dogBounds = dogUnit.BodyCollider.bounds;
        Bounds playerBounds = player.GetComponent<PlayerHealth>().BodyCollider.bounds;
        bool playerIsBehindDog = RearPositionBeliefCondition.IsWithinRearRange(
            dogBounds, playerBounds, dogUnit.FacingDirection, 4f, 1.5f);
        Assert.That(dogUnit.Belief.CurrentBelief, Is.EqualTo(beliefAfterEvadedCharge - 10f).Within(1.5f),
            $"Remaining behind the Dog should continuously reduce its belief at 25 per second " +
            $"(facing={dogUnit.FacingDirection}, dogBounds={dogBounds}, playerBounds={playerBounds}, rear={playerIsBehindDog}).");

        playerBody.position = new Vector2(1f, playerBody.position.y);
        Physics2D.SyncTransforms();
        float beliefAfterLeavingRearRange = dogUnit.Belief.CurrentBelief;
        yield return new WaitForSeconds(0.2f);
        Assert.That(dogUnit.Belief.CurrentBelief, Is.EqualTo(beliefAfterLeavingRearRange).Within(1f),
            "Rear-position belief should stop decreasing as soon as the player leaves its range.");

        playerBody.position = new Vector2(1f, playerBody.position.y);
        playerBody.linearVelocity = new Vector2(0f, playerBody.linearVelocity.y);
        Physics2D.SyncTransforms();

        dogUnit.enabled = false;
        dogAttack.CancelForOwnerDeath();
        gotStoneOutcome = gotCircleOutcome = false;
        typeof(EnemyAttack).GetField("cooldownRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(stoneAttack, 0f);
        typeof(EnemyAttack).GetField("cooldownRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(circleAttack, 0f);
        Assert.That(stoneAttack.TryAttack(player.transform), Is.True);
        Assert.That(circleAttack.TryAttack(player.transform), Is.True);
        yield return WaitForCondition(
            () => stoneAttack.IsAttacking && stoneAttack.WasPlayerThreatenedDuringWarning &&
                  circleAttack.IsAttacking && circleAttack.WasPlayerThreatenedDuringWarning,
            3f);
        Assert.That(stoneAttack.WasPlayerThreatenedDuringWarning, Is.True);
        Assert.That(circleAttack.WasPlayerThreatenedDuringWarning, Is.True);

        playerBody.position = new Vector2(5.5f, playerBody.position.y);
        playerBody.linearVelocity = new Vector2(0f, playerBody.linearVelocity.y);
        Physics2D.SyncTransforms();

        yield return WaitForCondition(() => gotStoneOutcome && gotCircleOutcome, 4f);

        Assert.That(gotStoneOutcome, Is.True, "Stone projectile did not reach a final result.");
        Assert.That(gotCircleOutcome, Is.True, "Magic circle did not reach its effect-time result.");
        Assert.That(stoneOutcome, Is.EqualTo(AttackOutcome.EvadedByPlayer));
        Assert.That(circleOutcome, Is.EqualTo(AttackOutcome.EvadedByPlayer));
        Assert.That(believerUnit.Belief.CurrentBelief, Is.EqualTo(75f).Within(0.01f));
        Assert.That(executiveUnit.Belief.CurrentBelief, Is.EqualTo(75f).Within(0.01f));
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        SceneManager.sceneLoaded -= CacheAndPauseEnemyAI;
        sceneActors.Clear();
        foreach (Object created in createdObjects)
        {
            if (created != null)
                Object.Destroy(created);
        }

        createdObjects.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator RandomBeliefIsSelectedOnceAndKeptForTheEnemyLifetime()
    {
        BeliefProfile rear=CreateProfile(BeliefType.RearPosition,25f,0f);
        BeliefProfile dodge=CreateProfile(BeliefType.DodgeAttack,0f,25f);
        var catalog=ScriptableObject.CreateInstance<BeliefProfileCatalog>(); createdObjects.Add(catalog);
        SetField(catalog,"rearPositionProfile",rear); SetField(catalog,"dodgeAttackProfile",dodge);
        var actor=new GameObject("Lifetime belief test"); actor.SetActive(false); createdObjects.Add(actor);
        var controller=actor.AddComponent<BeliefController>();
        SetField(controller,"profileCatalog",catalog);
        SetField(controller,"beliefTypeCandidates",new List<BeliefType>{BeliefType.RearPosition,BeliefType.DodgeAttack});
        actor.SetActive(true); yield return null;
        BeliefType selected=controller.Type;
        Assert.That(selected==BeliefType.RearPosition || selected==BeliefType.DodgeAttack,Is.True);
        controller.ReduceBelief(20f);
        actor.SetActive(false); actor.SetActive(true); yield return null;
        Assert.That(controller.Type,Is.EqualTo(selected)); Assert.That(controller.CurrentBelief,Is.EqualTo(80f));
    }

    private void CacheAndPauseEnemyAI(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "BeliefTest")
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Dog" || root.name == "Believer" || root.name == "Executive")
            {
                sceneActors[root.name] = root;
                EnemyUnit enemy = root.GetComponent<EnemyUnit>();
                if (enemy != null)
                    enemy.enabled = false;
                root.SetActive(true); // Test fixtures include all three species regardless of the manual scene selection.
                // This scenario tests specific attack/belief pairings. Production scenes choose
                // a random lifetime belief, so pin only these test instances before EnemyUnit.Start.
                BeliefController belief = root.GetComponent<BeliefController>();
                BeliefType required = root.name == "Dog" ? BeliefType.RearPosition : BeliefType.DodgeAttack;
                SetField(belief, "beliefTypeCandidates", new List<BeliefType> { required });
                typeof(BeliefController).GetMethod("ConfigureProfile", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(belief, null);
            }
        }
    }

    private GameObject GetCachedActor(string actorName)
    {
        sceneActors.TryGetValue(actorName, out GameObject actor);
        return actor;
    }

    private static IEnumerator WaitForCondition(Func<bool> condition, float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (!condition() && Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    private BeliefProfile CreateProfile(BeliefType type, float rearLoss, float dodgeLoss)
    {
        BeliefProfile profile = ScriptableObject.CreateInstance<BeliefProfile>();
        createdObjects.Add(profile);
        SetField(profile, "type", type);
        SetField(profile, "phrase", type.ToString());
        SetField(profile, "maxBelief", 100f);
        SetField(profile, "maintainedPlayerDamageMultiplier", 0.6f);
        SetField(profile, "vulnerablePlayerDamageMultiplier", 2f);
        SetField(profile, "vulnerableDuration", 5f);
        SetField(profile, "recoveryRatio", 1f);
        SetField(profile, "reductionPerSecond", rearLoss);
        SetField(profile, "reductionPerEvade", dodgeLoss);
        return profile;
    }

    private BeliefController CreateController(BeliefProfileCatalog catalog)
    {
        GameObject enemy = new GameObject("BeliefControllerTestEnemy");
        enemy.SetActive(false);
        createdObjects.Add(enemy);
        BeliefController controller = enemy.AddComponent<BeliefController>();
        SetField(controller, "beliefType", BeliefType.RearPosition);
        SetField(controller, "profileCatalog", catalog);
        enemy.SetActive(true);
        return controller;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing serialized field {fieldName}");
        field.SetValue(target, value);
    }

    private static AsyncOperation LoadSceneForPlayMode(string path, LoadSceneParameters parameters)
    {
        Type sceneManagerType = null;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            sceneManagerType = assembly.GetType("UnityEditor.SceneManagement.EditorSceneManager", false);
            if (sceneManagerType != null)
                break;
        }

        Assert.That(sceneManagerType, Is.Not.Null, "EditorSceneManager is unavailable in the PlayMode test runner.");
        MethodInfo loadMethod = sceneManagerType.GetMethod(
            "LoadSceneAsyncInPlayMode",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(string), typeof(LoadSceneParameters) },
            null);
        Assert.That(loadMethod, Is.Not.Null, "EditorSceneManager.LoadSceneAsyncInPlayMode is unavailable.");
        return (AsyncOperation)loadMethod.Invoke(null, new object[] { path, parameters });
    }
}
