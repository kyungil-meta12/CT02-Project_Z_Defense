using UnityEngine;

/// <summary>
/// Ignition_Turret 전용 최대체력 비례 화상 성장값을 공통 터렛 성장값과 함께 관리하는 프로필.
/// </summary>
[CreateAssetMenu(menuName = "Project Z Defense/Ignition Turret Stat Growth Profile")]
public class IgnitionTurretStatGrowthProfileSO : TurretStatGrowthProfileSO
{
    [Header("Ignition 전용 화상 성장")]
    [Range(0.0f, 1.0f)] public float ignitionMaxHpDamageRatioPerTickPerLevel = 0.0f;
    [Range(0.0f, 1.0f)] public float maxIgnitionMaxHpDamageRatioPerTick = 1.0f;
    [Min(0.0f)] public float ignitionDurationPerLevel = 0.0f;
    [Min(0.0f)] public float maxIgnitionDuration = 0.0f;

    [Header("Ignition 반응 화상 성장")]
    [Range(0.0f, 1.0f)] public float ignitionReactionMaxHpDamageRatioPerTickPerLevel = 0.0f;
    [Range(0.0f, 1.0f)] public float maxIgnitionReactionMaxHpDamageRatioPerTick = 1.0f;
    [Min(0.0f)] public float ignitionReactionTickIntervalReductionPerLevel = 0.0f;
    [Min(0.01f)] public float minIgnitionReactionTickInterval = 0.01f;

    // 현재 레벨에 맞는 Ignition 최대체력 비례 틱데미지를 계산한다
    public override float CalculateIgnitionMaxHpDamageRatioPerTick(float baseValue, int level)
    {
        float value = Mathf.Clamp01(baseValue) + ignitionMaxHpDamageRatioPerTickPerLevel * GetCompletedGrowthLevel(level);
        return ClampRatioWithOptionalMax(value, maxIgnitionMaxHpDamageRatioPerTick);
    }

    // 현재 레벨에 맞는 Ignition 지속시간을 계산한다
    public override float CalculateIgnitionDuration(float baseValue, int level)
    {
        float value = Mathf.Max(0.0f, baseValue) + ignitionDurationPerLevel * GetCompletedGrowthLevel(level);
        return ApplyOptionalMax(value, maxIgnitionDuration);
    }

    // 현재 레벨에 맞는 Ignition 반응 최대체력 비례 틱데미지를 계산한다
    public override float CalculateIgnitionReactionMaxHpDamageRatioPerTick(float baseValue, int level)
    {
        float value = Mathf.Clamp01(baseValue) + ignitionReactionMaxHpDamageRatioPerTickPerLevel * GetCompletedGrowthLevel(level);
        return ClampRatioWithOptionalMax(value, maxIgnitionReactionMaxHpDamageRatioPerTick);
    }

    // 현재 레벨에 맞는 Ignition 반응 틱 간격을 계산한다
    public override float CalculateIgnitionReactionTickInterval(float baseValue, int level)
    {
        float value = Mathf.Max(0.01f, baseValue) - ignitionReactionTickIntervalReductionPerLevel * GetCompletedGrowthLevel(level);
        return Mathf.Max(minIgnitionReactionTickInterval, value);
    }

    // 비율 상한이 0보다 클 때만 상한을 적용하고 최종 비율을 보정한다
    private static float ClampRatioWithOptionalMax(float value, float maxValue)
    {
        float clampedValue = Mathf.Clamp01(value);
        return maxValue > 0.0f ? Mathf.Min(clampedValue, Mathf.Clamp01(maxValue)) : clampedValue;
    }

    // 인스펙터에서 입력한 Ignition 성장값을 안전한 범위로 보정한다
    protected override void OnValidate()
    {
        base.OnValidate();
        ignitionMaxHpDamageRatioPerTickPerLevel = Mathf.Clamp01(ignitionMaxHpDamageRatioPerTickPerLevel);
        maxIgnitionMaxHpDamageRatioPerTick = Mathf.Clamp01(maxIgnitionMaxHpDamageRatioPerTick);
        ignitionDurationPerLevel = Mathf.Max(0.0f, ignitionDurationPerLevel);
        maxIgnitionDuration = Mathf.Max(0.0f, maxIgnitionDuration);
        ignitionReactionMaxHpDamageRatioPerTickPerLevel = Mathf.Clamp01(ignitionReactionMaxHpDamageRatioPerTickPerLevel);
        maxIgnitionReactionMaxHpDamageRatioPerTick = Mathf.Clamp01(maxIgnitionReactionMaxHpDamageRatioPerTick);
        ignitionReactionTickIntervalReductionPerLevel = Mathf.Max(0.0f, ignitionReactionTickIntervalReductionPerLevel);
        minIgnitionReactionTickInterval = Mathf.Max(0.01f, minIgnitionReactionTickInterval);
    }
}
