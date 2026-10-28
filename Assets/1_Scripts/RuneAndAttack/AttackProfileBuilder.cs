using System;
using System.Collections.Generic;

public static class AttackProfileBuilder 
{
     public static bool TryBuild(IReadOnlyList<RuneData> slots, out AttackProfile profile, out string error)
    {
        profile = null;
        error = null;

        if (slots == null)
        {
            error = "룬 슬롯 목록이 null입니다.";
            return false;
        }

        var result = new AttackProfile();
        var equippedIds = new HashSet<string>();

        // 향후 타격 수를 변경하는 룬이 추가되면
        // 결합 규칙을 별도로 정해야 합니다.
        bool hasHitCountOverride = false;

        for (int i = 0; i < slots.Count; i++)
        {
            RuneData rune = slots[i];

            // 빈 슬롯
            if (rune == null)
                continue;

            if (string.IsNullOrEmpty(rune.Id))
            {
                error = $"{i}번 슬롯의 룬 ID가 없습니다.";
                return false;
            }

            if (!equippedIds.Add(rune.Id))
            {
                error = $"동일한 룬은 중복 장착할 수 없습니다: {rune.Id}";
                return false;
            }

            if (IsPositiveFinite(rune.DamageMultiplier) == false || IsPositiveFinite(rune.AttackSpeedMultiplier) == false || IsPositiveFinite(rune.RangeMultiplier) == false || IsPositiveFinite(rune.ProjectileSizeMultiplier) == false )
            {
                error = $"룬의 배율이 올바르지 않습니다: {rune.Id}";
                return false;
            }

            switch (rune.RuneType)
            {
                case "Type":
                    {
                        switch (rune.EffectType)
                        {
                            case "TripleHit":
                                if (rune.HitCount <= 0 || hasHitCountOverride)
                                {
                                    error = $"타격 수 설정을 확인하세요: {rune.Id}";
                                    return false;
                                }

                                result._hitCount = rune.HitCount;
                                hasHitCountOverride = true;
                                break;

                            case "Projectile":
                                result._useProjectile = true;
                                result._attackPrefabPath = rune.AttackPrefabPath;
                                break;

                            case "Heavy":
                                break;

                            default:
                                error = $"지원하지 않는 유형 효과: {rune.EffectType}";
                                return false;
                        }

                        break;
                    }

                case "Element":
                    {
                        if (rune.EffectType != "ChangeElement" || Enum.TryParse(rune.DamageType, out RuneAttackElement element) == false || Enum.IsDefined(typeof(RuneAttackElement), element) == false)
                        {
                            error = $"속성 룬 설정이 올바르지 않습니다: {rune.Id}";
                            return false;
                        }

                        // 왼쪽부터 읽으므로 가장 오른쪽 속성이 최종 적용됩니다.
                        result._attackElement = element;
                        break;
                    }

                case "Support":
                    {
                        switch (rune.EffectType)
                        {
                            case "ScaleAttack":
                                break;

                            case "ApplyStatus":
                                if (rune.Trigger != "OnHit" ||
                                    string.IsNullOrEmpty(rune.StatusProfileId))
                                {
                                    error = $"상태이상 룬 설정을 확인하세요: {rune.Id}";
                                    return false;
                                }

                                if (!result._onHitStatusProfileIdList.Contains(rune.StatusProfileId))
                                {
                                    result._onHitStatusProfileIdList.Add(rune.StatusProfileId);
                                }
                                break;

                            case "HealOnHit":
                                if (rune.Trigger != "OnHit" || IsPositiveFinite(rune.HealAttackRatio) == false || rune.HealAttackRatio > 1f)
                                {
                                    error = $"회복 룬 설정을 확인하세요: {rune.Id}";
                                    return false;
                                }

                                // 현재 회복 룬은 박쥐 하나입니다.
                                result._healAttackRatio = rune.HealAttackRatio;
                                break;

                            default:
                                error = $"지원하지 않는 보조 효과: {rune.EffectType}";
                                return false;
                        }

                        break;
                    }

                default:
                    error = $"지원하지 않는 룬 분류: {rune.RuneType}";
                    return false;
            }

            result._damageMultiple = result._damageMultiple * rune.DamageMultiplier;
            result._attackSpeedMultiple = result._attackSpeedMultiple * rune.AttackSpeedMultiplier;
            result._rangeMultiple = result._rangeMultiple * rune.RangeMultiplier;
            result._projectileSizeMultiple = result._projectileSizeMultiple * rune.ProjectileSizeMultiplier;

            result._preventAttackInterrupt = result._preventAttackInterrupt | rune.PreventAttackInterrupt; //하나라도 공격 중단 방지 있으면 결과가 true로 함.
        }

        if (!IsPositiveFinite(result._damageMultiple) ||
            !IsPositiveFinite(result._attackSpeedMultiple) ||
            !IsPositiveFinite(result._rangeMultiple) ||
            !IsPositiveFinite(result._projectileSizeMultiple))
        {
            error = "최종 공격 배율이 유효 범위를 벗어났습니다.";
            return false;
        }

        profile = result;
        return true;
    }

    private static bool IsPositiveFinite(float value)
    {
        return (value > 0f && float.IsNaN(value) == false && float.IsInfinity(value) == false);
    }
}
