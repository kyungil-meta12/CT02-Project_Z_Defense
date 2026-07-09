using System.Collections.Generic;
using UnityEngine;

// 웨이브별로 진화 그래프의 모든 터렛 종류 중, 예산으로 가장 높은 총 DPS를 내는 상위 3개를 뽑아 웨이브 클리어 표를 만든다.
internal sealed class TurretWaveClearRankingCalculator
{
    private const int MAX_RANK_COUNT = 3;
    private const int MAX_TOTAL_TURRET_INSTALL_COUNT = 8;

    // 모든 웨이브에 대해 종류별 상세 데이터(터렛 시나리오 상세와 동일한 레벨 체크포인트의 DPS)를 참고해 상위 순위 목록을 만든다
    public void BuildRanks(TurretBalanceReportResult result, List<TurretEvolutionNode> nodes, TurretBalanceDpsSettings dpsSettings)
    {
        for (int i = 0; i < result.WaveRows.Count; i++)
        {
            ItemBalanceRow? itemBalanceRow = i < result.ItemBalanceRows.Count ? result.ItemBalanceRows[i] : null;
            result.WaveClearRows.Add(CreateWaveClearRow(result.WaveRows[i], itemBalanceRow, nodes, result.SpeciesDetailRows, dpsSettings));
        }
    }

    // 웨이브 하나의 예산으로 상위 3개 종류를 뽑아 대표 행을 만든다
    private static WaveClearSimulationRow CreateWaveClearRow(WaveSummaryRow wave, ItemBalanceRow? itemBalanceRow, List<TurretEvolutionNode> nodes, List<TurretSpeciesDetailRow> speciesRows, TurretBalanceDpsSettings dpsSettings)
    {
        Dictionary<RewardCurrencyType, float> budget = GetItemBudgetForWave(wave, itemBalanceRow);
        List<WaveClearRankEntry> candidates = new List<WaveClearRankEntry>(nodes.Count);
        int count = Mathf.Min(nodes.Count, speciesRows.Count);
        for (int i = 0; i < count; i++)
        {
            WaveClearRankEntry? candidate = FindBestEntryForSpecies(nodes[i], speciesRows[i], budget, wave, dpsSettings);
            if (candidate.HasValue)
            {
                candidates.Add(candidate.Value);
            }
        }

        candidates.Sort((left, right) => right.CriticalExpectedTotalDps.CompareTo(left.CriticalExpectedTotalDps));
        List<WaveClearRankEntry> topRanks = candidates.Count > MAX_RANK_COUNT ? candidates.GetRange(0, MAX_RANK_COUNT) : candidates;
        float bestDps = topRanks.Count > 0 ? topRanks[0].CriticalExpectedTotalDps : 0.0f;

        return new WaveClearSimulationRow
        {
            WaveLabel = wave.WaveLabel,
            NormalSpawnCount = wave.NormalSpawnCount,
            BossSpawnCount = wave.BossSpawnCount,
            TotalWaveHp = wave.TotalWaveHp,
            AverageRewardPerWave = wave.AverageRewardPerWave,
            CumulativeReward = wave.CumulativeReward,
            TopRanks = topRanks,
            SpeciesEntries = candidates,
            BestClearSeconds = bestDps <= 0.0f ? 0.0f : wave.TotalWaveHp / bestDps,
            Note = CombineWaveClearNotes(wave, topRanks.Count == 0 ? "예산으로 설치 가능한 터렛이 없습니다." : string.Empty)
        };
    }

