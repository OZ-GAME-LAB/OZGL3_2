using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerNetworkStats))]
public class PlayerRuneCombatActor : RuneCombatActor
{
    private PlayerNetworkStats _stats;
    private PlayerRuneAttackController _attackController;

    protected override NetworkUnit Unit => _stats;

    private void Awake()
    {
        _stats = GetComponent<PlayerNetworkStats>();
        _attackController = GetComponent<PlayerRuneAttackController>();
    }

    public override bool IsAlive => IsSpawned && _stats != null && _stats.IsAlive;
    public override bool CanAttack => IsAlive && isActiveAndEnabled;
    public override float AttackPower => _stats != null ? _stats.AttackPower : 0f;

    public override float MaxHp => _stats != null ? _stats.MaxHp : 0f;

    public override float PoisonResistance => _stats != null ? _stats.PoisonResistance : 0f;

    public override RuneDamageResult ReceiveStatusDamageOnServer(float finalDamage)
    {
        if (IsServer == false || IsAlive == false)
        {
            return default;
        }

        bool accepted = _stats.TryTakeDamageOnServer(finalDamage, out float actualDamage);

        return new RuneDamageResult
        {
            Accepted = accepted,
            ActualDamage = actualDamage
        };
    }

    public override bool CanHit(RuneCombatActor target)
    {
        return IsAlive && target != null && target != this && target.IsAlive
            && target is MonsterRuneCombatActor;
    }

    // TO USE : 서버에서 플레이어에게 직접 타격을 적용할 때 사용. 실제 HP가 감소하면 공격 중단도 요청.
    // 독의 주기적 피해는 NetworkUnit이 처리하므로 이 함수를 호출하지 않음.
    public override RuneDamageResult ReceiveDirectDamageOnServer(RuneDamageInfo damageInfo)
    {
        RuneDamageResult result = base.ReceiveDirectDamageOnServer(damageInfo);
        if (result.Accepted && result.ActualDamage > 0f && _attackController != null)
            _attackController.TryInterruptOnServer();
        return result;
    }
}
