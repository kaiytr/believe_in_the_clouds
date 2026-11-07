using System;

[Serializable]
public struct DamageInfo
{
    public UnityEngine.GameObject Attacker { get; }
    public string Detail { get; }
    public float Amount { get; }
    public DamageSource Source { get; }
    public PlayerAttackContext PlayerAttack { get; }
    public int HitIndex { get; }

    public DamageInfo(float amount, DamageSource source, UnityEngine.GameObject attacker = null, string detail = null)
    {
        Amount = amount;
        Attacker = attacker;
        Detail = detail;
        Source = source;
        PlayerAttack = null;
        HitIndex = 0;
    }

    public DamageInfo(float amount, PlayerAttackContext playerAttack, int hitIndex = 0)
    {
        Amount = amount;
        Attacker = playerAttack?.Actor;
        Detail = null;
        Source = DamageSource.PlayerOwned;
        PlayerAttack = playerAttack;
        HitIndex = hitIndex;
    }
}
