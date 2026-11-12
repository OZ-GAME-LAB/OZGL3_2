using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[UnityEngine.Scripting.Preserve]
public class MonsterMeleeAtkObj : MonoBehaviour
{
    private float _damage;
    private MonsterRuneCombatActor _attacker;

    [Header("적중 시 상태이상 없으면 비우기")]
    [SerializeField] private string _onHitStatusProfileId;

    private readonly HashSet<PlayerRuneCombatActor> _hitPlayers = new();

    public void Init(float damage)
    {
        _damage = damage;

        // 현재 공격 오브젝트는 몬스터 본체 아래에 생성됨.
        _attacker = GetComponentInParent<MonsterRuneCombatActor>();

        if (_attacker == null)
        {
            Debug.LogError("몬스터 본체에 MonsterRuneCombatActor가 필요합니다.", this);
        }
    }

    private void OnEnable()
    {
        // 공격 오브젝트를 켤 때마다 새 공격으로 취급.
        _hitPlayers.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_attacker == null || _attacker.IsServer == false || _attacker.IsAlive == false)
        {
            return;
        }

        if (float.IsNaN(_damage) || _damage <= 0f)
        {
            return;
        }

        PlayerRuneCombatActor target = other.GetComponentInParent<PlayerRuneCombatActor>();

        if (target == null || _hitPlayers.Contains(target) || _attacker.CanHit(target) == false)
        {
            return;
        }

        // 한 공격에서 같은 플레이어의 여러 콜라이더를 중복 처리하지 않음.
        _hitPlayers.Add(target);

        var damageInfo = new RuneDamageInfo
        {
            Attacker = _attacker,
            RawDamage = _damage,
            Element = RuneAttackElement.Physical
        };

        //ApplyDamage를 사용하지 않고 ReceiveDirectDamageOnServer를 호출하도록 수정했습니다(서버 때문에)
        //target.ReceiveDirectDamageOnServer(damageInfo);

        RuneDamageResult result = target.ReceiveDirectDamageOnServer(damageInfo);

        if (result.Accepted &&
            target != null &&
            target.IsSpawned &&
            target.IsAlive &&
            string.IsNullOrEmpty(_onHitStatusProfileId) == false)
        {
            target.ApplyStatusOnServer(_onHitStatusProfileId, _attacker);
        }

        /*
        if (_damage <= 0f)
            return;

        var networkManager = Unity.Netcode.NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsServer)
            return;

        PlayerControllerView player = other.GetComponentInParent<PlayerControllerView>();

        if (player == null)
            return;

        if (ServiceManager.Inst == null || ServiceManager.Inst.PlayerService == null)
            return;

        ApplyDamage();
        */
    }

    /*
    private void ApplyDamage()
    {
        ServiceManager.Inst.PlayerService.DecreaseHp(_damage);
    }
    */
}
