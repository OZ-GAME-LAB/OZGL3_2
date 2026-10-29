using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "MonsterAttackAction", story: "[Agent] attacks", category: "Action", id: "d6e0f1706378ab9e03d83480895ec1cb")]
public partial class MonsterAttackAction : Action
{
    [SerializeReference]
    public BlackboardVariable<GameObject> Agent;

    private MonsterSkillBase _skill;

    protected override Status OnStart()
    {
        _skill = null;
        if (Agent == null || Agent.Value == null)
            return Status.Failure;

        if (!Agent.Value.TryGetComponent<Monster>(out var monster))
            return Status.Failure;

        if (monster.VisibleTarget == null || !monster.VisibleTarget.isActiveAndEnabled)
            return Status.Failure;

        if (!monster.IsTargetInAttackRange())
            return Status.Failure;

        if (monster.Skills.Count == 0)
            return Status.Failure;

        _skill = monster.Skills[0];

        if (_skill == null || _skill.IsAttacking)
            return Status.Failure;

        Transform target = monster.VisibleTarget.transform;
        Transform attackPoint = monster.GetAttackPoint(target);

        if (attackPoint == null)
            return Status.Failure;

        _skill.UseSkill(target, attackPoint.position);

        return _skill.IsAttacking ? Status.Running : Status.Failure;
    }

    protected override Status OnUpdate()
    {
        if (_skill == null)
            return Status.Failure;

        return _skill.IsAttacking ? Status.Running : Status.Success;
    }
}

