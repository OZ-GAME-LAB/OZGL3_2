using System.Collections.Generic;
using UnityEngine;

[UnityEngine.Scripting.Preserve]
public class MonsterMeleeAtkObj : MonoBehaviour
{
    private RuneDamageInfo _damageInfo;
    private MonsterRuneCombatActor _attacker;
    private readonly HashSet<PlayerRuneCombatActor> _hitPlayers = new();

    public void Init(RuneDamageInfo damageInfo)
    {
        _damageInfo = damageInfo;
        _attacker = GetComponentInParent<MonsterRuneCombatActor>();
        if (_attacker == null)
            Debug.LogError("몬스터 본체에 MonsterRuneCombatActor가 필요합니다.", this);
    }

    public void SetSize(Vector2 size)
    {
        BoxCollider2D attackCollider = GetComponent<BoxCollider2D>();
        attackCollider.isTrigger = true;
        attackCollider.offset = Vector2.zero;
        attackCollider.size = size;

        SpriteRenderer visual = GetComponentInChildren<SpriteRenderer>(true);
        if (visual == null || visual.sprite == null)
            return;

        Vector2 spriteSize = visual.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            return;

        visual.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
        visual.transform.localPosition = (Vector3)attackCollider.offset
            - Vector3.Scale(visual.sprite.bounds.center, visual.transform.localScale);
    }

    private void OnEnable()
    {
        _hitPlayers.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_attacker == null || !_attacker.IsServer || !_attacker.IsAlive)
            return;

        if (float.IsNaN(_damageInfo.RawDamage) || float.IsInfinity(_damageInfo.RawDamage)
            || _damageInfo.RawDamage <= 0f)
            return;

        PlayerRuneCombatActor target = other.GetComponentInParent<PlayerRuneCombatActor>();
        if (target == null || !_attacker.CanHit(target) || !_hitPlayers.Add(target))
            return;

        RuneDamageInfo damageInfo = _damageInfo;
        damageInfo.HitDirection = ((Vector2)(other.bounds.center - transform.position)).normalized;
        target.ReceiveDirectDamageOnServer(damageInfo);
    }
}
