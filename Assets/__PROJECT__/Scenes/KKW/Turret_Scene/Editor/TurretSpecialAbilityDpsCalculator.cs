using ProjectZDefense.StatusEffects;
using UnityEngine;

// 터렛 밸런스 리포트에서 속성 터렛의 상태이상 효과를 웨이브 기준 기대 DPS로 환산한다.
internal static class TurretSpecialAbilityDpsCalculator
{
    // 터렛 정의와 웨이브 요약을 기준으로 리포트용 기대 DPS를 계산한다
    public static float CalculateDps(TurretDefinitionSO definition, int level, WaveSummaryRow wave, TurretBalanceDpsSettings settings)
    {
        return CalculateDps(definition, level, wave, settings, false);
    }

    // 터렛 정의와 웨이브 요약을 기준으로 치명타와 강타 기대값이 반영된 리포트용 DPS를 계산한다
    public static float CalculateCriticalExpectedDps(TurretDefinitionSO definition, int level, WaveSummaryRow wave, TurretBalanceDpsSettings settings)
    {
        return CalculateDps(definition, level, wave, settings, true);
    }

    // 터렛 정의와 웨이브 요약을 기준으로 리포트용 DPS를 계산한다
    private static float CalculateDps(TurretDefinitionSO definition, int level, WaveSummaryRow wave, TurretBalanceDpsSettings settings, bool includeCriticalExpectedValue)
    {
        if (definition == null)
        {
            return 0.0f;
        }

        TurretRuntimeStat stat = TurretStatCalculator.Calculate(definition, level);
        float directDps = CalculateDirectDps(stat, settings);
        if (includeCriticalExpectedValue)
        {
            directDps *= CalculateSpecialHitExpectedMultiplier(definition.damagePolishProfile);
        }

        if (definition.ignitionStatusProfile != null)
        {
            return CalculateIgnitionDps(definition.ignitionStatusProfile, definition.statGrowthProfile, level, directDps, wave, settings);
        }

        float totalDps = directDps;
        if (definition.poisonStatusProfile != null)
        {
            totalDps += CalculatePoisonStatusDps(definition.poisonStatusProfile, definition.statGrowthProfile, level, stat.fireInterval, wave, settings);
        }

        if (definition.electroStatusProfile != null)
        {
            totalDps = CalculateElectroChainDps(definition.electroStatusProfile, definition.statGrowthProfile, level, totalDps, stat.fireInterval, wave, settings);
        }

        if (definition.frostStatusProfile != null)
        {
            totalDps += CalculateFrostFreezeDps(definition.frostStatusProfile, level, stat.fireInterval, wave, settings);
        }

        return Mathf.Max(0.0f, totalDps);
    }

    // 데미지 폴리싱 프로필에서 치명타와 강타 기대 피해 배율을 계산한다
    private static float CalculateSpecialHitExpectedMultiplier(TurretDamagePolishProfileSO profile)
    {
        if (profile == null)
        {
            return 1.0f;
        }

        float heavyHitChance = Mathf.Clamp01(profile.HeavyHitChance);
        float heavyHitMultiplier = Mathf.Max(0.0f, profile.HeavyHitMultiplier);
        float criticalChance = Mathf.Clamp01(profile.CriticalChance);
        float criticalMultiplier = Mathf.Max(0.0f, profile.CriticalMultiplier);
        float heavyExpectedBonus = heavyHitChance * (heavyHitMultiplier - 1.0f);
        float criticalExpectedBonus = (1.0f - heavyHitChance) * criticalChance * (criticalMultiplier - 1.0f);
        return Mathf.Max(0.0f, 1.0f + heavyExpectedBonus + criticalExpectedBonus);
    }

    // 직접 피해 DPS에 관통 효율을 반영한다
    private static float CalculateDirectDps(TurretRuntimeStat stat, TurretBalanceDpsSettings settings)
    {
        float dps = TurretEconomySimulationCalculator.CalculateDps(stat);
        int pierceCount = Mathf.Max(0, stat.pierceCount);
        if (pierceCount <= 0)
        {
            return dps;
        }

        float multiplier = 1.0f + pierceCount * Mathf.Max(0.0f, settings.PierceDpsMultiplierPerCount);
        return dps * multiplier;
    }

    // Poison 틱 피해를 평균 대상 HP 기준 DPS로 환산한다
    private static float CalculatePoisonStatusDps(PoisonStatusProfileSO profile, TurretStatGrowthProfileSO growthProfile, int level, float fireInterval, WaveSummaryRow wave, TurretBalanceDpsSettings settings)
    {
        PoisonStatusPayload payload = profile.CreatePayload(level, growthProfile);
        if (!payload.hasPoisonStatus)
        {
            return 0.0f;
        }

        int stackCount = CalculateSustainableStackCount(payload.maxStackCount, payload.duration, fireInterval);
        return CalculateWeightedMaxHpDps(wave, payload.maxHpDamageRatioPerTick, payload.tickInterval, stackCount, payload.bossDamageMultiplier, settings.PoisonExpectedTargetCount);
    }

