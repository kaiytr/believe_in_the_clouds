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
    public BossArenaCamera ArenaCamera => arenaCamera;
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

    private void LateUpdate()
    {
        if (locked) AlignGatesToCombatEdges();
    }

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
        if (closed) AlignGatesToCombatEdges();
        foreach (Collider2D gate in gates)
        {
            if (gate == null) continue;
            gate.isTrigger = !closed;
            SpriteRenderer visual = gate.GetComponent<SpriteRenderer>();
            if (visual != null)
            {
                Color color = visual.color;
                color.a = 0f;
                visual.color = color;
                visual.enabled = false;
            }
        }
    }

    private void AlignGatesToCombatEdges()
    {
        Bounds limits = entry.bounds;
        bool fixedView = arenaCamera != null && arenaCamera.TryGetFixedCombatViewport(out limits);
        if (!fixedView) limits = entry.bounds;
        foreach (Collider2D gate in gates)
        {
            if (gate == null) continue;
            Vector3 center = gate.transform.TransformPoint(gate.offset);
            bool left = center.x < entry.bounds.center.x;
            // Put the whole wall outside the viewport; its inner face is the screen boundary.
            float halfWidth = gate.bounds.extents.x;
            float centerX = left ? limits.min.x - halfWidth : limits.max.x + halfWidth;
            Vector3 shift = new Vector3(centerX - center.x, 0f, 0f);
            if (fixedView && gate is BoxCollider2D box)
            {
                Vector2 size = box.size;
                float height = limits.size.y / Mathf.Max(.0001f, Mathf.Abs(gate.transform.lossyScale.y));
                if (Mathf.Abs(size.y - height) > .0001f) box.size = new Vector2(size.x, height);
                shift.y = limits.center.y - center.y;
            }
            if (shift.sqrMagnitude > .00000001f) gate.transform.position += shift;
        }
    }

    // 도입 연출 취소는 전투를 시작하거나 기존 전투를 종료하지 않는다.
    public void CancelIntro()
    {
        if (!HasEntered || boss != null && boss.EncounterStarted) return;
        HasEntered = false;
        enteredPlayer = null;
        boss?.ReleaseArenaVisuals();
        HandleBossStatus();
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
