using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-600)]
public sealed class TutorialLevelExit : InteractableBehaviour, IWorldInteractionPrompt
{
    [SerializeField] private PlayerHealth player;
    [SerializeField] private TextMesh[] instructions;
    private EconomyService previous, practice;
    private bool leaving;
    private string saveMessage;

    public Transform PromptAnchor => transform;
    public Vector3 PromptOffset => GetPromptOffsetAtArtCenter();
    public bool PromptCentered => true;
    public override string InteractionPrompt => string.IsNullOrEmpty(saveMessage) ? "E : 진입" : saveMessage;

    private void Start()
    {
        Font font = UiTheme.GetFont();
        if (instructions != null)
            foreach (TextMesh instruction in instructions)
            {
                if (instruction == null) continue;
                instruction.font = font;
                instruction.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }

        EconomyRuntime.Ensure();
        previous = Currency.Service;
        practice = new EconomyService(new MemoryEconomyStore());
        EconomyRuntime.Instance.SetService(practice);
        practice.TryInitializeRun(player.MaxHp);
        LobbyRunData run = practice.RunSnapshot();
        practice.TryDepartBranch(run.candidates.First(branch => branch.role == BranchRole.Combat).id, player.MaxHp);
    }

    public override bool CanInteract(GameObject interactor) =>
        !leaving && !player.IsDead && base.CanInteract(interactor);

    protected override void OnInteract(GameObject interactor)
    {
        CurrencyResult result = previous?.TryMarkTutorialCompleted();
        if (result == null || !result.Success)
        {
            saveMessage = "저장 실패 · E : 다시 시도";
            return;
        }
        RestoreService();
        try
        {
            leaving = true;
            if (SceneManager.LoadSceneAsync(LobbyFlowRuntime.LobbyScene) == null)
                throw new System.InvalidOperationException("로비를 불러오지 못했습니다.");
        }
        catch (System.Exception ex)
        {
            leaving = false;
            EconomyRuntime.Instance.SetService(practice);
            saveMessage = "전환 실패 · E : 다시 시도";
            Debug.LogWarning(ex.Message, this);
        }
    }

    private void OnDestroy() => RestoreService();

    private void RestoreService()
    {
        if (previous != null && Currency.Service == practice && EconomyRuntime.Instance != null)
            EconomyRuntime.Instance.SetService(previous);
    }
}
