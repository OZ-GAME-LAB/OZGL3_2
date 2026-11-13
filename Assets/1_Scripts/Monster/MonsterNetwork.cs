using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.Behavior;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public partial class MonsterNetwork : NetworkUnit
{
    private NetworkVariable<FixedString128Bytes> _monsterId = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField, Min(0f)] private float _staggerDuration = 0.3f;
    private float _staggerRemaining;

    private Monster _monster;
    private MonsterData _monsterData;
    private BehaviorGraphAgent _bt;
    private Rigidbody2D _body;
    private RigidbodyConstraints2D _originalConstraints;
    private float _originalGravityScale;

    public bool IsReady { get; private set; }
    protected override bool IsReadyForCombat => IsReady;
    public bool IsStaggered => _staggerRemaining > 0f;
    public bool CanAttack => IsReady && IsAlive && !IsStaggered && AttackPower > 0f;
    public event System.Action<MonsterNetwork> Died;
    public event System.Action<MonsterNetwork> Despawned;

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

    public void ApplyHitStaggerOnServer()
    {
        if (!IsServer || !IsSpawned || !IsReady || !IsAlive || _monster == null || _monster.IsDashing)
            return;

        if (float.IsNaN(_staggerDuration) || float.IsInfinity(_staggerDuration) || _staggerDuration <= 0f)
            return;

        _staggerRemaining = _staggerDuration;
        _monster.MonsterStopMove();
    }

    private void FixedUpdate()
    {
        if (!IsServer || !IsSpawned || !IsReady || !IsAlive || !IsStaggered)
            return;

        _staggerRemaining = Mathf.Max(0f, _staggerRemaining - Time.fixedDeltaTime);
        _monster.MonsterStopMove();
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

        _staggerRemaining = 0f;
        _nextMeleeAttackId = 0;
        _lastMeleeAttackId = 0;
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

            _monsterData = data;

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

        if (!IsServer)
        {
            bool visualsPrepared = await PrepareMeleeVisualsAsync(_monsterId.Value.ToString());
            if (this == null || !IsSpawned || !visualsPrepared)
                return;
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
        DestroyMeleeVisuals();
        _staggerRemaining = 0f;
        IsReady = false;

        StatsChanged -= RefreshViewModel;
        StatusChanged -= RefreshViewModel;

        _bt.enabled = false;
        _monster.enabled = false;

        if (_body != null)
            _body.simulated = false;

        try
        {
            if (IsServer)
                Despawned?.Invoke(this);
        }
        finally
        {
            base.OnNetworkDespawn();
        }
    }

    // TO USE : 서버에서 몬스터 HP에 계산 완료된 최종 피해만 적용하는 기존 호출용 함수.
    // 속성·독·피격 처리가 필요한 공격은 대상 Actor의 ReceiveDirectDamageOnServer를 사용.
    public void TakeDamageOnServer(float damage)
    {
        TryTakeDamageOnServer(damage, out _);
    }

    public void BroadcastHealArea(string skillId, Vector3 position, double expiresAt)
    {
        if (IsServer && IsSpawned)
            PlayHealAreaClientRpc(skillId, position, expiresAt);
    }

    [ClientRpc]
    private void PlayHealAreaClientRpc(string skillId, Vector3 position, double expiresAt)
    {
        if (IsServer)
            return;

        ShowHealAreaAsync(NetworkManager, skillId, position, expiresAt).Forget();
    }

    private static async UniTask ShowHealAreaAsync(NetworkManager manager, string skillId, Vector3 position, double expiresAt)
    {
        try
        {
            if (manager == null || !manager.IsListening || manager.ServerTime.Time >= expiresAt
                || DataManager.Inst == null || ResourceManager.Inst == null || string.IsNullOrWhiteSpace(skillId))
                return;

            MonsterSkillData data = DataManager.Inst.GetData<MonsterSkillData>(skillId);
            if (data == null || string.IsNullOrWhiteSpace(data.SkillPrefabPath))
            {
                Debug.LogError($"회복 이펙트의 스킬 데이터를 확인하세요: {skillId}");
                return;
            }

            GameObject prefab = await ResourceManager.Inst.LoadAsset<GameObject>(data.SkillPrefabPath);
            if (manager == null || !manager.IsListening || manager.ServerTime.Time >= expiresAt || prefab == null)
                return;

            if (!prefab.TryGetComponent(out HealAreaObject healPrefab) || !healPrefab.HasValidSettings())
            {
                Debug.LogError($"회복 프리팹의 HealAreaObject 설정을 확인하세요: {skillId}");
                return;
            }

            HealAreaObject area = Instantiate(healPrefab, position, Quaternion.identity);
            area.gameObject.SetActive(true);
            if (!area.Play(manager, expiresAt, false))
                Destroy(area.gameObject);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    protected override void OnDiedOnServer()
    {
        _staggerRemaining = 0f;
        IsReady = false;
        _bt.enabled = false;
        _monster.enabled = false;
        _monster.MonsterStopMove();

        Debug.Log($"몬스터 사망: {_monsterId.Value}", this);

        int playerCount = NetworkManager.ConnectedClientsList.Count;
        Vector3 deathPosition = transform.position;
        MonsterData rewardData = _monsterData;

        try
        {
            _monster.ExecuteDeathSkills();
            Died?.Invoke(this);
        }
        finally
        {
            try
            {
                DropRewardUtil.SpawnMonsterRewardsOnServer(rewardData, deathPosition, playerCount, this);
            }
            finally
            {
                if (this != null && IsSpawned)
                    NetworkObject.Despawn();
            }
        }
    }

}
