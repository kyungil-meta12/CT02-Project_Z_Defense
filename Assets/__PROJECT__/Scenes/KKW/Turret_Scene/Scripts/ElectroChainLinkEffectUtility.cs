using UnityEngine;
using ProjectZDefense.StatusEffects;

/// <summary>
/// Electro 체인 구간 사이에 전용 파티클 프리팹을 배치하고 거리 기반 스케일을 적용한다.
/// </summary>
public static class ElectroChainLinkEffectUtility
{
    private const float MIN_DIRECTION_SQR = 0.0001f;

    // 지정한 두 대상 사이에 체인 링크 이펙트를 재생한다
    public static GameObject Play(ElectroStatusPayload payload, Vector3 startPosition, Vector3 endPosition)
    {
        if (!CanPlay(payload, startPosition, endPosition))
        {
            return null;
        }

        GameObject effectObject = SpawnEffect(payload, startPosition, endPosition);
        if (effectObject == null)
        {
            return null;
        }

        InitEffectReturner(effectObject, ResolveEffectDuration(payload));
        return effectObject;
    }

    // 지정한 두 대상 Transform 사이에 체인 링크 이펙트를 재생하고 짧게 위치를 추적한다
    public static GameObject Play(ElectroStatusPayload payload, Transform startTransform, Transform endTransform, Vector3 fallbackStartPosition, Vector3 fallbackEndPosition)
    {
        Vector3 startPosition = ResolveAnchorPosition(startTransform, fallbackStartPosition);
        Vector3 endPosition = ResolveAnchorPosition(endTransform, fallbackEndPosition);
        if (!CanPlay(payload, startPosition, endPosition))
        {
            return null;
        }

        GameObject effectObject = SpawnEffect(payload, startPosition, endPosition);
        if (effectObject == null)
        {
            return null;
        }

        InitAnchorTracker(effectObject, payload, startTransform, endTransform, fallbackStartPosition, fallbackEndPosition);
        InitEffectReturner(effectObject, ResolveEffectDuration(payload));
        return effectObject;
    }

    // 지정한 두 대상 Collider 사이에 체인 링크 이펙트를 재생하고 짧게 위치를 추적한다
    public static GameObject Play(ElectroStatusPayload payload, Collider startCollider, Collider endCollider, Transform startTransform, Transform endTransform, Vector3 fallbackStartPosition, Vector3 fallbackEndPosition)
    {
        Vector3 startPosition = ResolveAnchorPosition(startCollider, startTransform, fallbackStartPosition);
        Vector3 endPosition = ResolveAnchorPosition(endCollider, endTransform, fallbackEndPosition);
        if (!CanPlay(payload, startPosition, endPosition))
        {
            return null;
        }

        GameObject effectObject = SpawnEffect(payload, startPosition, endPosition);
        if (effectObject == null)
        {
            return null;
        }

        InitAnchorTracker(effectObject, payload, startCollider, endCollider, startTransform, endTransform, fallbackStartPosition, fallbackEndPosition);
        InitEffectReturner(effectObject, ResolveEffectDuration(payload));
        return effectObject;
    }

    // 체인 링크 이펙트를 생성하고 현재 시작/끝 위치에 맞춰 배치한다
    private static GameObject SpawnEffect(ElectroStatusPayload payload, Vector3 startPosition, Vector3 endPosition)
    {
        PlacementData placementData = CalculatePlacement(payload, startPosition, endPosition);
        GameObject effectObject = PooledObjectUtility.Spawn(ResolveEffectPrefab(payload), placementData.Position, placementData.Rotation);
        if (effectObject == null)
        {
            return null;
        }

        ApplyEffectScale(effectObject, placementData.Scale);
        ApplyCoreLineEffect(effectObject, payload, startPosition, endPosition);
        return effectObject;
    }

    // 생성된 체인 링크 이펙트를 최신 시작/끝 위치에 다시 맞춘다
    public static void ApplyPlacement(GameObject effectObject, ElectroStatusPayload payload, Vector3 startPosition, Vector3 endPosition)
    {
        if (effectObject == null || !CanPlay(payload, startPosition, endPosition))
        {
            return;
        }

        PlacementData placementData = CalculatePlacement(payload, startPosition, endPosition);
        Transform effectTransform = effectObject.transform;
        effectTransform.SetPositionAndRotation(placementData.Position, placementData.Rotation);
        effectTransform.localScale = placementData.Scale;
    }