    // 종류 하나에서, 레벨 체크포인트 중 총 DPS가 가장 높은 (레벨, 설치 수) 조합을 찾는다
    private static WaveClearRankEntry? FindBestEntryForSpecies(TurretEvolutionNode node, TurretSpeciesDetailRow speciesRow, Dictionary<RewardCurrencyType, float> budget, WaveSummaryRow wave, TurretBalanceDpsSettings dpsSettings)
    {
        WaveClearRankEntry? best = null;
        for (int i = 0; i < speciesRow.LevelSamples.Count; i++)
        {
            TurretLevelCostSample sample = speciesRow.LevelSamples[i];
            if (!sample.LevelAvailable)
            {
                continue;
            }

            Dictionary<RewardCurrencyType, int> perUnitNonRootCosts = BuildPerUnitNonRootCosts(node, sample.Level);
            int installCount = CalculateMaxInstallCount(node.RootShopEntry, perUnitNonRootCosts, budget);
            if (installCount <= 0)
            {
                continue;
            }

            float effectiveDps = TurretSpecialAbilityDpsCalculator.CalculateDps(node.Definition, sample.Level, wave, dpsSettings);
            float criticalExpectedDps = TurretSpecialAbilityDpsCalculator.CalculateCriticalExpectedDps(node.Definition, sample.Level, wave, dpsSettings);
            float totalDps = installCount * effectiveDps;
            float criticalExpectedTotalDps = installCount * criticalExpectedDps;
            if (!best.HasValue || criticalExpectedTotalDps > best.Value.CriticalExpectedTotalDps)
            {
                best = new WaveClearRankEntry
                {
                    TurretName = speciesRow.TurretName,
                    InstallCount = installCount,
                    Level = sample.Level,
                    TotalDps = totalDps,
                    CriticalExpectedTotalDps = criticalExpectedTotalDps
                };
            }
        }

        return best;
    }

    // 루트 상점 엔트리의 계단식 설치비와 단위당 진화/업그레이드 비용으로 예산 안에서 설치 가능한 최대 개수를 계산한다
    private static int CalculateMaxInstallCount(TurretShopEntrySO rootShopEntry, Dictionary<RewardCurrencyType, int> perUnitNonRootCosts, Dictionary<RewardCurrencyType, float> budget)
    {
        if (rootShopEntry == null)
        {
            return 0;
        }

        int installCount = 0;
        Dictionary<RewardCurrencyType, float> remainingBudget = new Dictionary<RewardCurrencyType, float>(budget);
        while (installCount < MAX_TOTAL_TURRET_INSTALL_COUNT)
        {
            Dictionary<RewardCurrencyType, int> unitCost = ExtractCosts(rootShopEntry.GetPlacementCosts(installCount));
            AddCosts(unitCost, perUnitNonRootCosts);
            if (IsEmptyCost(unitCost))
            {
                installCount++;
                continue;
            }

            if (!CanAfford(unitCost, remainingBudget))
            {
                break;
            }

            SubtractCosts(remainingBudget, unitCost);
            installCount++;
        }

        return installCount;
    }

    // 아이템 밸런스 예산을 가져오고 없으면 기존 누적 보상으로 대체한다
    private static Dictionary<RewardCurrencyType, float> GetItemBudgetForWave(WaveSummaryRow wave, ItemBalanceRow? itemBalanceRow)
    {
        if (itemBalanceRow.HasValue && itemBalanceRow.Value.MaxItemAmounts != null)
        {
            return new Dictionary<RewardCurrencyType, float>(itemBalanceRow.Value.MaxItemAmounts);
        }

        Dictionary<RewardCurrencyType, float> source = TurretBalanceReportCurrencyProjector.BuildWaveBudgetSource(wave);
        return TurretBalanceReportCurrencyProjector.ProjectRewards(source, TurretBalanceReportCurrencyProjector.BuildAllCurrencyScope());
    }

    // 루트 설치비를 제외한 단위당 도달/업그레이드 비용을 만든다
    private static Dictionary<RewardCurrencyType, int> BuildPerUnitNonRootCosts(TurretEvolutionNode node, int level)
    {
        Dictionary<RewardCurrencyType, int> costs = TurretEconomySimulationCalculator.CloneCosts(node.CumulativeReachCost);
        SubtractCosts(costs, ExtractCosts(node.RootShopEntry == null ? null : node.RootShopEntry.GetPlacementCosts(0)));
        if (node.Definition != null && node.Definition.upgradeCostProfile != null)
        {
            AddCosts(costs, ExtractCosts(node.Definition.upgradeCostProfile.GetCosts(1, level)));
        }

        RemoveNonPositiveCosts(costs);
        return costs;
    }

