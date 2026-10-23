using System;

[Serializable]
public struct DamageInfo
{
    public float Amount { get; }
    public DamageSource Source { get; }

    public DamageInfo(float amount, DamageSource source)
    {
        Amount = amount;
        Source = source;
    }
}
