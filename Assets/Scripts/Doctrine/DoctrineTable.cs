// Doctrine table: fixed values from the spec.
// Subjects 4 / Conditions 4 / Results 4.
// Subject usages: player/enemy/boss/all.
// Condition triggers:
//   OnHit      = whenever hit (each time)
//   OnDeath    = when dead (after death motion ends)
//   LowHealth  = the moment HP drops to 30% or below (once)
//   OnAttack   = when attack motion is done AND the hit lands on a target
// Result effects:
//   Explode        = 30% of exploder's max HP as damage, friends and foes alike, 5s cooldown
//   IncreaseSpeed  = +40% of base move speed (persistent, no expiry)
//   IncreaseAttack = +20% damage dealt, for 5s after trigger
//   Heal           = 10% of max HP, per-doctrine 20s cooldown
public static class DoctrineTable
{
    public const float ExplodeMaxHpRatio = 0.3f;
    public const float ExplodeRadius = 3f;
    public const float ExplodeCooldownSec = 5f;


    public const float SpeedIncreaseRatio = 0.4f; // x1.4, persistent
    public const float AttackIncreaseRatio = 0.2f; // x1.2
    public const float AttackBuffDuration = 5f;

    public const float HealMaxHpRatio = 0.1f;
    public const float HealCooldownSec = 20f;
}
