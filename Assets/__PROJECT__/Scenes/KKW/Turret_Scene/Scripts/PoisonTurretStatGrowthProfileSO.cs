using UnityEngine;

/// <summary>
/// Poison_Turret 전용 상태이상 성장값을 공통 터렛 성장값과 함께 관리하는 프로필.
/// </summary>
[CreateAssetMenu(menuName = "Project Z Defense/Poison Turret Stat Growth Profile")]
public class PoisonTurretStatGrowthProfileSO : TurretStatGrowthProfileSO
{
    [Header("포이즌 상태이상 성장")]
    [Range(0.0f, 1.0f)] public float poisonMaxHpDamageRatioPerTickPerLevel = 0.0f;
    [Range(0.0f, 1.0f)] public float maxPoisonMaxHpDamageRatioPerTick = 1.0f;
    [Min(0.0f)] public float poisonDurationPerLevel = 0.0f;
    [Min(0.0f)] public float maxPoisonDuration = 0.0f;

    [Header("포이즌 처형 폭발 성장")]
    [Min(0.0f)] public float poisonDeathBurstRadiusPerLevel = 0.0f;
    [Min(0.0f)] public float maxPoisonDeathBurstRadius = 0.0f;
    [Range(0.0f, 1.0f)] public float poisonDeathBurstMaxHpDamageRatioPerTickPerLevel = 0.0f;
    [Range(0.0f, 1.0f)] public float maxPoisonDeathBurstMaxHpDamageRatioPerTick = 1.0f;
    [Min(0.0f)] public float poisonDeathBurstDurationPerLevel = 0.0f;
    [Min(0.0f)] public float maxPoisonDeathBurstDuration = 0.0f;

    // 현재 레벨에 맞는 Poison 최대체력 비례 틱데미지를 계산한다
    public override float CalculatePoisonMaxHpDamageRatioPerTick(float baseValue, int level)
    {
        float value = Mathf.Clamp01(baseValue) + poisonMaxHpDamageRatioPerTickPerLevel * GetCompletedGrowthLevel(level);
        return ClampRatioWithOptionalMax(value, maxPoisonMaxHpDamageRatioPerTick);
    }

    // 현재 레벨에 맞는 Poison 지속시간을 계산한다
    public override float CalculatePoisonDuration(float baseValue, int level)
    {
        float value = Mathf.Max(0.0f, baseValue) + poisonDurationPerLevel * GetCompletedGrowthLevel(level);
        return ApplyOptionalMax(value, maxPoisonDuration);
    }

    // 현재 레벨에 맞는 Poison 처형 폭발 반경을 계산한다
    public override float CalculatePoisonDeathBurstRadius(float baseValue, int level)
    {
        float value = Mathf.Max(0.0f, baseValue) + poisonDeathBurstRadiusPerLevel * GetCompletedGrowthLevel(level);
        return ApplyOptionalMax(value, maxPoisonDeathBurstRadius);
    }

    // 현재 레벨에 맞는 Poison 처형 폭발 약한 중독 틱데미지를 계산한다
    public override float CalculatePoisonDeathBurstMaxHpDamageRatioPerTick(float baseValue, int level)
    {
        float value = Mathf.Clamp01(baseValue) + poisonDeathBurstMaxHpDamageRatioPerTickPerLevel * GetCompletedGrowthLevel(level);
        return ClampRatioWithOptionalMax(value, maxPoisonDeathBurstMaxHpDamageRatioPerTick);
    }

    // 현재 레벨에 맞는 Poison 처형 폭발 약한 중독 지속시간을 계산한다
    public override float CalculatePoisonDeathBurstDuration(float baseValue, int level)
    {
        float value = Mathf.Max(0.0f, baseValue) + poisonDeathBurstDurationPerLevel * GetCompletedGrowthLevel(level);
        return ApplyOptionalMax(value, maxPoisonDeathBurstDuration);
    }

    // 비율 상한이 0보다 클 때만 상한을 적용하고 최종 비율을 보정한다
    private static float ClampRatioWithOptionalMax(float value, float maxValue)
    {
        float clampedValue = Mathf.Clamp01(value);
        return maxValue > 0.0f ? Mathf.Min(clampedValue, Mathf.Clamp01(maxValue)) : clampedValue;
    }

    // 인스펙터에서 입력한 Poison 성장값을 안전한 범위로 보정한다
    protected override void OnValidate()
    {
        base.OnValidate();
        poisonMaxHpDamageRatioPerTickPerLevel = Mathf.Clamp01(poisonMaxHpDamageRatioPerTickPerLevel);
        maxPoisonMaxHpDamageRatioPerTick = Mathf.Clamp01(maxPoisonMaxHpDamageRatioPerTick);
        poisonDurationPerLevel = Mathf.Max(0.0f, poisonDurationPerLevel);
        maxPoisonDuration = Mathf.Max(0.0f, maxPoisonDuration);
        poisonDeathBurstRadiusPerLevel = Mathf.Max(0.0f, poisonDeathBurstRadiusPerLevel);
        maxPoisonDeathBurstRadius = Mathf.Max(0.0f, maxPoisonDeathBurstRadius);
        poisonDeathBurstMaxHpDamageRatioPerTickPerLevel = Mathf.Clamp01(poisonDeathBurstMaxHpDamageRatioPerTickPerLevel);
        maxPoisonDeathBurstMaxHpDamageRatioPerTick = Mathf.Clamp01(maxPoisonDeathBurstMaxHpDamageRatioPerTick);
        poisonDeathBurstDurationPerLevel = Mathf.Max(0.0f, poisonDeathBurstDurationPerLevel);
        maxPoisonDeathBurstDuration = Mathf.Max(0.0f, maxPoisonDeathBurstDuration);
    }
}
