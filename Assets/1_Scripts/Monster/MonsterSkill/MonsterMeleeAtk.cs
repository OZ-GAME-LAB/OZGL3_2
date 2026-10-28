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
    public bool IsAttacking { get; private set; }

    private GameObject _attackObject;
    private Rigidbody2D _ownerBody;


    public override void InitSkill(Transform owner, float bodyHeight) 
    {
        if (owner == null || bodyHeight <= 0f)
        {
            Debug.LogError("몬스터 본체와 유효한 몸 높이가 필요합니다.", this);
            return;
        }

        float ownerScaleY = Mathf.Abs(owner.lossyScale.y);

        if (ownerScaleY <= 0f)
        {
            Debug.LogError("몬스터 본체의 Y 스케일이 0입니다.", this);
            return;
        }

        _ownerBody = owner.GetComponent<Rigidbody2D>();

        _attackObject = new GameObject("MeleeAttackHitbox");
        _attackObject.SetActive(false);
        _attackObject.transform.SetParent(owner, false);

        BoxCollider2D attackCollider = _attackObject.AddComponent<BoxCollider2D>();

        attackCollider.isTrigger = true;
        attackCollider.size = new Vector2(_attackWidth, bodyHeight / ownerScaleY); ;
    }

    public override void UseSkill(Transform target, Vector3 attackPosition) 
    {
        if (IsAttacking || !isActiveAndEnabled)
            return;

        if (target == null || _ownerBody == null || _attackObject == null)
            return;

        var networkManager = Unity.Netcode.NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsServer)
            return;

        float direction = target.position.x >= _ownerBody.position.x ? 1f : -1f;

        float halfWidth = _attackWidth * Mathf.Abs(_attackObject.transform.lossyScale.x) * 0.5f;

        _attackObject.transform.position = attackPosition + Vector3.right * direction * halfWidth;

        StartCoroutine(AttackRoutine(direction));
    }

    private IEnumerator AttackRoutine(float direction)
    {
        IsAttacking = true;
        _attackObject.SetActive(true);

        Vector2 velocity = _ownerBody.linearVelocity;
        velocity.x = 0f;
        _ownerBody.linearVelocity = velocity;

        _ownerBody.AddForce(Vector2.right * direction * _dashPower, ForceMode2D.Impulse);

        yield return new WaitForSeconds(_attackDuration);

        FinishAttack();
    }

    private void FinishAttack()
    {
        if (_attackObject != null)
            _attackObject.SetActive(false);

        if (_ownerBody != null)
        {
            Vector2 velocity = _ownerBody.linearVelocity;
            velocity.x = 0f;
            _ownerBody.linearVelocity = velocity;
        }

        IsAttacking = false;
    }

    private void OnDisable()
    {
        if (!IsAttacking)
            return;

        StopAllCoroutines();
        FinishAttack();
    }
}
