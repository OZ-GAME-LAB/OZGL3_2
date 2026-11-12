using System.Collections.Generic;

public sealed class UnitStatusState
{
    internal List<float> PoisonStackValues { get; } = new(5);
    internal List<float> BleedStackDamages { get; } = new(5);

    public int PoisonStackCount => PoisonStackValues.Count;
    public int BleedStackCount => BleedStackDamages.Count;
    public int BossPoisonStackCount { get; internal set; }

    internal bool PoisonUsesMaxHp { get; set; }
    internal double PoisonExpiresAt { get; set; }
    internal double NextPoisonTickAt { get; set; }
    internal double PendingPoisonDamage { get; set; }
    internal double PendingPoisonMaxHpRatio { get; set; }
    internal double NextBossPoisonTickAt { get; set; }
    internal double PendingBossPoisonStackTicks { get; set; }
    internal double BossPoisonProtectedUntil { get; set; }
    internal double NextBleedDecayAt { get; set; }

    internal void Clear()
    {
        PoisonStackValues.Clear();
        BleedStackDamages.Clear();
        BossPoisonStackCount = 0;
        PoisonUsesMaxHp = false;
        PoisonExpiresAt = 0d;
        NextPoisonTickAt = 0d;
        PendingPoisonDamage = 0d;
        PendingPoisonMaxHpRatio = 0d;
        NextBossPoisonTickAt = 0d;
        PendingBossPoisonStackTicks = 0d;
        BossPoisonProtectedUntil = 0d;
        NextBleedDecayAt = 0d;
    }
}