    // ResourceCost 배열을 재화별 비용 Dictionary로 변환한다
    private static Dictionary<RewardCurrencyType, int> ExtractCosts(ResourceCost[] costs)
    {
        Dictionary<RewardCurrencyType, int> result = new Dictionary<RewardCurrencyType, int>();
        if (costs == null)
        {
            return result;
        }

        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null || cost.amount <= 0)
            {
                continue;
            }

            result.TryGetValue(cost.currencyType, out int existing);
            result[cost.currencyType] = existing + cost.amount;
        }

        return result;
    }

    // 비용 Dictionary에 다른 비용 Dictionary를 더한다
    private static void AddCosts(Dictionary<RewardCurrencyType, int> target, Dictionary<RewardCurrencyType, int> costs)
    {
        foreach (KeyValuePair<RewardCurrencyType, int> pair in costs)
        {
            target.TryGetValue(pair.Key, out int existing);
            target[pair.Key] = existing + pair.Value;
        }
    }

    // 정수 비용 Dictionary에서 비용을 차감한다
    private static void SubtractCosts(Dictionary<RewardCurrencyType, int> target, Dictionary<RewardCurrencyType, int> costs)
    {
        foreach (KeyValuePair<RewardCurrencyType, int> pair in costs)
        {
            target.TryGetValue(pair.Key, out int existing);
            target[pair.Key] = existing - pair.Value;
        }
    }

    // 실수 예산 Dictionary에서 비용을 차감한다
    private static void SubtractCosts(Dictionary<RewardCurrencyType, float> target, Dictionary<RewardCurrencyType, int> costs)
    {
        foreach (KeyValuePair<RewardCurrencyType, int> pair in costs)
        {
            target.TryGetValue(pair.Key, out float existing);
            target[pair.Key] = existing - pair.Value;
        }
    }

    // 비용 Dictionary의 모든 재화가 예산 이하인지 확인한다
    private static bool CanAfford(Dictionary<RewardCurrencyType, int> costs, Dictionary<RewardCurrencyType, float> budget)
    {
        foreach (KeyValuePair<RewardCurrencyType, int> pair in costs)
        {
            budget.TryGetValue(pair.Key, out float available);
            if (available < pair.Value)
            {
                return false;
            }
        }

        return true;
    }

    // 0 이하 비용을 제거한다
    private static void RemoveNonPositiveCosts(Dictionary<RewardCurrencyType, int> costs)
    {
        List<RewardCurrencyType> removeKeys = new List<RewardCurrencyType>();
        foreach (KeyValuePair<RewardCurrencyType, int> pair in costs)
        {
            if (pair.Value <= 0)
            {
                removeKeys.Add(pair.Key);
            }
        }

        for (int i = 0; i < removeKeys.Count; i++)
        {
            costs.Remove(removeKeys[i]);
        }
    }

    // 비용 Dictionary가 비어 있거나 모두 0 이하인지 확인한다
    private static bool IsEmptyCost(Dictionary<RewardCurrencyType, int> costs)
    {
        foreach (KeyValuePair<RewardCurrencyType, int> pair in costs)
        {
            if (pair.Value > 0)
            {
                return false;
            }
        }

        return true;
    }

    // 웨이브 대표 행의 비고 문자열을 만든다
    private static string CombineWaveClearNotes(WaveSummaryRow wave, string simulationNote)
    {
        wave.AverageRewardPerWave.TryGetValue(RewardCurrencyType.Coin, out float averageCoinPerWave);
        string rewardNote = wave.SpawnCount > 0 && averageCoinPerWave <= 0.0f ? "웨이브 획득 코인이 0입니다. 좀비 보상 프로필을 확인하세요." : string.Empty;
        if (string.IsNullOrWhiteSpace(rewardNote))
        {
            return simulationNote;
        }

        if (string.IsNullOrWhiteSpace(simulationNote))
        {
            return rewardNote;
        }

        return rewardNote + " / " + simulationNote;
    }
}
