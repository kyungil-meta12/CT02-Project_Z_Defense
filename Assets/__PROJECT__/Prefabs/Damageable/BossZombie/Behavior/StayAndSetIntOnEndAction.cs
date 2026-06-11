using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
/// <summary>
/// 무한 대기 중 해당 노드가 속한 브랜치가 종료될시 부활 포인트로 이동
/// </summary>
[Serializable, GeneratePropertyBag]
[NodeDescription(name: "StayAndSetIntOnEnd", story: "Stay and set [Int1] to [Int2]on end", category: "Action", id: "769081fb99c3332be0bf9b3b25c8f087")]
public partial class StayAndSetIntOnEndAction : Action
{
    [SerializeReference] public BlackboardVariable<int> Int1;
    [SerializeReference] public BlackboardVariable<int> Int2;
    
    protected override Status OnUpdate()
    {
        return Status.Running;
    }

    protected override void OnEnd()
    {
        if (Int1 == null || Int2 == null) return;
        Int1.Value = Int2.Value;
    }
}

