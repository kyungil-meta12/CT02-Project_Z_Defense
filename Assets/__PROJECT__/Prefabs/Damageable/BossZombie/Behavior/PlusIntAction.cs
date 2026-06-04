using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
/// <summary>
/// 정수값 덧셈
/// </summary>
[Serializable, GeneratePropertyBag]
[NodeDescription(name: "PlusInt", story: "[Int1] = [Int2] + [Int3]", category: "Action", id: "9246bdb7d2da3de19ec0d31debad1834")]
public partial class PlusIntAction : Action
{
    [SerializeReference] public BlackboardVariable<int> Int1;
    [SerializeReference] public BlackboardVariable<int> Int2;
    [SerializeReference] public BlackboardVariable<int> Int3;

    protected override Status OnStart()
    {
        if(Int1 == null || Int2 == null || Int3 == null) return Status.Failure;
        Int1.Value = Int2.Value + Int3.Value;
        return Status.Success;
    }
}

