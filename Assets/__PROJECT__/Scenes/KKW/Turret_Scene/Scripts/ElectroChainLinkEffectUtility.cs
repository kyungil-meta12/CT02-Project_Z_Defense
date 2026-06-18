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

        float verticalOffset = ResolveVerticalOffset(payload);
        Vector3 elevatedStart = startPosition + Vector3.up * verticalOffset;
        Vector3 elevatedEnd = endPosition + Vector3.up * verticalOffset;
        Vector3 direction = elevatedEnd - elevatedStart;
        float distance = direction.magnitude;
        Quaternion effectRotation = ResolveEffectRotation(payload, direction);
        Vector3 effectScale = ResolveEffectScale(payload, distance);
        Vector3 effectPosition = ResolveEffectPosition(payload, elevatedStart, elevatedEnd, effectRotation, effectScale);

        GameObject effectObject = PooledObjectUtility.Spawn(ResolveEffectPrefab(payload), effectPosition, effectRotation);
        if (effectObject == null)
        {
            return null;
        }

        ApplyEffectScale(effectObject, effectScale);
        InitEffectReturner(effectObject, ResolveEffectDuration(payload));
        return effectObject;
    }

    // 체인 링크 이펙트를 재생할 수 있는지 확인한다
    private static bool CanPlay(ElectroStatusPayload payload, Vector3 startPosition, Vector3 endPosition)
    {
        return ResolvePlayChainLinkEffect(payload) &&
               ResolveEffectPrefab(payload) != null &&
               ResolveEffectDuration(payload) > 0.0f &&
               (endPosition - startPosition).sqrMagnitude > MIN_DIRECTION_SQR;
    }

    // 이펙트 프리팹을 체인 거리와 굵기 설정에 맞춰 스케일링한다
    private static void ApplyEffectScale(GameObject effectObject, Vector3 effectScale)
    {
        effectObject.transform.localScale = effectScale;
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
}
