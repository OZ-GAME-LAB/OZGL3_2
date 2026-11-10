using UnityEngine;
using UnityEngine.Scripting;
using System.Collections;

[Preserve]
public class BossThornSkill : MonsterSkillBase
{
    private Boss _boss;
    private BossThornAttackObject _attackObject;

    [SerializeField, Min(0.01f)]
    private float _thornHeight = 2f;

    private void OnDestroy()
    {
        if (_attackObject == null)
            return;

        _attackObject.AttackStarted -= StartCooldown;

        Destroy(_attackObject.gameObject);
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        if (_attackObject != null)
            _attackObject.gameObject.SetActive(false);

        IsAttacking = false;
    }

    public override void InitSkill(Transform owner, float bodyHeight, float atk, GameObject attackPrefab)
    {
        if (_attackObject != null)
            return;

        if (owner == null || attackPrefab == null)
        {
            Debug.LogError("보스 또는 가시 공격 프리팹이 없습니다.", this);
            return;
        }

        if (!owner.TryGetComponent(out _boss))
        {
            Debug.LogError("스킬 소유 오브젝트에 Boss가 필요합니다.", this);
            return;
        }

        if (!attackPrefab.TryGetComponent(out BossThornAttackObject prefabComponent))
        {
            Debug.LogError("공격 프리팹 루트에 BossThornAttackObject가 필요합니다.", this);
            return;
        }

        _attackObject = Instantiate(prefabComponent, owner.position, Quaternion.identity);

        _attackObject.transform.localScale = Vector3.one;
        _attackObject.gameObject.SetActive(false);

        _attackObject.AttackStarted += StartCooldown;
    }

    public override void UseSkill(Transform target, Vector3 attackPosition)
    {
        if (!isActiveAndEnabled || IsAttacking || !IsCooldownComplete)
            return;

        var manager = Unity.Netcode.NetworkManager.Singleton;

        if (manager == null || !manager.IsServer)
            return;

        if (_boss == null || _attackObject == null)
            return;

        if (!_boss.TrySelectGroundAttackArea(out BossDetector selectedArea))
            return;

        if (!selectedArea.TryGetComponent(out BoxCollider2D areaCollider))
        {
            Debug.LogError("선택한 지면에 BoxCollider2D가 없습니다.", selectedArea);
            return;
        }

        Bounds bounds = areaCollider.bounds;

        Vector3 position = new Vector3(bounds.center.x, bounds.max.y, selectedArea.transform.position.z);

        _attackObject.transform.SetPositionAndRotation(position, Quaternion.identity);

        if (!_attackObject.Initialize(bounds.size.x, _thornHeight))
            return;

        _attackObject.gameObject.SetActive(true);
        _attackObject.Play();

        if (!_attackObject.IsPlaying)
        {
            _attackObject.gameObject.SetActive(false);
            return;
        }

        IsAttacking = true;
        StartCoroutine(WaitForAttackEnd());
    }

    private IEnumerator WaitForAttackEnd()
    {
        while (_attackObject != null && _attackObject.IsPlaying)
        {
            yield return null;
        }

        if (_attackObject != null)
            _attackObject.gameObject.SetActive(false);

        IsAttacking = false;
    }
}
