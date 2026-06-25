using UnityEngine;
using ProjectZDefense.StatusEffects;

/// <summary>
/// Electro 투사체가 직접 적중한 대상 주변으로 체인 라이트닝 데미지를 전파한다.
/// </summary>
public static class ElectroChainLightningUtility
{
    private const int CHAIN_BUFFER_SIZE = 32;

    private static readonly Collider[] ChainHitBuffer = new Collider[CHAIN_BUFFER_SIZE];
    private static readonly IDamageable[] ChainedTargets = new IDamageable[CHAIN_BUFFER_SIZE];

    // 직접 피격 대상에서 시작해 주변 유효 대상에게 체인 데미지를 순차 적용한다
    public static void ApplyChain(ElectroStatusPayload payload, IDamageable primaryTarget, Collider primaryCollider, Vector3 primaryPosition, float sourceDamage)
    {
        if (!CanApplyChain(payload, primaryTarget, sourceDamage))
        {
            return;
        }

        int chainedTargetCount = 0;
        RegisterChainedTarget(primaryTarget, ref chainedTargetCount);

        Collider currentCollider = primaryCollider;
        Transform currentTransform = ResolveTargetTransform(primaryTarget);
        Vector3 currentPosition = ResolveTargetPosition(primaryTarget, primaryCollider, primaryPosition);
        for (int chainIndex = 1; chainIndex < payload.maxChainTargets && chainedTargetCount < CHAIN_BUFFER_SIZE; chainIndex++)
        {
            IDamageable nextTarget = FindNearestChainTarget(payload, currentPosition, chainedTargetCount, out Collider nextCollider);
            if (nextTarget == null)
            {
                break;
            }

            float chainDamage = sourceDamage * CalculateChainDamageMultiplier(payload, chainIndex);
            Vector3 nextPosition = ResolveTargetPosition(nextTarget, nextCollider, currentPosition);
            Transform nextTransform = ResolveTargetTransform(nextTarget);
            ElectroChainLinkEffectUtility.Play(payload, currentCollider, nextCollider, currentTransform, nextTransform, currentPosition, nextPosition);
            nextTarget.TakeDamage(new DamageInfo(chainDamage, DamagePopupType.Normal, DamagePopupPolicyResolver.ResolveChain()));
            ApplyElectroStatus(payload, nextTarget, chainIndex, sourceDamage);
            RegisterChainedTarget(nextTarget, ref chainedTargetCount);
            currentCollider = nextCollider;
            currentTransform = nextTransform;
            currentPosition = nextPosition;
        }

        ClearChainedTargets(chainedTargetCount);
    }

    // 체인을 적용할 수 있는 최소 조건을 확인한다
    private static bool CanApplyChain(ElectroStatusPayload payload, IDamageable primaryTarget, float sourceDamage)
    {
        return payload.hasElectroStatus &&
               payload.maxChainTargets > 1 &&
               payload.chainRadius > 0.0f &&
               sourceDamage > 0.0f &&
               primaryTarget != null;
    }

