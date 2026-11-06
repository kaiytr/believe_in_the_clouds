using UnityEngine;
using UnityEngine.UI;

/// <summary>Buttons of the developer tool panel prefab (Resources/UI/DevTools). DevToolsPanel wires their actions.</summary>
public sealed class DevToolsRefs : MonoBehaviour
{
    [SerializeField] private Button grantCoins, damage, death, resetAndGoToTitle;

    public Button GrantCoins => grantCoins;
    public Button Damage => damage;
    public Button Death => death;
    public Button ResetAndGoToTitle => resetAndGoToTitle;
    public bool IsComplete => grantCoins != null && damage != null && death != null && resetAndGoToTitle != null;
}
