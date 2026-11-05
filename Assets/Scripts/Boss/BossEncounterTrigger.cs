using System;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[DefaultExecutionOrder(-600)]
public sealed class BossEncounterTrigger : MonoBehaviour
{
    [SerializeField] private BossCombatController boss;
    [SerializeField] private Collider2D[] gates = new Collider2D[0];
    [SerializeField] private BossArenaCamera arenaCamera;
    [Tooltip("true: entering the arena starts the fight at once (default). false: entry only seals the arena and prepares the clouds; the integrator calls Boss.BeginEncounter() once, e.g. after an intro cutscene.")]
    [SerializeField] private bool autoBeginEncounter = true;
    private BoxCollider2D entry;
    private PlayerHealth enteredPlayer;
    private bool locked;

    public bool HasEntered { get; private set; }
    public BossCombatController Boss => boss;
    public bool AutoBeginEncounter { get => autoBeginEncounter; set { autoBeginEncounter = value; HandleBossStatus(); } }
    /// <summary>The player is inside and sealed in, but the fight has not been started yet (waiting for an intro).</summary>
    public bool HoldingForIntro => HasEntered && !autoBeginEncounter && boss != null && !boss.EncounterStarted && !boss.IsDead &&
        enteredPlayer != null && !enteredPlayer.IsDead;
    /// <summary>Raised once after the whole player body is inside, the arena is sealed and the clouds are prepared.</summary>
    public event Action ArenaEntered;
    public Vector2 PatternXLimits => new Vector2(entry.bounds.min.x + 1f, entry.bounds.max.x - 1f);

    private void Awake()
    {
        entry = GetComponent<BoxCollider2D>();
        entry.isTrigger = true;
        if (boss == null)
            boss = FindFirstObjectByType<BossCombatController>(FindObjectsInactive.Include);
        if (arenaCamera == null) arenaCamera = GetComponent<BossArenaCamera>();
        if (boss != null) boss.BindArena(this);
        SetGates(false);
    }

    private void OnEnable()
    {
        if (boss != null)
        {
            boss.StatusChanged += HandleBossStatus;
            HandleBossStatus();
        }
    }

    private void OnTriggerEnter2D(Collider2D other) => TryEnter(other);
    private void OnTriggerStay2D(Collider2D other) => TryEnter(other);

    private void TryEnter(Collider2D other)
    {
        if (HasEntered || boss == null || boss.IsDead) return;
        LobbyFlowRuntime flow = LobbyFlowRuntime.Instance;
        if (flow != null && flow.IsActiveRunStage(BranchRole.Boss) && !flow.InputReady) return;
        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player == null || player.IsDead || !player.CompareTag("Player") || other != player.BodyCollider) return;

        // Wait until the whole player body crosses the entrance so a closing gate cannot push them outside.
        Bounds playerBounds = player.BodyCollider.bounds;
        Bounds entryBounds = entry.bounds;
        if (playerBounds.min.x < entryBounds.min.x + .05f || playerBounds.max.x > entryBounds.max.x - .05f) return;

        HasEntered = true;
        enteredPlayer = player;
        if (!boss.gameObject.activeSelf)
            boss.gameObject.SetActive(true);
        // Clouds are part of the arena, not of the fight: they exist from entry, with no combat running.
        boss.PrepareArenaVisuals();
        if (autoBeginEncounter)
        {
            boss.BeginEncounter();
            if (!boss.EncounterStarted)
            {
                HasEntered = false;
                enteredPlayer = null;
                boss.ReleaseArenaVisuals();
                return;
            }
        }
        HandleBossStatus();
        ArenaEntered?.Invoke();
    }

    private void HandleBossStatus()
    {
        bool shouldLock = HasEntered && boss != null && !boss.IsDead && (boss.EncounterStarted || HoldingForIntro);
        if (locked == shouldLock) return;
        locked = shouldLock;
        SetGates(locked);
        Transform focus = boss != null && boss.PlayerHealth != null ? boss.PlayerHealth.transform : enteredPlayer != null ? enteredPlayer.transform : null;
        if (locked) { if (focus != null) arenaCamera?.Enter(focus); }
        else arenaCamera?.Exit();
    }

    private void SetGates(bool closed)
    {
        foreach (Collider2D gate in gates)
        {
            if (gate == null) continue;
            gate.isTrigger = !closed;
            SpriteRenderer visual = gate.GetComponent<SpriteRenderer>();
            if (visual != null) visual.enabled = closed;
        }
    }

    private void OnDisable()
    {
        if (boss != null) boss.StatusChanged -= HandleBossStatus;
        locked = false;
        SetGates(false);
        arenaCamera?.Exit();
    }

    private void OnDestroy()
    {
        if (boss != null) boss.UnbindArena(this);
    }
}
