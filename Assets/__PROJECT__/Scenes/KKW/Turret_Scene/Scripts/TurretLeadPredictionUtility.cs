using UnityEngine;

/// <summary>
/// 발사체 속도와 타겟 이동 속도를 기준으로 터렛의 선행 조준 위치를 계산한다.
/// </summary>
public static class TurretLeadPredictionUtility
{
    private const float MIN_PROJECTILE_SPEED = 0.01f;
    private const float MIN_TIME_DENOMINATOR = 0.0001f;

    // 발사체와 타겟이 만날 수 있는 선행 조준 위치를 계산한다
    public static Vector3 PredictInterceptPosition(Vector3 shooterPosition, Vector3 targetPosition, Vector3 targetVelocity, float projectileSpeed, float maxLeadTime)
    {
        float safeProjectileSpeed = Mathf.Max(MIN_PROJECTILE_SPEED, projectileSpeed);
        float safeMaxLeadTime = Mathf.Max(0.0f, maxLeadTime);

        if (safeMaxLeadTime <= 0.0f || targetVelocity.sqrMagnitude <= 0.0001f)
        {
            return targetPosition;
        }

        Vector3 relativePosition = targetPosition - shooterPosition;
        float leadTime = CalculateInterceptTime(relativePosition, targetVelocity, safeProjectileSpeed);
        if (leadTime <= 0.0f)
        {
            leadTime = relativePosition.magnitude / safeProjectileSpeed;
        }

        leadTime = Mathf.Min(leadTime, safeMaxLeadTime);
        return targetPosition + targetVelocity * leadTime;
    }

    // 발사체와 이동 타겟의 예상 교차 시간을 계산한다
    private static float CalculateInterceptTime(Vector3 relativePosition, Vector3 targetVelocity, float projectileSpeed)
    {
        float a = Vector3.Dot(targetVelocity, targetVelocity) - projectileSpeed * projectileSpeed;
        float b = 2.0f * Vector3.Dot(relativePosition, targetVelocity);
        float c = Vector3.Dot(relativePosition, relativePosition);

        if (Mathf.Abs(a) <= MIN_TIME_DENOMINATOR)
        {
            return CalculateLinearInterceptTime(b, c);
        }

        float discriminant = b * b - 4.0f * a * c;
        if (discriminant < 0.0f)
        {
            return 0.0f;
        }

        float sqrtDiscriminant = Mathf.Sqrt(discriminant);
        float denominator = 2.0f * a;
        float timeA = (-b - sqrtDiscriminant) / denominator;
        float timeB = (-b + sqrtDiscriminant) / denominator;

        return SelectPositiveTime(timeA, timeB);
    }

    // 선형 근사 상황에서 교차 시간을 계산한다
    private static float CalculateLinearInterceptTime(float b, float c)
    {
        if (Mathf.Abs(b) <= MIN_TIME_DENOMINATOR)
        {
            return 0.0f;
        }

        float time = -c / b;
        return time > 0.0f ? time : 0.0f;
    }

    // 두 시간 후보 중 가장 작은 양수 시간을 선택한다
    private static float SelectPositiveTime(float timeA, float timeB)
    {
        bool timeAValid = timeA > 0.0f;
        bool timeBValid = timeB > 0.0f;

        if (timeAValid && timeBValid)
        {
            return Mathf.Min(timeA, timeB);
        }

        if (timeAValid)
        {
            return timeA;
        }

        return timeBValid ? timeB : 0.0f;
    }
}
