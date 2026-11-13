using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "BossPoisonSprayAction", story: "[Agent] sprays poison", category: "Action", id: "0e88a441f17148fe91f8b8cfd96d23a1")]
public partial class BossPoisonSprayAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    private BossPoisonSpraySkill _skill;

    protected override Status OnStart()
    {
        _skill = null;
        if (Agent == null || Agent.Value == null
            || !Agent.Value.TryGetComponent(out Boss boss)
            || !Agent.Value.TryGetComponent(out BossNetwork network)
            || !network.IsServer || !network.IsSpawned || !network.IsReady)
            return Status.Failure;

        foreach (MonsterSkillBase skill in boss.Skills)
        {
            if (skill is BossPoisonSpraySkill poisonSkill)
            {
                _skill = poisonSkill;
                break;
            }
        }

        if (_skill == null || !_skill.isActiveAndEnabled || _skill.IsAttacking || !_skill.IsCooldownComplete)
            return Status.Failure;

        _skill.UseSkill(null, Vector3.zero);
        return _skill.IsAttacking ? Status.Running : Status.Failure;
    }

    protected override Status OnUpdate()
    {
        if (_skill == null || !_skill.isActiveAndEnabled)
            return Status.Failure;
        return _skill.IsAttacking ? Status.Running : Status.Success;
    }
}
