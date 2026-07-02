using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// 좀비 스폰 프로필 입력을 런타임 스폰 의미에 맞춰 웨이브별 기대값으로 변환한다.
internal sealed class TurretWaveRuntimeAnalyzer
{
    private readonly ZombieRewardExpectationCalculator rewardCalculator = new ZombieRewardExpectationCalculator();
    private IReadOnlyList<ZombieWaveDpsMeasurementProfileSO> zombieDpsMeasurementProfiles;
    private IReadOnlyList<WaveProfileInput> waveProfiles;

    // 모든 웨이브 스폰 프로필을 개별 웨이브 행으로 분석한다
    public void BuildWaveRows(TurretBalanceInputSnapshot snapshot, List<WaveSummaryRow> waveRows, List<ReportWarning> warnings)
    {
        zombieDpsMeasurementProfiles = snapshot.ZombieDpsMeasurementProfiles;
        waveProfiles = snapshot.WaveProfiles;
        for (int i = 0; i < snapshot.WaveProfiles.Count; i++)
        {
            WaveProfileInput source = snapshot.WaveProfiles[i];
            AddWaveRows(source, waveRows, warnings);
        }

        waveRows.Sort(CompareWaveRows);
        CalculateCumulativeRewards(snapshot.InitialWalletCoin, snapshot.WaveClearCoinBonusPercentage, waveRows);
    }

    // 웨이브 행 정렬 순서를 비교한다
    private static int CompareWaveRows(WaveSummaryRow left, WaveSummaryRow right)
    {
        int minWaveCompare = left.MinWave.CompareTo(right.MinWave);
        return minWaveCompare != 0 ? minWaveCompare : left.MaxWave.CompareTo(right.MaxWave);
    }

    // 단일 웨이브 스폰 프로필의 스테이지를 개별 웨이브 행으로 펼쳐 추가한다
    private void AddWaveRows(WaveProfileInput profile, List<WaveSummaryRow> waveRows, List<ReportWarning> warnings)
    {
        if (profile.Stages.Count <= 0)
        {
            return;
        }

        for (int i = 0; i < profile.Stages.Count; i++)
        {
            WaveStageInput stage = profile.Stages[i];
            int minWave = Mathf.Max(1, stage.MinWave);
            int maxWave = stage.MaxWave;
            if (maxWave <= 0)
            {
                waveRows.Add(CreateWaveRow(stage, profile.Path, i, minWave, true, warnings));
                ReportWarning.Add(warnings, ReportWarningSeverity.Info, "ZombieWaveSpawnProfileSO", profile.Path, $"{i + 1}번째 스테이지의 MaxWave가 0 이하라 {minWave} 웨이브 대표 행만 표시합니다.");
                continue;
            }

            int normalizedMaxWave = Mathf.Max(minWave, maxWave);
            for (int wave = minWave; wave <= normalizedMaxWave; wave++)
            {
                waveRows.Add(CreateWaveRow(stage, profile.Path, i, wave, false, warnings));
            }
        }
    }

