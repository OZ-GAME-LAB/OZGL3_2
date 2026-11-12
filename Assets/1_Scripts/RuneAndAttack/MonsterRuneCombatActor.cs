using UnityEngine;

[RequireComponent(typeof(MonsterNetwork))]
public class MonsterRuneCombatActor : RuneCombatActor
{
    private MonsterNetwork _monsterNetwork;
    private bool _warnedMissingStatusReceiver;

    private void Awake()
    {
        _monsterNetwork = GetComponent<MonsterNetwork>();
    }

    public override bool IsAlive => IsSpawned && _monsterNetwork != null && _monsterNetwork.IsAlive;

    public override bool CanAttack => IsAlive && _monsterNetwork.CanAttack;

    // 현재 MonsterNetwork.AttackPower는 서버에서 초기화한 값.
    public override float AttackPower => _monsterNetwork != null ? _monsterNetwork.AttackPower : 0f;

    public override bool CanHit(RuneCombatActor target)
    {
        if (CanAttack == false || target == null || target == this || target.IsAlive == false)
        {
            return false;
        }

        // 현재 프로젝트 기준: 몬스터는 플레이어를 공격.
        // 플레이어 연결 컴포넌트와 ControllerView는 같은 루트에 배치.
        return target.GetComponent<PlayerControllerView>() != null;
    }

    public override RuneDamageResult ReceiveDirectDamageOnServer(RuneDamageInfo damageInfo)
    {
        if (IsServer == false || IsAlive == false)
        {
            return default;
        }

        // 현재 MonsterNetwork에는 속성 저항력 계산이 없으므로
        // 우선 저항력 0인 상태로 연결.
        float finalDamage = damageInfo.RawDamage;

        bool accepted = _monsterNetwork.TryTakeDamageOnServer(
            finalDamage,
            out float actualDamage);

        return new RuneDamageResult
        {
            Accepted = accepted,
            ActualDamage = actualDamage
        };
    }

    public override void HealOnServer(float amount)
    {
        if (!IsServer || !IsAlive)
            return;

        _monsterNetwork.HealOnServer(amount);
    }

    public override void ApplyStatusOnServer(
        string statusProfileId,
        RuneCombatActor attacker)
    {
        if (!IsServer ||
            !IsAlive ||
            string.IsNullOrEmpty(statusProfileId))
        {
            return;
        }

        // 상태이상 컴포넌트도 같은 루트에 배치.
        IRuneStatusReceiver receiver =
            GetComponent<IRuneStatusReceiver>();

        if (receiver == null)
        {
            if (!_warnedMissingStatusReceiver)
            {
                Debug.LogWarning(
                    "상태이상 처리 컴포넌트가 없어 독·출혈을 적용하지 못했습니다.",
                    this);

                _warnedMissingStatusReceiver = true;
            }

            return;
        }

        receiver.ApplyStatusOnServer(statusProfileId, attacker);
    }
}