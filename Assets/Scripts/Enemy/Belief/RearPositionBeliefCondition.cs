using UnityEngine;

public class RearPositionBeliefCondition : BeliefCondition
{
    [SerializeField, Min(0f)]
    private float rearCheckInterval = 0.05f;

    private EnemyUnit owner;
    private Transform player;
    private float nextCheckTime;
    private int lastPlayerSide;
    private bool hasReducedForCurrentRearEntry;
    private int lastFacingDirection;
    private bool initialized;

    private void OnEnable()
    {
        nextCheckTime = 0f;
        lastPlayerSide = 0;
        lastFacingDirection = 0;
        hasReducedForCurrentRearEntry = false;
        initialized = false;
    }

    protected override void Awake()
    {
        base.Awake();
        owner = GetComponent<EnemyUnit>();
    }

    private void Update()
    {
        if (owner == null || owner.IsDead || Time.time < nextCheckTime)
            return;

        nextCheckTime = Time.time + rearCheckInterval;

        if (player == null || !player.gameObject.activeInHierarchy)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
        }

        if (player == null)
            return;

        int playerSide = GetSide(player.position.x - transform.position.x);
        int facingDirection = GetFacingDirection();

        if (!initialized)
        {
            lastPlayerSide = playerSide;
            lastFacingDirection = facingDirection;
            initialized = true;
            return;
        }

        bool crossedFromFrontToBack =
            lastPlayerSide == lastFacingDirection &&
            playerSide != 0 &&
            playerSide != lastFacingDirection;
        bool enemyTurnedAwayFromPlayer =
            facingDirection != lastFacingDirection &&
            playerSide != 0 &&
            playerSide != facingDirection;

        bool enteredRear =
            crossedFromFrontToBack ||
            enemyTurnedAwayFromPlayer;
        if (enteredRear && !hasReducedForCurrentRearEntry)
        {
            ReduceBelief();
            hasReducedForCurrentRearEntry = true;
        }
        else if (playerSide == facingDirection)
        {
            hasReducedForCurrentRearEntry = false;
        }

        if (playerSide != 0)
            lastPlayerSide = playerSide;
        lastFacingDirection = facingDirection;
    }

    private int GetFacingDirection()
    {
        return transform.localScale.x >= 0f ? 1 : -1;
    }

    private static int GetSide(float offset)
    {
        return Mathf.Abs(offset) < 0.01f ? 0 : (offset > 0f ? 1 : -1);
    }
}
