using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// 좀비 스폰 프로필 입력을 런타임 스폰 의미에 맞춰 웨이브별 기대값으로 변환한다.
internal sealed class TurretWaveRuntimeAnalyzer
{
    private readonly ZombieRewardExpectationCalculator rewardCalculator = new ZombieRewardExpectationCalculator();

    // 모든 웨이브 스폰 프로필을 개별 웨이브 행으로 분석한다
    public void BuildWaveRows(TurretBalanceInputSnapshot snapshot, List<WaveSummaryRow> waveRows, List<ReportWarning> warnings)
    {
        for (int i = 0; i < snapshot.WaveProfiles.Count; i++)
        {
            WaveProfileInput source = snapshot.WaveProfiles[i];
            AddWaveRows(source, waveRows, warnings);
        }

        waveRows.Sort(CompareWaveRows);
        CalculateCumulativeRewards(snapshot.InitialWalletCoin, waveRows);
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
        float rewardMultiplier = stage.RewardMultiplier;
        int bossSpawnCount = spawnBossAsLastEnemy && spawnCount > 0 ? 1 : 0;
        int normalSpawnCount = Mathf.Max(0, spawnCount - bossSpawnCount);

        WeightedZombieSummary normalSummary = CalculateWeightedZombieSummary(stage.NormalEntries, wave, hpMultiplier, rewardMultiplier, path, stageIndex, "일반", false, warnings);
        WeightedZombieSummary bossSummary = CalculateWeightedZombieSummary(stage.BossEntries, wave, hpMultiplier, rewardMultiplier, path, stageIndex, "보스", true, warnings);
        AddMissingCandidateWarning(path, stageIndex, wave, normalSpawnCount, normalSummary.CandidateCount, "일반", warnings);
        AddMissingCandidateWarning(path, stageIndex, wave, bossSpawnCount, bossSummary.CandidateCount, "보스", warnings);

        float totalHp = normalSummary.AverageHp * normalSpawnCount + bossSummary.AverageHp * bossSpawnCount;
        float averageCoinPerWave = normalSummary.AverageCoinPerKill * normalSpawnCount + bossSummary.AverageCoinPerKill * bossSpawnCount;
        int candidateCount = normalSummary.CandidateCount + bossSummary.CandidateCount;

        return new WaveSummaryRow
        {
            ProfilePath = path,
            WaveLabel = isOpenEndedWave ? $"{wave}+" : wave.ToString(CultureInfo.InvariantCulture),
            MinWave = wave,
            MaxWave = wave,
            SpawnCount = spawnCount,
            NormalSpawnCount = normalSpawnCount,
            BossSpawnCount = bossSpawnCount,
            HpMultiplier = hpMultiplier,
            RewardMultiplier = rewardMultiplier,
            CandidateCount = candidateCount,
            AverageZombieHp = spawnCount <= 0 ? 0.0f : totalHp / spawnCount,
            TotalWaveHp = totalHp,
            AverageCoinPerWave = averageCoinPerWave
        };
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

    // 스폰 후보 가중치를 반영해 평균 HP와 평균 Coin 보상을 계산한다
    private WeightedZombieSummary CalculateWeightedZombieSummary(List<SpawnEntryInput> entries, int wave, float hpMultiplier, float rewardMultiplier, string profilePath, int stageIndex, string groupLabel, bool isBoss, List<ReportWarning> warnings)
    {
        WeightedZombieSummary summary = new WeightedZombieSummary();
        if (entries == null)
        {
            return summary;
        }

        int totalWeight = 0;
        float weightedHp = 0.0f;
        float weightedCoin = 0.0f;
        for (int i = 0; i < entries.Count; i++)
        {
            SpawnEntryInput entry = entries[i];
            if (!IsSpawnEntryAvailable(entry, wave))
            {
                continue;
            }

            int weight = Mathf.Max(0, entry.Weight);
            if (!TryGetPrefabBalanceData(entry, wave, isBoss, rewardMultiplier, out ZombieBalanceData zombieData))
            {
                ReportWarning.Add(warnings, ReportWarningSeverity.Warning, "ZombieWaveSpawnProfileSO", profilePath, $"{stageIndex + 1}번째 스테이지 {groupLabel} 엔트리의 HP/보상 데이터를 읽을 수 없습니다.");
                continue;
            }

            if (zombieData.ExpectedCoinReward <= 0.0f)
            {
                string prefabName = entry.PrefabReference == null ? "None" : entry.PrefabReference.name;
                ReportWarning.Add(warnings, ReportWarningSeverity.Warning, "ZombieWaveSpawnProfileSO", profilePath, $"{stageIndex + 1}번째 스테이지 {groupLabel} 엔트리 {prefabName}의 Coin 보상이 0으로 계산됩니다.");
            }

            totalWeight += weight;
            summary.CandidateCount++;
            weightedHp += zombieData.AverageHp * hpMultiplier * weight;
            weightedCoin += zombieData.ExpectedCoinReward * weight;
        }

        if (totalWeight <= 0)
        {
            return summary;
        }

        summary.AverageHp = weightedHp / totalWeight;
        summary.AverageCoinPerKill = weightedCoin / totalWeight;
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

    // 프리팹에서 리포트용 HP와 기대 보상 데이터를 읽는다
    private bool TryGetPrefabBalanceData(SpawnEntryInput entry, int wave, bool isBoss, float rewardMultiplier, out ZombieBalanceData data)
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
            data.ExpectedCoinReward = rewardCalculator.CalculateExpectedCoin(entry.RewardProfileOverride, normalZombie.spec, wave, false, rewardMultiplier);
            return data.AverageHp > 0.0f;
        }

        BossZombie bossZombie = entry.BossZombie;
        if (bossZombie == null)
        {
            return false;
        }

        data.AverageHp = GetAverageBossZombieHp(bossZombie);
        data.ExpectedCoinReward = rewardCalculator.CalculateExpectedCoin(entry.RewardProfileOverride, bossZombie.spec, wave, isBoss, rewardMultiplier);
        return data.AverageHp > 0.0f;
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

    // 웨이브 기대 보상을 누적 보상으로 변환한다
    private static void CalculateCumulativeRewards(int initialWalletCoin, List<WaveSummaryRow> waveRows)
    {
        float cumulativeWaveRewardCoin = 0.0f;
        for (int i = 0; i < waveRows.Count; i++)
        {
            WaveSummaryRow row = waveRows[i];
            row.InitialWalletCoin = initialWalletCoin;
            // 해당 웨이브는 아직 클리어하지 않아 보상을 받기 전이므로, 직전 웨이브까지의 누적 보상만 예산에 포함한다.
            row.AvailableBudgetCoin = initialWalletCoin + cumulativeWaveRewardCoin;
            cumulativeWaveRewardCoin += Mathf.Max(0.0f, row.AverageCoinPerWave);
            row.CumulativeWaveRewardCoin = cumulativeWaveRewardCoin;
            waveRows[i] = row;
        }
    }

    // 좀비 프리팹에서 읽은 계산용 밸런스 데이터.
    private struct ZombieBalanceData
    {
        public float AverageHp;
        public float ExpectedCoinReward;
    }

    // 스폰 후보 가중치를 반영한 평균 데이터.
    private struct WeightedZombieSummary
    {
        public int CandidateCount;
        public float AverageHp;
        public float AverageCoinPerKill;
    }
}

