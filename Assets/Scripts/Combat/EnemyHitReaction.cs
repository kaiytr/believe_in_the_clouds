using UnityEngine;

/// <summary>Shared horizontal hit reaction for ordinary enemies and bosses. The owner ticks it before its own movement.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyHitReaction : MonoBehaviour
{
    private const float MaximumKnockbackSpeed = 16f;
    private Rigidbody2D body;
    private IHealth health;
    private float movementStunUntil;
    private float knockbackSpeed, knockbackDirection;
    private bool controlledMovement;
    private float requestedMovement;

    public bool IsMovementSuppressed => isActiveAndEnabled &&
        (Time.time < movementStunUntil || knockbackSpeed >= .01f);

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<IHealth>();
    }

    public bool Apply(float movementStunSeconds, float horizontalKnockbackSpeed, float sourceX)
    {
        if (health == null) health = GetComponent<IHealth>();
        if (!isActiveAndEnabled || health != null && health.IsDead ||
            !IsFinite(movementStunSeconds) || !IsFinite(horizontalKnockbackSpeed) || !IsFinite(sourceX))
            return false;

        movementStunUntil = Mathf.Max(movementStunUntil, Time.time + Mathf.Max(0f, movementStunSeconds));
        if (horizontalKnockbackSpeed > 0f)
        {
            float direction = body.position.x - sourceX;
            if (Mathf.Abs(direction) < .01f) direction = transform.localScale.x >= 0f ? -1f : 1f;
            knockbackDirection = Mathf.Sign(direction);
            knockbackSpeed = Mathf.Min(knockbackSpeed + horizontalKnockbackSpeed, MaximumKnockbackSpeed);
        }
        else
        {
            // A stun-only hit stops horizontal motion, including an earlier hit's remaining knockback.
            knockbackSpeed = 0f;
        }
        controlledMovement = true;
        requestedMovement = 0f;
        body.linearVelocity = new Vector2(knockbackSpeed * knockbackDirection, body.linearVelocity.y);
        return true;
    }

    /// <summary>Call once per physics tick. True means all actor-owned horizontal movement must yield to the hit.</summary>
    public bool TryApplyMovementOverride()
    {
        if (IsMovementSuppressed)
        {
            body.linearVelocity = new Vector2(knockbackSpeed * knockbackDirection, body.linearVelocity.y);
            // Preserve the previous 50 Hz decay curve while also supporting other fixed timesteps.
            float retention = Mathf.Pow(.75f, Time.fixedDeltaTime / .02f);
            knockbackSpeed = Mathf.Max(0f, (knockbackSpeed + .04f) * retention - .04f);
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
        knockbackSpeed = knockbackDirection = 0f;
        requestedMovement = 0f;
        if (controlledMovement && body != null)
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        controlledMovement = false;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private void OnDisable() => Clear();
}