    // 스폰 스테이지 하나를 런타임 의미의 웨이브 계산 행으로 변환한다
    private WaveSummaryRow CreateWaveRow(WaveStageInput stage, string path, int stageIndex, int wave, bool isOpenEndedWave, List<ReportWarning> warnings)
    {
        int spawnCount = Mathf.Max(0, stage.SpawnCount);
        bool spawnBossAsLastEnemy = stage.SpawnBossAsLastEnemy;
        float hpMultiplier = stage.HpMultiplier;
        float attackDamageMultiplier = stage.AttackDamageMultiplier;
        float moveAttackSpeedMultiplier = stage.MoveAttackSpeedMultiplier;
        float rewardMultiplier = stage.RewardMultiplier;
        int bossSpawnCount = spawnBossAsLastEnemy && spawnCount > 0 ? 1 : 0;
        int normalSpawnCount = Mathf.Max(0, spawnCount - bossSpawnCount);

        WeightedZombieSummary normalSummary = CalculateWeightedZombieSummary(stage.NormalEntries, wave, hpMultiplier, attackDamageMultiplier, moveAttackSpeedMultiplier, rewardMultiplier, path, stageIndex, "일반", false, warnings);
        WeightedZombieSummary bossSummary = CalculateWeightedZombieSummary(stage.BossEntries, wave, hpMultiplier, attackDamageMultiplier, moveAttackSpeedMultiplier, rewardMultiplier, path, stageIndex, "보스", true, warnings);
        AddMissingCandidateWarning(path, stageIndex, wave, normalSpawnCount, normalSummary.CandidateCount, "일반", warnings);
        AddMissingCandidateWarning(path, stageIndex, wave, bossSpawnCount, bossSummary.CandidateCount, "보스", warnings);

        float totalHp = normalSummary.AverageHp * normalSpawnCount + bossSummary.AverageHp * bossSpawnCount;
        float averageNormalZombieDps = normalSummary.AverageDps;
        string dpsDataNote = BuildDpsDataNote(normalSpawnCount, bossSpawnCount, normalSummary, bossSummary);
        Dictionary<RewardCurrencyType, float> averageRewardPerWave = new Dictionary<RewardCurrencyType, float>();
        AddScaledRewards(averageRewardPerWave, normalSummary.AverageRewardPerKill, normalSpawnCount);
        AddScaledRewards(averageRewardPerWave, bossSummary.AverageRewardPerKill, bossSpawnCount);
        int candidateCount = normalSummary.CandidateCount + bossSummary.CandidateCount;

        return new WaveSummaryRow
        {
            ProfilePath = path,
            WaveLabel = isOpenEndedWave ? $"{wave}+" : wave.ToString(CultureInfo.InvariantCulture),
            MinWave = wave,
            MaxWave = wave,
            SpawnInterval = stage.SpawnInterval,
            SpawnCount = spawnCount,
            NormalSpawnCount = normalSpawnCount,
            BossSpawnCount = bossSpawnCount,
            HpMultiplier = hpMultiplier,
            RewardMultiplier = rewardMultiplier,
            CandidateCount = candidateCount,
            AverageZombieHp = spawnCount <= 0 ? 0.0f : totalHp / spawnCount,
            AverageNormalZombieHp = normalSummary.AverageHp,
            AverageBossZombieHp = bossSummary.AverageHp,
            TotalWaveHp = totalHp,
            AverageRewardPerWave = averageRewardPerWave,
            AverageNormalZombieDps = averageNormalZombieDps,
            DpsDataNote = dpsDataNote
        };
    }

    // 스폰 그룹별 DPS 데이터 누락 비고를 만든다
    private static string BuildDpsDataNote(int normalSpawnCount, int bossSpawnCount, WeightedZombieSummary normalSummary, WeightedZombieSummary bossSummary)
    {
        bool missingNormalDps = normalSpawnCount > 0 && normalSummary.CandidateCount > 0 && normalSummary.HasMissingDpsData;
        bool missingBossDps = bossSpawnCount > 0 && bossSummary.CandidateCount > 0 && bossSummary.HasMissingDpsData;
        if (missingNormalDps && missingBossDps)
        {
            return "일반/보스 좀비 DPS 데이터 없음";
        }

        if (missingNormalDps)
        {
            return "일반 좀비 DPS 데이터 없음";
        }

        if (missingBossDps)
        {
            return "보스 좀비 DPS 데이터 없음";
        }

        return string.Empty;
    }

    // 스폰 수가 있는데 사용할 수 있는 후보가 없으면 원천 데이터 경고를 추가한다
    private static void AddMissingCandidateWarning(string profilePath, int stageIndex, int wave, int spawnCount, int candidateCount, string groupLabel, List<ReportWarning> warnings)
    {
        if (spawnCount <= 0 || candidateCount > 0)
        {
            return;
        }

        ReportWarning.Add(warnings, ReportWarningSeverity.Warning, "ZombieWaveSpawnProfileSO", profilePath, $"{stageIndex + 1}번째 스테이지 {wave}웨이브 {groupLabel} 좀비 스폰 수가 있지만 사용 가능한 후보가 없습니다.");
    }

