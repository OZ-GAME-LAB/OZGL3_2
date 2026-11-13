using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "BossFlowerSummonAction", story: "[Agent] summons a flower", category: "Action", id: "c1739a6d023e40b5a8c9d7e8f104b263")]
public partial class BossFlowerSummonAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    private BossFlowerSummonSkill _skill;

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
            if (skill is BossFlowerSummonSkill flowerSkill)
            {
                _skill = flowerSkill;
                break;
            }
        }

        if (_skill == null || !_skill.isActiveAndEnabled || !_skill.IsCooldownComplete)
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
