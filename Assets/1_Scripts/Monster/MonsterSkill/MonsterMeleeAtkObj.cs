using System.Collections.Generic;
using UnityEngine;

[UnityEngine.Scripting.Preserve]
public class MonsterMeleeAtkObj : MonoBehaviour
{
    private RuneDamageInfo _damageInfo;
    private MonsterRuneCombatActor _attacker;
    private readonly HashSet<PlayerRuneCombatActor> _hitPlayers = new();

    public void Init(RuneDamageInfo damageInfo)
    {
        _damageInfo = damageInfo;
        _attacker = GetComponentInParent<MonsterRuneCombatActor>();
        if (_attacker == null)
            Debug.LogError("몬스터 본체에 MonsterRuneCombatActor가 필요합니다.", this);
    }

    private void OnEnable()
    {
        _hitPlayers.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_attacker == null || !_attacker.IsServer || !_attacker.IsAlive)
            return;

        if (float.IsNaN(_damageInfo.RawDamage) || float.IsInfinity(_damageInfo.RawDamage)
            || _damageInfo.RawDamage <= 0f)
            return;

        PlayerRuneCombatActor target = other.GetComponentInParent<PlayerRuneCombatActor>();
        if (target == null || !_attacker.CanHit(target) || !_hitPlayers.Add(target))
            return;

        target.ReceiveDirectDamageOnServer(_damageInfo);
    }
}
