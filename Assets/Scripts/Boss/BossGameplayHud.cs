using UnityEngine;
using UnityEngine.UI;

/// <summary>Enemy-style health, belief and speech; no pattern instructions or numeric diagnostics.</summary>
public sealed class BossGameplayHud : MonoBehaviour
{
    [SerializeField] private Canvas beliefCanvas;
    [SerializeField] private Canvas healthCanvas;
    [SerializeField] private Canvas playerCanvas;
    [SerializeField] private Image healthFill;
    [SerializeField] private Image beliefFill;
    [SerializeField] private Image beliefBackground;
    [SerializeField] private Image speechBubble;
    [SerializeField] private Text phrase;
    [SerializeField] private Image playerHealthFill;
    [SerializeField] private Text playerState;
    private BossCombatController owner;

    public bool IsConfigured => beliefCanvas != null && healthCanvas != null && playerCanvas != null &&
        healthFill != null && beliefFill != null && beliefBackground != null && speechBubble != null &&
        phrase != null && playerHealthFill != null && playerState != null;
    public Canvas BeliefCanvas => beliefCanvas;
    public Canvas HealthCanvas => healthCanvas;
    public Canvas PlayerCanvas => playerCanvas;

    private void Awake() { SetVisible(false); }
    public void Initialize(BossCombatController boss, Font font)
    {
        owner = boss;
        foreach (Text text in beliefCanvas.GetComponentsInChildren<Text>(true)) text.font = font;
        foreach (Text text in playerCanvas.GetComponentsInChildren<Text>(true)) text.font = font;
        Refresh();
    }

    private void LateUpdate() { if (owner != null && IsConfigured) Refresh(); }
    private void Refresh()
    {
        bool fighting = owner.isActiveAndEnabled && owner.EncounterStarted && !owner.IsDead;
        bool gameplay = !owner.DebugUiVisible;
        beliefCanvas.enabled = healthCanvas.enabled = fighting && gameplay;
        PlayerHealth player = owner.PlayerHealth;
        playerCanvas.enabled = gameplay && owner.isActiveAndEnabled && !owner.IsDead && (fighting || player != null && player.IsDead);
        healthFill.fillAmount = owner.MaxHp > 0f ? owner.CurrentHp / owner.MaxHp : 0f;
        beliefFill.fillAmount = owner.IsVulnerable ? 0f : owner.NormalizedBelief;
        Color neutral = new Color(.4f, .4f, .4f);
        healthFill.color = neutral;
        beliefFill.color = owner.IsVulnerable ? new Color(.94f, .31f, .25f) : neutral;
        beliefBackground.color = owner.IsVulnerable ? new Color(.78f, .64f, .62f) : new Color(.73f, .73f, .73f);
        phrase.text = owner.IsVulnerable ? "믿음 붕괴" : owner.ActiveBeliefPhrase;
        speechBubble.gameObject.SetActive(!string.IsNullOrEmpty(phrase.text));
        if (player != null)
        {
            playerHealthFill.fillAmount = player.MaxHp > 0f ? player.CurrentHp / player.MaxHp : 0f;
            BossBurnEffect burn = player.GetComponent<BossBurnEffect>();
            playerState.text = player.IsDead ? "쓰러짐" : burn != null && burn.IsBurning ? "화상" : "";
        }
        PositionWorldView();
    }

    private void PositionWorldView()
    {
        Camera camera = Camera.main;
        float scale = .01f;
        if (camera != null && camera.orthographic)
            scale = Mathf.Max(scale, 12f / 19f * camera.orthographicSize * 2f / Mathf.Max(1, camera.pixelHeight));
        Bounds body = owner.BodyCollider.bounds;
        // Match BeliefView's anchors; allow a minimum readable font size in a small window.
        beliefCanvas.transform.SetPositionAndRotation(body.center + Vector3.up * (body.extents.y + .48f * scale / .01f), Quaternion.identity);
        healthCanvas.transform.SetPositionAndRotation(new Vector3(body.center.x, body.min.y - .18f * scale / .01f, body.center.z), Quaternion.identity);
        beliefCanvas.transform.localScale = healthCanvas.transform.localScale = Vector3.one * scale;
        beliefCanvas.worldCamera = healthCanvas.worldCamera = camera;
    }
    private void SetVisible(bool visible)
    {
        if (beliefCanvas != null) beliefCanvas.enabled = visible;
        if (healthCanvas != null) healthCanvas.enabled = visible;
        if (playerCanvas != null) playerCanvas.enabled = visible;
    }
    private void OnDisable() { SetVisible(false); }
}
