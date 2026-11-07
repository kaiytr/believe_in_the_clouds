using UnityEngine;

/// <summary>Phase transitions do not replace or restart this shared vulnerable window.</summary>
public sealed class BossSharedBelief
{
    public float Current { get; private set; }
    public float Maximum { get; }
    public bool IsVulnerable { get; private set; }
    private float vulnerableUntil;
    public BossSharedBelief(float maximum) { Maximum = maximum; Reset(); }
    public void Reset() { Current = Maximum; IsVulnerable = false; vulnerableUntil = 0f; }
    public float Remaining(float now) => IsVulnerable ? Mathf.Max(0f, vulnerableUntil - now) : 0f;
    public bool Reduce(float amount, float now, float duration)
    {
        if (IsVulnerable || amount <= 0f) return false;
        Current = Mathf.Max(0f, Current - amount);
        if (Current > 0f) return false;
        IsVulnerable = true;
        vulnerableUntil = now + duration;
        return true;
    }
    public bool Tick(float now, float recoveryRatio)
    {
        if (!IsVulnerable || now < vulnerableUntil) return false;
        Current = Maximum * Mathf.Clamp(recoveryRatio, 0.01f, 1f);
        IsVulnerable = false;
        return true;
    }
}
