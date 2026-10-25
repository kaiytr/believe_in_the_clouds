using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class StageTestController : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;
    [SerializeField] private BossCombatController boss;
    [SerializeField] private Text instructions;

    private string previousMessage;

    private void Start()
    {
        Font font = CombatUiResources.KoreanFont();
        if (font != null) instructions.font = font;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R) && !UiModal.IsOpen)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameObject.scene.name);
            return;
        }

        string status = player.IsDead ? "플레이어 사망 · R로 다시 시작" :
            boss.IsDead ? "보스 격파! · R로 다시 시작" :
            boss.EncounterStarted ? "보스 전투" : "오른쪽으로 이동 · 개 → 신도 → 간부 → 보스";
        string message = "STAGE TEST  |  " + status +
            "\nA/D 또는 ←/→ 이동 · Space 점프 · 좌클릭 공격 · Shift 대쉬 · R 재시작";
        if (previousMessage == message) return;
        previousMessage = message;
        instructions.text = message;
    }
}
