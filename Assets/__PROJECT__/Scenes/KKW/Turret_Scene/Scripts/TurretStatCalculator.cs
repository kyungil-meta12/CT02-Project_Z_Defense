using UnityEngine;

public static class TurretStatCalculator
{
    private const int FALLBACK_GROWTH_END_LEVEL = 100;

    // 터렛 정의의 진화/레벨 상한을 기준으로 지정 레벨의 스탯을 계산한다
    public static TurretRuntimeStat Calculate(TurretDefinitionSO definition, int level)
    {
        if (definition == null)
        {
            return new TurretRuntimeStat();
        }

        int growthEndLevel = ResolveGrowthEndLevel(definition);
        return Calculate(definition.baseStatProfile, definition.statGrowthProfile, level, growthEndLevel);
    }

    // 호환용 기본 성장 종료 레벨을 기준으로 지정 레벨의 스탯을 계산한다
    public static TurretRuntimeStat Calculate(TurretStatProfileSO baseStatProfile, TurretStatGrowthProfileSO growthProfile, int level)
    {
        return Calculate(baseStatProfile, growthProfile, level, FALLBACK_GROWTH_END_LEVEL);
    }

    // 지정 성장 종료 레벨을 기준으로 지정 레벨의 스탯을 계산한다
    public static TurretRuntimeStat Calculate(TurretStatProfileSO baseStatProfile, TurretStatGrowthProfileSO growthProfile, int level, int growthEndLevel)
    {
        TurretRuntimeStat result = new TurretRuntimeStat();

        if (baseStatProfile == null)
        {
            return result;
        }

        level = Mathf.Max(1, level);
        int completedLevels = level - 1;

        result.damage = baseStatProfile.damage;
        result.range = baseStatProfile.range;
        result.fireInterval = baseStatProfile.fireInterval;
        result.projectileSpeed = baseStatProfile.projectileSpeed;
        result.projectileCount = Mathf.Max(1, baseStatProfile.projectileCount);
        result.pierceCount = Mathf.Max(0, baseStatProfile.pierceCount);

        if (growthProfile == null)
        {
            return result;
        }

        result.damage = CalculateLogDamage(result.damage, growthProfile, level, growthEndLevel);

        result.range += growthProfile.rangePerLevel * completedLevels;
        if (growthProfile.maxRange > 0.0f)
        {
            result.range = Mathf.Min(result.range, growthProfile.maxRange);
        }

        result.fireInterval -= growthProfile.fireIntervalReductionPerLevel * completedLevels;
        result.fireInterval = Mathf.Max(growthProfile.minFireInterval, result.fireInterval);

        int projectileSpeedIntervals = GetCompletedIntervals(level, growthProfile.projectileSpeedIntervalLevel);
        result.projectileSpeed += projectileSpeedIntervals * growthProfile.projectileSpeedPerInterval;
        if (growthProfile.maxProjectileSpeed > 0.0f)
        {
            result.projectileSpeed = Mathf.Min(result.projectileSpeed, growthProfile.maxProjectileSpeed);
        }

        int projectileCountIntervals = GetCompletedIntervals(level, growthProfile.projectileCountIntervalLevel);
        result.projectileCount += projectileCountIntervals;
        result.projectileCount = Mathf.Min(result.projectileCount, growthProfile.maxProjectileCount);

        int pierceCountIntervals = GetCompletedIntervals(level, growthProfile.pierceCountIntervalLevel);
        result.pierceCount += pierceCountIntervals;
        result.pierceCount = Mathf.Min(result.pierceCount, growthProfile.maxPierceCount);

        return result;
    }

    // 성장 프로필과 레벨 기준으로 로그 곡선 데미지를 계산한다
    private static float CalculateLogDamage(float baseDamage, TurretStatGrowthProfileSO growthProfile, int level, int growthEndLevel)
    {
        if (growthProfile == null || level <= 1)
        {
            return baseDamage;
        }

        int safeEndLevel = Mathf.Max(2, growthEndLevel);
        float targetDamage = Mathf.Max(0.0f, growthProfile.targetDamageAtMaxLevel);
        if (level >= safeEndLevel)
        {
            return targetDamage;
        }

        float progress = Mathf.Clamp01((level - 1.0f) / (safeEndLevel - 1.0f));
        float curvedProgress = CalculateLogProgress(progress, growthProfile.damageLogCurveStrength);
        return Mathf.Lerp(baseDamage, targetDamage, curvedProgress);
    }

    // 정규화된 진행도를 로그 곡선 진행도로 변환한다
    private static float CalculateLogProgress(float progress, float strength)
    {
        float safeProgress = Mathf.Clamp01(progress);
        if (strength <= 0.0f)
        {
            return safeProgress;
        }

        return Mathf.Log(1.0f + strength * safeProgress) / Mathf.Log(1.0f + strength);
    }

    // 터렛 정의에서 데미지 성장 종료 레벨을 찾는다
    private static int ResolveGrowthEndLevel(TurretDefinitionSO definition)
    {
        int evolutionLevel = definition.evolutionProgressionProfile == null
            ? 0
            : definition.evolutionProgressionProfile.GetNextRequiredEvolutionLevel(1);
        if (evolutionLevel > 1)
        {
            return evolutionLevel;
        }

        if (definition.maxLevel > 1)
        {
            return definition.maxLevel;
        }

        return FALLBACK_GROWTH_END_LEVEL;
    }

    // 완료된 구간 성장 횟수를 계산한다
    private static int GetCompletedIntervals(int level, int intervalLevel)
    {
        if (intervalLevel <= 0)
        {
            return 0;
        }

        return Mathf.Max(0, level / intervalLevel);
    }
}
