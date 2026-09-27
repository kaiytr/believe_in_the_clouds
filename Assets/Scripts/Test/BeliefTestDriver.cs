using UnityEngine;

public class BeliefTestDriver : MonoBehaviour
{
    private const int RearMode = 0;
    private const int DodgeMode = 1;
    private const int FlankMode = 2;

    [SerializeField] private Transform player;
    [SerializeField] private BeliefTestDummy dummy;
    [SerializeField] private BeliefController belief;
    [SerializeField] private float playerSpeed = 5f;
    [SerializeField] private float hitDistance = 1.2f;

    private readonly string[] modeNames =
    {
        "후방 이동",
        "공격 회피",
        "앞뒤 연속 공격"
    };

    private readonly string[] modeDescriptions =
    {
        "믿음: 플레이어는 내 뒤로 이동할 수 없다.  오른쪽에서 왼쪽으로 적을 통과하세요.",
        "믿음: 플레이어는 내 공격을 피할 수 없다.  K를 누르고 0.8초 동안 4 유닛 밖으로 피하세요.",
        "믿음: 나는 한 번에 한 명만 상대할 수 있다.  적 가까이서 오른쪽 L, 왼쪽 J 순으로 1.5초 안에 공격하세요."
    };

    private RearPositionBeliefCondition rearCondition;
    private DodgeAttackBeliefCondition dodgeCondition;
    private SequentialFlankAttackBeliefCondition flankCondition;

    private GUIStyle titleStyle;
    private Vector3 initialPlayerPosition;
    private Vector3 initialDummyPosition;
    private float horizontalInput;
    private Rigidbody2D playerBody;
    private PlayerHealth playerHealth;
    private BeliefTestAttack testAttack;
    private int selectedMode = -1;
    private int lastFlankHitSide;
    private float lastFlankHitTime = float.NegativeInfinity;
    private string actionStatus = "준비 완료";
    private float statusExpireTime;

    public void Configure(
        Transform testPlayer,
        BeliefTestDummy testDummy,
        BeliefController testBelief,
        RearPositionBeliefCondition rear,
        DodgeAttackBeliefCondition dodge,
        SequentialFlankAttackBeliefCondition flank
    )
    {
        player = testPlayer;
        dummy = testDummy;
        belief = testBelief;
        rearCondition = rear;
        dodgeCondition = dodge;
        flankCondition = flank;
        playerBody = player.GetComponent<Rigidbody2D>();
        playerHealth = player.GetComponent<PlayerHealth>();
        testAttack = dummy.GetComponent<BeliefTestAttack>();
        initialPlayerPosition = player.position;
        initialDummyPosition = dummy.transform.position;

        SelectMode(RearMode);
    }

    private void Start()
    {
        if (player != null)
            initialPlayerPosition = player.position;
        if (dummy != null)
            initialDummyPosition = dummy.transform.position;
    }

    private void Update()
    {
        if (player == null || dummy == null)
            return;

        horizontalInput = Input.GetAxisRaw("Horizontal");
        if (Input.GetKeyDown(KeyCode.Space) && IsPlayerGrounded())
        {
            Vector2 velocity = playerBody.linearVelocity;
            velocity.y = 8f;
            playerBody.linearVelocity = velocity;
        }

        if (Input.GetKeyDown(KeyCode.J))
            TryPlayerHit(-1);
        if (Input.GetKeyDown(KeyCode.L))
            TryPlayerHit(1);
        if (Input.GetKeyDown(KeyCode.K))
            TryStartAttack();
        if (Input.GetKeyDown(KeyCode.R))
            ResetTest();
    }

    private void FixedUpdate()
    {
        if (playerBody == null)
            return;

        Vector2 velocity = playerBody.linearVelocity;
        velocity.x = horizontalInput * playerSpeed;
        playerBody.linearVelocity = velocity;
    }

    private bool IsPlayerGrounded()
    {
        return playerBody != null &&
               Mathf.Abs(playerBody.position.y + 1.5f) < 0.12f &&
               Mathf.Abs(playerBody.linearVelocity.y) < 0.15f;
    }

    private void TryPlayerHit(int side)
    {
        if (selectedMode != FlankMode)
        {
            SetStatus("앞뒤 연속 공격 모드를 선택해야 시험할 수 있어요.");
            return;
        }

        float actualSide = player.position.x - dummy.transform.position.x;
        if (Mathf.Abs(actualSide) > hitDistance)
        {
            SetStatus("적의 1.2 유닛 안까지 가까이 이동하세요.");
            return;
        }

        if (Mathf.Sign(actualSide) != side)
        {
            SetStatus(side < 0
                ? "먼저 적의 왼쪽으로 이동하세요."
                : "먼저 적의 오른쪽으로 이동하세요.");
            return;
        }

        float sequenceWindow = flankCondition.SequenceWindow;
        bool completesSequence =
            lastFlankHitSide != 0 &&
            lastFlankHitSide != side &&
            Time.time - lastFlankHitTime <= sequenceWindow;
        float previousHp = dummy.CurrentHpForTest;
        float previousBelief = belief.CurrentBelief;
        dummy.ReceiveTestHit(side);
        float damage = previousHp - dummy.CurrentHpForTest;
        if (damage <= 0f)
        {
            lastFlankHitSide = 0;
            SetStatus("공격이 등록되지 않았어요. R로 초기화 후 다시 시도하세요.");
            return;
        }

        if (completesSequence)
        {
            lastFlankHitSide = 0;
            float beliefLoss = previousBelief - belief.CurrentBelief;
            SetStatus(beliefLoss > 0f
                ? $"앞뒤 연속 공격 성공! 믿음 {beliefLoss:0} 감소"
                : "앞뒤 연속 공격 성공. 취약 상태에서는 믿음이 더 감소하지 않습니다.");
        }
        else
        {
            lastFlankHitSide = side;
            lastFlankHitTime = Time.time;
            SetStatus($"{(side < 0 ? "왼쪽" : "오른쪽")} 공격 기록. 반대편에서 {sequenceWindow:0.0}초 안에 공격하세요.");
        }
    }

