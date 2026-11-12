using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.Behavior;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class MonsterNetwork : NetworkUnit
{
    private NetworkVariable<FixedString128Bytes> _monsterId = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private Monster _monster;
    private BehaviorGraphAgent _bt;
    private Rigidbody2D _body;
    private RigidbodyConstraints2D _originalConstraints;
    private float _originalGravityScale;

    public bool IsReady { get; private set; }
    protected override bool IsReadyForCombat => IsReady;
    public bool CanAttack => IsReady && IsAlive && AttackPower > 0f;
    public event System.Action<MonsterNetwork> Died;

    private void RefreshViewModel()
    {
        MonsterVM viewModel = _monster.ViewModel;
        viewModel.MaxHp = MaxHp;
        viewModel.CurHp = CurrentHp;
        viewModel.PhysicalResistance = PhysicalResistance;
        viewModel.FireResistance = FireResistance;
        viewModel.IceResistance = IceResistance;
        viewModel.ElectricalResistance = ElectricalResistance;
        viewModel.PoisonResistance = PoisonResistance;
        viewModel.BleedAble = CanReceiveBleed;
        viewModel.PoisonStackCount = PoisonStackCount;
        viewModel.BossPoisonStackCount = BossPoisonStackCount;
        viewModel.BleedStackCount = BleedStackCount;
    }

    private void Awake()
    {
        _monster = GetComponent<Monster>();
        _bt = GetComponent<BehaviorGraphAgent>();
        _body = GetComponent<Rigidbody2D>();

        if (_body != null)
        {
            _originalConstraints = _body.constraints;
            _originalGravityScale = _body.gravityScale;
        }

        _monster.enabled = false;
        _bt.enabled = false;
    }

    public bool SetMonsterId(string id)
    {
        if (Unity.Netcode.NetworkManager.Singleton == null ||
            !Unity.Netcode.NetworkManager.Singleton.IsServer ||
            IsSpawned ||
            string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        _monsterId.Value = new FixedString128Bytes(id);
        return true;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        IsReady = false;

        _monster.enabled = false;
        _bt.enabled = false;

        if (_body != null)
            _body.simulated = false;

        if (IsServer)
        {
            if (DataManager.Inst == null)
            {
                Debug.LogError("DataManager가 준비되지 않았습니다.");
                NetworkObject.Despawn();
                return;
            }

            MonsterData data = DataManager.Inst.GetData<MonsterData>(_monsterId.Value.ToString());

            if (data == null)
            {
                Debug.LogError("몬스터 데이터를 찾을 수 없습니다.");
                NetworkObject.Despawn();
                return;
            }

            if (!InitializeUnitOnServer(data.Hp, data.Atk, canReceiveBleed: data.BleedAble)
                || !SetResistancesOnServer(data.PhysicalResistance, data.FireResistance,
                    data.IceResistance, data.ElectricalResistance, data.PoisonResistance))
            {
                Debug.LogError($"몬스터 능력치 초기화 실패: {_monsterId.Value}", this);
                NetworkObject.Despawn();
                return;
            }
        }

        StatsChanged += RefreshViewModel;
        StatusChanged += RefreshViewModel;

        RefreshViewModel();

        InitializeMonster(default).Forget();
    }

    private async UniTask InitializeMonster(CancellationToken token)
    {
        bool initialized = await _monster.InitMonsterVisual(_monsterId.Value.ToString(), token);

        if (this == null || !IsSpawned)
            return;

        if (!initialized)
        {
            Debug.LogError($"몬스터 초기화 실패: {_monsterId.Value}");

            if (IsServer)
                NetworkObject.Despawn();

            return;
        }

        if (_body != null)
        {
            bool fixedPosition = _monster.MovementType == MoveType.Fix;
            _body.constraints = fixedPosition
                ? _originalConstraints | RigidbodyConstraints2D.FreezePosition
                : _originalConstraints;
            _body.gravityScale = fixedPosition ? 0f : _originalGravityScale;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _body.simulated = true;
        }

        if (IsServer)
        {
            bool skillsInitialized = await _monster.InitMonsterSkills(_monsterId.Value.ToString());

            if (this == null || !IsSpawned)
                return;

            if (!skillsInitialized)
            {
                Debug.LogError($"몬스터 스킬 준비 실패: {_monsterId.Value}");
                NetworkObject.Despawn();
                return;
            }
        }

        IsReady = true;

        if (IsServer)
        {
            _monster.enabled = true;
            _bt.enabled = true;
        }
    }

    public override void OnNetworkDespawn()
    {
        IsReady = false;

        StatsChanged -= RefreshViewModel;
        StatusChanged -= RefreshViewModel;

        _bt.enabled = false;
        _monster.enabled = false;

        if (_body != null)
            _body.simulated = false;

        base.OnNetworkDespawn();
    }

    // TO USE : 서버에서 몬스터 HP에 계산 완료된 최종 피해만 적용하는 기존 호출용 함수.
    // 속성·독·피격 처리가 필요한 공격은 대상 Actor의 ReceiveDirectDamageOnServer를 사용.
    public void TakeDamageOnServer(float damage)
    {
        TryTakeDamageOnServer(damage, out _);
    }

    protected override void OnDiedOnServer()
    {
        IsReady = false;
        _bt.enabled = false;
        _monster.enabled = false;
        _monster.MonsterStopMove();

        Debug.Log($"몬스터 사망: {_monsterId.Value}", this);

        try
        {
            _monster.ExecuteDeathSkills();
            Died?.Invoke(this);
        }
        finally
        {
            if (this != null && IsSpawned)
                NetworkObject.Despawn();
        }
    }

}
