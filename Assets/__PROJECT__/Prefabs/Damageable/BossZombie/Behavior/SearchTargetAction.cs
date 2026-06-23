using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

/// <summary>
/// 레이캐스트 스피어로 씬에서 가장 가까운 Obstacle 레이어를 찾고 Target으로 설정
/// </summary>
[Serializable, GeneratePropertyBag]
[NodeDescription(name: "SearchTarget", story: "[Self] Search [Target]", category: "Action", id: "f9c138d2ff522ac33fce6b3d862deef4")]
public partial class SearchTargetAction : Action
{
    private const int DefaultTargetLayerMask = 0;

    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeField] private float searchRadius = 1000f;
    [SerializeField] private int maxTargetCount = 64;

    private Collider[] targets;
    private int targetLayerMask;

    protected override Status OnStart()
    {
        targets = new Collider[Mathf.Max(1, maxTargetCount)];
        targetLayerMask = GetTargetLayerMask();

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Self == null || Self.Value == null || Target == null)
        {
            return Status.Failure;
        }

        int targetCount = Physics.OverlapSphereNonAlloc(
            Self.Value.transform.position,
            searchRadius,
            targets,
            targetLayerMask,
            QueryTriggerInteraction.Collide
        );

        float closestSqrDistance = float.MaxValue;
        GameObject closestTarget = null;
        Vector3 selfPosition = Self.Value.transform.position;

        for (int i = 0; i < targetCount; i++)
        {
            Collider targetCollider = targets[i];
            if (targetCollider == null)
            {
                continue;
            }

            GameObject candidate = targetCollider.attachedRigidbody
                ? targetCollider.attachedRigidbody.gameObject
                : targetCollider.gameObject;

            float sqrDistance = (candidate.transform.position - selfPosition).sqrMagnitude;
            if (sqrDistance >= closestSqrDistance)
            {
                continue;
            }

            closestSqrDistance = sqrDistance;
            closestTarget = candidate;
        }

        Target.Value = closestTarget;
        Array.Clear(targets, 0, targetCount);

        return closestTarget ? Status.Success : Status.Failure;
    }

    private static int GetTargetLayerMask()
    {
        int mask = DefaultTargetLayerMask;

        AddLayerToMask("Obstacle", ref mask);

        return mask;
    }

    private static void AddLayerToMask(string layerName, ref int mask)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            return;
        }

        mask |= 1 << layer;
    }
}

