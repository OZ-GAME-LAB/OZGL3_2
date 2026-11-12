using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class BossPlatformThornSkill : MonsterSkillBase
{
    private Boss _boss;
    private BossNetwork _bossNetwork;

    private readonly BossThornAttackObject[] _attackObjects = new BossThornAttackObject[2];

    private bool _initialized;
    private bool _cooldownStarted;

    [SerializeField, Min(0.01f)]
    private float _thornHeight = 2f;

    [SerializeField, Min(0f)] private float _holdSeconds = 2f;

    private readonly List<Transform> _selectedPlatforms = new();


    private void OnDisable()
    {
        StopAllCoroutines();

        foreach (BossThornAttackObject attackObject in _attackObjects)
        {
            if (attackObject != null)
                attackObject.gameObject.SetActive(false);
        }

        for (int i = 0; i < _selectedPlatforms.Count; i++)
            ReleaseSelectedPlatform(i);

        _selectedPlatforms.Clear();
        IsAttacking = false;
    }

    private void OnDestroy()
    {
        foreach (BossThornAttackObject attackObject in _attackObjects)
        {
            if (attackObject == null)
                continue;

            attackObject.AttackStarted -= OnAttackStarted;
            Destroy(attackObject.gameObject);
        }
    }

    public override void InitSkill(Transform owner, float bodyHeight, float atk, GameObject attackPrefab)
    {
        if (_initialized)
            return;

        if (owner == null || attackPrefab == null)
        {
            Debug.LogError("보스 또는 발판 침식 프리팹이 없습니다.", this);
            return;
        }

        if (!owner.TryGetComponent(out _boss) || !owner.TryGetComponent(out _bossNetwork))
        {
            Debug.LogError("스킬 소유 오브젝트에 Boss와 BossNetwork가 필요합니다.", this);
            return;
        }

        if (!attackPrefab.TryGetComponent(out BossThornAttackObject prefabComponent))
        {
            Debug.LogError("공격 프리팹 루트에 BossThornAttackObject가 필요합니다.", this);
            return;
        }

        if (attackPrefab.GetComponentInChildren<BossThornHitbox>(true) == null)
        {
            Debug.LogError("공격 프리팹의 Hitbox에 BossThornHitbox가 필요합니다.", this);
            return;
        }

        for (int i = 0; i < _attackObjects.Length; i++)
        {
            BossThornAttackObject instance = Instantiate(prefabComponent, owner.position, Quaternion.identity);

            instance.transform.localScale = Vector3.one;
            instance.gameObject.SetActive(false);

            BossThornHitbox hitbox = instance.GetComponentInChildren<BossThornHitbox>(true);
            hitbox.Initialize(_bossNetwork, atk);

            instance.AttackStarted += OnAttackStarted;
            _attackObjects[i] = instance;
        }

        _initialized = true;
    }

    private void OnAttackStarted()
    {
        if (_cooldownStarted)
            return;

        _cooldownStarted = true;
        StartCooldown();
    }

    public override void UseSkill(Transform target, Vector3 attackPosition)
    {
        if (!_initialized || !isActiveAndEnabled || IsAttacking || !IsCooldownComplete)
            return;

        if (_boss == null || _bossNetwork == null)
            return;

        if (!_bossNetwork.IsServer || !_bossNetwork.IsSpawned)
            return;

        if (!_boss.TryReservePlatforms(_selectedPlatforms))
            return;

        _cooldownStarted = false;
        IsAttacking = true;

        double startTime = _bossNetwork.NetworkManager.ServerTime.Time;

        for (int i = 0; i < _selectedPlatforms.Count; i++)
        {
            Transform platform = _selectedPlatforms[i];
            BossThornAttackObject attackObject = _attackObjects[i];

            if (platform == null || attackObject == null)
            {
                ReleaseSelectedPlatform(i);
                continue;
            }

            if (!platform.TryGetComponent(out BoxCollider2D area))
            {
                ReleaseSelectedPlatform(i);
                continue;
            }

            Bounds bounds = area.bounds;
            Vector3 position = new Vector3(bounds.center.x, bounds.max.y, platform.position.z);

            attackObject.transform.SetPositionAndRotation(position, Quaternion.identity);

            if (!attackObject.Initialize(bounds.size.x, _thornHeight, _holdSeconds))
            {
                ReleaseSelectedPlatform(i);
                continue;
            }

            attackObject.gameObject.SetActive(true);
            attackObject.Play(startTime);

            if (!attackObject.IsPlaying)
            {
                attackObject.gameObject.SetActive(false);
                ReleaseSelectedPlatform(i);
                continue;
            }

            Debug.Log($"침식 발판: {platform.name}, 위치: {position}, 너비: {bounds.size.x}", attackObject);

            _bossNetwork.BroadcastThornAttack(SkillId, position, bounds.size.x, _thornHeight, startTime, _holdSeconds, attackObject.WarningSeconds);
        }

        StartCoroutine(WaitForAttacksEnd());
    }

    

    private void ReleaseSelectedPlatform(int index)
    {
        Transform platform = _selectedPlatforms[index];

        if (_boss != null)
            _boss.ReleasePlatform(platform);

        _selectedPlatforms[index] = null;
    }

    private IEnumerator WaitForAttacksEnd()
    {
        while (true)
        {
            bool anyPlaying = false;

            for (int i = 0; i < _selectedPlatforms.Count; i++)
            {
                BossThornAttackObject attackObject = _attackObjects[i];

                if (attackObject != null && attackObject.IsPlaying)
                {
                    anyPlaying = true;
                    continue;
                }

                if (attackObject != null)
                    attackObject.gameObject.SetActive(false);

                ReleaseSelectedPlatform(i);
            }

            if (!anyPlaying)
                break;

            yield return null;
        }

        _selectedPlatforms.Clear();
        IsAttacking = false;
    }
}
