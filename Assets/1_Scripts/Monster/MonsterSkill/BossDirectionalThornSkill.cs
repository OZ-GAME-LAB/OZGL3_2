using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class BossDirectionalThornSkill : MonsterSkillBase
{
    [SerializeField, Min(0f)]
    private float _firstWarningSeconds = 1f;

    [SerializeField, Min(0.01f)]
    private float _warningSeconds = 0.5f;

    [SerializeField, Min(0.01f)]
    private float _targetThornWidth = 2f;

    [SerializeField, Min(0.01f)]
    private float _lastThornHeight = 1f;


    private Boss _boss;
    private BossNetwork _bossNetwork;
    private BossThornAttackObject _attackPrefab;

    private float _firstThornHeight;
    private float _attackPower;

    private readonly List<BossThornAttackObject> _spawnedThorns = new();

    private void OnDisable()
    {
        StopAllCoroutines();
        ClearSpawnedThorns();
    }

    public override void InitSkill(Transform owner, float bodyHeight, float atk, GameObject attackPrefab)
    {
        if (_attackPrefab != null)
            return;

        if (owner == null || attackPrefab == null)
        {
            Debug.LogError("보스 또는 기둥 공격 프리팹이 없습니다.", this);
            return;
        }

        if (!owner.TryGetComponent(out _boss) || !owner.TryGetComponent(out _bossNetwork))
        {
            Debug.LogError("스킬 소유 오브젝트에 Boss와 BossNetwork가 필요합니다.", this);
            return;
        }

        if (float.IsNaN(bodyHeight) || float.IsInfinity(bodyHeight) || bodyHeight <= 0f)
        {
            Debug.LogError("보스의 높이가 올바르지 않습니다.", this);
            return;
        }

        if (!attackPrefab.TryGetComponent(out BossThornAttackObject prefabComponent))
        {
            Debug.LogError("기둥 프리팹 루트에 BossThornAttackObject가 필요합니다.", this);
            return;
        }

        if (attackPrefab.GetComponentInChildren<BossThornHitbox>(true) == null)
        {
            Debug.LogError("기둥 프리팹에 BossThornHitbox가 필요합니다.", this);
            return;
        }

        _firstThornHeight = bodyHeight;
        _attackPower = atk;
        _attackPrefab = prefabComponent;
    }

    private void CalculateThornLayout(Bounds attackBounds, int direction, out Vector3[] positions, out float[] heights, out float thornWidth)
    {
        float targetWidth = Mathf.Max(0.01f, _targetThornWidth);
        int count = Mathf.Max(1, Mathf.CeilToInt(attackBounds.size.x / targetWidth));

        thornWidth = attackBounds.size.x / count;

        positions = new Vector3[count];
        heights = new float[count];

        float lastHeight = Mathf.Min(_firstThornHeight, _lastThornHeight);

        for (int i = 0; i < count; i++)
        {
            float offset = thornWidth * (i + 0.5f);

            float x = direction > 0
                ? attackBounds.min.x + offset
                : attackBounds.max.x - offset;

            positions[i] = new Vector3(x, attackBounds.max.y, attackBounds.center.z);

            float progress = count > 1 ? (float)i / (count - 1) : 0f;
            heights[i] = Mathf.Lerp(_firstThornHeight, lastHeight, progress);
        }
    }

    public override void UseSkill(Transform target, Vector3 attackPosition)
    {
        if (!isActiveAndEnabled || IsAttacking || !IsCooldownComplete)
            return;

        if (_boss == null || _bossNetwork == null || _attackPrefab == null)
            return;

        if (!_bossNetwork.IsServer || !_bossNetwork.IsSpawned)
            return;

        if (!_boss.TrySelectNearAttackDirection(out int direction))
            return;

        if (!_boss.TryGetDirectionalThornBounds(direction, out Bounds attackBounds))
            return;

        CalculateThornLayout(attackBounds, direction, out Vector3[] positions, out float[] heights, out float thornWidth);

        IsAttacking = true;
        StartCoroutine(PlayThornSequence(positions, heights, thornWidth));
    }

    private IEnumerator PlayThornSequence(Vector3[] positions, float[] heights, float thornWidth)
    {
        var manager = _bossNetwork.NetworkManager;

        double startTime = manager.ServerTime.Time;
        float holdSeconds = _warningSeconds;

        BossThornAttackObject lastThorn = null;

        for (int i = 0; i < positions.Length; i++)
        {
            while (manager != null && manager.IsListening && manager.ServerTime.Time < startTime)
                yield return null;

            if (manager == null || !manager.IsListening || _bossNetwork == null || !_bossNetwork.IsSpawned)
                break;

            float warningSeconds = i == 0 ? _firstWarningSeconds : holdSeconds;

            BossThornAttackObject thorn = Instantiate(_attackPrefab, positions[i], Quaternion.identity);
            thorn.transform.localScale = Vector3.one;
            thorn.gameObject.SetActive(false);

            _spawnedThorns.Add(thorn);

            if (!thorn.Initialize(thornWidth, heights[i], holdSeconds, warningSeconds))
                break;

            BossThornHitbox hitbox = thorn.GetComponentInChildren<BossThornHitbox>(true);
            hitbox.Initialize(_bossNetwork, _attackPower);

            if (i == 0)
                thorn.AttackStarted += StartCooldown;

            thorn.gameObject.SetActive(true);
            thorn.Play(startTime);

            if (!thorn.IsPlaying)
                break;

            _bossNetwork.BroadcastThornAttack(SkillId, positions[i], thornWidth, heights[i], startTime, holdSeconds, warningSeconds);

            lastThorn = thorn;
            startTime += warningSeconds + thorn.RiseSeconds;
        }

        while (lastThorn != null && lastThorn.IsPlaying)
            yield return null;

        ClearSpawnedThorns();
    }

    private void ClearSpawnedThorns()
    {
        foreach (BossThornAttackObject thorn in _spawnedThorns)
        {
            if (thorn == null)
                continue;

            thorn.AttackStarted -= StartCooldown;
            thorn.gameObject.SetActive(false);
            Destroy(thorn.gameObject);
        }

        _spawnedThorns.Clear();
        IsAttacking = false;
    }


}
