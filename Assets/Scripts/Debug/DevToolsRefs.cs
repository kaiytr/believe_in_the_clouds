using UnityEngine;
using UnityEngine.UI;

/// <summary>Buttons of the developer tool panel prefab (Resources/UI/DevTools). DevToolsPanel wires their actions.</summary>
public sealed class DevToolsRefs : MonoBehaviour
{
    [SerializeField] private Button grantCoins, damage, death, resetAndGoToTitle;
    [SerializeField] private Button heal, unlockBoss, rerollBranches;
    [SerializeField] private Button storyPreview, endStoryPreview;
    [SerializeField] private Text status, feedback;

    public Button GrantCoins => grantCoins;
    public Button Damage => damage;
    public Button Death => death;
    public Button ResetAndGoToTitle => resetAndGoToTitle;
    public Button Heal => heal;
    public Button UnlockBoss => unlockBoss;
    public Button RerollBranches => rerollBranches;
    public Button StoryPreview => storyPreview;
    public Button EndStoryPreview => endStoryPreview;
    public Text Status => status;
    public Text Feedback => feedback;
    public bool IsComplete => grantCoins != null && damage != null && death != null && resetAndGoToTitle != null &&
        heal != null && unlockBoss != null && rerollBranches != null && storyPreview != null && endStoryPreview != null &&
        status != null && feedback != null;
}
