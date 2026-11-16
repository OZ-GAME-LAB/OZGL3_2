using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class PlayerNetworkStats : NetworkUnit
{
    protected override bool PoisonUsesMaxHp => true;

    [Header("임시 기본 스탯")]
    [SerializeField, Min(1f)] private float _initialMaxHp = 100f;
    [SerializeField, Min(0f)] private float _initialAttackPower = 100f;
    [SerializeField, Min(0f)] private float _initialMoveSpeed = 12f;
    [SerializeField, Min(0f)] private float _initialJumpForce = 17.5f;

    [Header("기본 공격 간격")]
    [Tooltip("3타별 모션 시간의 기준 간격(초)입니다. 룬 공격속도 배율은 공격 시 적용합니다.")]
    [SerializeField, Min(0.01f)] private float _initialBaseAttackInterval = 0.75f;

    [Header("상태 이상")]
    [SerializeField, Range(-1f, 1f)] private float _initialPoisonResistance;

    public bool SetPoisonResistanceOnServer(float value)
    {
        return SetResistancesOnServer(PhysicalResistance, FireResistance,
            IceResistance, ElectricalResistance, value);
    }

    private NetworkVariable<float> _moveSpeed = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<float> _jumpForce = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<float> _baseAttackInterval = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // 플레이어마다 서로 다른 VM 인스턴스를 가짐.
    public PlayerStatViewModel ViewModel { get; } = new();

    private readonly NetworkVariable<bool> _encounterLocked = new(false);
    public bool IsEncounterLocked => _encounterLocked.Value;
    protected override bool IsReadyForCombat => !IsEncounterLocked;
    private double _encounterLockedAt;

    public void SetEncounterLockedOnServer(bool locked)
    {
        if (!IsServer || !IsSpawned || IsEncounterLocked == locked) return;
        double now = NetworkManager.ServerTime.Time;
        if (locked)
        {
            base.Update();
            if (!IsAlive) return;
            _encounterLockedAt = now;
        }
        else
        {
            // 연출 중 멈춘 지속 피해를 해제 순간에 몰아서 적용하지 않습니다.
            double pause = System.Math.Max(0d, now - _encounterLockedAt);
            if (StatusState.PoisonExpiresAt > 0d) StatusState.PoisonExpiresAt += pause;
            if (StatusState.NextPoisonTickAt > 0d) StatusState.NextPoisonTickAt += pause;
            if (StatusState.NextBossPoisonTickAt > 0d) StatusState.NextBossPoisonTickAt += pause;
            if (StatusState.BossPoisonProtectedUntil > 0d) StatusState.BossPoisonProtectedUntil += pause;
            if (StatusState.NextBleedDecayAt > 0d) StatusState.NextBleedDecayAt += pause;
        }
        _encounterLocked.Value = locked;
    }

    public float Hp => CurrentHp;
    public float BaseAttackInterval => _baseAttackInterval.Value;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool _developerInvincible;
#endif

    public void RequestToggleDeveloperInvincibility()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!IsSpawned || !IsOwner)
            return;

        ToggleDeveloperInvincibilityServerRpc();
#endif
    }

    [ServerRpc]
    private void ToggleDeveloperInvincibilityServerRpc(ServerRpcParams rpcParams = default)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!IsServer || !IsSpawned || rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (!IsAlive)
        {
            Debug.LogWarning("[개발자 무적] 사망한 플레이어는 부활시키지 않습니다. 다시 시작해 주세요.", this);
            return;
        }

        _developerInvincible = !_developerInvincible;
        Debug.Log($"[개발자 무적] 플레이어 {OwnerClientId}: {(_developerInvincible ? "ON" : "OFF")}", this);
#endif
    }

    public override bool TryTakeDamageOnServer(float finalDamage, out float actualDamage)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (IsServer && IsSpawned && _developerInvincible)
        {
            actualDamage = 0f;
            return false;
        }
#endif
        return base.TryTakeDamageOnServer(finalDamage, out actualDamage);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _developerInvincible = false;
#endif

        if (IsServer)
        {
            _encounterLocked.Value = false;
            if (!InitializeUnitOnServer(ValidOrDefault(_initialMaxHp, 100f, 1f),
                ValidOrDefault(_initialAttackPower, 100f, 0f), canReceiveBossPoison: true))
                return;

            _moveSpeed.Value = ValidOrDefault(_initialMoveSpeed, 12f, 0f);
            _jumpForce.Value = ValidOrDefault(_initialJumpForce, 17.5f, 0f);
            _baseAttackInterval.Value = ValidOrDefault(_initialBaseAttackInterval, 0.75f, 0.01f);
            SetPoisonResistanceOnServer(ValidOrDefault(_initialPoisonResistance, 0f, -1f));
        }

        StatsChanged += RefreshViewModel;
        StatusChanged += RefreshViewModel;
        _moveSpeed.OnValueChanged += OnStatChanged;
        _jumpForce.OnValueChanged += OnStatChanged;

        RefreshViewModel();
    }

    public override void OnNetworkDespawn()
    {
        StatsChanged -= RefreshViewModel;
        StatusChanged -= RefreshViewModel;
        _moveSpeed.OnValueChanged -= OnStatChanged;
        _jumpForce.OnValueChanged -= OnStatChanged;

        ViewModel.CurHp = 0f;
        ViewModel.PoisonStackCount = 0;
        ViewModel.BossPoisonStackCount = 0;
        ViewModel.BleedStackCount = 0;

        base.OnNetworkDespawn();
    }

    private void OnStatChanged(float previousValue, float newValue)
    {
        RefreshViewModel();
    }

    private void RefreshViewModel()
    {
        ViewModel.MaxHp = MaxHp;
        ViewModel.CurHp = CurrentHp;
        ViewModel.AttackPower = AttackPower;
        ViewModel.PhysicalResistance = PhysicalResistance;
        ViewModel.FireResistance = FireResistance;
        ViewModel.IceResistance = IceResistance;
        ViewModel.ElectricalResistance = ElectricalResistance;
        ViewModel.PoisonResistance = PoisonResistance;
        ViewModel.PoisonStackCount = PoisonStackCount;
        ViewModel.BossPoisonStackCount = BossPoisonStackCount;
        ViewModel.BleedStackCount = BleedStackCount;
        ViewModel.MoveSpeed = _moveSpeed.Value;
        ViewModel.JumpForce = _jumpForce.Value;
    }

    public bool AddMoveSpeedOnServer(float value)
    {
        return TryAddStatOnServer(_moveSpeed, value);
    }

    public bool AddJumpForceOnServer(float value)
    {
        return TryAddStatOnServer(_jumpForce, value);
    }

    private bool TryAddStatOnServer(NetworkVariable<float> stat, float value)
    {
        if (IsServer == false || IsSpawned == false)
        {
            return false;
        }

        if (IsFinite(value) == false)
        {
            return false;
        }

        float nextValue = stat.Value + value;

        if (IsFinite(nextValue) == false)
        {
            return false;
        }

        stat.Value = Mathf.Max(0f, nextValue);

        return true;
    }

    private static bool IsFinite(float value)
    {
        return float.IsNaN(value) == false && float.IsInfinity(value) == false;
    }

    private static float ValidOrDefault(float value, float fallback, float minimum)
    {
        if (IsFinite(value) == false)
        {
            return fallback;
        }

        return Mathf.Max(minimum, value);
    }

    [ContextMenu("테스트/서버에서 피해 25")]
    private void TestDamage()
    {
        if (Application.isPlaying == false)
        { 
            return;
        }

        TryTakeDamageOnServer(25f, out _);
    }
}
