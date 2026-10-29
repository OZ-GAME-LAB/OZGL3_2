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


    public override void InitSkill(Transform owner, float bodyHeight, float atk, GameObject attackPrefab) 
    {
        if (owner == null || attackPrefab == null || bodyHeight <= 0f)
        {
            Debug.LogError("몬스터 본체, 공격 프리팹, 몸 높이를 확인하세요.", this);
            return;
        }

        float ownerScaleY = Mathf.Abs(owner.lossyScale.y);

        if (ownerScaleY <= 0f)
            return;

        if (!owner.TryGetComponent(out _ownerBody))
        {
            Debug.LogError("몬스터 본체에 Rigidbody2D가 필요합니다.", this);
            return;
        }

        if (!attackPrefab.TryGetComponent<BoxCollider2D>(out _) || !attackPrefab.TryGetComponent<MonsterMeleeAtkObj>(out _))
        {
            Debug.LogError("공격 프리팹 루트에 BoxCollider2D와 MonsterMeleeAtkObj가 필요합니다.", this);
            return;
        }

        _attackObject = Instantiate(attackPrefab, owner, false);
        _attackObject.SetActive(false);

        _attackObject.transform.localPosition = Vector3.zero;
        _attackObject.transform.localRotation = Quaternion.identity;
        _attackObject.transform.localScale = Vector3.one;

        BoxCollider2D attackCollider = _attackObject.GetComponent<BoxCollider2D>();

        attackCollider.isTrigger = true;
        attackCollider.offset = Vector2.zero;
        attackCollider.size = new Vector2(_attackWidth, bodyHeight / ownerScaleY);

        MonsterMeleeAtkObj hitbox = _attackObject.GetComponent<MonsterMeleeAtkObj>();

        SpriteRenderer visual = _attackObject.GetComponentInChildren<SpriteRenderer>(true);

        if (visual != null && visual.sprite != null)
        {
            Vector2 spriteSize = visual.sprite.bounds.size;
            Vector2 hitboxSize = attackCollider.size;

            if (spriteSize.x > 0f && spriteSize.y > 0f)
            {
                visual.transform.localScale = new Vector3(
                    hitboxSize.x / spriteSize.x,
                    hitboxSize.y / spriteSize.y,
                    1f);

                Vector3 spriteCenter = visual.sprite.bounds.center;

                visual.transform.localPosition = (Vector3)attackCollider.offset - Vector3.Scale(spriteCenter, visual.transform.localScale);
            }
        }

        hitbox.Init(atk);
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
