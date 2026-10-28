using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.Behavior;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class MonsterNetwork : NetworkBehaviour
{
    private NetworkVariable<FixedString128Bytes> _monsterId = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private Monster _monster;
    private BehaviorGraphAgent _bt;
    private Rigidbody2D _body;

    private NetworkVariable<float> _maxHp = new(
    0f,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server);
    private void OnMaxHpChanged(float previousValue, float newValue)
    {
        _monster.ViewModel.MaxHp = newValue;
    }

    private NetworkVariable<float> _curHp = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private void OnCurHpChanged(float previousValue, float newValue)
    {
        _monster.ViewModel.CurHp = newValue;
    }

    private void ApplyHpToViewModel()
    {
        _monster.ViewModel.MaxHp = _maxHp.Value;
        _monster.ViewModel.CurHp = _curHp.Value;
    }

    private void Awake()
    {
        _monster = GetComponent<Monster>();
        _bt = GetComponent<BehaviorGraphAgent>();
        _body = GetComponent<Rigidbody2D>();

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

            _maxHp.Value = Mathf.Max(0f, data.Hp);
            _curHp.Value = _maxHp.Value;
        }

        _maxHp.OnValueChanged += OnMaxHpChanged;
        _curHp.OnValueChanged += OnCurHpChanged;

        ApplyHpToViewModel();

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
            _body.simulated = true;

        if (IsServer)
        {
            _monster.enabled = true;
            _bt.enabled = true;
        }
    }

    public override void OnNetworkDespawn()
    {
        _maxHp.OnValueChanged -= OnMaxHpChanged;
        _curHp.OnValueChanged -= OnCurHpChanged;

        _bt.enabled = false;
        _monster.enabled = false;

        if (_body != null)
            _body.simulated = false;

        base.OnNetworkDespawn();
    }

    public void TakeDamageOnServer(float damage)
    {
        if (!IsServer || !IsSpawned)
            return;

        if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f)
            return;

        _curHp.Value = Mathf.Max(0f, _curHp.Value - damage);
    }
}
