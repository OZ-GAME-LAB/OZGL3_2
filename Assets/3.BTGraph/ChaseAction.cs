using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "ChaseAction", story: "ChaseAction", category: "Action", id: "f31482b54804bf42fee8ea447d7b2994")]
public partial class ChaseAction : Action
{
    [SerializeReference]
    public BlackboardVariable<GameObject> Agent;

    private Monster _monster;

    protected override Status OnStart()
    {
        if (Agent == null || Agent.Value == null || !Agent.Value.TryGetComponent<Monster>(out _monster))
            return Status.Failure;

        if (_monster.VisibleTarget == null || !_monster.VisibleTarget.isActiveAndEnabled)
            return Status.Failure;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (_monster == null)
            return Status.Failure;

        if (_monster.VisibleTarget == null || !_monster.VisibleTarget.isActiveAndEnabled)
            return Status.Failure;

        if (_monster.IsTargetInAttackRange())
            return Status.Success;

        _monster.MonsterChaisingTarget();
        return Status.Running;
    }

    protected override void OnEnd()
    {
        if (_monster != null)
            _monster.MonsterStopMove();
    }
}

