using UnityEngine;

/// <summary>
/// Poison 상태이상의 틱 적용 가능 여부, 남은 틱 수, 틱데미지 계산을 공통 처리한다.
/// </summary>
public static class PoisonStatusRuntimeUtility
{
    private const float TICK_BOUNDARY_EPSILON = 0.000001f;
    private const float LETHAL_PREDICTION_EPSILON = 0.0001f;

    // 현재 프레임의 Poison 틱이 지속시간 안에서 발생 가능한지 확인한다
    public static bool CanApplyTick(float previousRemainingDuration, float previousTickTimer)
    {
        return previousTickTimer <= previousRemainingDuration + TICK_BOUNDARY_EPSILON;
    }

    // 남은 지속시간 안에 발생할 Poison 틱 수를 계산한다
    public static int GetRemainingTickCount(float remainingDuration_, float nextTickTime_, float tickInterval_)
    {
        float remainingDuration = Mathf.Max(0.0f, remainingDuration_);
        float nextTickTime = Mathf.Max(0.0f, nextTickTime_);
        float tickInterval = Mathf.Max(0.01f, tickInterval_);
        float predictableDuration = remainingDuration - LETHAL_PREDICTION_EPSILON;
        if (predictableDuration <= 0.0f || nextTickTime > predictableDuration)
        {
            return 0;
        }

        return 1 + Mathf.FloorToInt(Mathf.Max(0.0f, predictableDuration - nextTickTime) / tickInterval);
    }

    // 최대체력 비례 Poison 틱데미지를 계산한다
    public static float CalculateTickDamage(float totalHp, float maxHpDamageRatioPerTick, int stackCount, float damageMultiplier)
    {
        if (totalHp <= 0.0f || maxHpDamageRatioPerTick <= 0.0f || stackCount <= 0 || damageMultiplier <= 0.0f)
        {
            return 0.0f;
        }

        return totalHp * Mathf.Clamp01(maxHpDamageRatioPerTick) * stackCount * Mathf.Max(0.0f, damageMultiplier);
    }
}