    private void TryStartAttack()
    {
        if (selectedMode != DodgeMode)
        {
            SetStatus("공격 회피 모드를 선택해야 시험할 수 있어요.");
            return;
        }

        if (testAttack != null && testAttack.TryAttack(player))
            SetStatus("적이 공격 준비 중입니다. 0.8초 안에 4 유닛 밖으로 피하세요.");
        else
            SetStatus("적의 4 유닛 안에 있고 공격이 준비됐는지 확인하세요.");
    }

    private void SelectMode(int mode)
    {
        if (rearCondition == null ||
            dodgeCondition == null ||
            flankCondition == null)
            return;

        selectedMode = mode;
        rearCondition.enabled = mode == RearMode;
        dodgeCondition.enabled = mode == DodgeMode;
        flankCondition.enabled = mode == FlankMode;
        ResetTest();
        SetStatus($"'{modeNames[mode]}' 믿음 시험을 시작합니다.");
    }

    private void SetStatus(string message)
    {
        actionStatus = message;
        statusExpireTime = Time.unscaledTime + 4f;
    }

    private void ResetTest()
    {
        if (testAttack != null)
            testAttack.ResetForTest();

        if (player != null)
            player.position = initialPlayerPosition;
        if (playerBody != null)
        {
            playerBody.position = initialPlayerPosition;
            playerBody.linearVelocity = Vector2.zero;
        }
        horizontalInput = 0f;
        lastFlankHitSide = 0;
        lastFlankHitTime = float.NegativeInfinity;

        if (dummy != null)
        {
            dummy.transform.position = initialDummyPosition;
            Rigidbody2D dummyBody = dummy.GetComponent<Rigidbody2D>();
            if (dummyBody != null)
            {
                dummyBody.position = initialDummyPosition;
                dummyBody.linearVelocity = Vector2.zero;
            }
            dummy.gameObject.SetActive(true);
            dummy.ResetTestHealth();
        }

        if (belief != null)
            belief.ResetBelief();
        if (playerHealth != null)
            playerHealth.ResetHealth();
        if (flankCondition != null)
            flankCondition.ResetSequence();

        actionStatus = "시험 초기화 완료";
        statusExpireTime = Time.unscaledTime + 2f;
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(14, 14, 520, 385), GUI.skin.box);
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
        }

        GUILayout.Label("믿음 시스템 테스트", titleStyle);
        GUILayout.Label("이동 A/D  ·  점프 Space  ·  적 공격 K  ·  좌/우 공격 J/L  ·  초기화 R");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("후방 이동", GUILayout.Height(34)))
            SelectMode(RearMode);
        if (GUILayout.Button("공격 회피", GUILayout.Height(34)))
            SelectMode(DodgeMode);
        if (GUILayout.Button("앞뒤 연속 공격", GUILayout.Height(34)))
            SelectMode(FlankMode);
        GUILayout.EndHorizontal();

        string selectedDescription = "모드를 선택하세요.";
        if (selectedMode >= 0)
        {
            selectedDescription = selectedMode == FlankMode
                ? $"믿음: 나는 한 번에 한 명만 상대할 수 있다. 적 가까이서 오른쪽 L, 왼쪽 J 순으로 {flankCondition.SequenceWindow:0.0}초 안에 공격하세요."
                : modeDescriptions[selectedMode];
        }
        GUILayout.Label(selectedDescription, GUI.skin.box);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("적 공격 (K)", GUILayout.Height(30)))
            TryStartAttack();
        if (GUILayout.Button("왼쪽 공격 (J)", GUILayout.Height(30)))
            TryPlayerHit(-1);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("오른쪽 공격 (L)", GUILayout.Height(30)))
            TryPlayerHit(1);
        if (GUILayout.Button("초기화 (R)", GUILayout.Height(30)))
            ResetTest();
        GUILayout.EndHorizontal();

        if (belief != null)
        {
            GUILayout.Space(6);
            GUILayout.Label($"믿음: {(selectedMode >= 0 ? modeNames[selectedMode] : belief.BeliefName)}  {belief.CurrentBelief:0}/{belief.MaxBelief:0}    상태: {belief.CurrentState}");
            Rect bar = GUILayoutUtility.GetRect(480, 20);
            GUI.Box(bar, GUIContent.none);
            Rect fill = bar;
            fill.width *= belief.NormalizedBelief;
            GUI.color = belief.IsVulnerable ? new Color(0.95f, 0.3f, 0.2f) : new Color(0.25f, 0.75f, 0.65f);
            GUI.Box(fill, GUIContent.none);
            GUI.color = Color.white;
            string playerHp = playerHealth != null
                ? $"    플레이어 HP: {playerHealth.CurrentHp:0}"
                : string.Empty;
            GUILayout.Label($"허수아비 HP: {dummy.CurrentHpForTest:0}{playerHp}");
        }

        if (Time.unscaledTime < statusExpireTime)
            GUILayout.Label(actionStatus, GUI.skin.box);

        GUILayout.EndArea();
    }
}
