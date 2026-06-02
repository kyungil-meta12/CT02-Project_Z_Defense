using UnityEngine;

public static class TurretStatCalculator
{
    public static TurretRuntimeStat Calculate(TurretStatProfileSO baseStatProfile, TurretStatGrowthProfileSO growthProfile, int level)
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

        float damageMultiplier = Mathf.Pow(1.0f + (growthProfile.damagePercentPerLevel * 0.01f), completedLevels);
        result.damage *= damageMultiplier;

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

    private static int GetCompletedIntervals(int level, int intervalLevel)
    {
        if (intervalLevel <= 0)
        {
            return 0;
        }

        return Mathf.Max(0, level / intervalLevel);
    }
}
