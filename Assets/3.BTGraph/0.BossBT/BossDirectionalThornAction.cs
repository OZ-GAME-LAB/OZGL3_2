using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "BossDirectionalThornAction", story: "[Agent] uses directional thorns", category: "Action", id: "5b5dc9244f259e5c4835fc62b39d7668")]
public partial class BossDirectionalThornAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;

    private BossDirectionalThornSkill _skill;

    protected override Status OnStart()
    {
        _skill = null;

        if (Agent == null || Agent.Value == null)
            return Status.Failure;

        if (!Agent.Value.TryGetComponent(out Boss boss))
            return Status.Failure;

        if (!Agent.Value.TryGetComponent(out BossNetwork bossNetwork))
            return Status.Failure;

        if (!bossNetwork.IsServer || !bossNetwork.IsSpawned || !bossNetwork.IsReady)
            return Status.Failure;

        foreach (MonsterSkillBase skill in boss.Skills)
        {
            if (skill is BossDirectionalThornSkill directionalSkill)
            {
                _skill = directionalSkill;
                break;
            }
        }

        if (_skill == null)
            return Status.Failure;

        if (_skill.IsAttacking || !_skill.IsCooldownComplete)
            return Status.Failure;

        _skill.UseSkill(null, Vector3.zero);

        if (!_skill.IsAttacking)
            return Status.Failure;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (_skill == null || !_skill.isActiveAndEnabled)
            return Status.Failure;

        if (_skill.IsAttacking)
            return Status.Running;

        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}

