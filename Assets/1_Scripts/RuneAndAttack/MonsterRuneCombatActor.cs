using UnityEngine;

[DisallowMultipleComponent]
public class MonsterRuneCombatActor : RuneCombatActor
{
    private NetworkUnit _unit;
    protected override NetworkUnit Unit => _unit;

    private void Awake()
    {
        _unit = GetComponent<NetworkUnit>();
        if (!(_unit is MonsterNetwork) && !(_unit is BossNetwork))
        {
            Debug.LogError("몬스터 전투 컴포넌트에는 MonsterNetwork 또는 BossNetwork가 필요합니다.", this);
            _unit = null;
        }
    }

    public override bool IsAlive => IsSpawned && _unit != null && _unit.IsAlive;
    public override float AttackPower => _unit != null ? _unit.AttackPower : 0f;
    public override bool CanAttack => IsAlive && AttackPower > 0f
        && (_unit is MonsterNetwork monster ? monster.IsReady
            : _unit is BossNetwork boss && boss.IsReady);

    public override RuneDamageResult ReceiveDirectDamageOnServer(RuneDamageInfo damageInfo)
    {
        RuneDamageResult result = base.ReceiveDirectDamageOnServer(damageInfo);

        if (result.Accepted && result.ActualDamage > 0f && _unit is MonsterNetwork monster)
            monster.ApplyHitStaggerOnServer();

        return result;
    }

    public override bool CanHit(RuneCombatActor target)
    {
        return CanAttack && target != null && target != this && target.IsAlive
            && target is PlayerRuneCombatActor;
    }
}
