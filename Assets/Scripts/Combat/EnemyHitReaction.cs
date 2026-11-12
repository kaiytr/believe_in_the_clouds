using UnityEngine;

/// <summary>Shared horizontal hit reaction for ordinary enemies and bosses. The owner ticks it before its own movement.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyHitReaction : MonoBehaviour
{
    private const float MaximumKnockbackSpeed = 16f;
    private Rigidbody2D body;
    private IHealth health;
    private EnemyAttack[] enemyAttacks;
    private BossCombatController boss;
    private float movementStunUntil;
    private float knockbackDistance, knockbackDirection, knockbackDistancePerWeightedTick;
    private int knockbackTicksRemaining;
    private bool controlledMovement;
    private float requestedMovement;

    public bool IsMovementSuppressed => isActiveAndEnabled &&
        (Time.time < movementStunUntil || knockbackTicksRemaining > 0);

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<IHealth>();
        enemyAttacks = GetComponents<EnemyAttack>();
        boss = GetComponent<BossCombatController>();
    }

    /// <summary>True while an attack of this actor shows a warning that is positioned around the actor itself.</summary>
    private bool IsAttackWarningActive()
    {
        if (boss != null && boss.IsAttackWarningActive) return true;
        if (enemyAttacks == null) return false;
        for (int i = 0; i < enemyAttacks.Length; i++)
            if (enemyAttacks[i] != null && enemyAttacks[i].IsWindingUp && enemyAttacks[i].WarningFollowsOwner) return true;
        return false;
    }

    public bool Apply(float movementStunSeconds, float horizontalKnockbackDistance, float sourceX)
    {
        if (health == null) health = GetComponent<IHealth>();
        if (!isActiveAndEnabled || health != null && health.IsDead ||
            !IsFinite(movementStunSeconds) || !IsFinite(horizontalKnockbackDistance) || !IsFinite(sourceX))
            return false;

        // Hitting an enemy whose attack warning is laid out around it does not push it: the warned area stays where it is.
        if (IsAttackWarningActive()) horizontalKnockbackDistance = 0f;

        movementStunUntil = Mathf.Max(movementStunUntil, Time.time + Mathf.Max(0f, movementStunSeconds));
        if (horizontalKnockbackDistance > 0f)
        {
            float direction = body.position.x - sourceX;
            if (Mathf.Abs(direction) < .01f) direction = transform.localScale.x >= 0f ? -1f : 1f;
            knockbackDirection = Mathf.Sign(direction);
            knockbackDistance += horizontalKnockbackDistance;
            PrepareKnockbackTrajectory(movementStunSeconds);
        }
        else
        {
            // A stun-only hit stops horizontal motion, including an earlier hit's remaining knockback.
            knockbackDistance = knockbackDistancePerWeightedTick = 0f;
            knockbackTicksRemaining = 0;
        }
        controlledMovement = true;
        requestedMovement = 0f;
        body.linearVelocity = new Vector2(CurrentKnockbackSpeed(), body.linearVelocity.y);
        return true;
    }

    /// <summary>Call once per physics tick. True means all actor-owned horizontal movement must yield to the hit.</summary>
    public bool TryApplyMovementOverride()
    {
        if (IsMovementSuppressed)
        {
            body.linearVelocity = new Vector2(CurrentKnockbackSpeed(), body.linearVelocity.y);
            if (knockbackTicksRemaining > 0)
            {
                float distanceThisTick = knockbackDistancePerWeightedTick * knockbackTicksRemaining;
                knockbackDistance = Mathf.Max(0f, knockbackDistance - distanceThisTick);
                knockbackTicksRemaining--;
                if (knockbackTicksRemaining == 0)
                    knockbackDistance = knockbackDistancePerWeightedTick = 0f;
            }
            controlledMovement = true;
            return true;
        }
        if (controlledMovement) Clear();
        return false;
    }

    /// <summary>Queue one physics tick of scripted motion. A hit can preempt it before physics runs.</summary>
    public void RequestHorizontalMovement(float speed)
    {
        if (!IsMovementSuppressed && IsFinite(speed)) requestedMovement = speed;
    }

    public void TickScriptedMovement()
    {
        float movement = requestedMovement;
        if (!TryApplyMovementOverride())
            body.linearVelocity = new Vector2(movement, body.linearVelocity.y);
        requestedMovement = 0f;
    }

    public void Clear()
    {
        movementStunUntil = 0f;
        knockbackDistance = knockbackDirection = knockbackDistancePerWeightedTick = 0f;
        knockbackTicksRemaining = 0;
        requestedMovement = 0f;
        if (controlledMovement && body != null)
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        controlledMovement = false;
    }

    private void PrepareKnockbackTrajectory(float durationSeconds)
    {
        float fixedDelta = Mathf.Max(Time.fixedDeltaTime, .001f);
        int durationTicks = Mathf.Max(1, Mathf.CeilToInt(durationSeconds / fixedDelta));
        // A triangular speed curve gives an easing stop while covering the requested distance exactly.
        // Extend the curve when needed to keep the first velocity within the shared speed limit.
        int speedLimitedTicks = Mathf.Max(1, Mathf.CeilToInt(2f * knockbackDistance /
            (MaximumKnockbackSpeed * fixedDelta) - 1f));
        knockbackTicksRemaining = Mathf.Max(durationTicks, speedLimitedTicks);
        float totalWeight = knockbackTicksRemaining * (knockbackTicksRemaining + 1f) * .5f;
        knockbackDistancePerWeightedTick = knockbackDistance / totalWeight;
    }

    private float CurrentKnockbackSpeed()
    {
        if (knockbackTicksRemaining <= 0 || Time.fixedDeltaTime <= 0f) return 0f;
        float distanceThisTick = knockbackDistancePerWeightedTick * knockbackTicksRemaining;
        return knockbackDirection * distanceThisTick / Time.fixedDeltaTime;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private void OnDisable() => Clear();
}
