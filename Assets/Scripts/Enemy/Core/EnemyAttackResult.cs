/// <summary>Final attack result, including accepted HP damage rather than net HP change.</summary>
public readonly struct EnemyAttackResult
{
    public int AttackId { get; }
    public AttackOutcome Outcome { get; }
    public bool WasLaunched { get; }
    public bool PlayerHpDamaged { get; }
    public bool CompletedNormally { get; }
    public float OccurredAt { get; }

    public EnemyAttackResult(int attackId, AttackOutcome outcome, bool wasLaunched,
        bool playerHpDamaged, bool completedNormally, float occurredAt)
    {
        AttackId = attackId;
        Outcome = outcome;
        WasLaunched = wasLaunched;
        PlayerHpDamaged = playerHpDamaged;
        CompletedNormally = completedNormally;
        OccurredAt = occurredAt;
    }
}
