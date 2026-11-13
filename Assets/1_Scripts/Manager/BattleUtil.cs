using System;
using System.Collections.Generic;

public static class BattleUtil
{
    public const int MaxPoisonStacks = 5;
    public const double PoisonDurationSeconds = 10d;
    public const double PoisonTickIntervalSeconds = 1d;
    public const double PlayerPoisonMaxHpRatio = 0.03d;
    public const double MonsterPoisonDamageRatio = 0.05d;
    public const int MaxBossPoisonStacks = 3;
    public const double BossPoisonProtectionSeconds = 1d;
    public const double BossPoisonMaxHpRatio = 0.1d;
    public const double ProtectedBossPoisonMaxHpRatio = 0.02d;
    public const int BleedBurstStacks = 5;
    public const double BleedDamageRatio = 0.2d;
    public const double BleedDecayDelaySeconds = 3d;
    public const double BleedDecayIntervalSeconds = 1d;

    // TO USE : 피해 속성에 맞는 저항력을 적용한 수치를 계산할 때 사용. HP는 변경하지 않음.
    public static float CalculateDamage(float rawDamage, DamageType damageType,
        float physicalResistance, float fireResistance, float iceResistance, float electricalResistance)
    {
        float resistance;

        switch (damageType)
        {
            case DamageType.Physical:
                resistance = physicalResistance;
                break;
            case DamageType.Fire:
                resistance = fireResistance;
                break;
            case DamageType.Ice:
                resistance = iceResistance;
                break;
            case DamageType.Electric:
                resistance = electricalResistance;
                break;
            default:
                return 0f;
        }

        return CalculateResistedDamage(rawDamage, resistance);
    }

    // TO USE : 지정한 저항력 하나로 피해를 계산할 때 사용. HP는 변경하지 않음.
    public static float CalculateResistedDamage(float rawDamage, float resistance)
    {
        return ToDamage(ApplyResistance(rawDamage, resistance));
    }

    // TO USE : 현재 일반 독 중첩의 1회 피해량을 계산할 때 사용. 피해 발생 시각이나 HP는 변경하지 않음.
    public static float CalculatePoisonDamage(UnitStatusState state, float maxHp, float poisonResistance)
    {
        if (state == null || !IsFinite(maxHp) || maxHp < 0f)
            return 0f;

        double damage = Sum(state.PoisonStackValues);
        if (state.PoisonUsesMaxHp)
            damage *= maxHp;

        return ToDamage(ApplyResistance(damage, poisonResistance));
    }

    // TO USE : 현재 보스 독 중첩과 부적 여부로 1회 피해량을 계산할 때 사용. HP는 변경하지 않음.
    public static float CalculateBossPoisonDamage(UnitStatusState state, float maxHp,
        bool hasPoisonProtectionAmulet)
    {
        if (state == null || !IsFinite(maxHp) || maxHp < 0f)
            return 0f;

        return ToDamage(GetBossPoisonDamage(state.BossPoisonStackCount, maxHp, hasPoisonProtectionAmulet));
    }

    // TO USE : 플레이어 일반 독 상태를 계산하는 내부 처리용. 실제 게임에서 부여할 때는 NetworkUnit.ApplyPoisonOnServer 사용.
    public static bool ApplyPlayerPoison(UnitStatusState state, double currentTime)
    {
        return ApplyPoison(state, currentTime, (float)PlayerPoisonMaxHpRatio, true);
    }

    // TO USE : 몬스터 일반 독 상태를 계산하는 내부 처리용. 실제 게임에서 부여할 때는 NetworkUnit.ApplyPoisonOnServer 사용.
    public static bool ApplyMonsterPoison(UnitStatusState state, double currentTime, float rawAttackDamage)
    {
        if (!IsFinite(rawAttackDamage) || rawAttackDamage < 0f)
            return false;

        return ApplyPoison(state, currentTime, ToDamage(rawAttackDamage * MonsterPoisonDamageRatio), false);
    }

    private static bool ApplyPoison(UnitStatusState state, double currentTime, float stackValue, bool usesMaxHp)
    {
        if (!CanUpdate(state, currentTime))
            return false;

        AdvancePoison(state, currentTime);

        if (state.PoisonStackCount > 0 && state.PoisonUsesMaxHp != usesMaxHp)
            return false;

        if (state.PoisonStackCount == 0)
            state.NextPoisonTickAt = currentTime + PoisonTickIntervalSeconds;

        state.PoisonUsesMaxHp = usesMaxHp;
        state.PoisonExpiresAt = currentTime + PoisonDurationSeconds;

        List<float> stacks = state.PoisonStackValues;
        if (stacks.Count < MaxPoisonStacks)
        {
            stacks.Add(stackValue);
            return true;
        }

        int weakestIndex = 0;
        for (int i = 1; i < stacks.Count; i++)
        {
            if (stacks[i] < stacks[weakestIndex])
                weakestIndex = i;
        }

        if (stackValue > stacks[weakestIndex])
            stacks[weakestIndex] = stackValue;

        return true;
    }

