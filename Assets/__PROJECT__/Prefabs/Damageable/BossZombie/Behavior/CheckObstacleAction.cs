using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

/// <summary>
/// 보스와 현재 공격 대상 사이를 주기적으로 검사해 앞을 가로막는 장애물을 공격 대상으로 전환한다.
/// </summary>
[Serializable, GeneratePropertyBag]
[NodeDescription(name: "CheckObstacle", story: "Periodically check if there is a [AttackTarget] in front of [Self]", category: "Action", id: "c7f361a447de67ad2b6cadb938c1739c")]
public partial class CheckObstacleAction : Action
{
    private const int DefaultObstacleLayerMask = 0;
    private const float MinimumCheckInterval = 0.01f;
    private const float MinimumRayDistance = 0.001f;

    [SerializeReference] public BlackboardVariable<GameObject> AttackTarget;
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeField, Min(MinimumCheckInterval)] private float checkInterval = 1.0f;

    private float elapsedTime;
    private int obstacleLayerMask;

    // 장애물 감시 주기와 레이어 마스크를 초기화한다
    protected override Status OnStart()
    {
        checkInterval = Mathf.Max(MinimumCheckInterval, checkInterval);
        elapsedTime = checkInterval;
        obstacleLayerMask = GetObstacleLayerMask();

        return Status.Running;
    }

    // 일정 주기마다 현재 공격 대상 앞을 가로막는 장애물을 검사한다
    protected override Status OnUpdate()
    {
        if (Self == null || Self.Value == null || AttackTarget == null || AttackTarget.Value == null)
        {
            return Status.Running;
        }

        elapsedTime += Time.deltaTime;
        if (elapsedTime < checkInterval)
        {
            return Status.Running;
        }

        elapsedTime = 0.0f;
        UpdateAttackTargetIfObstacleBlocks();

        return Status.Running;
    }

    // 노드 종료 시 누적 시간을 초기화한다
    protected override void OnEnd()
    {
        elapsedTime = 0.0f;
    }

    // 현재 공격 대상 방향으로 레이를 쏴서 가로막는 장애물이 있으면 공격 대상으로 교체한다
    private void UpdateAttackTargetIfObstacleBlocks()
    {
        Vector3 selfPosition = Self.Value.transform.position;
        Vector3 targetPosition = AttackTarget.Value.transform.position;
        float raycastHeight = targetPosition.y - selfPosition.y;
        Vector3 rayOrigin = selfPosition + Vector3.up * raycastHeight;
        Vector3 rayDirection = targetPosition - rayOrigin;
        float rayDistance = rayDirection.magnitude;

        if (rayDistance <= MinimumRayDistance)
        {
            return;
        }

        rayDirection /= rayDistance;

        if (!Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, rayDistance, obstacleLayerMask, QueryTriggerInteraction.Collide))
        {
            return;
        }

        if (hit.collider == null || hit.collider.gameObject == AttackTarget.Value)
        {
            return;
        }

        Obstacle blockingObstacle = hit.collider.GetComponentInParent<Obstacle>();
        if (blockingObstacle == null || !blockingObstacle.IsAlive)
        {
            return;
        }

        AttackTarget.Value = blockingObstacle.gameObject;
    }

    // 장애물 레이어만 포함하는 레이캐스트 마스크를 계산한다
    private static int GetObstacleLayerMask()
    {
        int mask = DefaultObstacleLayerMask;

        AddLayerToMask("Obstacle", ref mask);

        return mask;
    }

    // 레이어 이름이 유효하면 마스크에 추가한다
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

