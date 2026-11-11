using System.Collections.Generic;
using UnityEngine;

public class BossThornHitbox : MonoBehaviour
{
    private BossNetwork _owner;
    private float _damage;

    private readonly HashSet<PlayerRuneCombatActor> _hitPlayers = new();

    public void Initialize(BossNetwork owner, float damage)
    {
        _owner = owner;
        _damage = damage;
    }

    private void OnEnable()
    {
        _hitPlayers.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_owner == null || !_owner.IsServer || !_owner.IsSpawned || !_owner.IsReady)
            return;

        if (float.IsNaN(_damage) || float.IsInfinity(_damage) || _damage <= 0f)
            return;

        if (other.isTrigger)
            return;

        PlayerRuneCombatActor player = other.GetComponentInParent<PlayerRuneCombatActor>();

        if (player == null || !player.IsAlive)
            return;

        if (!_hitPlayers.Add(player))
            return;

        RuneDamageInfo damageInfo = new RuneDamageInfo
        {
            Attacker = null,
            RawDamage = _damage,
            Element = RuneAttackElement.Physical
        };

        RuneDamageResult result = player.ReceiveDirectDamageOnServer(damageInfo);

        if (result.Accepted)
        {
            Debug.Log($"가시 적중: {player.name}, 실제 피해: {result.ActualDamage}", player);
        }
    }
}
