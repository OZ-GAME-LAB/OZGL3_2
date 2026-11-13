using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public partial class MonsterNetwork
{
    private readonly Dictionary<string, MonsterMeleeAtkObj> _meleeVisuals = new();
    private GameObject _meleeVisualRoot;
    private MonsterMeleeAtkObj _activeMeleeVisual;
    private Coroutine _meleeVisualRoutine;
    private uint _meleeVisualVersion;
    private uint _nextMeleeAttackId;
    private uint _lastMeleeAttackId;
    private bool _meleeVisualsReady;
    private MeleeVisualRequest? _pendingMeleeVisual;

    private struct MeleeVisualRequest
    {
        public string SkillId;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
        public Vector2 Size;
        public double StartTime;
        public float Duration;
    }

    private async UniTask<bool> PrepareMeleeVisualsAsync(string monsterId)
    {
        uint version = _meleeVisualVersion;

        try
        {
            if (DataManager.Inst == null || ResourceManager.Inst == null || MonsterSkillManager.Inst == null)
            {
                Debug.LogError($"몬스터 공격 연출 준비에 필요한 매니저가 없습니다: {monsterId}", this);
                return false;
            }

            MonsterData monsterData = DataManager.Inst.GetData<MonsterData>(monsterId);
            if (monsterData == null)
                return false;

            if (monsterData.HaveSkillIdList != null)
            {
                foreach (string skillId in monsterData.HaveSkillIdList)
                {
                    MonsterSkillData skillData = DataManager.Inst.GetData<MonsterSkillData>(skillId);
                    if (skillData == null)
                    {
                        Debug.LogError($"몬스터 공격 연출의 스킬 데이터가 없습니다: {skillId}", this);
                        return false;
                    }

                    if (!MonsterSkillManager.Inst.TryGetSkillType(skillData.SkillClassName, out System.Type skillType))
                        return false;

                    if (!typeof(MonsterMeleeAtk).IsAssignableFrom(skillType) || _meleeVisuals.ContainsKey(skillId))
                        continue;

                    if (string.IsNullOrWhiteSpace(skillData.SkillPrefabPath))
                    {
                        Debug.LogError($"몬스터 공격 연출의 프리팹 주소가 없습니다: {skillId}", this);
                        return false;
                    }

                    GameObject prefab = await ResourceManager.Inst.LoadAsset<GameObject>(skillData.SkillPrefabPath);
                    if (this == null || !IsSpawned || version != _meleeVisualVersion)
                        return false;

                    if (prefab == null || !prefab.TryGetComponent(out MonsterMeleeAtkObj prefabHitbox)
                        || !prefab.TryGetComponent<BoxCollider2D>(out _))
                    {
                        Debug.LogError($"몬스터 공격 연출 프리팹의 MonsterMeleeAtkObj와 BoxCollider2D를 확인하세요: {skillId}", this);
                        return false;
                    }

                    if (_meleeVisualRoot == null)
                    {
                        _meleeVisualRoot = new GameObject("AttackVisuals");
                        _meleeVisualRoot.SetActive(false);
                        _meleeVisualRoot.transform.SetParent(transform, false);
                    }

                    MonsterMeleeAtkObj visual = Instantiate(prefabHitbox, _meleeVisualRoot.transform, false);
                    visual.gameObject.SetActive(false);
                    visual.enabled = false;

                    foreach (Collider2D collider in visual.GetComponentsInChildren<Collider2D>(true))
                        collider.enabled = false;

                    _meleeVisuals.Add(skillId, visual);
                }
            }

            if (_meleeVisualRoot != null)
                _meleeVisualRoot.SetActive(true);

            _meleeVisualsReady = true;
            TryPlayPendingMeleeVisual();
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
        }
    }

    public uint BroadcastMeleeAttack(string skillId, Transform attackTransform, Vector2 size, double startTime, float duration)
    {
        if (!IsServer || !IsSpawned || attackTransform == null)
            return 0;

        uint attackId = ++_nextMeleeAttackId;
        PlayMeleeAttackClientRpc(attackId, skillId, attackTransform.localPosition,
            attackTransform.localRotation, attackTransform.localScale, size, startTime, duration);
        return attackId;
    }

    public void BroadcastMeleeAttackFinished(uint attackId)
    {
        if (IsServer && IsSpawned && attackId != 0)
            FinishMeleeAttackClientRpc(attackId);
    }

    [ClientRpc]
    private void PlayMeleeAttackClientRpc(uint attackId, string skillId, Vector3 localPosition,
        Quaternion localRotation, Vector3 localScale, Vector2 size, double startTime, float duration)
    {
        if (IsServer || !IsSpawned || attackId <= _lastMeleeAttackId)
            return;

        _lastMeleeAttackId = attackId;
        _pendingMeleeVisual = new MeleeVisualRequest
        {
            SkillId = skillId,
            LocalPosition = localPosition,
            LocalRotation = localRotation,
            LocalScale = localScale,
            Size = size,
            StartTime = startTime,
            Duration = duration
        };
        TryPlayPendingMeleeVisual();
    }

    [ClientRpc]
    private void FinishMeleeAttackClientRpc(uint attackId)
    {
        if (IsServer || attackId != _lastMeleeAttackId)
            return;

        _pendingMeleeVisual = null;
        StopMeleeVisual();
    }

    private void TryPlayPendingMeleeVisual()
    {
        if (!_meleeVisualsReady || !_pendingMeleeVisual.HasValue)
            return;

        MeleeVisualRequest request = _pendingMeleeVisual.Value;
        _pendingMeleeVisual = null;
        StopMeleeVisual();

        if (!IsSpawned || !IsAlive || NetworkManager == null
            || NetworkManager.ServerTime.Time >= request.StartTime + request.Duration)
            return;

        if (!_meleeVisuals.TryGetValue(request.SkillId, out MonsterMeleeAtkObj visual) || visual == null)
        {
            Debug.LogError($"준비된 몬스터 공격 연출이 없습니다: {request.SkillId}", this);
            return;
        }

        visual.transform.SetLocalPositionAndRotation(request.LocalPosition, request.LocalRotation);
        visual.transform.localScale = request.LocalScale;
        visual.SetSize(request.Size);
        _activeMeleeVisual = visual;
        _meleeVisualRoutine = StartCoroutine(PlayMeleeVisualRoutine(request.StartTime, request.Duration));
    }

    private IEnumerator PlayMeleeVisualRoutine(double startTime, float duration)
    {
        while (IsSpawned && IsAlive && NetworkManager.ServerTime.Time < startTime)
            yield return null;

        double endTime = startTime + duration;
        if (IsSpawned && IsAlive && NetworkManager.ServerTime.Time < endTime && _activeMeleeVisual != null)
        {
            _activeMeleeVisual.gameObject.SetActive(true);

            while (IsSpawned && IsAlive && NetworkManager.ServerTime.Time < endTime)
                yield return null;
        }

        if (_activeMeleeVisual != null)
            _activeMeleeVisual.gameObject.SetActive(false);

        _activeMeleeVisual = null;
        _meleeVisualRoutine = null;
    }

    private void StopMeleeVisual()
    {
        if (_meleeVisualRoutine != null)
        {
            StopCoroutine(_meleeVisualRoutine);
            _meleeVisualRoutine = null;
        }

        if (_activeMeleeVisual != null)
            _activeMeleeVisual.gameObject.SetActive(false);

        _activeMeleeVisual = null;
    }

    private void DestroyMeleeVisuals()
    {
        _meleeVisualVersion++;
        _meleeVisualsReady = false;
        _pendingMeleeVisual = null;
        StopMeleeVisual();
        _meleeVisuals.Clear();

        if (_meleeVisualRoot != null)
        {
            _meleeVisualRoot.SetActive(false);
            Destroy(_meleeVisualRoot);
            _meleeVisualRoot = null;
        }
    }
}