    // 체인 링크 이펙트를 재생할 수 있는지 확인한다
    private static bool CanPlay(ElectroStatusPayload payload, Vector3 startPosition, Vector3 endPosition)
    {
        return ResolvePlayChainLinkEffect(payload) &&
               ResolveEffectPrefab(payload) != null &&
               ResolveEffectDuration(payload) > 0.0f &&
               (endPosition - startPosition).sqrMagnitude > MIN_DIRECTION_SQR;
    }

    // 체인 링크 이펙트의 위치, 회전, 스케일을 계산한다
    private static PlacementData CalculatePlacement(ElectroStatusPayload payload, Vector3 startPosition, Vector3 endPosition)
    {
        float verticalOffset = ResolveVerticalOffset(payload);
        Vector3 elevatedStart = startPosition + Vector3.up * verticalOffset;
        Vector3 elevatedEnd = endPosition + Vector3.up * verticalOffset;
        Vector3 direction = elevatedEnd - elevatedStart;
        float distance = direction.magnitude;
        Quaternion effectRotation = ResolveEffectRotation(payload, direction);
        Vector3 effectScale = ResolveEffectScale(payload, distance);
        Vector3 effectPosition = ResolveEffectPosition(payload, elevatedStart, elevatedEnd, effectRotation, effectScale);
        return new PlacementData(effectPosition, effectRotation, effectScale);
    }

    // 이펙트 프리팹을 체인 거리와 굵기 설정에 맞춰 스케일링한다
    private static void ApplyEffectScale(GameObject effectObject, Vector3 effectScale)
    {
        effectObject.transform.localScale = effectScale;
    }

    // 코어 라인을 현재 체인 시작점과 끝점에 맞춰 갱신한다
    private static void ApplyCoreLineEffect(GameObject effectObject, ElectroStatusPayload payload, Vector3 startPosition, Vector3 endPosition)
    {
        if (!ResolvePlayChainCoreLine(payload))
        {
            ElectroChainCoreLineEffect existingCoreLine = effectObject.GetComponent<ElectroChainCoreLineEffect>();
            if (existingCoreLine != null)
            {
                existingCoreLine.DisableLine();
            }

            return;
        }

        ElectroChainCoreLineEffect coreLine = effectObject.GetComponent<ElectroChainCoreLineEffect>();
        if (coreLine == null)
        {
            coreLine = effectObject.AddComponent<ElectroChainCoreLineEffect>();
        }

        coreLine.Apply(payload, startPosition, endPosition);
    }

    // 이펙트 프리팹을 체인 거리와 굵기 설정에 맞춰 스케일값으로 계산한다
    private static Vector3 ResolveEffectScale(ElectroStatusPayload payload, float distance)
    {
        GameObject effectPrefab = ResolveEffectPrefab(payload);
        Vector3 baseScale = effectPrefab.transform.localScale;
        Vector3 sourceAxis = ResolveSourceAxis(payload);
        float safeThicknessScale = Mathf.Max(0.01f, ResolveThicknessScale(payload));
        float lengthScale = Mathf.Max(0.01f, distance * ResolveLengthScaleMultiplier(payload));
        return new Vector3(
            baseScale.x * ResolveAxisScale(sourceAxis.x, lengthScale, safeThicknessScale),
            baseScale.y * ResolveAxisScale(sourceAxis.y, lengthScale, safeThicknessScale),
            baseScale.z * ResolveAxisScale(sourceAxis.z, lengthScale, safeThicknessScale));
    }

    // 대상 Transform이 살아있으면 최신 위치를 사용하고 아니면 대체 위치를 사용한다
    private static Vector3 ResolveAnchorPosition(Transform anchorTransform, Vector3 fallbackPosition)
    {
        return anchorTransform != null ? anchorTransform.position : fallbackPosition;
    }

    // 대상 Collider가 살아있으면 콜라이더 중심을 우선 사용하고 아니면 Transform 또는 대체 위치를 사용한다
    private static Vector3 ResolveAnchorPosition(Collider anchorCollider, Transform anchorTransform, Vector3 fallbackPosition)
    {
        if (anchorCollider != null)
        {
            return anchorCollider.bounds.center;
        }

        return ResolveAnchorPosition(anchorTransform, fallbackPosition);
    }