    // Ignition 연소 피해를 평균 대상 HP 기준 DPS로 환산한다
    private static float CalculateIgnitionDps(IgnitionStatusProfileSO profile, TurretStatGrowthProfileSO growthProfile, int level, float sourceDamagePerSecond, WaveSummaryRow wave, TurretBalanceDpsSettings settings)
    {
        IgnitionStatusPayload payload = profile.CreatePayload(level, sourceDamagePerSecond, growthProfile);
        if (!payload.hasIgnitionStatus)
        {
            return 0.0f;
        }

        int stackCount = CalculateSustainableStackCount(payload.maxStackCount, payload.duration, payload.tickInterval);
        float expectedTargetCount = settings.IgnitionExpectedTargetCount;
        float statusDps = CalculateWeightedMaxHpDps(wave, payload.maxHpDamageRatioPerTick, payload.tickInterval, stackCount, payload.bossDamageMultiplier, expectedTargetCount);
        float fallbackDps = Mathf.Max(0.0f, payload.damagePerSecond) * stackCount * ResolveExpectedTargetCount(wave, expectedTargetCount);
        return Mathf.Max(statusDps, fallbackDps);
    }

    // Electro 체인 라이트닝 피해를 체인 대상 수와 낙차 기준으로 환산한다
    private static float CalculateElectroChainDps(ElectroStatusProfileSO profile, TurretStatGrowthProfileSO growthProfile, int level, float directDps, float fireInterval, WaveSummaryRow wave, TurretBalanceDpsSettings settings)
    {
        ElectroStatusPayload payload = profile.CreatePayload(level, growthProfile);
        if (!payload.hasElectroStatus || directDps <= 0.0f)
        {
            return Mathf.Max(0.0f, directDps);
        }

        int availableTargets = Mathf.FloorToInt(ResolveExpectedTargetCount(wave, settings.ElectroExpectedTargetCount));
        int chainTargetCount = Mathf.Min(Mathf.Max(1, payload.maxChainTargets), Mathf.Max(1, availableTargets));
        float multiplierSum = 0.0f;
        for (int chainIndex = 0; chainIndex < chainTargetCount; chainIndex++)
        {
            multiplierSum += Mathf.Max(0.0f, 1.0f - Mathf.Clamp01(payload.chainDamageFalloffPerJump) * chainIndex);
        }

        float chainDps = directDps * multiplierSum;
        float overloadDps = CalculateElectroOverloadDps(payload, wave, settings, fireInterval);
        return chainDps + overloadDps;
    }

    // Electro 오버로드 체력 비례 피해를 발동 기대율 기준 DPS로 환산한다
    private static float CalculateElectroOverloadDps(ElectroStatusPayload payload, WaveSummaryRow wave, TurretBalanceDpsSettings settings, float fireInterval)
    {
        float triggerExpectation = Mathf.Clamp01(settings.ElectroOverloadTriggerExpectation);
        if (triggerExpectation <= 0.0f)
        {
            return 0.0f;
        }

        int requiredStackCount = Mathf.Max(1, payload.maxShockStackCount);
        float directHitInterval = Mathf.Max(0.01f, fireInterval);
        float overloadCycleSeconds = Mathf.Max(0.01f, directHitInterval * requiredStackCount);
        float normalDamage = ResolveNormalTargetHp(wave) * Mathf.Clamp01(payload.overloadMaxHpDamageRatio);
        float bossDamage = ResolveBossTargetHp(wave) * Mathf.Clamp01(payload.bossOverloadMaxHpDamageRatio);
        float weightedDamage = CalculateWeightedDamage(wave, normalDamage, bossDamage);
        return weightedDamage / overloadCycleSeconds * triggerExpectation * ResolveExpectedTargetCount(wave, settings.ElectroExpectedTargetCount);
    }

    // Frost 빙결 폭발 피해를 빙결 주기 기준 DPS로 환산한다
    private static float CalculateFrostFreezeDps(FrostStatusProfileSO profile, int level, float tickInterval, WaveSummaryRow wave, TurretBalanceDpsSettings settings)
    {
        FrostStatusPayload payload = profile.CreatePayload(level, tickInterval);
        if (payload.maxSlowRatio <= 0.0f || payload.maxSlowRatio < payload.freezeTriggerRatio)
        {
            return 0.0f;
        }

        float normalHp = ResolveNormalTargetHp(wave);
        if (normalHp <= 0.0f)
        {
            return 0.0f;
        }

        float triggerSeconds = Mathf.Max(0.0f, payload.slowBuildUpDuration);
        float cycleSeconds = Mathf.Max(0.01f, triggerSeconds + payload.freezeCooldownPerTarget);
        float expectedTargetCount = ResolveExpectedTargetCount(wave, settings.FrostExpectedTargetCount);
        float primaryDamage = Mathf.Max(0.0f, payload.freezeExplosionDamage) + normalHp * Mathf.Clamp01(payload.freezePrimaryTargetMaxHpDamageRatio);
        float areaDamage = Mathf.Max(0.0f, payload.freezeExplosionDamage) * Mathf.Max(0.0f, expectedTargetCount - 1.0f);
        float normalRatio = wave.SpawnCount <= 0 ? 1.0f : wave.NormalSpawnCount / (float)Mathf.Max(1, wave.SpawnCount);
        return (primaryDamage + areaDamage) / cycleSeconds * normalRatio;
    }

