using UnityEngine;

[CreateAssetMenu(menuName = "Economy/Settings")]
public sealed class EconomySettings : ScriptableObject
{
    public const int MaxCoins = 999999999;
    public const int MaxPiecesPerKind = 9999;
    public const int DeathLossPercent = 30;

    [Header("Coin presentation (does not change rewards)")]
    [Min(0f)] public float scatterSeconds = 0.2f;
    [Min(0f)] public float groundRestSeconds = 0.4f;
    [Min(0.01f)] public float flightMinSeconds = 0.45f;
    [Min(0.01f)] public float flightMaxSeconds = 0.7f;
    [Min(0f)] public float tokenStaggerSeconds = 0.03f;
    [Range(1, 64)] public int maxVisibleTokens = 64;
    [Min(1f)] public float tokenDiameter = 24f;
    [Min(0f)] public float scatterRadius = 42f;
    public Color tokenColor = new Color(1f, 0.76f, 0.18f, 1f);

    public static int RollDrop(EnemyRewardKind kind, IEconomyRandom random)
    {
        switch (kind)
        {
            case EnemyRewardKind.Dog: return random.Next(5, 8);
            case EnemyRewardKind.Believer: return random.Next(3, 6);
            case EnemyRewardKind.Executive: return random.Next(7, 9);
            case EnemyRewardKind.Boss: return 0;
            default: throw new System.ArgumentOutOfRangeException(nameof(kind));
        }
    }
}
