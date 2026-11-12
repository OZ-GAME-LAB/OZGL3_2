using Unity.Netcode;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Unity.Behavior;

public class BossNetwork : NetworkBehaviour
{
    private bool _statsInitialized;
    private Boss _boss;
    public bool IsReady { get; private set; }
    private BehaviorGraphAgent _bt;

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
        _bt = GetComponent<BehaviorGraphAgent>();

        if (_bt != null)
            _bt.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        IsReady = false;

        if (_bt != null)
            _bt.enabled = false;

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
        if (_bt != null)
            _bt.enabled = false;

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

            if (!IsServer)
                return;

            if (_bt == null || _bt.Graph == null)
            {
                Debug.LogError("보스의 Behavior Agent와 BossBehavior 그래프 연결을 확인해 주세요.", this);

                NetworkObject.Despawn();
                return;
            }

            IsReady = true;
            _bt.enabled = true;

            Debug.Log("보스 스킬 준비 완료. BT를 시작합니다.", this);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);

            if (this != null && IsServer && IsSpawned)
                NetworkObject.Despawn();
        }
    }

    public void BroadcastThornAttack(string skillId, Vector3 position, float width, float height, double startTime, float holdSeconds, float warningSeconds)
    {
        if (!IsServer || !IsSpawned)
            return;

        PlayThornAttackClientRpc(skillId, position, width, height, startTime, holdSeconds, warningSeconds);
    }

    [ClientRpc]
    private void PlayThornAttackClientRpc(string skillId, Vector3 position, float width, float height, double startTime, float holdSeconds, float warningSeconds)
    {
        if (IsServer)
            return;

        ShowThornVisualAsync(skillId, position, width, height, startTime, holdSeconds, warningSeconds).Forget();
    }

    private async UniTask ShowThornVisualAsync(string skillId, Vector3 position, float width, float height, double startTime, float holdSeconds, float warningSeconds)
    {
        BossThornAttackObject visual = null;

        try
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return;

            if (DataManager.Inst == null || ResourceManager.Inst == null)
            {
                Debug.LogError("가시 연출에 필요한 매니저가 준비되지 않았습니다.", this);
                return;
            }

            MonsterSkillData data = DataManager.Inst.GetData<MonsterSkillData>(skillId);

            if (data == null || string.IsNullOrWhiteSpace(data.SkillPrefabPath))
            {
                Debug.LogError($"가시 스킬 데이터를 확인해 주세요: {skillId}", this);
                return;
            }

            GameObject prefab = await ResourceManager.Inst.LoadAsset<GameObject>(data.SkillPrefabPath);

            if (this == null || !IsSpawned || prefab == null)
                return;

            if (!prefab.TryGetComponent(out BossThornAttackObject prefabComponent))
            {
                Debug.LogError($"프리팹에 BossThornAttackObject가 없습니다: {skillId}", this);

                return;
            }

            visual = Instantiate(prefabComponent, position, Quaternion.identity);

            visual.transform.localScale = Vector3.one;
            visual.gameObject.SetActive(false);

            if (!visual.Initialize(width, height, holdSeconds, warningSeconds))
                return;

            visual.gameObject.SetActive(true);
            visual.PlayClientVisual(startTime);

            await UniTask.WaitUntil(() => this == null || !IsSpawned || visual == null || !visual.IsPlaying);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            if (visual != null)
                Destroy(visual.gameObject);
        }
    }
}
