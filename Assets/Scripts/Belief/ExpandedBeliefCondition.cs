using UnityEngine;

/// <summary>Connects the selected challenge to actual attacks, HP hits and AI state.</summary>
public sealed class ExpandedBeliefCondition : BeliefCondition
{
    [SerializeField, Min(0f)] private float maxRearDistance = 4f;
    [SerializeField, Min(0f)] private float maxVerticalDifference = 1.5f;
    private EnemyUnit owner;
    private EnemyAttack attack;
    private BeliefChallengeState challenge;
    private bool subscribed;

    public override BeliefType SupportedType => Controller != null ? Controller.Type : BeliefType.Unassigned;
    public override bool Supports(BeliefType type) => BeliefRules.IsExpanded(type);

    protected override void Awake()
    {
        base.Awake();
        owner = GetComponent<EnemyUnit>();
        attack = GetComponent<EnemyAttack>();
    }

    private void OnEnable() => TryInitialize();
    private void Start() => TryInitialize();

    private void TryInitialize()
    {
        if (subscribed || Controller == null || Controller.Profile == null ||
            !Supports(Controller.Type) || owner == null || attack == null) return;
        BeliefProfile profile = Controller.Profile;
        challenge = new BeliefChallengeState(profile.Rule, profile.ReductionPerSuccess,
            profile.TimeWindow, profile.RearInterval);
        owner.PlayerHitReceived += HandlePlayerHit;
        attack.AttackResolved += HandleAttack;
        Controller.StateChanged += HandleState;
        subscribed = true;
    }

    private bool CanEvaluate => challenge != null && isActiveAndEnabled && owner != null &&
        !owner.IsDead && Controller != null && !Controller.IsStopped && !Controller.IsVulnerable &&
        owner.HasCombatTarget && owner.BodyCollider != null;

    private void LateUpdate()
    {
        TryInitialize();
        if (!CanEvaluate)
        {
            challenge?.Reset();
            return;
        }
        ObserveRetreat(Time.time);
        ReduceBelief(challenge.TickRear(IsBehind(owner.BodyCollider.bounds,
            owner.TargetBodyCollider.bounds, owner.FacingDirection), Time.deltaTime));
    }

    private bool IsBehind(Bounds enemy, Bounds player, float facing) =>
        RearPositionBeliefCondition.IsWithinRearRange(enemy, player, facing, maxRearDistance, maxVerticalDifference);

    private void HandlePlayerHit(PlayerHitResult hit)
    {
        if (!CanEvaluate || hit.AppliedDamage <= 0f) return;
        ObserveRetreat(hit.OccurredAt);
        ReduceBelief(challenge.Hit(hit.OccurredAt,
            IsBehind(hit.EnemyBounds, hit.PlayerBounds, hit.EnemyFacing),
            attack is MagicCircleAttack && attack.IsWindingUp));
    }

    private void ObserveRetreat(float now)
    {
        if (owner is BelieverEnemy believer)
            challenge.ObserveRetreat(believer.IsRetreating,
                Mathf.Abs(owner.Target.position.x - owner.transform.position.x) <= believer.RetreatTriggerDistance, now);
    }

    private void HandleAttack(EnemyAttack source, EnemyAttackResult result)
    {
        if (CanEvaluate) ReduceBelief(challenge.ResolveAttack(result));
    }

    private void HandleState(BeliefController source, BeliefState state) => challenge?.Reset();

    private void OnDisable()
    {
        if (subscribed)
        {
            if (owner != null) owner.PlayerHitReceived -= HandlePlayerHit;
            if (attack != null) attack.AttackResolved -= HandleAttack;
            if (Controller != null) Controller.StateChanged -= HandleState;
        }
        subscribed = false;
        challenge?.Reset();
    }
}
