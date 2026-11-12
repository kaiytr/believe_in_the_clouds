using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-300)]
public sealed class BossEndingSequence : MonoBehaviour
{
    private enum SequencePhase { WaitingForWalk, MovingLeftOfCorpse, Turning, WalkingRight, OffscreenPause, Finishing }

    [SerializeField] private BossCombatController boss;
    [SerializeField, Min(0f)] private float deathPause = .8f;
    [SerializeField, Min(.1f)] private float walkSpeed = 2.8f;
    [SerializeField, Min(0f)] private float corpseGap = .35f;
    [SerializeField, Min(0f)] private float turnPause = .25f;
    [SerializeField, Min(0f)] private float offscreenPause = .35f;

    private PlayerHealth player;
    private PlayerMove playerMove;
    private PlayerAttack playerAttack;
    private PlayerDash playerDash;
    private Rigidbody2D playerBody;
    private Collider2D playerCollider;
    private BossArenaCamera arenaCamera;
    private Bounds corpseBounds;
    private SequencePhase phase;
    private float phaseEndsAt;
    private float sequenceStartedAt;
    private int horizontalDirection;
    private bool started;
    private bool running;
    private bool finished;

    public bool HoldsStageCompletion => started && !finished;

    private void Awake()
    {
        if (boss == null) boss = FindFirstObjectByType<BossCombatController>(FindObjectsInactive.Include);
        if (player == null) FindPlayer();
        Subscribe();
    }

    private void OnEnable() => Subscribe();

    public void Initialize(BossCombatController target)
    {
        if (boss != null) boss.StatusChanged -= HandleBossStatusChanged;
        boss = target != null ? target : FindFirstObjectByType<BossCombatController>(FindObjectsInactive.Include);
        if (player == null) FindPlayer();
        Subscribe();
    }

    private void Subscribe()
    {
        if (isActiveAndEnabled && boss != null)
        {
            boss.StatusChanged -= HandleBossStatusChanged;
            boss.StatusChanged += HandleBossStatusChanged;
        }
    }

    private void OnDisable()
    {
        if (boss != null) boss.StatusChanged -= HandleBossStatusChanged;
    }

    private void HandleBossStatusChanged()
    {
        if (boss != null && boss.IsDead) Begin();
    }

    private void Update()
    {
        if (!started && boss != null && boss.IsDead) Begin();
        if (!running) return;

        switch (phase)
        {
            case SequencePhase.WaitingForWalk:
                if (boss != null && boss.IsDeathDialoguePlaying) break;
                if (Time.unscaledTime >= phaseEndsAt &&
                    (playerMove.IsGrounded || Time.unscaledTime - sequenceStartedAt >= deathPause + 2f))
                {
                    if (PlayerBounds().center.x > corpseBounds.center.x)
                    {
                        SetFacing(-1);
                        phase = SequencePhase.MovingLeftOfCorpse;
                        horizontalDirection = -1;
                    }
                    else
                    {
                        SetFacing(1);
                        phase = SequencePhase.WalkingRight;
                        horizontalDirection = 1;
                    }
                }
                break;

            case SequencePhase.MovingLeftOfCorpse:
                if (PlayerBounds().max.x <= corpseBounds.min.x - corpseGap)
                {
                    horizontalDirection = 0;
                    SetFacing(1);
                    phase = SequencePhase.Turning;
                    phaseEndsAt = Time.unscaledTime + turnPause;
                }
                break;

            case SequencePhase.Turning:
                if (Time.unscaledTime >= phaseEndsAt)
                {
                    phase = SequencePhase.WalkingRight;
                    horizontalDirection = 1;
                }
                break;

            case SequencePhase.WalkingRight:
                if (PlayerBounds().min.x >= corpseBounds.max.x + corpseGap)
                    horizontalDirection = 1;

                if (PlayerBounds().min.x >= corpseBounds.max.x + corpseGap && IsBeyondCameraRightEdge())
                {
                    horizontalDirection = 0;
                    phase = SequencePhase.OffscreenPause;
                    phaseEndsAt = Time.unscaledTime + offscreenPause;
                }
                break;

            case SequencePhase.OffscreenPause:
                if (Time.unscaledTime >= phaseEndsAt)
                {
                    phase = SequencePhase.Finishing;
                    StartCoroutine(CompleteStageAndLoadEnding());
                }
                break;
        }
    }

