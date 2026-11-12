using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerNetworkStats))]
public class PlayerRuneCombatActor : RuneCombatActor
{
    private PlayerNetworkStats _stats;
    private PlayerRuneAttackController _attackController;

    private bool _warnedMissingStatusReceiver;

    private void Awake()
    {
        _stats = GetComponent<PlayerNetworkStats>();
        _attackController = GetComponent<PlayerRuneAttackController>();
    }

    public override bool IsAlive => IsSpawned && _stats != null && _stats.IsAlive;

    // 현재는 생존 여부만 검사.
    // 기절 등 서버의 행동 제한 상태는 이후 여기에 추가.
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
        if (IsAlive == false || target == null || target == this || target.IsAlive == false)
        {
            return false;
        }

        // 현재는 플레이어가 몬스터만 공격하도록 처리.
        return target is MonsterRuneCombatActor;
    }

    public override RuneDamageResult ReceiveDirectDamageOnServer(RuneDamageInfo damageInfo)
    {
        if (IsServer == false || IsAlive == false)
        {
            return default;
        }

        // 플레이어 저항력 데이터가 아직 없으므로
        // 현재는 저항력 0으로 처리.
        float finalDamage = damageInfo.RawDamage;

        bool accepted = _stats.TryTakeDamageOnServer(finalDamage, out float actualDamage);

        if (accepted && _attackController != null)
        {
            // 강타 룬이 있다면 이 메서드에서 공격 중단을 막음.
            // 사망 시에는 공격 컨트롤러의 IsAlive 검사로 중단됨.
            _attackController.TryInterruptOnServer();
        }

        return new RuneDamageResult
        {
            Accepted = accepted,
            ActualDamage = actualDamage
        };
    }

    public override void HealOnServer(float amount)
    {
        if (IsServer == false || IsAlive == false)
        {
            return;
        }

        _stats.HealOnServer(amount);
    }

    public override void ApplyStatusOnServer(string statusProfileId, RuneCombatActor attacker)
    {
        if (IsServer == false || IsAlive == false || string.IsNullOrEmpty(statusProfileId))
        {
            return;
        }

        IRuneStatusReceiver receiver = GetComponent<IRuneStatusReceiver>();

        if (receiver == null)
        {
            if (_warnedMissingStatusReceiver == false)
            {
                Debug.LogWarning("플레이어 상태이상 처리 컴포넌트가 없습니다.", this);

                _warnedMissingStatusReceiver = true;
            }

            return;
        }

        receiver.ApplyStatusOnServer(statusProfileId, attacker);
    }
}