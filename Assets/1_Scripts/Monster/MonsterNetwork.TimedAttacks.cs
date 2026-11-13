using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public partial class MonsterNetwork
{
    private sealed class TimedAttackPool
    {
        public MonsterTimedAttackObject Prefab;
        public readonly List<MonsterTimedAttackObject> Objects = new();
    }

    private struct TimedAttackRequest
    {
        public string SkillId;
        public Vector3 Position;
        public Vector2 Direction;
        public double StartTime;
        public float Duration;
    }

    private readonly Dictionary<string, TimedAttackPool> _timedAttackPools = new();
    private readonly Dictionary<uint, MonsterTimedAttackObject> _activeTimedAttacks = new();
    private readonly Dictionary<MonsterTimedAttackObject, uint> _timedAttackIds = new();
    private readonly Dictionary<uint, TimedAttackRequest> _pendingTimedAttacks = new();
    private GameObject _timedAttackRoot;
    private GameObject _timedAttackCreationRoot;
    private uint _nextTimedAttackId;
    private uint _timedAttackVersion;
    private bool _timedAttackVisualsReady;

    public bool RegisterTimedAttackPrefab(string skillId, MonsterTimedAttackObject prefab)
    {
        if (!IsSpawned || string.IsNullOrWhiteSpace(skillId) || prefab == null || !prefab.HasValidSettings())
            return false;

        if (_timedAttackPools.ContainsKey(skillId))
            return true;

        if (_timedAttackRoot == null)
        {
            _timedAttackRoot = new GameObject($"{name}_TimedAttacks");
            _timedAttackCreationRoot = new GameObject("InactiveCreation");
            _timedAttackCreationRoot.SetActive(false);
            _timedAttackCreationRoot.transform.SetParent(_timedAttackRoot.transform, false);
        }

        TimedAttackPool pool = new TimedAttackPool { Prefab = prefab };
        _timedAttackPools.Add(skillId, pool);
        CreateTimedAttack(pool);
        return true;
    }

    private MonsterTimedAttackObject CreateTimedAttack(TimedAttackPool pool)
    {
        MonsterTimedAttackObject instance = Instantiate(pool.Prefab, _timedAttackCreationRoot.transform, false);
        instance.gameObject.SetActive(false);
        instance.transform.SetParent(_timedAttackRoot.transform, false);
        foreach (Collider2D collider in instance.GetComponentsInChildren<Collider2D>(true))
            collider.enabled = false;
        instance.Finished += OnTimedAttackFinished;
        pool.Objects.Add(instance);
        return instance;
    }

    private MonsterTimedAttackObject GetTimedAttack(string skillId)
    {
        if (!_timedAttackPools.TryGetValue(skillId, out TimedAttackPool pool))
            return null;

        foreach (MonsterTimedAttackObject instance in pool.Objects)
        {
            if (instance != null && !instance.IsPlaying)
                return instance;
        }

        return CreateTimedAttack(pool);
    }

    public bool PlayTimedAttackOnServer(string skillId, RuneDamageInfo damageInfo, Vector3 position, Vector2 direction)
    {
        if (!IsServer || !IsSpawned || !IsAlive || !IsReady)
            return false;

        MonsterTimedAttackObject instance = GetTimedAttack(skillId);
        if (instance == null)
            return false;

        uint attackId = ++_nextTimedAttackId;
        double startTime = NetworkManager.ServerTime.Time;
        float duration = instance.DurationSeconds;
        TrackTimedAttack(attackId, instance);
        PlayTimedAttackClientRpc(attackId, skillId, position, direction, startTime, duration);
        instance.Play(this, damageInfo, position, direction, startTime, duration);
        return true;
    }

    private void TrackTimedAttack(uint attackId, MonsterTimedAttackObject instance)
    {
        _activeTimedAttacks[attackId] = instance;
        _timedAttackIds[instance] = attackId;
    }

    private void OnTimedAttackFinished(MonsterTimedAttackObject instance)
    {
        if (!_timedAttackIds.TryGetValue(instance, out uint attackId))
            return;

        _timedAttackIds.Remove(instance);
        _activeTimedAttacks.Remove(attackId);
        if (IsServer && IsSpawned)
            FinishTimedAttackClientRpc(attackId);
    }

    [ClientRpc]
    private void PlayTimedAttackClientRpc(uint attackId, string skillId, Vector3 position,
        Vector2 direction, double startTime, float duration)
    {
        if (IsServer || !IsSpawned)
            return;

        TimedAttackRequest request = new TimedAttackRequest
        {
            SkillId = skillId, Position = position, Direction = direction,
            StartTime = startTime, Duration = duration
        };
        if (_timedAttackVisualsReady)
            PlayTimedAttackVisual(attackId, request);
        else
            _pendingTimedAttacks[attackId] = request;
    }

    [ClientRpc]
    private void FinishTimedAttackClientRpc(uint attackId)
    {
        if (IsServer)
            return;

        _pendingTimedAttacks.Remove(attackId);
        if (_activeTimedAttacks.TryGetValue(attackId, out MonsterTimedAttackObject instance))
            instance.Stop();
    }

    private void PlayTimedAttackVisual(uint attackId, TimedAttackRequest request)
    {
        if (!IsSpawned || !IsAlive || NetworkManager.ServerTime.Time >= request.StartTime + request.Duration)
            return;

        MonsterTimedAttackObject instance = GetTimedAttack(request.SkillId);
        if (instance == null)
            return;

        TrackTimedAttack(attackId, instance);
        instance.Play(this, default, request.Position, request.Direction, request.StartTime, request.Duration);
    }

    private async UniTask<bool> PrepareTimedAttackVisualsAsync(string monsterId)
    {
        uint version = _timedAttackVersion;
        try
        {
            MonsterData data = DataManager.Inst.GetData<MonsterData>(monsterId);
            if (data == null)
                return false;

            if (data.HaveSkillIdList != null)
            {
                foreach (string skillId in data.HaveSkillIdList)
                {
                    MonsterSkillData skillData = DataManager.Inst.GetData<MonsterSkillData>(skillId);
                    if (skillData == null || !MonsterSkillManager.Inst.TryGetSkillType(skillData.SkillClassName, out System.Type type))
                        return false;

                    bool ranged = typeof(MonsterRangedAtk).IsAssignableFrom(type);
                    bool mist = typeof(MonsterPoisonMistSkill).IsAssignableFrom(type);
                    if (!ranged && !mist)
                        continue;

                    GameObject prefab = await ResourceManager.Inst.LoadAsset<GameObject>(skillData.SkillPrefabPath);
                    if (this == null || !IsSpawned || version != _timedAttackVersion)
                        return false;

                    if (prefab == null || !prefab.TryGetComponent(out MonsterTimedAttackObject effect)
                        || (ranged && !(effect is MonsterRangedProjectile))
                        || (mist && !(effect is MonsterPoisonMistObject))
                        || !RegisterTimedAttackPrefab(skillId, effect))
                    {
                        Debug.LogError($"몬스터 공격 연출 프리팹의 컴포넌트와 설정을 확인하세요: {skillId}", this);
                        return false;
                    }
                }
            }

            _timedAttackVisualsReady = true;
            foreach (KeyValuePair<uint, TimedAttackRequest> request in _pendingTimedAttacks)
                PlayTimedAttackVisual(request.Key, request.Value);
            _pendingTimedAttacks.Clear();
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
        }
    }

    private void DestroyTimedAttacks()
    {
        _timedAttackVersion++;
        _timedAttackVisualsReady = false;
        _pendingTimedAttacks.Clear();
        _activeTimedAttacks.Clear();
        _timedAttackIds.Clear();
        _timedAttackPools.Clear();
        if (_timedAttackRoot != null)
        {
            _timedAttackRoot.SetActive(false);
            Destroy(_timedAttackRoot);
            _timedAttackRoot = null;
            _timedAttackCreationRoot = null;
        }
    }
}
