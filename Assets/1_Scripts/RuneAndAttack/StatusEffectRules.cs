public class StatusEffectRules
{
    public const string PoisonId = "status_poison_enemy";
    public const string BleedId = "status_bleed_enemy";
    public const string BossPoisonId = "status_boss_poison";

    public const double DurationSeconds = 10d;
    public const double TickIntervalSeconds = 1d;

    public const float PlayerPoisonMaxHpRatio = 0.05f;
    public const float MonsterPoisonDamage = 10f;

    public const int BleedMaxStacks = 5;
    public const double BleedDecayIntervalSeconds = 5d;
    public const float BleedMaxHpRatio = 0.4f;

    public const int BossPoisonMaxStacks = 3;
    public const float BossPoisonMaxHpRatio = 0.1f;
    public const float ProtectedBossPoisonMaxHpRatio = 0.02f;
}
