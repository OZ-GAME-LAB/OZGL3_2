using System;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public abstract class NetworkUnit : NetworkBehaviour
{
    private NetworkVariable<float> _maxHp = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> _curHp = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> _attackPower = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> _physicalResistance = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> _fireResistance = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> _iceResistance = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> _electricalResistance = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> _poisonResistance = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> _canReceiveBleed = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> _canReceiveBossPoison = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _poisonStackCount = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _bossPoisonStackCount = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _bleedStackCount = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool _unitInitialized;
    private bool _deathHandled;
    private bool _hasPoisonProtectionAmulet;

    internal UnitStatusState StatusState { get; } = new();

    public float MaxHp => _maxHp.Value;
    public float CurrentHp => _curHp.Value;
    public float AttackPower => _attackPower.Value;
    public float PhysicalResistance => _physicalResistance.Value;
    public float FireResistance => _fireResistance.Value;
    public float IceResistance => _iceResistance.Value;
    public float ElectricalResistance => _electricalResistance.Value;
    public float PoisonResistance => _poisonResistance.Value;
    public bool CanReceiveBleed => _canReceiveBleed.Value;
    public bool CanReceiveBossPoison => _canReceiveBossPoison.Value;
    public int PoisonStackCount => _poisonStackCount.Value;
    public int BossPoisonStackCount => _bossPoisonStackCount.Value;
    public int BleedStackCount => _bleedStackCount.Value;
    public bool IsAlive => IsSpawned && CurrentHp > 0f;
    public bool IsPoisoned => PoisonStackCount > 0;
    public bool IsBossPoisoned => BossPoisonStackCount > 0;
    public bool IsBleeding => BleedStackCount > 0;

    public event Action StatsChanged;
    public event Action StatusChanged;

    private bool CanChangeStats => IsServer && IsSpawned && _unitInitialized;
    protected virtual bool IsReadyForCombat => true;
    protected virtual bool PoisonUsesMaxHp => false;
    private bool CanProcessCombat => CanChangeStats && IsAlive && IsReadyForCombat;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _unitInitialized = false;
        _deathHandled = false;
        _hasPoisonProtectionAmulet = false;
        StatusState.Clear();

        _maxHp.OnValueChanged += OnStatChanged;
        _curHp.OnValueChanged += OnStatChanged;
        _attackPower.OnValueChanged += OnStatChanged;
        _physicalResistance.OnValueChanged += OnStatChanged;
        _fireResistance.OnValueChanged += OnStatChanged;
        _iceResistance.OnValueChanged += OnStatChanged;
        _electricalResistance.OnValueChanged += OnStatChanged;
        _poisonResistance.OnValueChanged += OnStatChanged;
        _canReceiveBleed.OnValueChanged += OnStatusPermissionChanged;
        _canReceiveBossPoison.OnValueChanged += OnStatusPermissionChanged;
        _poisonStackCount.OnValueChanged += OnStackCountChanged;
        _bossPoisonStackCount.OnValueChanged += OnStackCountChanged;
        _bleedStackCount.OnValueChanged += OnStackCountChanged;

        StatsChanged?.Invoke();
        StatusChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        _maxHp.OnValueChanged -= OnStatChanged;
        _curHp.OnValueChanged -= OnStatChanged;
        _attackPower.OnValueChanged -= OnStatChanged;
        _physicalResistance.OnValueChanged -= OnStatChanged;
        _fireResistance.OnValueChanged -= OnStatChanged;
        _iceResistance.OnValueChanged -= OnStatChanged;
        _electricalResistance.OnValueChanged -= OnStatChanged;
        _poisonResistance.OnValueChanged -= OnStatChanged;
        _canReceiveBleed.OnValueChanged -= OnStatusPermissionChanged;
        _canReceiveBossPoison.OnValueChanged -= OnStatusPermissionChanged;
        _poisonStackCount.OnValueChanged -= OnStackCountChanged;
        _bossPoisonStackCount.OnValueChanged -= OnStackCountChanged;
        _bleedStackCount.OnValueChanged -= OnStackCountChanged;

        _unitInitialized = false;
        _deathHandled = false;
        _hasPoisonProtectionAmulet = false;
        StatusState.Clear();

        base.OnNetworkDespawn();
    }

    protected bool InitializeUnitOnServer(float maxHp, float attackPower,
        bool canReceiveBleed = false, bool canReceiveBossPoison = false)
    {
        if (!IsServer || !IsSpawned || _unitInitialized)
            return false;

        if (!IsFinite(maxHp) || maxHp <= 0f || !IsFinite(attackPower))
        {
            Debug.LogError("유닛 초기화에 사용할 최대 HP 또는 공격력이 올바르지 않습니다.", this);
            return false;
        }

        _maxHp.Value = maxHp;
        _attackPower.Value = Mathf.Max(0f, attackPower);
        _physicalResistance.Value = 0f;
        _fireResistance.Value = 0f;
        _iceResistance.Value = 0f;
        _electricalResistance.Value = 0f;
        _poisonResistance.Value = 0f;
        _canReceiveBleed.Value = canReceiveBleed;
        _canReceiveBossPoison.Value = canReceiveBossPoison;
        _deathHandled = false;
        _unitInitialized = true;

        ClearStatusEffectsOnServer();
        _curHp.Value = maxHp;
        return true;
    }

    // TO USE : 서버에서 유닛의 저항력을 설정할 때 사용. 각 값은 -1~1로 제한.
    public bool SetResistancesOnServer(float physical, float fire, float ice, float electrical, float poison)
    {
        if (!CanChangeStats)
            return false;

        if (!IsFinite(physical) || !IsFinite(fire) || !IsFinite(ice)
            || !IsFinite(electrical) || !IsFinite(poison))
        {
            Debug.LogError("저항력에는 유한한 값을 사용해야 합니다.", this);
            return false;
        }

        UpdateStatusEffectsOnServer();
        if (!CanChangeStats)
            return false;

        _physicalResistance.Value = Mathf.Clamp(physical, -1f, 1f);
        _fireResistance.Value = Mathf.Clamp(fire, -1f, 1f);
        _iceResistance.Value = Mathf.Clamp(ice, -1f, 1f);
        _electricalResistance.Value = Mathf.Clamp(electrical, -1f, 1f);
        _poisonResistance.Value = Mathf.Clamp(poison, -1f, 1f);
        return true;
    }

    // TO USE : 서버에서 계산이 끝난 최종 피해를 HP에 적용할 때 사용. actualDamage는 실제 감소한 HP.
    // 저항력을 다시 계산하지 않으며, 직접 타격의 공격 중단 반응은 실행하지 않음.
    public virtual bool TryTakeDamageOnServer(float finalDamage, out float actualDamage)
    {
        actualDamage = 0f;

        if (!CanProcessCombat || !IsFinite(finalDamage) || finalDamage <= 0f)
            return false;

        float previousHp = CurrentHp;
        float nextHp = Mathf.Max(0f, previousHp - finalDamage);
        actualDamage = previousHp - nextHp;

        if (actualDamage <= 0f)
            return false;

        _curHp.Value = nextHp;

        if (CurrentHp <= 0f && !_deathHandled)
        {
            _deathHandled = true;
            ClearStatusEffectsOnServer();
            OnDiedOnServer();
        }

        return true;
    }

    // TO USE : 서버에서 저항력 적용 전 피해·속성·독 부여 여부를 받아 공통 피격 처리를 할 때 사용.
    // 피격 반응까지 필요한 공격은 대상 Actor의 ReceiveDirectDamageOnServer를 호출.
    public bool TryReceiveHitOnServer(float rawDamage, DamageType damageType,
        bool applyPoison, bool applyBossPoison, out float actualDamage)
    {
        actualDamage = 0f;
        if (!CanProcessCombat || !IsFinite(rawDamage) || rawDamage <= 0f
            || !Enum.IsDefined(typeof(DamageType), damageType))
            return false;

        UpdateStatusEffectsOnServer();
        if (!CanProcessCombat)
            return false;

        float damage = BattleUtil.CalculateDamage(rawDamage, damageType,
            PhysicalResistance, FireResistance, IceResistance, ElectricalResistance);

        if (damage > 0f && !TryTakeDamageOnServer(damage, out actualDamage))
            return false;

        if (CanProcessCombat)
        {
            if (applyPoison)
                ApplyPoisonOnServer(rawDamage);
            if (applyBossPoison)
                ApplyBossPoisonOnServer();
        }

        return true;
    }

    // TO USE : 서버에서 일반 독만 부여할 때 사용. rawAttackDamage는 저항력 적용 전 타격 피해.
    // 플레이어는 최대 HP 비율, 몬스터는 전달한 타격 피해를 기준으로 독 피해를 계산.
    public bool ApplyPoisonOnServer(float rawAttackDamage)
    {
        if (!CanProcessCombat || !IsFinite(rawAttackDamage) || rawAttackDamage < 0f)
            return false;

        UpdateStatusEffectsOnServer();
        if (!CanProcessCombat)
            return false;

        int previousStacks = StatusState.PoisonStackCount;
        double now = NetworkManager.ServerTime.Time;
        bool applied = PoisonUsesMaxHp
            ? BattleUtil.ApplyPlayerPoison(StatusState, now)
            : BattleUtil.ApplyMonsterPoison(StatusState, now, rawAttackDamage);
        SynchronizeStatusOnServer();
        if (applied)
            Debug.Log($"[일반 독] 대상: {name}, 중첩: {previousStacks} → {PoisonStackCount} (최대 {BattleUtil.MaxPoisonStacks})", this);
        return applied;
    }

    // TO USE : 서버에서 보스 독만 부여할 때 사용. 일반 독과 별개이며 최대 3중첩, 재부여 보호 1초.
    public bool ApplyBossPoisonOnServer()
    {
        if (!CanProcessCombat || !CanReceiveBossPoison)
            return false;

        UpdateStatusEffectsOnServer();
        if (!CanProcessCombat)
            return false;

        int previousStacks = StatusState.BossPoisonStackCount;
        bool applied = BattleUtil.ApplyBossPoison(StatusState, NetworkManager.ServerTime.Time,
            CanReceiveBossPoison);
        SynchronizeStatusOnServer();
        if (applied)
            Debug.Log($"[보스 독] 대상: {name}, 중첩: {previousStacks} → {BossPoisonStackCount} (최대 {BattleUtil.MaxBossPoisonStacks})", this);
        return applied;
    }

    // TO USE : 서버에서 보스 독 중첩만 줄일 때 사용. 일반 독은 유지하며 감소 결과를 동기화.
    public bool RemoveBossPoisonStacksOnServer(int amount = 1)
    {
        if (!CanProcessCombat || amount <= 0 || StatusState.BossPoisonStackCount == 0)
            return false;

        UpdateStatusEffectsOnServer();
        if (!CanProcessCombat)
            return false;

        int previousStacks = StatusState.BossPoisonStackCount;
        bool removed = BattleUtil.RemoveBossPoisonStacks(StatusState, NetworkManager.ServerTime.Time, amount);
        SynchronizeStatusOnServer();
        if (removed)
            Debug.Log($"[보스 독 감소] 대상: {name}, 중첩: {previousStacks} → {BossPoisonStackCount}", this);
        return removed;
    }

    // TO USE : 서버에서 출혈을 부여할 때 사용. rawAttackDamage는 저항력 적용 전 타격 피해.
    // 출혈을 받을 수 있는 대상만 적용되며, 5중첩 폭발 피해와 중첩 초기화까지 처리.
    public bool ApplyBleedOnServer(float rawAttackDamage)
    {
        if (!CanProcessCombat || !CanReceiveBleed || !IsFinite(rawAttackDamage) || rawAttackDamage < 0f)
            return false;

        UpdateStatusEffectsOnServer();
        if (!CanProcessCombat)
            return false;

        float burstDamage = BattleUtil.ApplyBleed(StatusState, NetworkManager.ServerTime.Time,
            rawAttackDamage, CanReceiveBleed);
        SynchronizeStatusOnServer();
        if (burstDamage > 0f)
            TryTakeDamageOnServer(burstDamage, out _);
        return true;
    }

    // TO USE : 서버에서 독 보호 부적의 장착·해제 결과를 전달할 때 사용. true는 장착, false는 해제.
    // 보스 독 피해에만 적용되며, 실제 인벤토리 장착 연동은 호출하는 쪽에서 담당.
    public bool SetPoisonProtectionAmuletOnServer(bool hasAmulet)
    {
        if (!CanChangeStats)
            return false;

        UpdateStatusEffectsOnServer();
        if (!CanChangeStats)
            return false;

        _hasPoisonProtectionAmulet = hasAmulet;
        return true;
    }

    protected virtual void Update()
    {
        UpdateStatusEffectsOnServer();
    }

    private void UpdateStatusEffectsOnServer()
    {
        if (!CanProcessCombat || NetworkManager == null || !NetworkManager.IsListening)
            return;

        if (StatusState.PoisonStackCount == 0 && StatusState.BossPoisonStackCount == 0
            && StatusState.BleedStackCount == 0)
            return;

        float damage = BattleUtil.UpdateStatusEffects(StatusState, NetworkManager.ServerTime.Time,
            MaxHp, PoisonResistance, _hasPoisonProtectionAmulet);
        SynchronizeStatusOnServer();
        if (damage > 0f)
            TryTakeDamageOnServer(damage, out _);
    }

    // TO USE : 서버에서 유닛을 회복할 때 사용. amount만큼 회복하되 최대 HP를 넘지 않으며 부활은 처리하지 않음.
    public bool HealOnServer(float amount)
    {
        if (!CanChangeStats || !IsAlive || !IsFinite(amount) || amount <= 0f)
            return false;

        float actualHeal = Mathf.Min(amount, MaxHp - CurrentHp);
        if (actualHeal <= 0f)
            return false;

        _curHp.Value = Mathf.Min(MaxHp, CurrentHp + actualHeal);
        return true;
    }

    // TO USE : 서버에서 일반 독·보스 독·출혈을 모두 해제할 때 사용. 중첩·타이머 초기화와 동기화까지 처리.
    public bool ClearStatusEffectsOnServer()
    {
        if (!CanChangeStats)
            return false;

        StatusState.Clear();
        SynchronizeStatusOnServer();
        return true;
    }

    internal void SynchronizeStatusOnServer()
    {
        if (!CanChangeStats)
            return;

        _poisonStackCount.Value = StatusState.PoisonStackCount;
        _bossPoisonStackCount.Value = StatusState.BossPoisonStackCount;
        _bleedStackCount.Value = StatusState.BleedStackCount;
    }

    protected virtual void OnDiedOnServer() { }

    private void OnStatChanged(float previousValue, float newValue)
    {
        StatsChanged?.Invoke();
    }

    private void OnStatusPermissionChanged(bool previousValue, bool newValue)
    {
        StatsChanged?.Invoke();
    }

    private void OnStackCountChanged(int previousValue, int newValue)
    {
        StatusChanged?.Invoke();
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