    private void FixedUpdate()
    {
        if (!running || playerBody == null || player != null && player.IsDead) return;
        playerBody.linearVelocity = new Vector2(horizontalDirection * walkSpeed, playerBody.linearVelocity.y);
    }

    public void Begin()
    {
        if (started || finished || boss == null || !boss.IsDead) return;
        started = true;
        running = true;
        sequenceStartedAt = Time.unscaledTime;
        Time.timeScale = 1f;

        if (player == null) FindPlayer();
        BossTestBootstrap tools = FindFirstObjectByType<BossTestBootstrap>();
        if (tools != null)
        {
            tools.ShowDebugPanel = false;
            tools.enabled = false;
        }

        arenaCamera = boss.ArenaCamera != null ? boss.ArenaCamera : FindFirstObjectByType<BossArenaCamera>();
        if (player == null || player.IsDead || !player.TryGetComponent(out playerBody) ||
            !player.TryGetComponent(out playerMove))
        {
            phase = SequencePhase.Finishing;
            StartCoroutine(CompleteStageAndLoadEnding());
            return;
        }

        playerCollider = player.BodyCollider;
        playerAttack = player.GetComponent<PlayerAttack>();
        playerDash = player.GetComponent<PlayerDash>();
        if (playerAttack != null) playerAttack.enabled = false;
        if (playerDash != null) playerDash.enabled = false;
        playerMove.ControlLocked = true;

        SpriteRenderer corpseRenderer = boss.BodyRenderer;
        corpseBounds = corpseRenderer != null
            ? corpseRenderer.bounds
            : new Bounds(boss.transform.position, new Vector3(1.2f, 2f, 0f));
        if (corpseBounds.size.x < .1f) corpseBounds.Expand(new Vector3(1.2f, 0f, 0f));

        arenaCamera?.HoldForEnding(player.transform);
        phase = SequencePhase.WaitingForWalk;
        phaseEndsAt = Time.unscaledTime + deathPause;
        horizontalDirection = 0;
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) player = playerObject.GetComponent<PlayerHealth>();
    }

    private Bounds PlayerBounds()
    {
        if (playerCollider != null) return playerCollider.bounds;
        return new Bounds(player != null ? player.transform.position : Vector3.zero, new Vector3(.6f, 1.8f, 0f));
    }

    private void SetFacing(int direction)
    {
        if (player == null) return;
        Vector3 scale = player.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (direction < 0 ? -1f : 1f);
        player.transform.localScale = scale;
    }

    private bool IsBeyondCameraRightEdge()
    {
        Camera view = Camera.main;
        Bounds bounds = PlayerBounds();
        if (view == null) return bounds.min.x > corpseBounds.max.x + 10f;

        Vector3 viewport = view.WorldToViewportPoint(new Vector3(bounds.min.x, bounds.center.y, 0f));
        return viewport.x > 1.03f;
    }

    private IEnumerator CompleteStageAndLoadEnding()
    {
        running = false;
        horizontalDirection = 0;
        if (playerBody != null)
            playerBody.linearVelocity = new Vector2(0f, playerBody.linearVelocity.y);

        LobbyFlowRuntime flow = LobbyFlowRuntime.Instance;
        while (flow != null && flow.State != null && flow.State.area == RunArea.TemporaryStage &&
            flow.State.stage != null && flow.State.stage.role == BranchRole.Boss && !flow.State.stage.completed)
        {
            CurrencyResult result = flow.CompleteStage();
            if (result.Success) break;
            yield return new WaitForSecondsRealtime(.5f);
        }

        Time.timeScale = 1f;
        finished = true;
        AsyncOperation load = SceneManager.LoadSceneAsync(ProjectScenes.EndingSceneName);
        if (load == null)
        {
            Debug.LogError("임시 엔딩 씬을 불러오지 못했습니다. 빌드 씬 목록을 확인해 주세요.", this);
            yield break;
        }
    }
}
