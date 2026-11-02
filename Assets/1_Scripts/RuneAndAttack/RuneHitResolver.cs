using UnityEngine;

public static class RuneHitResolver 
{
    public static bool TryHitOnServer(RuneCombatActor attacker, RuneCombatActor target, AttackProfile profile, float attackPower)
    {
        if (attacker == null ||
            attacker.IsServer == false ||
            attacker.IsSpawned == false ||
            target == null ||
            target.IsSpawned == false ||
            target.IsAlive == false ||
            target == attacker ||
            profile == null)
        {
            return false;
        }

        if (attacker.CanHit(target) == false)
        {
            return false;
        }

        var damageInfo = new RuneDamageInfo
        {
            Attacker = attacker,
            RawDamage = attackPower * profile._damageMultiple,
            Element = profile._attackElement
        };

        RuneDamageResult result = target.ReceiveDirectDamageOnServer(damageInfo);

        if (result.Accepted == false)
        {
            return false;
        }

        // 직접 피해로 사망한 대상에는 상태이상을 추가하지 않음.
        foreach (string statusId in profile._onHitStatusProfileIdList)
        {
            if (target == null || target.IsSpawned == false || target.IsAlive == false)
            {
                break;
            }

            target.ApplyStatusOnServer(statusId, attacker);
        }

        // 박쥐: 실제 체력 감소량에 비례해서 회복.
        if (attacker != null &&
            attacker.IsSpawned &&
            attacker.IsAlive &&
            result.ActualDamage > 0f &&
            profile._healAttackRatio > 0f)
        {
            float healAmount = result.ActualDamage * profile._healAttackRatio;

            attacker.HealOnServer(healAmount);
        }

        return true;
    }
}