    // 스폰 후보 가중치를 반영해 평균 HP와 재화별 평균 보상을 계산한다
    private WeightedZombieSummary CalculateWeightedZombieSummary(List<SpawnEntryInput> entries, int wave, float hpMultiplier, float attackDamageMultiplier, float moveAttackSpeedMultiplier, float rewardMultiplier, string profilePath, int stageIndex, string groupLabel, bool isBoss, List<ReportWarning> warnings)
    {
        WeightedZombieSummary summary = new WeightedZombieSummary
        {
            AverageRewardPerKill = new Dictionary<RewardCurrencyType, float>()
        };

        if (entries == null)
        {
            return summary;
        }

        int totalWeight = 0;
        float weightedHp = 0.0f;
        float weightedDps = 0.0f;
        Dictionary<RewardCurrencyType, float> weightedRewards = new Dictionary<RewardCurrencyType, float>();
        for (int i = 0; i < entries.Count; i++)
        {
            SpawnEntryInput entry = entries[i];
            if (!IsSpawnEntryAvailable(entry, wave))
            {
                continue;
            }

            int weight = Mathf.Max(0, entry.Weight);
            if (!TryGetPrefabBalanceData(entry, wave, isBoss, attackDamageMultiplier, moveAttackSpeedMultiplier, rewardMultiplier, out ZombieBalanceData zombieData))
            {
                ReportWarning.Add(warnings, ReportWarningSeverity.Warning, "ZombieWaveSpawnProfileSO", profilePath, $"{stageIndex + 1}번째 스테이지 {groupLabel} 엔트리의 HP/보상 데이터를 읽을 수 없습니다.");
                continue;
            }

            zombieData.ExpectedRewards.TryGetValue(RewardCurrencyType.Coin, out float expectedCoinReward);
            if (expectedCoinReward <= 0.0f)
            {
                string prefabName = entry.PrefabReference == null ? "None" : entry.PrefabReference.name;
                ReportWarning.Add(warnings, ReportWarningSeverity.Warning, "ZombieWaveSpawnProfileSO", profilePath, $"{stageIndex + 1}번째 스테이지 {groupLabel} 엔트리 {prefabName}의 Coin 보상이 0으로 계산됩니다.");
            }

            totalWeight += weight;
            summary.CandidateCount++;
            summary.HasMissingDpsData |= zombieData.HasMissingDpsData;
            weightedHp += zombieData.AverageHp * hpMultiplier * weight;
            weightedDps += zombieData.AverageDps * weight;
            AddScaledRewards(weightedRewards, zombieData.ExpectedRewards, weight);
        }

        if (totalWeight <= 0)
        {
            return summary;
        }

        summary.AverageHp = weightedHp / totalWeight;
        summary.AverageDps = weightedDps / totalWeight;
        AddScaledRewards(summary.AverageRewardPerKill, weightedRewards, 1.0f / totalWeight);
        return summary;
    }

    // 스폰 후보가 현재 웨이브에서 런타임 선택 대상인지 확인한다
    private static bool IsSpawnEntryAvailable(SpawnEntryInput entry, int wave)
    {
        int weight = Mathf.Max(0, entry.Weight);
        if (weight <= 0 || entry.PrefabReference == null)
        {
            return false;
        }

        int safeWave = Mathf.Max(1, wave);
        return safeWave >= entry.MinWave && (entry.MaxWave <= 0 || safeWave <= entry.MaxWave);
    }

    // 프리팹에서 리포트용 HP와 재화별 기대 보상 데이터를 읽는다
    private bool TryGetPrefabBalanceData(SpawnEntryInput entry, int wave, bool isBoss, float attackDamageMultiplier, float moveAttackSpeedMultiplier, float rewardMultiplier, out ZombieBalanceData data)
    {
        data = new ZombieBalanceData();
        if (entry.Prefab == null)
        {
            return false;
        }

        NormalZombie normalZombie = entry.NormalZombie;
        if (normalZombie != null)
        {
            data.AverageHp = GetAverageNormalZombieHp(normalZombie);
            if (TryResolveNormalZombieDps(wave, attackDamageMultiplier, moveAttackSpeedMultiplier, out float measuredDps))
            {
                data.AverageDps = measuredDps;
            }
            else
            {
                data.HasMissingDpsData = true;
            }

            data.ExpectedRewards = rewardCalculator.CalculateExpectedRewards(entry.RewardProfileOverride, normalZombie.spec, wave, false, rewardMultiplier);
            return data.AverageHp > 0.0f;
        }

        BossZombie bossZombie = entry.BossZombie;
        if (bossZombie == null)
        {
            return false;
        }

        data.AverageHp = GetAverageBossZombieHp(bossZombie);
        if (TryResolveBossZombieDps(wave, entry.BossType, attackDamageMultiplier, moveAttackSpeedMultiplier, out float bossDps))
        {
            data.AverageDps = bossDps;
        }
        else
        {
            data.HasMissingDpsData = true;
        }

        data.ExpectedRewards = rewardCalculator.CalculateExpectedRewards(entry.RewardProfileOverride, bossZombie.spec, wave, isBoss, rewardMultiplier);
        return data.AverageHp > 0.0f;
    }

