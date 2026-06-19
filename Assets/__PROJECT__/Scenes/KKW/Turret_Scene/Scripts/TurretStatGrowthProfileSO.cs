using UnityEngine;

/// <summary>
/// 터렛 레벨에 따른 공통 전투 스탯 성장값을 관리하는 프로필.
/// </summary>
[CreateAssetMenu(menuName = "Project Z Defense/Turret Stat Growth Profile")]
public class TurretStatGrowthProfileSO : ScriptableObject
{
    [Header("Per Level")]
    public float damagePercentPerLevel = 1.0f;
    public float rangePerLevel = 0.0f;
    public float fireIntervalReductionPerLevel = 0.0f;

    [Header("Interval Growth")]
    public int projectileSpeedIntervalLevel = 50;
    public float projectileSpeedPerInterval = 1.0f;
    public int projectileCountIntervalLevel = 0;
    public int pierceCountIntervalLevel = 0;

    [Header("Limits")]
    public float maxRange = 200.0f;
    public float minFireInterval = 0.05f;
    public float maxProjectileSpeed = 200.0f;
    public int maxProjectileCount = 20;
    public int maxPierceCount = 20;

    // 현재 레벨에 맞는 Poison 최대체력 비례 틱데미지를 계산한다
    public virtual float CalculatePoisonMaxHpDamageRatioPerTick(float baseValue, int level)
    {
        return Mathf.Clamp01(baseValue);
    }

    // 현재 레벨에 맞는 Poison 지속시간을 계산한다
    public virtual float CalculatePoisonDuration(float baseValue, int level)
    {
        return Mathf.Max(0.0f, baseValue);
    }

    // 현재 레벨에 맞는 Poison 처형 폭발 반경을 계산한다
    public virtual float CalculatePoisonDeathBurstRadius(float baseValue, int level)
    {
        return Mathf.Max(0.0f, baseValue);
    }

    // 현재 레벨에 맞는 Poison 처형 폭발 약한 중독 틱데미지를 계산한다
    public virtual float CalculatePoisonDeathBurstMaxHpDamageRatioPerTick(float baseValue, int level)
    {
        return Mathf.Clamp01(baseValue);
    }

    // 현재 레벨에 맞는 Poison 처형 폭발 약한 중독 지속시간을 계산한다
    public virtual float CalculatePoisonDeathBurstDuration(float baseValue, int level)
    {
        return Mathf.Max(0.0f, baseValue);
    }

    // 현재 레벨에 맞는 Electro Shock 스택 유지시간을 계산한다
    public virtual float CalculateElectroShockStackDuration(float baseValue, int level)
    {
        return Mathf.Max(0.0f, baseValue);
    }

    // 현재 레벨에 맞는 Electro 체인 대상 수를 계산한다
    public virtual int CalculateElectroMaxChainTargets(int baseValue, int level)
    {
        return Mathf.Max(1, baseValue);
    }

    // 현재 레벨에 맞는 Electro 과부하 최대체력 비례 데미지를 계산한다
    public virtual float CalculateElectroOverloadMaxHpDamageRatio(float baseValue, int level)
    {
        return Mathf.Clamp01(baseValue);
    }

    // 현재 레벨에 맞는 Electro 보스 과부하 최대체력 비례 데미지를 계산한다
    public virtual float CalculateElectroBossOverloadMaxHpDamageRatio(float baseValue, int level)
    {
        return Mathf.Clamp01(baseValue);
    }

    // 레벨 1을 기준으로 완료된 성장 단계 수를 반환한다
    protected static int GetCompletedGrowthLevel(int level)
    {
        return Mathf.Max(0, level - 1);
    }

    // 최대값이 0보다 클 때만 상한을 적용한다
    protected static float ApplyOptionalMax(float value, float maxValue)
    {
        return maxValue > 0.0f ? Mathf.Min(value, maxValue) : value;
    }

    // 인스펙터에서 입력한 성장값을 안전한 범위로 보정한다
    protected virtual void OnValidate()
    {
        damagePercentPerLevel = Mathf.Max(0.0f, damagePercentPerLevel);
        fireIntervalReductionPerLevel = Mathf.Max(0.0f, fireIntervalReductionPerLevel);
        projectileSpeedIntervalLevel = Mathf.Max(0, projectileSpeedIntervalLevel);
        projectileSpeedPerInterval = Mathf.Max(0.0f, projectileSpeedPerInterval);
        projectileCountIntervalLevel = Mathf.Max(0, projectileCountIntervalLevel);
        pierceCountIntervalLevel = Mathf.Max(0, pierceCountIntervalLevel);
        maxRange = Mathf.Max(0.0f, maxRange);
        minFireInterval = Mathf.Max(0.01f, minFireInterval);
        maxProjectileSpeed = Mathf.Max(0.0f, maxProjectileSpeed);
        maxProjectileCount = Mathf.Max(1, maxProjectileCount);
        maxPierceCount = Mathf.Max(0, maxPierceCount);
    }
}
