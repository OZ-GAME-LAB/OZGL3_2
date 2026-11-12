using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class StatusEffectController : NetworkBehaviour, IRuneStatusReceiver
{
    private RuneCombatActor _actor;

    // 표시용 상태만 동기화.
    // 피해 계산과 시간 관리는 서버에서만 수행.
    private NetworkVariable<bool> _poisoned = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<int> _bleedStacks = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<int> _bossPoisonStacks = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private double _poisonEndTime;
    private double _nextPoisonTickTime;

    private double _nextBleedDecayTime;

    private double _bossPoisonEndTime;
    private double _nextBossPoisonTickTime;

    // 장비 시스템 연결 전에는 기본 false.
    // 클라이언트가 임의로 변경할 수 없도록 서버 메서드로만 설정.
    private bool _hasPoisonProtectionCharm;

    public bool IsPoisoned => _poisoned.Value;
    public int BleedStacks => _bleedStacks.Value;
    public int BossPoisonStacks => _bossPoisonStacks.Value;

    private bool CanProcess =>
        IsServer &&
        IsSpawned &&
        isActiveAndEnabled &&
        _actor != null &&
        _actor.IsSpawned &&
        _actor.IsAlive;

    private void Awake()
    {
        _actor = GetComponent<RuneCombatActor>();

        if (_actor == null)
        {
            Debug.LogError("같은 루트에 RuneCombatActor가 필요합니다.", this);
        }
    }

    public override void OnNetworkSpawn()
    {
        ResetLocalState();

        if (IsServer)
        {
            ClearStatusesOnServer();
        }
    }

    public override void OnNetworkDespawn()
    {
        // Despawn 이후에는 NetworkVariable을 수정하지 않음.
        ResetLocalState();
    }

    private void OnDisable()
    {
        if (IsServer && IsSpawned)
        {
            ClearStatusesOnServer();
        }
    }

    private void ResetLocalState()
    {
        _poisonEndTime = 0d;
        _nextPoisonTickTime = 0d;

        _nextBleedDecayTime = 0d;

        _bossPoisonEndTime = 0d;
        _nextBossPoisonTickTime = 0d;

        _hasPoisonProtectionCharm = false;
    }

    private void Update()
    {
        if (IsServer == false || IsSpawned == false)
            return;

        if (_actor == null || !_actor.IsSpawned || !_actor.IsAlive)
        {
            ClearStatusesOnServer();
            return;
        }

        ProcessStatusesOnServer(NetworkManager.ServerTime.Time);
    }

    public void ApplyStatusOnServer(string statusProfileId, RuneCombatActor attacker)
    {
        if (CanProcess == false || string.IsNullOrEmpty(statusProfileId))
        {
            return;
        }

        bool isPlayer = _actor is PlayerRuneCombatActor;
        bool isMonster = _actor is MonsterRuneCombatActor;

        // 대상에게 적용 가능한 상태인지 먼저 검사.
        switch (statusProfileId)
        {
            case StatusEffectRules.PoisonId:
                if (isPlayer == false && isMonster == false)
                {
                    return;
                }
                break;

            case StatusEffectRules.BleedId:
                if (isMonster == false || _actor.CanReceiveBleed == false)
                {
                    return;
                }
                break;

            case StatusEffectRules.BossPoisonId:
                if (isPlayer == false)
                {
                    return;
                }
                break;

            default:
                Debug.LogWarning($"지원하지 않는 상태이상 ID: {statusProfileId}", this);
                return;
        }

        double now = NetworkManager.ServerTime.Time;

        // 이미 도래한 피해와 중첩 감소부터 처리.
        // 만료된 상태를 실수로 연장하거나,
        // 과거의 피해에 새 중첩을 적용하지 않도록 함.
        ProcessStatusesOnServer(now);

        if (CanProcess == false)
        {
            return;
        }

        switch (statusProfileId)
        {
            case StatusEffectRules.PoisonId:
                ApplyPoison(now);
                break;

            case StatusEffectRules.BleedId:
                ApplyBleed(now);
                break;

            case StatusEffectRules.BossPoisonId:
                ApplyBossPoison(now);
                break;
        }

        // 현재 attacker는 인터페이스 호환을 위해 받음.
        // 상태이상 처치 기여도·보상 귀속은 아직 구현하지 않음.
    }

    private void ApplyPoison(double now)
    {
        if (_poisoned.Value == false)
        {
            _poisoned.Value = true;

            // 첫 피해는 적용 즉시가 아닌 1초 후.
            _nextPoisonTickTime = now + StatusEffectRules.TickIntervalSeconds;
        }

        // 재적용 시 다음 피해 시각은 유지.
        _poisonEndTime = now + StatusEffectRules.DurationSeconds;
    }

    private void ApplyBleed(double now)
    {
        // 이미 5중첩인 상태에서 다음 출혈 공격을 받으면 폭발.
        if (_bleedStacks.Value >= StatusEffectRules.BleedMaxStacks)
        {
            _bleedStacks.Value = 0;
            _nextBleedDecayTime = 0d;

            float damage = _actor.MaxHp * StatusEffectRules.BleedMaxHpRatio;

            DealStatusDamage(damage);
            return;
        }

        _bleedStacks.Value++;

        // 출혈 공격을 받을 때마다 감소 시작 시각 갱신.
        _nextBleedDecayTime = now + StatusEffectRules.BleedDecayIntervalSeconds;
    }

    private void ApplyBossPoison(double now)
    {
        if (_bossPoisonStacks.Value == 0)
        {
            _nextBossPoisonTickTime = now + StatusEffectRules.TickIntervalSeconds;
        }

        _bossPoisonStacks.Value = Mathf.Min(_bossPoisonStacks.Value + 1, StatusEffectRules.BossPoisonMaxStacks);

        // 전체 중첩의 만료 시각을 함께 갱신.
        // 최대 중첩에서도 지속시간은 갱신.
        _bossPoisonEndTime = now + StatusEffectRules.DurationSeconds;
    }

    private void ProcessStatusesOnServer(double now)
    {
        if (CanProcess == false)
        {
            return;
        }

        // 독 두 종류는 예정된 피해 시각 순서로 처리.
        // 같은 시각이면 일반 독을 먼저 처리.
        while (CanProcess)
        {
            bool poisonDue = _poisoned.Value && _nextPoisonTickTime <= now && _nextPoisonTickTime <= _poisonEndTime;

            bool bossPoisonDue = _bossPoisonStacks.Value > 0 && _nextBossPoisonTickTime <= now && _nextBossPoisonTickTime <= _bossPoisonEndTime;

            if (poisonDue == false && bossPoisonDue == false)
                break;

            if (poisonDue && (bossPoisonDue == false || _nextPoisonTickTime <= _nextBossPoisonTickTime))
            {
                _nextPoisonTickTime += StatusEffectRules.TickIntervalSeconds;

                ApplyPoisonTick();
            }
            else
            {
                _nextBossPoisonTickTime += StatusEffectRules.TickIntervalSeconds;

                ApplyBossPoisonTick();
            }
        }

        // 피해 처리 중 죽어서 Despawn될 수 있음.
        if (CanProcess == false)
            return;

        // 10초 시점의 마지막 피해를 처리한 다음 만료.
        if (_poisoned.Value && now >= _poisonEndTime)
        {
            _poisoned.Value = false;
            _poisonEndTime = 0d;
            _nextPoisonTickTime = 0d;
        }

        if (_bossPoisonStacks.Value > 0 && now >= _bossPoisonEndTime)
        {
            _bossPoisonStacks.Value = 0;
            _bossPoisonEndTime = 0d;
            _nextBossPoisonTickTime = 0d;
        }

        while (_bleedStacks.Value > 0 && now >= _nextBleedDecayTime)
        {
            _bleedStacks.Value--;

            _nextBleedDecayTime += StatusEffectRules.BleedDecayIntervalSeconds;
        }

        if (_bleedStacks.Value == 0)
        {
            _nextBleedDecayTime = 0d;
        }
    }

    private void ApplyPoisonTick()
    {
        float rawDamage;

        if (_actor is PlayerRuneCombatActor)
        {
            rawDamage = _actor.MaxHp * StatusEffectRules.PlayerPoisonMaxHpRatio;
        }
        else
        {
            rawDamage = StatusEffectRules.MonsterPoisonDamage;
        }

        float resistance = _actor.PoisonResistance;

        if (float.IsNaN(resistance) || float.IsInfinity(resistance))
        {
            resistance = 0f;
        }

        float damageMultiplier = 1f - Mathf.Clamp(resistance, 0f, 100f) / 100f;

        DealStatusDamage(rawDamage * damageMultiplier);
    }

    private void ApplyBossPoisonTick()
    {
        float ratio = _hasPoisonProtectionCharm ? StatusEffectRules.ProtectedBossPoisonMaxHpRatio : StatusEffectRules.BossPoisonMaxHpRatio;

        float damage = _actor.MaxHp * ratio * _bossPoisonStacks.Value;

        // 일반 독 저항력은 적용하지 않음.
        DealStatusDamage(damage);
    }

    private void DealStatusDamage(float damage)
    {
        if (CanProcess == false || float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f)
        {
            return;
        }

        _actor.ReceiveStatusDamageOnServer(damage);
    }

    public bool SetPoisonProtectionCharmOnServer(bool equipped)
    {
        if (IsServer == false || IsSpawned == false || _actor is not PlayerRuneCombatActor)
        {
            return false;
        }

        // 변경 전까지 예정된 피해는 기존 부적 상태로 처리.
        if (CanProcess)
        {
            ProcessStatusesOnServer(NetworkManager.ServerTime.Time);
        }

        if (IsSpawned == false)
        {
            return false;
        }

        _hasPoisonProtectionCharm = equipped;
        return true;
    }

    public void ClearStatusesOnServer()
    {
        if (IsServer == false || IsSpawned == false)
            return;

        _poisoned.Value = false;
        _bleedStacks.Value = 0;
        _bossPoisonStacks.Value = 0;

        _poisonEndTime = 0d;
        _nextPoisonTickTime = 0d;
        _nextBleedDecayTime = 0d;
        _bossPoisonEndTime = 0d;
        _nextBossPoisonTickTime = 0d;
    }
}