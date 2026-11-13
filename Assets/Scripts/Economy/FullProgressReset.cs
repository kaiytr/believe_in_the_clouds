using UnityEngine;

/// <summary>
/// Wipes every kind of progress so the next "start" is a brand-new game: the saved profile (coins, pieces, run, tutorial and
/// first altar story state), the tutorial "first launch" marker and the in-memory leftovers. Used after the ending credits
/// and by the developer reset.
/// </summary>
public static class FullProgressReset
{
    // Set when the save file could not be removed (e.g. it is locked): the reset is finished at the next launch.
    public const string PendingKey = "believe_in_the_clouds.full_reset_pending";

    /// <summary>True when the save was wiped. When it could not be, the reset is queued for the next launch.</summary>
    public static bool Run()
    {
        bool cleared = EconomyRuntime.Instance == null || EconomyRuntime.Instance.ResetSavedProfile();
        PlayerPrefs.DeleteKey(TitleScreen.TutorialStartedKey);
        if (cleared) PlayerPrefs.DeleteKey(PendingKey);
        else PlayerPrefs.SetInt(PendingKey, 1);
        PlayerPrefs.Save();

        DoctrineSlotStore.Instance?.ResetToDefault();
        DoctrineManager.Instance?.Clear();
        // The lobby router keeps the old run in memory; it is rebuilt from the fresh profile when needed.
        if (LobbyFlowRuntime.Instance != null) Object.Destroy(LobbyFlowRuntime.Instance.gameObject);
        return cleared;
    }
}
