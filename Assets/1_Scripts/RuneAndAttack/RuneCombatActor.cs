using Unity.Netcode;

public struct RuneDamageInfo
{
    public RuneCombatActor Attacker;

    // 저항력을 적용하기 전 피해량
    public float RawDamage;

    public RuneAttackElement Element;
}

public struct RuneDamageResult
{
    // 무적·이미 사망 등의 이유로 적중이 거절되었는지 구분
    public bool Accepted;

    // 저항력과 남은 체력을 반영해 실제 감소한 체력량
    public float ActualDamage;
}

public abstract class RuneCombatActor : NetworkBehaviour
{
    public abstract bool IsAlive { get; }
    public abstract bool CanAttack { get; }
    public abstract float AttackPower { get; }
    public abstract float MaxHp { get; }

    // 일반 독에만 사용. 20이면 20%.
    public virtual float PoisonResistance => 0f;

    // 기본적으로 출혈 불가. 몬스터 구현체에서 설정.
    public virtual bool CanReceiveBleed => false;

    // 같은 팀, 무적 대상 등 공격 대상 규칙을 검사
    public abstract bool CanHit(RuneCombatActor target);

    // 서버에서만 호출.
    // 저항력 계산, 체력 감소, 사망 처리는 구현체에서 담당.
    public abstract RuneDamageResult ReceiveDirectDamageOnServer(RuneDamageInfo damageInfo);


    // 저항력 계산을 마친 상태이상 피해.
    // 직접 공격 효과, 흡혈, 피격 경직을 재발동하지 않음.
    public abstract RuneDamageResult ReceiveStatusDamageOnServer(float finalDamage);

    // 서버에서만 호출.
    // 최대 체력을 초과하지 않도록 구현체에서 처리.
    public abstract void HealOnServer(float amount);

    // 서버에서만 호출.
    // 독 갱신·공유 출혈 중첩 처리는 구현체에서 담당.
    public abstract void ApplyStatusOnServer(string statusProfileId, RuneCombatActor attacker);
}
