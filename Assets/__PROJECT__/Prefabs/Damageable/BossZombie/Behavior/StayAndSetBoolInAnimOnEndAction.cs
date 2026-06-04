using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
/// <summary>
/// 무한 대기 중 해당 노드가 속한 브랜치가 종료될시 애니메이션 파라미터 Bool값 변경
/// </summary>
[Serializable, GeneratePropertyBag]
[NodeDescription(name: "StayAndSetBoolInAnimOnEnd", story: "Stay and set [stat] in [Animator] to [bool] on end", category: "Action", id: "b4b300e88e96130f2b7404f2635e1219")]
public partial class StayAndSetBoolInAnimOnEndAction : Action
{
    [SerializeReference] public BlackboardVariable<string> Stat;
    [SerializeReference] public BlackboardVariable<Animator> Animator;
    [SerializeReference] public BlackboardVariable<bool> Bool;

    protected override Status OnUpdate()
    {
        return Status.Running;
    }

    protected override void OnEnd()
    {
        if (Animator == null || Animator.Value == null ||
            Stat == null || string.IsNullOrEmpty(Stat.Value) ||
            Bool == null) return;
        
        Animator.Value.SetBool(Stat.Value, Bool.Value);
    }
}

