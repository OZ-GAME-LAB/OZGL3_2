using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "CanAttack", story: "[Agent] can attack", category: "Conditions", id: "0efd807182832dd25963465360bf5c3c")]
public partial class CanAttackCondition : Condition
{

    [SerializeReference]
    public BlackboardVariable<GameObject> Agent;

    public override bool IsTrue()
    {
        if (Agent == null || Agent.Value == null)
            return false;

        if (!Agent.Value.TryGetComponent<Monster>(out var monster))
            return false;

        if (monster.Skills.Count == 0)
            return false;

        MonsterSkillBase skill = monster.Skills[0];

        if (skill == null || skill.IsAttacking)
            return false;

        return skill.IsCooldownComplete && monster.IsTargetInAttackRange();
    }
}
