using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Patrol", story: "Monster Patrol", category: "Action", id: "447860476efa69e89d764527a568c7b4")]
public partial class PatrolAction : Action
{
    [SerializeReference]
    public BlackboardVariable<GameObject> Agent;

    private Monster _monster;
    private Vector3 _targetPosition;

    protected override Status OnStart()
    {
        if (Agent.Value == null || !Agent.Value.TryGetComponent<Monster>(out _monster))
            return Status.Failure;

        _targetPosition = _monster.MonsterPatricPositionSelect();

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (_monster == null)
            return Status.Failure;

        if (_monster.MonsterPatricMoveAndCheck(_targetPosition))
        {
            _targetPosition = _monster.MonsterPatricPositionSelect();
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
    }
}

