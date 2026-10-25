using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "HasVisibleTarget", story: "[Agent] has visible target", category: "Conditions", id: "cba71d7790d7fd421513af998f74992b")]
public partial class HasVisibleTargetCondition : Condition
{
    [SerializeReference]
    public BlackboardVariable<GameObject> Agent;

    public override bool IsTrue()
    {
        if (Agent == null || Agent.Value == null)
            return false;

        if (!Agent.Value.TryGetComponent<Monster>(out var monster))
            return false;

        return monster.VisibleTarget != null && monster.VisibleTarget.isActiveAndEnabled;
    }

    public override void OnStart()
    {
    }

    public override void OnEnd()
    {
    }
}
