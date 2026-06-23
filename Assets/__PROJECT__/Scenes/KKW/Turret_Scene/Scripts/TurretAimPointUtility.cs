using UnityEngine;

/// <summary>
/// 터렛이 적의 콜라이더 중심보다 낮은 몸통 지점을 조준하도록 공통 조준점을 계산한다.
/// </summary>
public static class TurretAimPointUtility
{
    public const float DEFAULT_AIM_HEIGHT_RATIO = 0.35f;

    // 대상 오브젝트에서 콜라이더를 찾아 터렛 조준 위치를 반환한다
    public static Vector3 GetAimPosition(GameObject target)
    {
        return GetAimPosition(target, DEFAULT_AIM_HEIGHT_RATIO);
    }

    // 대상 오브젝트에서 콜라이더를 찾아 지정 높이 비율의 터렛 조준 위치를 반환한다
    public static Vector3 GetAimPosition(GameObject target, float aimHeightRatio)
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        IAimPointProvider aimPointProvider = target.GetComponent(typeof(IAimPointProvider)) as IAimPointProvider;
        if (aimPointProvider != null)
        {
            return aimPointProvider.GetAimPosition(aimHeightRatio);
        }

        Collider targetCollider = target.GetComponentInChildren<Collider>();
        if (targetCollider == null)
        {
            return target.transform.position;
        }

        return GetAimPosition(targetCollider, aimHeightRatio);
    }

    // 대상 콜라이더의 하단 기준 몸통 높이를 터렛 조준 위치로 반환한다
    public static Vector3 GetAimPosition(Collider targetCollider)
    {
        return GetAimPosition(targetCollider, DEFAULT_AIM_HEIGHT_RATIO);
    }

    // 대상 콜라이더의 하단 기준 지정 높이 비율을 터렛 조준 위치로 반환한다
    public static Vector3 GetAimPosition(Collider targetCollider, float aimHeightRatio)
    {
        if (targetCollider == null)
        {
            return Vector3.zero;
        }

        Bounds bounds = targetCollider.bounds;
        Vector3 aimPosition = bounds.center;
        aimPosition.y = Mathf.Lerp(bounds.min.y, bounds.max.y, Mathf.Clamp01(aimHeightRatio));
        return aimPosition;
    }
}
