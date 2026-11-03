using System;
using System.Collections.Generic;
using System.IO;

// Scene names remain stable for saved runs; this catalog owns their asset locations.
public static class ProjectScenes
{
    public const string Title = "Assets/Scenes/Gameplay/Title.unity";
    public const string Lobby = "Assets/Scenes/Gameplay/Lobby.unity";
    public const string ShopTest = "Assets/Scenes/Gameplay/ShopTest.unity";
    public const string DogCombatTest = "Assets/Scenes/Gameplay/DogCombatTest.unity";
    public const string BelieverCombatTest = "Assets/Scenes/Gameplay/BelieverCombatTest.unity";
    public const string ExecutiveCombatTest = "Assets/Scenes/Gameplay/ExecutiveCombatTest.unity";
    public const string BossTest = "Assets/Scenes/Gameplay/BossTest.unity";
    public const string TemporaryStage = "Assets/Scenes/Gameplay/TemporaryStage.unity";
    public const string StageTest = "Assets/Scenes/Tests/StageTest.unity";
    public const string EconomyTest = "Assets/Scenes/Tests/EconomyTest.unity";
    public const string ChestTest = "Assets/Scenes/Tests/ChestTest.unity";
    public const string FoundationTest = "Assets/Scenes/Tests/FoundationTest.unity";
    public const string PlayerTEST = "Assets/Scenes/Sources/PlayerTEST.unity";
    public const string EnemyTest = "Assets/Scenes/Sources/EnemyTest.unity";
    public const string BeliefTest = "Assets/Scenes/Sources/BeliefTest.unity";

    public static IEnumerable<string> GameplayPaths
    {
        get
        {
            yield return Lobby;
            yield return ShopTest;
            yield return DogCombatTest;
            yield return BelieverCombatTest;
            yield return ExecutiveCombatTest;
            yield return TemporaryStage;
            yield return BossTest;
        }
    }

    public static IEnumerable<string> AllPaths
    {
        get
        {
            foreach (string path in GameplayPaths) yield return path;
            yield return StageTest;
            yield return EconomyTest;
            yield return ChestTest;
            yield return FoundationTest;
            yield return PlayerTEST;
            yield return EnemyTest;
            yield return BeliefTest;
        }
    }

    public static string ForName(string sceneName)
    {
        foreach (string path in AllPaths)
            if (Path.GetFileNameWithoutExtension(path) == sceneName) return path;
        throw new ArgumentException("Unknown project scene: " + sceneName, nameof(sceneName));
    }
}
