using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class PlayerNetworkStats : NetworkBehaviour
{
    [Header("임시 기본 스탯")]
    [SerializeField, Min(1f)] private float _initialMaxHp = 100f;
    [SerializeField, Min(0f)] private float _initialAttackPower = 100f;
    [SerializeField, Min(0f)] private float _initialMoveSpeed = 12f;
    [SerializeField, Min(0f)] private float _initialJumpForce = 17.5f;

    [Header("상태 이상")]
    [SerializeField, Range(0f, 100f)] private float _initialPoisonResistance;

    private float _poisonResistance = 0f;
    public float PoisonResistance => _poisonResistance;

    public bool SetPoisonResistanceOnServer(float value)
    {
        if (IsServer == false || IsSpawned == false || IsFinite(value) == false)
        {
            return false;
        }

        _poisonResistance = Mathf.Clamp(value, 0f, 100f);
        return true;
    }

    private NetworkVariable<float> _maxHp = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<float> _hp = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<float> _attackPower = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<float> _moveSpeed = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<float> _jumpForce = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // 플레이어마다 서로 다른 VM 인스턴스를 가짐.
    public PlayerStatViewModel ViewModel { get; } = new();

    public float MaxHp => _maxHp.Value;
    public float Hp => _hp.Value;
    public float AttackPower => _attackPower.Value;

    public bool IsAlive => IsSpawned && Hp > 0f;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _maxHp.Value = ValidOrDefault(_initialMaxHp, 100f, 1f);
            _hp.Value = _maxHp.Value;
            _attackPower.Value = ValidOrDefault(_initialAttackPower, 100f, 0f);
            _moveSpeed.Value = ValidOrDefault(_initialMoveSpeed, 12f, 0f);
            _jumpForce.Value = ValidOrDefault(_initialJumpForce, 17.5f, 0f);
            _poisonResistance = Mathf.Clamp(ValidOrDefault(_initialPoisonResistance, 0f, 0f), 0f, 100f);
        }

        _maxHp.OnValueChanged += OnStatChanged;
        _hp.OnValueChanged += OnStatChanged;
        _attackPower.OnValueChanged += OnStatChanged;
        _moveSpeed.OnValueChanged += OnStatChanged;
        _jumpForce.OnValueChanged += OnStatChanged;

        RefreshViewModel();
    }

    public override void OnNetworkDespawn()
    {
        _maxHp.OnValueChanged -= OnStatChanged;
        _hp.OnValueChanged -= OnStatChanged;
        _attackPower.OnValueChanged -= OnStatChanged;
        _moveSpeed.OnValueChanged -= OnStatChanged;
        _jumpForce.OnValueChanged -= OnStatChanged;

        ViewModel.CurHp = 0f;
    }

    private void OnStatChanged(float previousValue, float newValue)
    {
        RefreshViewModel();
    }

    private void RefreshViewModel()
    {
        ViewModel.MaxHp = _maxHp.Value;
        ViewModel.CurHp = _hp.Value;
        ViewModel.AttackPower = _attackPower.Value;
        ViewModel.MoveSpeed = _moveSpeed.Value;
        ViewModel.JumpForce = _jumpForce.Value;
    }

    // 저항력 계산을 마친 최종 피해량을 받음.
    public bool TryTakeDamageOnServer(float damage, out float actualDamage)
    {
        actualDamage = 0f;

        if (IsServer == false || IsAlive == false)
        {
            return false;
        }

        if (IsFinite(damage) == false || damage <= 0f)
        {
            return false;
        }

        float previousHp = _hp.Value;

        _hp.Value = Mathf.Max(0f, previousHp - damage);

        actualDamage = previousHp - _hp.Value;

        return true;
    }

    public bool HealOnServer(float amount)
    {
        // 사망 상태에서는 일반 회복으로 부활시키지 않음.
        if (IsServer == false || IsAlive == false)
        {
            return false;
        }

        if (IsFinite(amount) == false || amount <= 0f)
        {
            return false;
        }

        float recoverable = Mathf.Max(0f, MaxHp - Hp);
        float actualHeal = Mathf.Min(amount, recoverable);

        if (actualHeal <= 0f)
        {
            return false;
        }

        _hp.Value += actualHeal;

        return true;
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
