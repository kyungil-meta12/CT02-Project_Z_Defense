using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
/// <summary>
/// 무한대기
/// </summary>
[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Stay", story: "Stay", category: "Action", id: "409059a504b01aabd47a5ab4567342c6")]
public partial class StayAction : Action
{
    protected override Status OnUpdate()
    {
        return Status.Running;
    }
}

