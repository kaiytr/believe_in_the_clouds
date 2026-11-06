using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The screen UI of a lobby/stage scene (the F2 test-tool panel), placed in the
/// scene as an ordinary prefab instance so it can be edited in Edit mode. LobbySceneController finds this in its
/// own scene and only fills in the text and wires the buttons at run time.
/// </summary>
public sealed class LobbyUiRefs : MonoBehaviour
{
    [SerializeField] private GameObject tools;
    [SerializeField] private Button freshMemory, grantCoins, completeStage, damage, death, reloadSave, failSave, hideTools, playLoop;

    public GameObject Tools => tools;
    public Button FreshMemory => freshMemory;
    public Button GrantCoins => grantCoins;
    public Button CompleteStage => completeStage;
    public Button Damage => damage;
    public Button Death => death;
    public Button ReloadSave => reloadSave;
    public Button FailSave => failSave;
    public Button HideTools => hideTools;
    public Button PlayLoop => playLoop;

    public bool IsComplete => tools != null && freshMemory != null && grantCoins != null &&
        completeStage != null && damage != null && death != null && reloadSave != null && failSave != null && hideTools != null && playLoop != null;
}
