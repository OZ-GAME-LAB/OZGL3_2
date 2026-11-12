using System;

public static class BattleUtil
{
    public const int MaxPoisonStacks = 5;
    public const double PoisonDurationSeconds = 10d;
    public const double PoisonTickIntervalSeconds = 1d;
    public const int MaxBossPoisonStacks = 3;
    public const double BossPoisonProtectionSeconds = 1d;
    public const int BleedBurstStacks = 5;
    public const double BleedDecayDelaySeconds = 3d;
    public const double BleedDecayIntervalSeconds = 1d;

    public static float CalculateDamage(UnitVM target, float rawDamage, DamageType damageType)
    {
        if (target == null)
            return 0f;

        float resistance;

        switch (damageType)
        {
            case DamageType.Physical:
                resistance = target.PhysicalResistance;
                break;
            case DamageType.Fire:
                resistance = target.FireResistance;
                break;
            case DamageType.Ice:
                resistance = target.IceResistance;
                break;
            case DamageType.Electric:
                resistance = target.ElectricalResistance;
                break;
            default:
                return 0f;
        }

        return CalculateResistedDamage(rawDamage, resistance);
    }

    public static float CalculateResistedDamage(float rawDamage, float resistance)
    {
        if (!IsFinite(rawDamage) || rawDamage <= 0f || !IsFinite(resistance))
            return 0f;

        return ToDamage((double)rawDamage * Math.Max(0d, 1d - resistance));
    }

    public static float CalculatePoisonDamage(UnitVM target, float baseDamagePerStack)
    {
        return CalculatePoisonDamage(target, baseDamagePerStack, target == null ? 0 : target.PoisonStackCount);
    }

    public static float CalculatePlayerPoisonDamage(PlayerStatViewModel target, float maxHpRatio)
    {
        if (target == null || !IsFinite(maxHpRatio) || maxHpRatio < 0f)
            return 0f;

        return CalculatePoisonDamage(target, ToDamage((double)target.MaxHp * maxHpRatio));
    }

    public static float CalculateMonsterPoisonDamage(MonsterVM target, float fixedDamagePerStack)
    {
        return CalculatePoisonDamage(target, fixedDamagePerStack);
    }

    public static float CalculateBossPoisonDamage(UnitVM target, bool hasPoisonProtectionAmulet)
    {
        return CalculateBossPoisonDamage(target, hasPoisonProtectionAmulet,
            target == null ? 0 : target.BossPoisonStackCount);
    }

    public static bool ApplyPoison(UnitVM target, double currentTime)
    {
        if (!CanApplyStatus(target, currentTime))
            return false;

        AdvancePoison(target, currentTime);

        if (!target.IsPoisoned)
            target.NextPoisonTickAt = currentTime + PoisonTickIntervalSeconds;

        target.PoisonExpiresAt = currentTime + PoisonDurationSeconds;
        target.PoisonStackCount = Math.Min(MaxPoisonStacks, target.PoisonStackCount + 1);
        return true;
    }

    public static bool ApplyBossPoison(UnitVM target, double currentTime)
    {
        if (!CanApplyStatus(target, currentTime) || !target.CanReceiveBossPoison)
            return false;

        if (currentTime < target.BossPoisonProtectedUntil)
            return false;

        AdvanceBossPoison(target, currentTime);

        if (!target.IsBossPoisoned)
            target.NextBossPoisonTickAt = currentTime + PoisonTickIntervalSeconds;

        target.BossPoisonProtectedUntil = currentTime + BossPoisonProtectionSeconds;
        target.BossPoisonStackCount = Math.Min(MaxBossPoisonStacks, target.BossPoisonStackCount + 1);
        return true;
    }

    public static float ApplyBleed(UnitVM target, double currentTime, float burstDamage)
    {
        if (!CanApplyStatus(target, currentTime) || !target.CanReceiveBleed || !IsFinite(burstDamage) || burstDamage < 0f)
            return 0f;

        UpdateBleed(target, currentTime);

        if (target.BleedStackCount + 1 >= BleedBurstStacks)
        {
            ClearBleed(target);
            return burstDamage;
        }

        target.NextBleedDecayAt = currentTime + BleedDecayDelaySeconds;
        target.BleedStackCount++;
        return 0f;
    }

