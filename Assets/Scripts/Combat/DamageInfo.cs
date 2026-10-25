using System;

[Serializable]
public struct DamageInfo
{
    public float Amount { get; }
    public DamageSource Source { get; }
    public PlayerAttackContext PlayerAttack { get; }
    public int HitIndex { get; }

    public DamageInfo(float amount, DamageSource source)
    {
        Amount = amount;
        Source = source;
        PlayerAttack = null;
        HitIndex = 0;
    }

    public DamageInfo(float amount, PlayerAttackContext playerAttack, int hitIndex = 0)
    {
        Amount = amount;
        Source = DamageSource.PlayerOwned;
        PlayerAttack = playerAttack;
        HitIndex = hitIndex;
    }
}
