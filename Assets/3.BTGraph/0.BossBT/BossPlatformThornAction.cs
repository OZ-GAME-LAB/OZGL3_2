using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "BossPlatformThornAction", story: "[Agent] uses platform thorns", category: "Action", id: "ce7911b1cb4b7491631ceffe9e7c87ba")]
public partial class BossPlatformThornAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    private BossPlatformThornSkill _skill;


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
            if (skill is BossPlatformThornSkill platformSkill)
            {
                _skill = platformSkill;
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

