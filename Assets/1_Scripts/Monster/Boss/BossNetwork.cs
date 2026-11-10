using Unity.Netcode;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class BossNetwork : NetworkBehaviour
{
    private bool _statsInitialized;
    private Boss _boss;
    public bool IsReady { get; private set; }

    private NetworkVariable<float> _maxHp = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<float> _curHp = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private void Awake()
    {
        _boss = GetComponent<Boss>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _maxHp.OnValueChanged += OnMaxHpChanged;
        _curHp.OnValueChanged += OnCurHpChanged;

        ApplyHpToViewModel();
    }

    private void OnMaxHpChanged(float previousValue, float newValue)
    {
        _boss.ViewModel.MaxHp = newValue;
    }

    private void OnCurHpChanged(float previousValue, float newValue)
    {
        _boss.ViewModel.CurHp = newValue;
    }

    private void ApplyHpToViewModel()
    {
        _boss.ViewModel.MaxHp = _maxHp.Value;
        _boss.ViewModel.CurHp = _curHp.Value;
    }

    public override void OnNetworkDespawn()
    {
        _maxHp.OnValueChanged -= OnMaxHpChanged;
        _curHp.OnValueChanged -= OnCurHpChanged;

        _statsInitialized = false;
        IsReady = false;

        base.OnNetworkDespawn();
    }

    public bool InitializeStatsOnServer(string id)
    {
        if (!IsServer || !IsSpawned)
        {
            Debug.LogWarning("보스 능력치 초기화는 서버에서 Spawn 이후에 호출해야 합니다.", this);

            return false;
        }

        if (_statsInitialized)
            return false;

        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogError("보스 데이터 ID가 비어 있습니다.", this);
            return false;
        }

        if (DataManager.Inst == null)
        {
            Debug.LogError("DataManager가 준비되지 않았습니다.", this);
            return false;
        }

        MonsterData data = DataManager.Inst.GetData<MonsterData>(id);

        if (data == null)
        {
            Debug.LogError($"보스 데이터를 찾을 수 없습니다: {id}", this);
            return false;
        }

        if (float.IsNaN(data.Hp) || float.IsInfinity(data.Hp) || data.Hp <= 0f)
        {
            Debug.LogError($"보스의 HP 설정이 올바르지 않습니다: {id}", this);
            return false;
        }

        _statsInitialized = true;

        _maxHp.Value = data.Hp;
        _curHp.Value = data.Hp;

        InitializeSkillsOnServerAsync(data).Forget();

        return true;
    }

    private async UniTask InitializeSkillsOnServerAsync(MonsterData data)
    {
        try
        {
            bool success = await _boss.InitSkillsAsync(data);

            if (this == null || !IsSpawned)
                return;

            if (!success)
            {
                Debug.LogError("보스 스킬 초기화에 실패했습니다.", this);
                NetworkObject.Despawn();
                return;
            }

            IsReady = true;
            Debug.Log("보스 HP와 스킬 준비가 완료되었습니다.", this);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);

            if (this != null && IsServer && IsSpawned)
                NetworkObject.Despawn();
        }
    }
}