    // 설정에 따라 루트 위치를 중간점 또는 로컬 시작점 기준으로 계산한다
    private static Vector3 ResolveEffectPosition(ElectroStatusPayload payload, Vector3 elevatedStart, Vector3 elevatedEnd, Quaternion effectRotation, Vector3 effectScale)
    {
        if (ResolveUseEndpointFit(payload))
        {
            Vector3 scaledStartPoint = Vector3.Scale(ResolveLocalStartPoint(payload), effectScale);
            return elevatedStart - effectRotation * scaledStartPoint + effectRotation * ResolveLocalPositionOffset(payload);
        }

        Vector3 midpoint = (elevatedStart + elevatedEnd) * 0.5f;
        return midpoint + effectRotation * ResolveLocalPositionOffset(payload);
    }

    // 체인 방향과 프리팹 기준 축을 맞춘 뒤 추가 회전 보정을 적용한다
    private static Quaternion ResolveEffectRotation(ElectroStatusPayload payload, Vector3 direction)
    {
        Quaternion axisRotation = Quaternion.FromToRotation(ResolveSourceAxis(payload), direction.normalized);
        return axisRotation * Quaternion.Euler(ResolveRotationEulerOffset(payload));
    }

    // 축 성분에 따라 길이 또는 두께 스케일을 선택한다
    private static float ResolveAxisScale(float axisComponent, float lengthScale, float thicknessScale)
    {
        return Mathf.Abs(axisComponent) >= 0.5f ? lengthScale : thicknessScale;
    }

    // 현재 사용할 체인 링크 프리팹을 반환한다
    private static GameObject ResolveEffectPrefab(ElectroStatusPayload payload)
    {
        if (payload.sourceProfile != null)
        {
            return payload.sourceProfile.chainLinkEffectPrefab;
        }

        return payload.chainLinkEffectPrefab;
    }

    // 현재 체인 링크 이펙트 재생 여부를 반환한다
    private static bool ResolvePlayChainLinkEffect(ElectroStatusPayload payload)
    {
        return payload.sourceProfile != null ? payload.sourceProfile.playChainLinkEffect : payload.playChainLinkEffect;
    }

    // 현재 사용할 체인 링크 높이 보정을 반환한다
    private static float ResolveVerticalOffset(ElectroStatusPayload payload)
    {
        return payload.sourceProfile != null ? payload.sourceProfile.chainLinkVerticalOffset : payload.chainLinkVerticalOffset;
    }

    // 현재 사용할 체인 링크 지속 시간을 반환한다
    private static float ResolveEffectDuration(ElectroStatusPayload payload)
    {
        return payload.sourceProfile != null ? payload.sourceProfile.chainLinkEffectDuration : payload.chainLinkEffectDuration;
    }

