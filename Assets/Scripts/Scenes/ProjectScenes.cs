using System;
using System.Collections.Generic;
using System.IO;

// Scene catalog. Only scenes of the real play loop exist:
//   Title -> Tutorial (first run) -> Lobby -> CombatLevel1..6 / Shop / Boss -> Ending -> Title
// Scene names are stable identifiers (LobbyFlowRuntime routes by name); this class owns their asset locations.
public static class ProjectScenes
{
    public const string Title = "Assets/Scenes/Title.unity";
    public const string Tutorial = "Assets/Scenes/Tutorial.unity";
    public const string FirstAltarStory = "Assets/Scenes/Story/FirstAltarStory.unity";
    public const string Lobby = "Assets/Scenes/Lobby.unity";
    public const string Ending = "Assets/Scenes/Ending.unity";
    public const string EndingSceneName = "Ending";
    public const string StageFolder = "Assets/Scenes/Stages/";
    public const int CombatLevelCount = 6;
    public const string Shop = StageFolder + "Shop.unity";
    public const string Boss = StageFolder + "Boss.unity";

    public static IEnumerable<string> GameplayPaths
    {
        get
        {
            yield return Lobby;
            yield return Shop;
            yield return Tutorial;
            yield return FirstAltarStory;
            for (int number = 1; number <= CombatLevelCount; number++)
                yield return CombatLevelPath(number);
            yield return Boss;
            yield return Ending;
        }
    }

    public static string CombatLevelName(int number) => "CombatLevel" + number;
    public static string CombatLevelPath(int number) => StageFolder + CombatLevelName(number) + ".unity";

    public static IEnumerable<string> AllPaths
    {
        get
        {
            yield return Title;
            foreach (string path in GameplayPaths) yield return path;
        }
    }

    public static string ForName(string sceneName)
    {
        foreach (string path in AllPaths)
            if (Path.GetFileNameWithoutExtension(path) == sceneName) return path;
        throw new ArgumentException("Unknown project scene: " + sceneName, nameof(sceneName));
    }
}
