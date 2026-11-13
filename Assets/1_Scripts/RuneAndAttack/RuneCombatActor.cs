using Unity.Netcode;
using UnityEngine;

public struct RuneDamageInfo
{
    public RuneCombatActor Attacker;
    public float RawDamage;
    public DamageType Element;
    public bool ApplyPoison;
    public bool ApplyBossPoison;
}

public struct RuneDamageResult
{
    public bool Accepted;
    public float ActualDamage;
}

public abstract class RuneCombatActor : NetworkBehaviour
{
    protected abstract NetworkUnit Unit { get; }
    public abstract bool IsAlive { get; }
    public abstract bool CanAttack { get; }
    public abstract float AttackPower { get; }
    public abstract bool CanHit(RuneCombatActor target);

    // TO USE : 서버에서 공격이 적중한 대상의 Actor에 호출하는 직접 피해 함수.
    // RawDamage에는 저항력 적용 전 피해를 전달. 속성 저항력·독 부여·대상별 피격 반응을 연결.
    public virtual RuneDamageResult ReceiveDirectDamageOnServer(RuneDamageInfo damageInfo)
    {
        if (!IsServer || !IsAlive || Unit == null)
            return default;

        bool accepted = Unit.TryReceiveHitOnServer(damageInfo.RawDamage, damageInfo.Element,
            damageInfo.ApplyPoison, damageInfo.ApplyBossPoison, out float actualDamage);
        return new RuneDamageResult { Accepted = accepted, ActualDamage = actualDamage };
    }

    // TO USE : 서버에서 대상 Actor를 통해 회복할 때 사용. 실제 HP 처리는 대상 NetworkUnit에 전달.
    public virtual void HealOnServer(float amount)
    {
        if (IsServer && IsAlive && Unit != null)
            Unit.HealOnServer(amount);
    }

    // TO USE : 서버에서 룬의 상태이상 ID로 독·출혈을 부여할 때 사용. rawAttackDamage는 저항력 적용 전 타격 피해.
    // status_poison_enemy는 일반 독, status_bleed_enemy는 출혈. 직접 타격 피해는 별도로 처리.
    public void ApplyStatusOnServer(string statusProfileId, float rawAttackDamage)
    {
        if (!IsServer || !IsAlive || Unit == null || string.IsNullOrWhiteSpace(statusProfileId))
            return;

        switch (statusProfileId)
        {
            case "status_poison_enemy":
                Unit.ApplyPoisonOnServer(rawAttackDamage);
                break;
            case "status_bleed_enemy":
                Unit.ApplyBleedOnServer(rawAttackDamage);
                break;
            default:
                Debug.LogWarning($"지원하지 않는 상태이상 ID입니다: {statusProfileId}", this);
                break;
        }
    }
}
