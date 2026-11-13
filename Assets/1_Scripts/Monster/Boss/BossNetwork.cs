using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Unity.Behavior;

public class BossNetwork : NetworkUnit
{
    private bool _statsInitialized;
    private Boss _boss;
    public bool IsReady { get; private set; }
    protected override bool IsReadyForCombat => IsReady;
    private BehaviorGraphAgent _bt;
    private readonly Dictionary<string, List<BossThornAttackObject>> _thornVisualPools = new();
    private uint _thornVisualVersion;


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

        StatsChanged += RefreshViewModel;
        StatusChanged += RefreshViewModel;

        RefreshViewModel();
    }

    private void RefreshViewModel()
    {
        MonsterVM viewModel = _boss.ViewModel;
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

    public override void OnNetworkDespawn()
    {
        DestroyThornVisuals();
        DestroyPoisonVisual();

        if (_bt != null)
            _bt.enabled = false;

        StatsChanged -= RefreshViewModel;
        StatusChanged -= RefreshViewModel;

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

        if (!InitializeUnitOnServer(data.Hp, data.Atk, canReceiveBleed: data.BleedAble)
            || !SetResistancesOnServer(data.PhysicalResistance, data.FireResistance,
                data.IceResistance, data.ElectricalResistance, data.PoisonResistance))
            return false;

        _statsInitialized = true;

        InitializeSkillsOnServerAsync(data).Forget();

        return true;
    }

    protected override void OnDiedOnServer()
    {
        IsReady = false;

        if (_bt != null)
            _bt.enabled = false;

        // TODO: 보스 소멸 연출이 완료된 뒤 Despawn하도록 연결.
        if (IsSpawned)
            NetworkObject.Despawn();
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

    private BossThornAttackObject GetAvailableThornVisual(string skillId)
    {
        if (!_thornVisualPools.TryGetValue(skillId, out List<BossThornAttackObject> pool))
            return null;

        foreach (BossThornAttackObject visual in pool)
        {
            if (visual != null && !visual.gameObject.activeSelf)
                return visual;
        }

        return null;
    }

    private void DestroyThornVisuals()
    {
        _thornVisualVersion++;
        foreach (List<BossThornAttackObject> pool in _thornVisualPools.Values)
        {
            foreach (BossThornAttackObject visual in pool)
            {
                if (visual == null)
                    continue;

                visual.gameObject.SetActive(false);
                Destroy(visual.gameObject);
            }
        }

        _thornVisualPools.Clear();
    }

    private async UniTask ShowThornVisualAsync(string skillId, Vector3 position, float width, float height, double startTime, float holdSeconds, float warningSeconds)
    {
        BossThornAttackObject visual = null;
        uint visualVersion = _thornVisualVersion;

        try
        {
            if (string.IsNullOrWhiteSpace(skillId) || this == null || !IsSpawned)
                return;

            visual = GetAvailableThornVisual(skillId);
            if (visual == null)
            {
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
                if (this == null || !IsSpawned || _thornVisualVersion != visualVersion || prefab == null)
                    return;

                visual = GetAvailableThornVisual(skillId);
                if (visual == null)
                {
                    if (!prefab.TryGetComponent(out BossThornAttackObject prefabComponent))
                    {
                        Debug.LogError($"프리팹에 BossThornAttackObject가 없습니다: {skillId}", this);
                        return;
                    }

                    visual = Instantiate(prefabComponent, position, Quaternion.identity);
                    visual.transform.localScale = Vector3.one;
                    visual.gameObject.SetActive(false);

                    if (!_thornVisualPools.TryGetValue(skillId, out List<BossThornAttackObject> pool))
                    {
                        pool = new List<BossThornAttackObject>();
                        _thornVisualPools.Add(skillId, pool);
                    }

                    pool.Add(visual);
                }
            }

            visual.transform.SetPositionAndRotation(position, Quaternion.identity);
            if (!visual.Initialize(width, height, holdSeconds, warningSeconds))
                return;

            visual.gameObject.SetActive(true);
            visual.PlayClientVisual(startTime);

            await UniTask.WaitUntil(() => this == null || !IsSpawned || _thornVisualVersion != visualVersion
                || visual == null || !visual.IsPlaying);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            if (visual != null)
                visual.gameObject.SetActive(false);
        }
    }

    private uint _nextPoisonAttackId;
    private uint _clientPoisonAttackId;
    private BossPoisonProjectile _poisonVisual;
    private string _poisonVisualSkillId;

    public uint BroadcastPoisonSpray(string skillId, Vector3 start, Vector3 control, Vector3 end,
        double startTime, float warningSeconds, float flightSeconds)
    {
        if (!IsServer || !IsSpawned)
            return 0;

        _nextPoisonAttackId++;
        if (_nextPoisonAttackId == 0)
            _nextPoisonAttackId++;

        PlayPoisonSprayClientRpc(_nextPoisonAttackId, skillId, start, control, end,
            startTime, warningSeconds, flightSeconds);
        return _nextPoisonAttackId;
    }

    public void BroadcastPoisonSprayFinished(uint attackId)
    {
        if (IsServer && IsSpawned && attackId != 0)
            FinishPoisonSprayClientRpc(attackId);
    }

    [ClientRpc]
    private void PlayPoisonSprayClientRpc(uint attackId, string skillId,
        Vector3 start, Vector3 control, Vector3 end, double startTime,
        float warningSeconds, float flightSeconds)
    {
        if (IsServer)
            return;

        StopPoisonVisual();
        _clientPoisonAttackId = attackId;
        ShowPoisonVisualAsync(attackId, skillId, start, control, end,
            startTime, warningSeconds, flightSeconds).Forget();
    }

    [ClientRpc]
    private void FinishPoisonSprayClientRpc(uint attackId)
    {
        if (!IsServer && _clientPoisonAttackId == attackId)
            StopPoisonVisual();
    }

    private void StopPoisonVisual()
    {
        _clientPoisonAttackId = 0;
        if (_poisonVisual != null)
            _poisonVisual.gameObject.SetActive(false);
    }

    private void DestroyPoisonVisual()
    {
        StopPoisonVisual();
        if (_poisonVisual != null)
            Destroy(_poisonVisual.gameObject);
        _poisonVisual = null;
        _poisonVisualSkillId = null;
    }

    private async UniTask ShowPoisonVisualAsync(uint attackId, string skillId,
        Vector3 start, Vector3 control, Vector3 end, double startTime,
        float warningSeconds, float flightSeconds)
    {
        BossPoisonProjectile visual = null;
        try
        {
            if (this == null || !IsSpawned || _clientPoisonAttackId != attackId)
                return;

            if (_poisonVisual == null || _poisonVisualSkillId != skillId)
            {
                if (DataManager.Inst == null || ResourceManager.Inst == null)
                    return;
                MonsterSkillData data = DataManager.Inst.GetData<MonsterSkillData>(skillId);
                if (data == null || string.IsNullOrWhiteSpace(data.SkillPrefabPath))
                    return;

                GameObject prefab = await ResourceManager.Inst.LoadAsset<GameObject>(data.SkillPrefabPath);
                if (this == null || !IsSpawned || _clientPoisonAttackId != attackId || prefab == null)
                    return;
                if (NetworkManager.ServerTime.Time >= startTime + warningSeconds + flightSeconds)
                    return;
                if (!prefab.TryGetComponent(out BossPoisonProjectile projectile))
                {
                    Debug.LogError($"독액 프리팹에 BossPoisonProjectile이 없습니다: {skillId}", this);
                    return;
                }

                if (_poisonVisual != null)
                    Destroy(_poisonVisual.gameObject);
                _poisonVisual = Instantiate(projectile, start, Quaternion.identity);
                _poisonVisual.transform.localScale = Vector3.one;
                _poisonVisual.gameObject.SetActive(false);
                _poisonVisualSkillId = skillId;
            }

            if (NetworkManager.ServerTime.Time >= startTime + warningSeconds + flightSeconds)
                return;

            visual = _poisonVisual;
            visual.transform.SetPositionAndRotation(start, Quaternion.identity);
            visual.gameObject.SetActive(true);
            visual.Play(this, false, default, start, control, end, startTime, warningSeconds, flightSeconds);
            await UniTask.WaitUntil(() => this == null || !IsSpawned
                || _clientPoisonAttackId != attackId || visual == null || !visual.IsPlaying);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            if (this != null && _clientPoisonAttackId == attackId)
                StopPoisonVisual();
        }
    }
}