    // 현재 사용할 체인 링크 기준 축을 반환한다
    private static Vector3 ResolveSourceAxis(ElectroStatusPayload payload)
    {
        Vector3 axis = payload.sourceProfile != null ? payload.sourceProfile.chainLinkSourceAxis : payload.chainLinkSourceAxis;
        if (axis.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return axis.normalized;
    }

    // 현재 사용할 체인 링크 로컬 위치 보정을 반환한다
    private static Vector3 ResolveLocalPositionOffset(ElectroStatusPayload payload)
    {
        return payload.sourceProfile != null ? payload.sourceProfile.chainLinkLocalPositionOffset : payload.chainLinkLocalPositionOffset;
    }

    // 체인 링크 로컬 시작점 보정을 사용할지 반환한다
    private static bool ResolveUseEndpointFit(ElectroStatusPayload payload)
    {
        return payload.sourceProfile != null ? payload.sourceProfile.useChainLinkEndpointFit : payload.useChainLinkEndpointFit;
    }

    // 현재 사용할 체인 링크 로컬 시작점을 반환한다
    private static Vector3 ResolveLocalStartPoint(ElectroStatusPayload payload)
    {
        return payload.sourceProfile != null ? payload.sourceProfile.chainLinkLocalStartPoint : payload.chainLinkLocalStartPoint;
    }

    // 현재 사용할 체인 링크 로컬 끝점을 반환한다
    private static Vector3 ResolveLocalEndPoint(ElectroStatusPayload payload)
    {
        return payload.sourceProfile != null ? payload.sourceProfile.chainLinkLocalEndPoint : payload.chainLinkLocalEndPoint;
    }

    // 현재 사용할 체인 링크 회전 보정을 반환한다
    private static Vector3 ResolveRotationEulerOffset(ElectroStatusPayload payload)
    {
        return payload.sourceProfile != null ? payload.sourceProfile.chainLinkRotationEulerOffset : payload.chainLinkRotationEulerOffset;
    }

    // 현재 사용할 체인 링크 길이 배율을 반환한다
    private static float ResolveLengthScaleMultiplier(ElectroStatusPayload payload)
    {
        if (!ResolveUseEndpointFit(payload))
        {
            return payload.sourceProfile != null ? payload.sourceProfile.chainLinkLengthScaleMultiplier : payload.chainLinkLengthScaleMultiplier;
        }

        float localLength = (ResolveLocalEndPoint(payload) - ResolveLocalStartPoint(payload)).magnitude;
        if (localLength <= 0.0001f)
        {
            return payload.sourceProfile != null ? payload.sourceProfile.chainLinkLengthScaleMultiplier : payload.chainLinkLengthScaleMultiplier;
        }

        return 1.0f / localLength;
    }

    // 현재 사용할 체인 링크 두께 배율을 반환한다
    private static float ResolveThicknessScale(ElectroStatusPayload payload)
    {
        return payload.sourceProfile != null ? payload.sourceProfile.chainLinkThicknessScale : payload.chainLinkThicknessScale;
    }

    // 현재 체인 코어 라인을 재생할지 반환한다
    private static bool ResolvePlayChainCoreLine(ElectroStatusPayload payload)
    {
        return payload.sourceProfile != null ? payload.sourceProfile.playChainCoreLine : payload.playChainCoreLine;
    }

    // 체인 링크 이펙트를 지정 시간 뒤 풀로 반환하도록 초기화한다
    private static void InitEffectReturner(GameObject effectObject, float duration)
    {
        PooledEffectReturner returner = effectObject.GetComponent<PooledEffectReturner>();
        if (returner == null)
        {
            returner = effectObject.AddComponent<PooledEffectReturner>();
        }

        returner.Init(duration);
    }

    // 체인 링크 이펙트가 파티클 지연 시간 동안 대상 이동을 따라가도록 초기화한다
    private static void InitAnchorTracker(GameObject effectObject, ElectroStatusPayload payload, Transform startTransform, Transform endTransform, Vector3 fallbackStartPosition, Vector3 fallbackEndPosition)
    {
        ElectroChainLinkAnchorTracker anchorTracker = effectObject.GetComponent<ElectroChainLinkAnchorTracker>();
        if (anchorTracker == null)
        {
            anchorTracker = effectObject.AddComponent<ElectroChainLinkAnchorTracker>();
        }

        anchorTracker.Init(payload, startTransform, endTransform, fallbackStartPosition, fallbackEndPosition, ResolveEffectDuration(payload));
    }

    // 체인 링크 이펙트가 파티클 지연 시간 동안 대상 콜라이더 중심을 따라가도록 초기화한다
    private static void InitAnchorTracker(GameObject effectObject, ElectroStatusPayload payload, Collider startCollider, Collider endCollider, Transform startTransform, Transform endTransform, Vector3 fallbackStartPosition, Vector3 fallbackEndPosition)
    {
        ElectroChainLinkAnchorTracker anchorTracker = effectObject.GetComponent<ElectroChainLinkAnchorTracker>();
        if (anchorTracker == null)
        {
            anchorTracker = effectObject.AddComponent<ElectroChainLinkAnchorTracker>();
        }

        anchorTracker.Init(payload, startCollider, endCollider, startTransform, endTransform, fallbackStartPosition, fallbackEndPosition, ResolveEffectDuration(payload));
    }

    // 체인 링크 배치 계산 결과를 보관한다
    private struct PlacementData
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        // 계산된 위치, 회전, 스케일을 저장한다
        public PlacementData(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
    }
}
