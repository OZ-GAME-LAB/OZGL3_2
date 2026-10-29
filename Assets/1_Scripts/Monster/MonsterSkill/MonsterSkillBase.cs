using UnityEngine;

public class MonsterSkillBase : MonoBehaviour
{
    public bool IsAttacking { get; protected set; }

    private float _cooldownSeconds;
    private float _nextUseTime;

    public virtual void InitSkill(Transform owner, float bodyHeight, float atk, GameObject attackPrefab) { }
    public virtual void UseSkill(Transform target, Vector3 attackPosition) { }

    public bool IsCooldownComplete => Time.time >= _nextUseTime;

    public void InitCooldown(float seconds)
    {
        _cooldownSeconds = seconds;
        _nextUseTime = 0f;
    }

    protected void StartCooldown()
    {
        _nextUseTime = Time.time + _cooldownSeconds;
    }
}
