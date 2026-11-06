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

    public Transform PromptAnchor => transform;
    public Vector3 PromptOffset => GetPromptOffsetAboveCollider(.7f);
    public override string InteractionPrompt => "E : 튜토리얼 종료 · 로비로 이동";

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
        leaving = true;
        SceneManager.LoadSceneAsync(LobbyFlowRuntime.LobbyScene);
    }

    private void OnDestroy() => RestoreService();

    private void RestoreService()
    {
        if (previous != null && Currency.Service == practice && EconomyRuntime.Instance != null)
            EconomyRuntime.Instance.SetService(previous);
    }
}