    // TO USE : 보스 독 상태를 계산하는 내부 처리용. 실제 게임에서 부여할 때는 NetworkUnit.ApplyBossPoisonOnServer 사용.
    public static bool ApplyBossPoison(UnitStatusState state, double currentTime, bool canReceiveBossPoison)
    {
        if (!CanUpdate(state, currentTime) || !canReceiveBossPoison)
            return false;

        if (currentTime < state.BossPoisonProtectedUntil)
            return false;

        AdvanceBossPoison(state, currentTime);

        if (state.BossPoisonStackCount == 0)
            state.NextBossPoisonTickAt = currentTime + PoisonTickIntervalSeconds;

        state.BossPoisonProtectedUntil = currentTime + BossPoisonProtectionSeconds;
        state.BossPoisonStackCount = Math.Min(MaxBossPoisonStacks, state.BossPoisonStackCount + 1);
        return true;
    }

    // TO USE : 보스 독 중첩을 줄이는 내부 처리용. 실제 게임에서는 NetworkUnit.RemoveBossPoisonStacksOnServer 사용.
    public static bool RemoveBossPoisonStacks(UnitStatusState state, double currentTime, int amount)
    {
        if (!CanUpdate(state, currentTime) || amount <= 0 || state.BossPoisonStackCount == 0)
            return false;

        AdvanceBossPoison(state, currentTime);
        state.BossPoisonStackCount = Math.Max(0, state.BossPoisonStackCount - amount);
        if (state.BossPoisonStackCount == 0)
            state.NextBossPoisonTickAt = 0d;
        return true;
    }

    // TO USE : 출혈 상태를 갱신하고 폭발 피해량을 반환하는 내부 처리용. HP는 변경하지 않음.
    // 실제 게임에서 부여할 때는 NetworkUnit.ApplyBleedOnServer 사용.
    public static float ApplyBleed(UnitStatusState state, double currentTime,
        float rawAttackDamage, bool canReceiveBleed)
    {
        if (!CanUpdate(state, currentTime) || !canReceiveBleed
            || !IsFinite(rawAttackDamage) || rawAttackDamage < 0f)
            return 0f;

        UpdateBleed(state, currentTime);
        state.BleedStackDamages.Add(ToDamage(rawAttackDamage * BleedDamageRatio));

        if (state.BleedStackCount >= BleedBurstStacks)
        {
            float burstDamage = ToDamage(Sum(state.BleedStackDamages));
            ClearBleed(state);
            return burstDamage;
        }

        state.NextBleedDecayAt = currentTime + BleedDecayDelaySeconds;
        return 0f;
    }

    // TO USE : 서버 내부에서 상태이상 시간을 갱신하고 이번에 적용할 독 피해 합계를 받을 때 사용.
    // currentTime은 서버의 현재 시각(초). 상태는 갱신하지만 HP와 NetworkVariable은 호출한 쪽에서 반영.
    public static float UpdateStatusEffects(UnitStatusState state, double currentTime,
        float maxHp, float poisonResistance, bool hasPoisonProtectionAmulet)
    {
        if (!CanUpdate(state, currentTime) || !IsFinite(maxHp) || maxHp < 0f
            || !IsFinite(poisonResistance))
            return 0f;

        AdvancePoison(state, currentTime);
        AdvanceBossPoison(state, currentTime);
        UpdateBleed(state, currentTime);

        double poisonDamage = state.PendingPoisonDamage + maxHp * state.PendingPoisonMaxHpRatio;
        double damage = ApplyResistance(poisonDamage, poisonResistance);
        damage += GetBossPoisonDamage(state.PendingBossPoisonStackTicks, maxHp, hasPoisonProtectionAmulet);

        state.PendingPoisonDamage = 0d;
        state.PendingPoisonMaxHpRatio = 0d;
        state.PendingBossPoisonStackTicks = 0d;
        return ToDamage(damage);
    }

