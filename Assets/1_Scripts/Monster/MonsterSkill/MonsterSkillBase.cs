using UnityEngine;

public class MonsterSkillBase : MonoBehaviour
{
    public bool IsAttacking { get; protected set; }
    public string SkillId { get; private set; }
    public virtual bool IsDeathSkill => false;
    public virtual bool IsDashing => false;
    public virtual bool RequiresAttackPrefab => true;
    public virtual bool StopAtAttackRange => false;

    private float _cooldownSeconds;
    private float _nextUseTime;
    private DamageType _damageType;
    private bool _applyPoison;
    private bool _applyBossPoison;

    // TO USE : 스킬 생성 시 데이터의 피해 속성과 일반 독·보스 독 부여 여부를 설정할 때 사용.
    public bool ConfigureDamage(MonsterSkillData data)
    {
        if (data == null || !data.TryGetDamageType(out _damageType))
        {
            return FailInitialization("스킬의 피해 속성이 올바르지 않습니다.");
        }

        _applyPoison = data.isPoison;
        _applyBossPoison = data.isBossPoison;
        return true;
    }

    // TO USE : 몬스터·보스 스킬에서 히트박스나 투사체에 전달할 피해 정보를 만들 때 사용.
    // rawDamage에는 저항력 적용 전 타격 피해를 전달. 반환값을 대상 Actor의 ReceiveDirectDamageOnServer에 전달.
    protected RuneDamageInfo CreateDamageInfo(float rawDamage)
    {
        return new RuneDamageInfo
        {
            Attacker = GetComponent<RuneCombatActor>(),
            RawDamage = rawDamage,
            Element = _damageType,
            ApplyPoison = _applyPoison,
            ApplyBossPoison = _applyBossPoison
        };
    }

    public virtual bool InitSkill(Transform owner, float bodyHeight, float atk, GameObject attackPrefab)
    {
        return FailInitialization("InitSkill이 구현되지 않았습니다.");
    }

    protected bool FailInitialization(string reason)
    {
        Debug.LogError($"[스킬 초기화 실패] ID: {SkillId}, 클래스: {GetType().Name}, 원인: {reason}", this);
        return false;
    }

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

    public void SetSkillId(string skillId)
    {
        SkillId = skillId;
    }
}