    // 지정 위치 주변에서 Shock 스택을 가장 완성하기 쉬운 데미지 대상을 찾는다
    private static IDamageable FindNearestChainTarget(ElectroStatusPayload payload, Vector3 position, int chainedTargetCount, out Collider nearestCollider)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            position,
            payload.chainRadius,
            ChainHitBuffer,
            payload.chainTargetLayerMask,
            QueryTriggerInteraction.Collide);

        IDamageable nearestTarget = null;
        nearestCollider = null;
        int bestShockStackPriority = -1;
        float nearestDistanceSqr = float.MaxValue;
        int maxShockStackCount = ResolveMaxShockStackCount(payload);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = ChainHitBuffer[i];
            if (hitCollider == null)
            {
                continue;
            }

            IDamageable candidate = hitCollider.GetComponentInParent<IDamageable>();
            ElectroStatusRuntime electroStatusRuntime = ResolveElectroStatusRuntime(candidate);
            if (!IsValidChainTarget(candidate, electroStatusRuntime, chainedTargetCount, maxShockStackCount))
            {
                continue;
            }

            Vector3 candidatePosition = ResolveTargetPosition(candidate, hitCollider, hitCollider.bounds.center);
            float distanceSqr = (candidatePosition - position).sqrMagnitude;
            int shockStackPriority = ResolveShockStackPriority(electroStatusRuntime, maxShockStackCount);
            if (!IsBetterChainTarget(shockStackPriority, distanceSqr, bestShockStackPriority, nearestDistanceSqr))
            {
                continue;
            }

            bestShockStackPriority = shockStackPriority;
            nearestDistanceSqr = distanceSqr;
            nearestTarget = candidate;
            nearestCollider = hitCollider;
        }

        ClearHitBuffer(hitCount);
        return nearestTarget;
    }

    // 체인 대상이 생존 중이며 전이 또는 스택 갱신이 가능한 대상인지 확인한다
    private static bool IsValidChainTarget(IDamageable candidate, ElectroStatusRuntime electroStatusRuntime, int chainedTargetCount, int maxShockStackCount)
    {
        if (candidate == null || !candidate.IsAlive || ContainsChainedTarget(candidate, chainedTargetCount))
        {
            return false;
        }

        if (electroStatusRuntime == null)
        {
            return true;
        }

        return !electroStatusRuntime.IsOverloadStunActive;
    }

    // Shock 스택 우선순위와 거리 우선순위를 비교한다
    private static bool IsBetterChainTarget(int shockStackPriority, float distanceSqr, int bestShockStackPriority, float bestDistanceSqr)
    {
        if (shockStackPriority > bestShockStackPriority)
        {
            return true;
        }

        return shockStackPriority == bestShockStackPriority && distanceSqr < bestDistanceSqr;
    }

    // 현재 payload 기준 최대 Shock 스택 수를 반환한다
    private static int ResolveMaxShockStackCount(ElectroStatusPayload payload)
    {
        return Mathf.Min(3, Mathf.Max(1, payload.maxShockStackCount));
    }

    // 대상의 현재 Electro Shock 스택 수를 반환한다
    private static int ResolveShockStackCount(ElectroStatusRuntime electroStatusRuntime)
    {
        return electroStatusRuntime == null ? 0 : electroStatusRuntime.ShockStackCount;
    }

    // 체인 후보 선택에 사용할 Shock 스택 우선순위를 계산한다
    private static int ResolveShockStackPriority(ElectroStatusRuntime electroStatusRuntime, int maxShockStackCount)
    {
        int shockStackCount = ResolveShockStackCount(electroStatusRuntime);
        return shockStackCount >= maxShockStackCount ? -1 : shockStackCount;
    }

    // 데미지 대상에서 Electro 런타임 컴포넌트를 찾는다
    private static ElectroStatusRuntime ResolveElectroStatusRuntime(IDamageable target)
    {
        if (target is Component targetComponent)
        {
            return targetComponent.GetComponentInParent<ElectroStatusRuntime>();
        }

        return null;
    }

    // 대상이 이미 이번 체인에 포함되어 있는지 확인한다
    private static bool ContainsChainedTarget(IDamageable target, int chainedTargetCount)
    {
        for (int i = 0; i < chainedTargetCount; i++)
        {
            if (ChainedTargets[i] == target)
            {
                return true;
            }
        }

        return false;
    }

    // 체인 대상 목록에 새 대상을 등록한다
    private static void RegisterChainedTarget(IDamageable target, ref int chainedTargetCount)
    {
        if (target == null || chainedTargetCount >= CHAIN_BUFFER_SIZE)
        {
            return;
        }

        ChainedTargets[chainedTargetCount] = target;
        chainedTargetCount++;
    }

    // 체인 순번에 따른 데미지 배율을 계산한다
    private static float CalculateChainDamageMultiplier(ElectroStatusPayload payload, int chainIndex)
    {
        float multiplier = 1.0f - Mathf.Clamp01(payload.chainDamageFalloffPerJump) * Mathf.Max(0, chainIndex);
        return Mathf.Max(0.0f, multiplier);
    }

    // 체인 대상에게 Electro 상태 효과를 전달한다
    private static void ApplyElectroStatus(ElectroStatusPayload payload, IDamageable target, int chainIndex, float sourceDamage)
    {
        if (target == null || !target.IsAlive)
        {
            return;
        }

        IElectroStatusEffectReceiver electroReceiver = target as IElectroStatusEffectReceiver;
        if (electroReceiver == null)
        {
            return;
        }

        electroReceiver.ApplyElectroStatus(payload, chainIndex, sourceDamage);
    }

    // 데미지 대상의 위치를 컴포넌트 기준으로 확인하고 실패 시 대체 위치를 반환한다
    private static Vector3 ResolveTargetPosition(IDamageable target, Collider targetCollider, Vector3 fallbackPosition)
    {
        if (targetCollider != null)
        {
            return targetCollider.bounds.center;
        }

        if (target is Component targetComponent)
        {
            return targetComponent.transform.position;
        }

        return fallbackPosition;
    }

    // 데미지 대상의 Transform 참조를 반환한다
    private static Transform ResolveTargetTransform(IDamageable target)
    {
        if (target is Component targetComponent)
        {
            return targetComponent.transform;
        }

        return null;
    }

    // 체인 탐색에 사용한 콜라이더 버퍼를 비운다
    private static void ClearHitBuffer(int hitCount)
    {
        int clearCount = Mathf.Min(hitCount, ChainHitBuffer.Length);
        for (int i = 0; i < clearCount; i++)
        {
            ChainHitBuffer[i] = null;
        }
    }

    // 이번 체인에 등록한 대상 버퍼를 비운다
    private static void ClearChainedTargets(int chainedTargetCount)
    {
        for (int i = 0; i < chainedTargetCount; i++)
        {
            ChainedTargets[i] = null;
        }
    }
}
