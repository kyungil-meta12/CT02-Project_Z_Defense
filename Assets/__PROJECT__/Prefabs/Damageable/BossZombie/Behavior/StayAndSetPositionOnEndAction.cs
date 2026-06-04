using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
/// <summary>
/// 무한 대기 중 해당 노드가 속한 브랜치가 종료될시 부활 포인트로 이동
/// </summary>
[Serializable, GeneratePropertyBag]
[NodeDescription(name: "StayAndSetPositionOnEnd", story: "Stay and set [Self] transform to [SpawnPosition] on end", category: "Action", id: "769081fb99c3332be0bf9b3b25c8f087")]
public partial class StayAndSetPositionOnEndAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> SpawnPosition;
    
    protected override Status OnUpdate()
    {
        return Status.Running;
    }

    protected override void OnEnd()
    {
        if (Self == null || Self.Value == null || SpawnPosition == null || SpawnPosition.Value == null) return;
        Self.Value.transform.position = SpawnPosition.Value.transform.position;
    }
}