    // 최대체력 비례 틱 피해를 일반/보스 비중에 맞춰 평균 DPS로 환산한다
    private static float CalculateWeightedMaxHpDps(WaveSummaryRow wave, float ratioPerTick, float tickInterval, int stackCount, float bossDamageMultiplier, float expectedTargetCount)
    {
        float safeRatio = Mathf.Clamp01(ratioPerTick);
        float safeTickInterval = Mathf.Max(0.01f, tickInterval);
        int safeStackCount = Mathf.Max(1, stackCount);
        float normalDps = ResolveNormalTargetHp(wave) * safeRatio * safeStackCount / safeTickInterval;
        float bossDps = ResolveBossTargetHp(wave) * safeRatio * safeStackCount * Mathf.Max(0.0f, bossDamageMultiplier) / safeTickInterval;
        if (wave.SpawnCount <= 0)
        {
            return ResolveAverageTargetHp(wave) * safeRatio * safeStackCount / safeTickInterval * ResolveExpectedTargetCount(wave, expectedTargetCount);
        }

        float weightedDps = normalDps * Mathf.Max(0, wave.NormalSpawnCount) + bossDps * Mathf.Max(0, wave.BossSpawnCount);
        return weightedDps / Mathf.Max(1, wave.SpawnCount) * ResolveExpectedTargetCount(wave, expectedTargetCount);
    }

    // 일반/보스 비중을 반영한 평균 피해량을 계산한다
    private static float CalculateWeightedDamage(WaveSummaryRow wave, float normalDamage, float bossDamage)
    {
        if (wave.SpawnCount <= 0)
        {
            return Mathf.Max(0.0f, normalDamage);
        }

        float weightedDamage = Mathf.Max(0.0f, normalDamage) * Mathf.Max(0, wave.NormalSpawnCount) + Mathf.Max(0.0f, bossDamage) * Mathf.Max(0, wave.BossSpawnCount);
        return weightedDamage / Mathf.Max(1, wave.SpawnCount);
    }

    // 설정된 기대 대상 수를 웨이브 스폰 수 안에서 안전하게 제한한다
    private static float ResolveExpectedTargetCount(WaveSummaryRow wave, float expectedTargetCount)
    {
        float safeTargetCount = Mathf.Max(1.0f, expectedTargetCount);
        if (wave.SpawnCount <= 0)
        {
            return safeTargetCount;
        }

        return Mathf.Min(safeTargetCount, Mathf.Max(1, wave.SpawnCount));
    }

    // 지속시간 안에서 유지 가능한 상태이상 중첩 수를 계산한다
    private static int CalculateSustainableStackCount(int maxStackCount, float duration, float applyInterval)
    {
        int safeMaxStackCount = Mathf.Max(1, maxStackCount);
        float safeApplyInterval = Mathf.Max(0.01f, applyInterval);
        if (duration <= 0.0f)
        {
            return 1;
        }

        return Mathf.Clamp(Mathf.FloorToInt(duration / safeApplyInterval) + 1, 1, safeMaxStackCount);
    }

    // 일반 좀비 평균 HP를 반환하고 없으면 전체 평균 HP로 대체한다
    private static float ResolveNormalTargetHp(WaveSummaryRow wave)
    {
        return wave.AverageNormalZombieHp > 0.0f ? wave.AverageNormalZombieHp : ResolveAverageTargetHp(wave);
    }

    // 보스 좀비 평균 HP를 반환하고 없으면 전체 평균 HP로 대체한다
    private static float ResolveBossTargetHp(WaveSummaryRow wave)
    {
        return wave.AverageBossZombieHp > 0.0f ? wave.AverageBossZombieHp : ResolveAverageTargetHp(wave);
    }

    // 웨이브 전체의 평균 대상 HP를 반환한다
    private static float ResolveAverageTargetHp(WaveSummaryRow wave)
    {
        if (wave.AverageZombieHp > 0.0f)
        {
            return wave.AverageZombieHp;
        }

        return wave.SpawnCount <= 0 ? 0.0f : wave.TotalWaveHp / Mathf.Max(1, wave.SpawnCount);
    }
}
