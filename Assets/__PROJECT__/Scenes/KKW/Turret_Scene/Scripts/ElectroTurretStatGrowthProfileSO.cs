using UnityEngine;

/// <summary>
/// Electro_Turret 전용 Shock 유지시간, 체인 대상 수, 과부하 데미지 성장값을 공통 터렛 성장값과 함께 관리하는 프로필.
/// </summary>
[CreateAssetMenu(menuName = "Project Z Defense/Electro Turret Stat Growth Profile")]
public class ElectroTurretStatGrowthProfileSO : TurretStatGrowthProfileSO
{
    [Header("전기 쇼크 성장")]
    [Min(0.0f)] public float shockStackDurationPerLevel = 0.0f;
    [Min(0.0f)] public float maxShockStackDuration = 0.0f;

    [Header("전기 체인 성장")]
    [Tooltip("체인 대상 수가 증가하는 레벨 구간입니다. 10이면 Lv1~10은 기본값, Lv11부터 첫 증가가 적용됩니다. 0이면 체인 대상 수 성장을 사용하지 않습니다.")]
    [Min(0)] public int chainTargetCountIntervalLevel;
    [Tooltip("레벨 구간이 완료될 때마다 추가되는 체인 대상 수입니다. 예: 구간 10, 증가량 1이면 10레벨마다 체인 대상 수가 1씩 증가합니다.")]
    [Min(0)] public int chainTargetCountPerInterval;
    [Tooltip("성장 후 체인 대상 수의 최대값입니다. 0이면 상한 없이 성장합니다.")]
    [Min(0)] public int maxChainTargetCount;

    [Header("전기 과부하 데미지 성장")]
    [Range(0.0f, 1.0f)] public float overloadMaxHpDamageRatioPerLevel = 0.0f;
    [Range(0.0f, 1.0f)] public float maxOverloadMaxHpDamageRatio = 1.0f;
    [Range(0.0f, 1.0f)] public float bossOverloadMaxHpDamageRatioPerLevel = 0.0f;
    [Range(0.0f, 1.0f)] public float maxBossOverloadMaxHpDamageRatio = 1.0f;

    // 현재 레벨에 맞는 Shock 스택 유지시간을 계산한다
    public override float CalculateElectroShockStackDuration(float baseValue, int level)
    {
        float value = Mathf.Max(0.0f, baseValue) + shockStackDurationPerLevel * GetCompletedGrowthLevel(level);
        return ApplyOptionalMax(value, maxShockStackDuration);
    }

    // 현재 레벨에 맞는 체인 대상 수를 계산한다
    public override int CalculateElectroMaxChainTargets(int baseValue, int level)
    {
        int value = Mathf.Max(1, baseValue);
        if (chainTargetCountIntervalLevel > 0 && chainTargetCountPerInterval > 0)
        {
            value += GetCompletedGrowthLevel(level) / chainTargetCountIntervalLevel * chainTargetCountPerInterval;
        }

        return maxChainTargetCount > 0 ? Mathf.Min(value, maxChainTargetCount) : value;
    }

    // 현재 레벨에 맞는 일반 대상 과부하 최대체력 비례 데미지를 계산한다
    public override float CalculateElectroOverloadMaxHpDamageRatio(float baseValue, int level)
    {
        float value = Mathf.Clamp01(baseValue) + overloadMaxHpDamageRatioPerLevel * GetCompletedGrowthLevel(level);
        return ClampRatioWithOptionalMax(value, maxOverloadMaxHpDamageRatio);
    }

    // 현재 레벨에 맞는 보스 대상 과부하 최대체력 비례 데미지를 계산한다
    public override float CalculateElectroBossOverloadMaxHpDamageRatio(float baseValue, int level)
    {
        float value = Mathf.Clamp01(baseValue) + bossOverloadMaxHpDamageRatioPerLevel * GetCompletedGrowthLevel(level);
        return ClampRatioWithOptionalMax(value, maxBossOverloadMaxHpDamageRatio);
    }

    // 비율 상한이 0보다 클 때만 상한을 적용하고 최종 비율을 보정한다
    private static float ClampRatioWithOptionalMax(float value, float maxValue)
    {
        float clampedValue = Mathf.Clamp01(value);
        return maxValue > 0.0f ? Mathf.Min(clampedValue, Mathf.Clamp01(maxValue)) : clampedValue;
    }

    // 인스펙터에서 입력한 Electro 성장값을 안전한 범위로 보정한다
    protected override void OnValidate()
    {
        base.OnValidate();
        shockStackDurationPerLevel = Mathf.Max(0.0f, shockStackDurationPerLevel);
        maxShockStackDuration = Mathf.Max(0.0f, maxShockStackDuration);
        chainTargetCountIntervalLevel = Mathf.Max(0, chainTargetCountIntervalLevel);
        chainTargetCountPerInterval = Mathf.Max(0, chainTargetCountPerInterval);
        maxChainTargetCount = Mathf.Max(0, maxChainTargetCount);
        overloadMaxHpDamageRatioPerLevel = Mathf.Clamp01(overloadMaxHpDamageRatioPerLevel);
        maxOverloadMaxHpDamageRatio = Mathf.Clamp01(maxOverloadMaxHpDamageRatio);
        bossOverloadMaxHpDamageRatioPerLevel = Mathf.Clamp01(bossOverloadMaxHpDamageRatioPerLevel);
        maxBossOverloadMaxHpDamageRatio = Mathf.Clamp01(maxBossOverloadMaxHpDamageRatio);
    }
}