    // 현재 웨이브의 일반 좀비 DPS를 실측값 또는 정규화 실측값으로 찾는다
    private bool TryResolveNormalZombieDps(int wave, float attackDamageMultiplier, float moveAttackSpeedMultiplier, out float dps)
    {
        if (TryGetMeasuredNormalZombieDps(wave, out dps))
        {
            return true;
        }

        return TryGetNormalizedMeasuredNormalZombieDps(attackDamageMultiplier, moveAttackSpeedMultiplier, out dps);
    }

    // 현재 웨이브의 보스 좀비 DPS를 실측값 또는 정규화 실측값으로 찾는다
    private bool TryResolveBossZombieDps(int wave, BossZombieType bossType, float attackDamageMultiplier, float moveAttackSpeedMultiplier, out float dps)
    {
        if (TryGetMeasuredBossZombieDps(wave, bossType, out dps))
        {
            return true;
        }

        return TryGetNormalizedMeasuredBossZombieDps(bossType, attackDamageMultiplier, moveAttackSpeedMultiplier, out dps);
    }

    // 저장된 웨이브별 일반 좀비 DPS 측정값을 찾는다
    private bool TryGetMeasuredNormalZombieDps(int wave, out float dps)
    {
        if (zombieDpsMeasurementProfiles == null)
        {
            dps = 0.0f;
            return false;
        }

        for (int i = 0; i < zombieDpsMeasurementProfiles.Count; i++)
        {
            ZombieWaveDpsMeasurementProfileSO profile = zombieDpsMeasurementProfiles[i];
            if (profile != null && profile.TryGetDps(wave, ZombieRewardTypeFilter.NormalOnly, out dps))
            {
                return true;
            }
        }

        dps = 0.0f;
        return false;
    }

    // 저장된 웨이브별 보스 좀비 DPS 측정값을 찾는다
    private bool TryGetMeasuredBossZombieDps(int wave, BossZombieType bossType, out float dps)
    {
        if (zombieDpsMeasurementProfiles == null)
        {
            dps = 0.0f;
            return false;
        }

        for (int i = 0; i < zombieDpsMeasurementProfiles.Count; i++)
        {
            ZombieWaveDpsMeasurementProfileSO profile = zombieDpsMeasurementProfiles[i];
            if (profile != null && profile.TryGetBossDps(wave, bossType, out dps))
            {
                return true;
            }
        }

        dps = 0.0f;
        return false;
    }

