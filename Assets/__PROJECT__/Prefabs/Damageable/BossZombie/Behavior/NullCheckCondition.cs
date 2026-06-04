using System;
using Unity.Behavior;
using UnityEngine;
/// <summary>
/// Target이 null인지 지속적으로 체크를 해서 Bool값과 일치할때 true를 반환
/// </summary>
[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "NullCheck", story: "Check null [Target] [bool]", category: "Conditions", id: "f1dcde5199713b4c1ada25d11b4074dd")]
public partial class NullCheckCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<bool> Bool;

    public override bool IsTrue()
    {
        bool isNull = Target == null || Target.Value == null;
        return isNull == Bool.Value;
    }
}
