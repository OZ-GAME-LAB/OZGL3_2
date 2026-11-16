using System.Collections;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;

public class MonsterMeleeAtk : MonsterSkillBase
{
    [SerializeField, Min(0.01f)]
    private float _attackWidth = 1f;

    [SerializeField, Min(0.01f)]
    private float _attackDuration = 0.3f;
    [SerializeField, Min(0f)]
    private float _dashPower = 5f;

    private GameObject _attackObject;
    private Rigidbody2D _ownerBody;
    private Monster _ownerMonster;
    private MonsterNetwork _ownerNetwork;
    private uint _attackId;

    protected virtual bool CanDashOnAttack => true;
    public override bool IsDashing => IsAttacking && CanDashOnAttack
        && _ownerMonster != null && _ownerMonster.CanMove;


    public override bool InitSkill(Transform owner, float bodyHeight, float atk, GameObject attackPrefab)
    {
        if (owner == null || attackPrefab == null || bodyHeight <= 0f)
        {
            return FailInitialization("몬스터 본체, 공격 프리팹, 몸 높이를 확인하세요.");
        }

        if (!owner.TryGetComponent(out _ownerMonster) || !owner.TryGetComponent(out _ownerNetwork))
            return FailInitialization("몬스터 본체에 Monster와 MonsterNetwork가 필요합니다.");

        float ownerScaleY = Mathf.Abs(owner.lossyScale.y);

        if (ownerScaleY <= 0f)
            return FailInitialization("몬스터 본체의 Y 크기는 0보다 커야 합니다.");

        if (!owner.TryGetComponent(out _ownerBody))
        {
            return FailInitialization("몬스터 본체에 Rigidbody2D가 필요합니다.");
        }

        if (!attackPrefab.TryGetComponent<BoxCollider2D>(out _) || !attackPrefab.TryGetComponent<MonsterMeleeAtkObj>(out _))
        {
            return FailInitialization("공격 프리팹 루트에 BoxCollider2D와 MonsterMeleeAtkObj가 필요합니다.");
        }

        _attackObject = Instantiate(attackPrefab, owner, false);
        _attackObject.SetActive(false);

        _attackObject.transform.localPosition = Vector3.zero;
        _attackObject.transform.localRotation = Quaternion.identity;
        _attackObject.transform.localScale = Vector3.one;

        MonsterMeleeAtkObj hitbox = _attackObject.GetComponent<MonsterMeleeAtkObj>();
        hitbox.SetSize(new Vector2(_attackWidth, bodyHeight / ownerScaleY));

        hitbox.Init(CreateDamageInfo(atk));
        return true;
    }

    public override void UseSkill(Transform target, Vector3 attackPosition) 
    {
        if (IsAttacking || !isActiveAndEnabled || _ownerMonster == null || !_ownerMonster.CanAttack || !IsCooldownComplete)
            return;

        if (target == null || _ownerBody == null || _attackObject == null)
            return;

        if (_ownerNetwork == null || !_ownerNetwork.IsServer || !_ownerNetwork.IsSpawned)
            return;

        float direction = target.position.x >= _ownerBody.position.x ? 1f : -1f;

        float halfWidth = _attackWidth * Mathf.Abs(_attackObject.transform.lossyScale.x) * 0.5f;

        _attackObject.transform.position = attackPosition + Vector3.right * direction * halfWidth;

        StartCoroutine(AttackRoutine(direction));
    }

    private IEnumerator AttackRoutine(float direction)
    {
        double startTime = _ownerNetwork.NetworkManager.ServerTime.Time;
        IsAttacking = true;
        _attackObject.SetActive(true);

        _attackId = _ownerNetwork.BroadcastMeleeAttack(SkillId, _attackObject.transform,
            _attackObject.GetComponent<BoxCollider2D>().size, startTime, _attackDuration);

        Vector2 velocity = _ownerBody.linearVelocity;
        velocity.x = 0f;
        _ownerBody.linearVelocity = velocity;

        if (_ownerMonster.CanMove && CanDashOnAttack)
            _ownerBody.AddForce(Vector2.right * direction * _dashPower, ForceMode2D.Impulse);

        double endTime = startTime + _attackDuration;
        while (_ownerNetwork != null && _ownerNetwork.IsSpawned
            && _ownerNetwork.NetworkManager.ServerTime.Time < endTime)
            yield return null;

        FinishAttack();
    }

    private void FinishAttack()
    {
        if (_ownerNetwork != null)
            _ownerNetwork.BroadcastMeleeAttackFinished(_attackId);

        _attackId = 0;

        if (_attackObject != null)
            _attackObject.SetActive(false);

        if (_ownerBody != null)
        {
            Vector2 velocity = _ownerBody.linearVelocity;
            velocity.x = 0f;
            _ownerBody.linearVelocity = velocity;
        }

        IsAttacking = false;
        StartCooldown();
    }

    private void OnDisable()
    {
        if (!IsAttacking)
            return;

        StopAllCoroutines();
        FinishAttack();
    }
}
