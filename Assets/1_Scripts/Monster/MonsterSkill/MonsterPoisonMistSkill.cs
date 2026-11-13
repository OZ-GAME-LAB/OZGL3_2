using System.Collections;
using UnityEngine;

[UnityEngine.Scripting.Preserve]
public class MonsterPoisonMistSkill : MonsterSkillBase
{
    private Monster _monster;
    private MonsterNetwork _network;
    private float _attackPower;

    public override bool InitSkill(Transform owner, float bodyHeight, float atk, GameObject attackPrefab)
    {
        if (owner == null || !owner.TryGetComponent(out _monster) || !owner.TryGetComponent(out _network))
            return FailInitialization("몬스터 본체에 Monster와 MonsterNetwork가 필요합니다.");

        if (attackPrefab == null || !attackPrefab.TryGetComponent(out MonsterPoisonMistObject mist)
            || !_network.RegisterTimedAttackPrefab(SkillId, mist))
            return FailInitialization("독안개 프리팹의 MonsterPoisonMistObject, Trigger Collider2D와 유지시간을 확인하세요.");

        _attackPower = atk;
        return true;
    }

    public override void UseSkill(Transform target, Vector3 attackPosition)
    {
        if (IsAttacking || !isActiveAndEnabled || !IsCooldownComplete
            || _network == null || !_network.IsServer || !_network.IsSpawned || !_monster.CanAttack)
            return;

        _monster.MonsterStopMove();
        if (!_network.PlayTimedAttackOnServer(SkillId, CreateDamageInfo(_attackPower), transform.position, Vector2.zero))
            return;

        StartCooldown();
        IsAttacking = true;
        StartCoroutine(FinishCast());
    }

    private IEnumerator FinishCast()
    {
        yield return null;
        IsAttacking = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        IsAttacking = false;
    }
}