    public static float UpdateStatusEffects(UnitVM target, double currentTime, float poisonBaseDamagePerStack,
        bool hasPoisonProtectionAmulet)
    {
        if (target == null || !IsFinite(currentTime) || currentTime < 0d)
            return 0f;

        if (target.CurHp <= 0f)
        {
            ClearStatusEffects(target);
            return 0f;
        }

        AdvancePoison(target, currentTime);
        AdvanceBossPoison(target, currentTime);
        UpdateBleed(target, currentTime);

        double damage = CalculatePoisonDamage(target, poisonBaseDamagePerStack, target.PendingPoisonStackTicks);
        damage += CalculateBossPoisonDamage(target, hasPoisonProtectionAmulet, target.PendingBossPoisonStackTicks);

        target.PendingPoisonStackTicks = 0d;
        target.PendingBossPoisonStackTicks = 0d;
        return ToDamage(damage);
    }

    public static void ClearPoison(UnitVM target)
    {
        if (target == null)
            return;

        target.PoisonExpiresAt = 0d;
        target.NextPoisonTickAt = 0d;
        target.PendingPoisonStackTicks = 0d;
        target.PoisonStackCount = 0;
    }

    public static void ClearBossPoison(UnitVM target)
    {
        if (target == null)
            return;

        target.NextBossPoisonTickAt = 0d;
        target.PendingBossPoisonStackTicks = 0d;
        target.BossPoisonProtectedUntil = 0d;
        target.BossPoisonStackCount = 0;
    }

    public static void ClearBleed(UnitVM target)
    {
        if (target == null)
            return;

        target.NextBleedDecayAt = 0d;
        target.BleedStackCount = 0;
    }

    public static void ClearStatusEffects(UnitVM target)
    {
        ClearPoison(target);
        ClearBossPoison(target);
        ClearBleed(target);
    }

    private static float CalculatePoisonDamage(UnitVM target, float baseDamagePerStack, double stackTicks)
    {
        if (target == null || !IsFinite(baseDamagePerStack) || baseDamagePerStack <= 0f || !IsFinite(target.PoisonResistance))
            return 0f;

        return ToDamage(baseDamagePerStack * stackTicks * Math.Max(0d, 1d - target.PoisonResistance));
    }

    private static float CalculateBossPoisonDamage(UnitVM target, bool hasPoisonProtectionAmulet, double stackTicks)
    {
        if (target == null || !target.CanReceiveBossPoison)
            return 0f;

        double ratioPerStack = hasPoisonProtectionAmulet ? 0.02d : 0.1d;
        return ToDamage(target.MaxHp * ratioPerStack * stackTicks);
    }

    private static void AdvancePoison(UnitVM target, double currentTime)
    {
        if (!target.IsPoisoned)
            return;

        double endTime = Math.Min(currentTime, target.PoisonExpiresAt);
        double tickCount = GetDueCount(target.NextPoisonTickAt, endTime, PoisonTickIntervalSeconds);

        target.PendingPoisonStackTicks += tickCount * target.PoisonStackCount;
        target.NextPoisonTickAt += tickCount * PoisonTickIntervalSeconds;

        if (currentTime >= target.PoisonExpiresAt)
        {
            target.PoisonExpiresAt = 0d;
            target.NextPoisonTickAt = 0d;
            target.PoisonStackCount = 0;
        }
    }

    private static void AdvanceBossPoison(UnitVM target, double currentTime)
    {
        if (!target.IsBossPoisoned)
            return;

        double tickCount = GetDueCount(target.NextBossPoisonTickAt, currentTime, PoisonTickIntervalSeconds);

        target.PendingBossPoisonStackTicks += tickCount * target.BossPoisonStackCount;
        target.NextBossPoisonTickAt += tickCount * PoisonTickIntervalSeconds;
    }

    private static void UpdateBleed(UnitVM target, double currentTime)
    {
        if (!target.IsBleeding)
            return;

        int count = (int)Math.Min(target.BleedStackCount,
            GetDueCount(target.NextBleedDecayAt, currentTime, BleedDecayIntervalSeconds));

        if (count == 0)
            return;

        target.NextBleedDecayAt += count * BleedDecayIntervalSeconds;
        target.BleedStackCount -= count;

        if (!target.IsBleeding)
            target.NextBleedDecayAt = 0d;
    }

    private static double GetDueCount(double nextTime, double currentTime, double interval)
    {
        return currentTime < nextTime ? 0d : Math.Floor((currentTime - nextTime) / interval) + 1d;
    }

    private static bool CanApplyStatus(UnitVM target, double currentTime)
    {
        return target != null && target.CurHp > 0f && IsFinite(currentTime) && currentTime >= 0d;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static float ToDamage(double value)
    {
        return !IsFinite(value) || value <= 0d ? 0f : (float)Math.Min(value, float.MaxValue);
    }
}
