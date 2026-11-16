using System.Collections;
using UnityEngine;

[UnityEngine.Scripting.Preserve]
public class MonsterRangedAtk : MonsterSkillBase
{
    private Monster _monster;
    private MonsterNetwork _network;
    private float _windupSeconds;
    private float _attackPower;
    public override bool StopAtAttackRange => true;

    public override bool InitSkill(Transform owner, float bodyHeight, float atk, GameObject attackPrefab)
    {
        if (owner == null || !owner.TryGetComponent(out _monster) || !owner.TryGetComponent(out _network))
            return FailInitialization("몬스터 본체에 Monster와 MonsterNetwork가 필요합니다.");

        if (attackPrefab == null || !attackPrefab.TryGetComponent(out MonsterRangedProjectile projectile)
            || !_network.RegisterTimedAttackPrefab(SkillId, projectile))
            return FailInitialization("투사체 프리팹의 MonsterRangedProjectile, Sprite, 속도와 시간을 확인하세요.");

        _windupSeconds = projectile.WindupSeconds;
        _attackPower = atk;
        return true;
    }

    public override void UseSkill(Transform target, Vector3 attackPosition)
    {
        if (IsAttacking || !isActiveAndEnabled || !IsCooldownComplete || target == null
            || _network == null || !_network.IsServer || !_network.IsSpawned || !_monster.CanAttack)
            return;

        IsAttacking = true;
        _monster.MonsterStopMove();
        StartCoroutine(FireRoutine(target));
    }

    private IEnumerator FireRoutine(Transform target)
    {
        float remaining = _windupSeconds;
        while (remaining > 0f && _network != null && _network.IsSpawned && _network.IsAlive)
        {
            yield return null;
            remaining -= Time.deltaTime;
        }

        if (target != null && target.gameObject.activeInHierarchy
            && _network != null && _network.IsSpawned && _network.IsAlive)
        {
            Transform origin = _monster.GetAttackPoint(target);
            Collider2D targetCollider = target.GetComponent<Collider2D>();
            Vector3 targetPosition = targetCollider != null ? targetCollider.bounds.center : target.position;
            if (origin != null)
            {
                Vector2 direction = targetPosition - origin.position;
                if (direction.sqrMagnitude < 0.0001f)
                    direction = target.position.x >= transform.position.x ? Vector2.right : Vector2.left;
                if (_network.PlayTimedAttackOnServer(SkillId, CreateDamageInfo(_attackPower), origin.position, direction.normalized))
                    StartCooldown();
            }
        }

        yield return null;
        IsAttacking = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        IsAttacking = false;
    }
}
