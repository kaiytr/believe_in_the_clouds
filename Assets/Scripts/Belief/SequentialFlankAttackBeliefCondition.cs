using UnityEngine;

public class SequentialFlankAttackBeliefCondition : BeliefCondition
{
    [SerializeField, Min(0.01f)]
    private float sequenceWindow = 1.5f;

    public float SequenceWindow => sequenceWindow;

    private float lastHitTime = float.NegativeInfinity;
    private int lastHitSide;

    private void OnEnable()
    {
        ResetSequence();
    }

    public void ResetSequence()
    {
        lastHitTime = float.NegativeInfinity;
        lastHitSide = 0;
    }

    public void RegisterPlayerHit(Vector2 directionFromEnemyToPlayer)
    {
        if (!isActiveAndEnabled)
            return;

        if (directionFromEnemyToPlayer.sqrMagnitude <= 0.0001f)
            return;

        int hitSide = directionFromEnemyToPlayer.x > 0f ? 1 : -1;
        float now = Time.time;

        if (now - lastHitTime <= sequenceWindow &&
            hitSide != lastHitSide)
        {
            ReduceBelief();
            ResetSequence();
            return;
        }

        lastHitTime = now;
        lastHitSide = hitSide;
    }
}
