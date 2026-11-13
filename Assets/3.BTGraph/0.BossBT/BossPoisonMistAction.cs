using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "BossPoisonMistAction", story: "[Agent] releases poison mist", category: "Action", id: "85f0327cb9d14abfb117a58e90cd2643")]
public partial class BossPoisonMistAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    private BossPoisonMistSkill _skill;

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
            if (skill is BossPoisonMistSkill mistSkill)
            {
                _skill = mistSkill;
                break;
            }
        }

        if (_skill == null || !_skill.isActiveAndEnabled)
            return Status.Failure;

        if (!_skill.HasActivated)
            _skill.UseSkill(null, Vector3.zero);

        if (_skill.IsAttacking)
            return Status.Running;

        return _skill.HasActivated ? Status.Success : Status.Failure;
    }

    protected override Status OnUpdate()
    {
        if (_skill == null || !_skill.isActiveAndEnabled)
            return Status.Failure;

        return _skill.IsAttacking ? Status.Running : Status.Success;
    }
}