    // TO USE : 일반 독의 계산 상태만 초기화할 때 사용. 네트워크 중첩 동기화는 별도 처리 필요.
    public static void ClearPoison(UnitStatusState state)
    {
        if (state == null)
            return;

        state.PoisonStackValues.Clear();
        state.PoisonUsesMaxHp = false;
        state.PoisonExpiresAt = 0d;
        state.NextPoisonTickAt = 0d;
        state.PendingPoisonDamage = 0d;
        state.PendingPoisonMaxHpRatio = 0d;
    }

    // TO USE : 보스 독의 계산 상태만 초기화할 때 사용. 네트워크 중첩 동기화는 별도 처리 필요.
    public static void ClearBossPoison(UnitStatusState state)
    {
        if (state == null)
            return;

        state.BossPoisonStackCount = 0;
        state.NextBossPoisonTickAt = 0d;
        state.PendingBossPoisonStackTicks = 0d;
        state.BossPoisonProtectedUntil = 0d;
    }

    // TO USE : 출혈의 계산 상태만 초기화할 때 사용. 네트워크 중첩 동기화는 별도 처리 필요.
    public static void ClearBleed(UnitStatusState state)
    {
        if (state == null)
            return;

        state.BleedStackDamages.Clear();
        state.NextBleedDecayAt = 0d;
    }

    // TO USE : 모든 상태이상의 계산 상태만 초기화할 때 사용. 실제 게임의 전체 해제는 NetworkUnit.ClearStatusEffectsOnServer 사용.
    public static void ClearStatusEffects(UnitStatusState state)
    {
        state?.Clear();
    }

    private static void AdvancePoison(UnitStatusState state, double currentTime)
    {
        if (state.PoisonStackCount == 0)
            return;

        double endTime = Math.Min(currentTime, state.PoisonExpiresAt);
        double tickCount = GetDueCount(state.NextPoisonTickAt, endTime, PoisonTickIntervalSeconds);
        double damage = tickCount * Sum(state.PoisonStackValues);

        if (state.PoisonUsesMaxHp)
            state.PendingPoisonMaxHpRatio += damage;
        else
            state.PendingPoisonDamage += damage;

        state.NextPoisonTickAt += tickCount * PoisonTickIntervalSeconds;

        if (currentTime >= state.PoisonExpiresAt)
        {
            state.PoisonStackValues.Clear();
            state.PoisonUsesMaxHp = false;
            state.PoisonExpiresAt = 0d;
            state.NextPoisonTickAt = 0d;
        }
    }

    private static void AdvanceBossPoison(UnitStatusState state, double currentTime)
    {
        if (state.BossPoisonStackCount == 0)
            return;

        double tickCount = GetDueCount(state.NextBossPoisonTickAt, currentTime, PoisonTickIntervalSeconds);
        state.PendingBossPoisonStackTicks += tickCount * state.BossPoisonStackCount;
        state.NextBossPoisonTickAt += tickCount * PoisonTickIntervalSeconds;
    }

    private static void UpdateBleed(UnitStatusState state, double currentTime)
    {
        if (state.BleedStackCount == 0)
            return;

        int count = (int)Math.Min(state.BleedStackCount,
            GetDueCount(state.NextBleedDecayAt, currentTime, BleedDecayIntervalSeconds));

        if (count == 0)
            return;

        state.BleedStackDamages.RemoveRange(0, count);
        state.NextBleedDecayAt = state.BleedStackCount == 0
            ? 0d
            : state.NextBleedDecayAt + count * BleedDecayIntervalSeconds;
    }

    private static double ApplyResistance(double damage, float resistance)
    {
        if (!IsFinite(damage) || damage <= 0d || !IsFinite(resistance))
            return 0d;

        double clampedResistance = Math.Max(-1d, Math.Min(1d, resistance));
        return damage * (1d - clampedResistance);
    }

    private static double GetBossPoisonDamage(double stackTicks, float maxHp, bool hasPoisonProtectionAmulet)
    {
        double ratio = hasPoisonProtectionAmulet ? ProtectedBossPoisonMaxHpRatio : BossPoisonMaxHpRatio;
        return maxHp * ratio * stackTicks;
    }

    private static double Sum(List<float> values)
    {
        double total = 0d;
        for (int i = 0; i < values.Count; i++)
            total += values[i];

        return total;
    }

    private static double GetDueCount(double nextTime, double currentTime, double interval)
    {
        return currentTime < nextTime ? 0d : Math.Floor((currentTime - nextTime) / interval) + 1d;
    }

    private static bool CanUpdate(UnitStatusState state, double currentTime)
    {
        return state != null && IsFinite(currentTime) && currentTime >= 0d;
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
