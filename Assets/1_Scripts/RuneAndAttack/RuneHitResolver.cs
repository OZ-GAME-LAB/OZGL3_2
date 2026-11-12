using UnityEngine;

public static class RuneHitResolver 
{
    // TO USE : 서버에서 플레이어 룬 공격이 적중했을 때 사용. 피해·룬 상태이상·흡혈을 함께 처리.
    // attackPower에는 룬 배율을 적용하기 전 공격력을 전달.
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

            target.ApplyStatusOnServer(statusId, damageInfo.RawDamage);
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
