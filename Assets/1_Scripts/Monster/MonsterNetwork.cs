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
        _bt.enabled = false;
        _monster.enabled = false;

        if (_body != null)
            _body.simulated = false;

        base.OnNetworkDespawn();
    }
}
