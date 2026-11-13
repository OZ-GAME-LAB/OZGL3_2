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

    public override bool CanHit(RuneCombatActor target)
    {
        return IsAlive &&
               target != null &&
               target != this &&
               target.IsAlive &&
               target is MonsterRuneCombatActor;
    }


    // 직접 공격으로 실제 HP가 감소했을 때만 공격 중단 요청.
    // 독의 주기적 피해는 NetworkUnit에서 별도로 처리.
    public override RuneDamageResult ReceiveDirectDamageOnServer(RuneDamageInfo damageInfo)
    {
        RuneDamageResult result = base.ReceiveDirectDamageOnServer(damageInfo);

        if (result.Accepted && result.ActualDamage > 0f && _attackController != null)
        {
            _attackController.TryInterruptOnServer();
        }

        return result;
    }
}
