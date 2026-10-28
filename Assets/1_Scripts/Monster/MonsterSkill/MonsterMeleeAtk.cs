using UnityEngine;

public class MonsterMeleeAtk : MonsterSkillBase
{
    private void OnEnable()
    {
        // 스킬 오브젝트 몬스터 아래에 생성 및 비활성화
    }

    // UseSkill() override 부분
    // 타겟 방향으로 오브젝트 생성
    public override void UseSkill(float atk) 
    {
        Debug.Log("몬스터 공격 구현");
    }
}