    // 다른 웨이브의 일반 좀비 실측값을 현재 웨이브 배율로 정규화한다
    private bool TryGetNormalizedMeasuredNormalZombieDps(float targetAttackDamageMultiplier, float targetMoveAttackSpeedMultiplier, out float dps)
    {
        dps = 0.0f;
        if (zombieDpsMeasurementProfiles == null)
        {
            return false;
        }

        for (int i = 0; i < zombieDpsMeasurementProfiles.Count; i++)
        {
            ZombieWaveDpsMeasurementProfileSO profile = zombieDpsMeasurementProfiles[i];
            if (profile == null)
            {
                continue;
            }

            IReadOnlyList<WaveZombieDpsSample> samples = profile.Samples;
            for (int j = 0; j < samples.Count; j++)
            {
                WaveZombieDpsSample sample = samples[j];
                if (sample == null || !sample.TryGetDps(ZombieRewardTypeFilter.NormalOnly, out float measuredDps))
                {
                    continue;
                }

                if (TryNormalizeMeasuredDps(sample.Wave, measuredDps, targetAttackDamageMultiplier, targetMoveAttackSpeedMultiplier, out dps))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // 다른 웨이브의 보스 타입별 실측값을 현재 웨이브 배율로 정규화한다
    private bool TryGetNormalizedMeasuredBossZombieDps(BossZombieType bossType, float targetAttackDamageMultiplier, float targetMoveAttackSpeedMultiplier, out float dps)
    {
        dps = 0.0f;
        if (zombieDpsMeasurementProfiles == null)
        {
            return false;
        }

        for (int i = 0; i < zombieDpsMeasurementProfiles.Count; i++)
        {
            ZombieWaveDpsMeasurementProfileSO profile = zombieDpsMeasurementProfiles[i];
            if (profile == null)
            {
                continue;
            }

            IReadOnlyList<WaveZombieDpsSample> samples = profile.Samples;
            for (int j = 0; j < samples.Count; j++)
            {
                WaveZombieDpsSample sample = samples[j];
                if (sample == null || !sample.TryGetBossDps(bossType, out float measuredDps))
                {
                    continue;
                }

                if (TryNormalizeMeasuredDps(sample.Wave, measuredDps, targetAttackDamageMultiplier, targetMoveAttackSpeedMultiplier, out dps))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // 실측 웨이브 배율을 제거한 뒤 대상 웨이브 배율을 적용한다
    private bool TryNormalizeMeasuredDps(int measuredWave, float measuredDps, float targetAttackDamageMultiplier, float targetMoveAttackSpeedMultiplier, out float dps)
    {
        dps = 0.0f;
        if (measuredDps <= 0.0f || !TryGetDpsMultipliersForWave(measuredWave, out float measuredAttackDamageMultiplier, out float measuredMoveAttackSpeedMultiplier))
        {
            return false;
        }

        float measuredFactor = SanitizeDpsMultiplier(measuredAttackDamageMultiplier) * SanitizeDpsMultiplier(measuredMoveAttackSpeedMultiplier);
        float targetFactor = SanitizeDpsMultiplier(targetAttackDamageMultiplier) * SanitizeDpsMultiplier(targetMoveAttackSpeedMultiplier);
        dps = measuredDps / measuredFactor * targetFactor;
        return dps > 0.0f;
    }

    // 지정 웨이브가 속한 스테이지의 DPS 관련 배율을 찾는다
    private bool TryGetDpsMultipliersForWave(int wave, out float attackDamageMultiplier, out float moveAttackSpeedMultiplier)
    {
        attackDamageMultiplier = 1.0f;
        moveAttackSpeedMultiplier = 1.0f;
        if (waveProfiles == null)
        {
            return false;
        }

        int safeWave = Mathf.Max(1, wave);
        for (int i = 0; i < waveProfiles.Count; i++)
        {
            WaveProfileInput profile = waveProfiles[i];
            if (profile == null)
            {
                continue;
            }

            for (int j = 0; j < profile.Stages.Count; j++)
            {
                WaveStageInput stage = profile.Stages[j];
                int minWave = Mathf.Max(1, stage.MinWave);
                int maxWave = stage.MaxWave;
                if (safeWave < minWave || (maxWave > 0 && safeWave > maxWave))
                {
                    continue;
                }

                attackDamageMultiplier = stage.AttackDamageMultiplier;
                moveAttackSpeedMultiplier = stage.MoveAttackSpeedMultiplier;
                return true;
            }
        }

        return false;
    }

    // DPS 배율에 사용할 수 없는 값을 안전한 기본값으로 바꾼다
    private static float SanitizeDpsMultiplier(float multiplier)
    {
        return multiplier > 0.0f ? multiplier : 1.0f;
    }

    // 일반 좀비 스펙의 평균 HP를 계산한다
    private static float GetAverageNormalZombieHp(NormalZombie zombie)
    {
        if (zombie == null || zombie.spec == null)
        {
            return 0.0f;
        }

        float minMultiplier = zombie.spec.MinHp > 0.0f ? zombie.spec.MinHp : 1.0f;
        float maxMultiplier = zombie.spec.MaxHp > 0.0f ? zombie.spec.MaxHp : minMultiplier;
        return Mathf.Max(0.0f, zombie.spec.Hp * ((minMultiplier + maxMultiplier) * 0.5f));
    }

    // 보스 좀비 스펙의 평균 HP를 계산한다
    private static float GetAverageBossZombieHp(BossZombie zombie)
    {
        if (zombie == null || zombie.spec == null)
        {
            return 0.0f;
        }

        float minMultiplier = zombie.spec.MinHp > 0.0f ? zombie.spec.MinHp : 1.0f;
        float maxMultiplier = zombie.spec.MaxHp > 0.0f ? zombie.spec.MaxHp : minMultiplier;
        return Mathf.Max(0.0f, zombie.spec.Hp * ((minMultiplier + maxMultiplier) * 0.5f));
    }

    // 웨이브 기대 보상을 웨이브 클리어 보너스(Coin)까지 포함한 누적 보상으로 변환한다
    private static void CalculateCumulativeRewards(int initialWalletCoin, int waveClearCoinBonusPercentage, List<WaveSummaryRow> waveRows)
    {
        Dictionary<RewardCurrencyType, float> cumulativeReward = new Dictionary<RewardCurrencyType, float>
        {
            [RewardCurrencyType.Coin] = initialWalletCoin
        };

        for (int i = 0; i < waveRows.Count; i++)
        {
            WaveSummaryRow row = waveRows[i];
            row.InitialWalletCoin = initialWalletCoin;

            // 해당 웨이브는 아직 클리어하지 않아 보상을 받기 전이므로, 직전 웨이브까지의 누적 보상만 예산에 포함한다.
            cumulativeReward.TryGetValue(RewardCurrencyType.Coin, out float budgetCoin);
            row.AvailableBudgetCoin = budgetCoin;

            AddScaledRewards(cumulativeReward, row.AverageRewardPerWave, 1.0f);

            // 웨이브 클리어 보너스: 이 웨이브에서 모은 Coin 기대값의 일정 퍼센트를 추가로 지급한다(GameManager.AddCoinBouns와 동일한 규칙).
            row.AverageRewardPerWave.TryGetValue(RewardCurrencyType.Coin, out float waveCoinReward);
            float bonusCoin = waveCoinReward * waveClearCoinBonusPercentage * 0.01f;
            if (bonusCoin > 0.0f)
            {
                cumulativeReward.TryGetValue(RewardCurrencyType.Coin, out float existingCoin);
                cumulativeReward[RewardCurrencyType.Coin] = existingCoin + bonusCoin;
            }

            // 초기 지갑과 웨이브 클리어 보너스까지 포함한 총 누적 보유 재화. 다음 웨이브의 시뮬레이션 예산(AvailableBudgetCoin)과 Coin 값이 같다.
            row.CumulativeReward = new Dictionary<RewardCurrencyType, float>(cumulativeReward);
            waveRows[i] = row;
        }
    }

    // 재화별 보상 표에 다른 보상 표를 배율만큼 곱해 더한다
    private static void AddScaledRewards(Dictionary<RewardCurrencyType, float> target, Dictionary<RewardCurrencyType, float> source, float scale)
    {
        if (source == null || scale == 0.0f)
        {
            return;
        }

        foreach (KeyValuePair<RewardCurrencyType, float> pair in source)
        {
            target.TryGetValue(pair.Key, out float existing);
            target[pair.Key] = existing + pair.Value * scale;
        }
    }

    // 좀비 프리팹에서 읽은 계산용 밸런스 데이터.
    private struct ZombieBalanceData
    {
        public float AverageHp;
        public float AverageDps;
        public bool HasMissingDpsData;
        public Dictionary<RewardCurrencyType, float> ExpectedRewards;
    }

    // 스폰 후보 가중치를 반영한 평균 데이터.
    private struct WeightedZombieSummary
    {
        public int CandidateCount;
        public float AverageHp;
        public float AverageDps;
        public bool HasMissingDpsData;
        public Dictionary<RewardCurrencyType, float> AverageRewardPerKill;
    }
}
