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

    [Header("상태 이상")]
    [SerializeField, Range(-1f, 1f)] private float _initialPoisonResistance;

    public bool SetPoisonResistanceOnServer(float value)
    {
        return SetResistancesOnServer(PhysicalResistance, FireResistance,
            IceResistance, ElectricalResistance, value);
    }

    private NetworkVariable<float> _moveSpeed = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<float> _jumpForce = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // 플레이어마다 서로 다른 VM 인스턴스를 가짐.
    public PlayerStatViewModel ViewModel { get; } = new();

    public float Hp => CurrentHp;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            if (!InitializeUnitOnServer(ValidOrDefault(_initialMaxHp, 100f, 1f),
                ValidOrDefault(_initialAttackPower, 100f, 0f), canReceiveBossPoison: true))
                return;

            _moveSpeed.Value = ValidOrDefault(_initialMoveSpeed, 12f, 0f);
            _jumpForce.Value = ValidOrDefault(_initialJumpForce, 17.5f, 0f);
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
