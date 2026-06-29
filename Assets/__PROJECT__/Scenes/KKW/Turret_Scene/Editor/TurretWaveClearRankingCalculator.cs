using System.Collections.Generic;
using UnityEngine;

// 웨이브별로 진화 그래프의 모든 터렛 종류 중, 예산으로 가장 높은 총 DPS를 내는 상위 3개를 뽑아 웨이브 클리어 표를 만든다.
internal sealed class TurretWaveClearRankingCalculator
{
    private const int MAX_RANK_COUNT = 3;
    private const int MAX_SIMULATED_INSTALL_COUNT = 100;

    // 모든 웨이브에 대해 종류별 상세 데이터(터렛 시나리오 상세와 동일한 레벨 체크포인트의 DPS)를 참고해 상위 순위 목록을 만든다
    public void BuildRanks(TurretBalanceReportResult result, List<TurretEvolutionNode> nodes)
    {
        for (int i = 0; i < result.WaveRows.Count; i++)
        {
            result.WaveClearRows.Add(CreateWaveClearRow(result.WaveRows[i], nodes, result.SpeciesDetailRows));
        }
    }

    // 웨이브 하나의 예산으로 상위 3개 종류를 뽑아 대표 행을 만든다
    private static WaveClearSimulationRow CreateWaveClearRow(WaveSummaryRow wave, List<TurretEvolutionNode> nodes, List<TurretSpeciesDetailRow> speciesRows)
    {
        int budget = Mathf.FloorToInt(Mathf.Max(0.0f, wave.AvailableBudgetCoin));
        List<WaveClearRankEntry> candidates = new List<WaveClearRankEntry>(nodes.Count);
        int count = Mathf.Min(nodes.Count, speciesRows.Count);
        for (int i = 0; i < count; i++)
        {
            WaveClearRankEntry? candidate = FindBestEntryForSpecies(nodes[i], speciesRows[i], budget);
            if (candidate.HasValue)
            {
                candidates.Add(candidate.Value);
            }
        }

        candidates.Sort((left, right) => right.TotalDps.CompareTo(left.TotalDps));
        List<WaveClearRankEntry> topRanks = candidates.Count > MAX_RANK_COUNT ? candidates.GetRange(0, MAX_RANK_COUNT) : candidates;
        float bestDps = topRanks.Count > 0 ? topRanks[0].TotalDps : 0.0f;

        return new WaveClearSimulationRow
        {
            WaveLabel = wave.WaveLabel,
            NormalSpawnCount = wave.NormalSpawnCount,
            BossSpawnCount = wave.BossSpawnCount,
            TotalWaveHp = wave.TotalWaveHp,
            AverageCoinPerWave = wave.AverageCoinPerWave,
            CumulativeWaveRewardCoin = wave.CumulativeWaveRewardCoin,
            TopRanks = topRanks,
            BestClearSeconds = bestDps <= 0.0f ? 0.0f : wave.TotalWaveHp / bestDps,
            Note = CombineWaveClearNotes(wave, topRanks.Count == 0 ? "예산으로 설치 가능한 터렛이 없습니다." : string.Empty)
        };
    }

    // 종류 하나에서, 레벨 체크포인트 중 총 DPS가 가장 높은 (레벨, 설치 수) 조합을 찾는다
    private static WaveClearRankEntry? FindBestEntryForSpecies(TurretEvolutionNode node, TurretSpeciesDetailRow speciesRow, int budget)
    {
        WaveClearRankEntry? best = null;
        for (int i = 0; i < speciesRow.LevelSamples.Count; i++)
        {
            TurretLevelCostSample sample = speciesRow.LevelSamples[i];
            if (!sample.LevelAvailable)
            {
                continue;
            }

            int levelUpgradeCoinCost = node.Definition.upgradeCostProfile == null
                ? 0
                : TurretEconomySimulationCalculator.GetCoinCost(node.Definition.upgradeCostProfile.GetCosts(1, sample.Level));
            int perUnitNonRootCoinCost = node.NonRootCoinCost + levelUpgradeCoinCost;
            int installCount = CalculateMaxInstallCount(node.RootShopEntry, perUnitNonRootCoinCost, budget);
            if (installCount <= 0)
            {
                continue;
            }

            float totalDps = installCount * sample.Dps;
            if (!best.HasValue || totalDps > best.Value.TotalDps)
            {
                best = new WaveClearRankEntry
                {
                    TurretName = speciesRow.TurretName,
                    InstallCount = installCount,
                    Level = sample.Level,
                    TotalDps = totalDps
                };
            }
        }

        return best;
    }

    // 루트 상점 엔트리의 계단식 설치비(대수별로 다름) + 단위당 진화/업그레이드 비용(대수와 무관하게 동일)으로 예산 안에서 설치 가능한 최대 개수를 계산한다
    private static int CalculateMaxInstallCount(TurretShopEntrySO rootShopEntry, int perUnitNonRootCoinCost, int budget)
    {
        if (rootShopEntry == null)
        {
            return 0;
        }

        int installCount = 0;
        int remainingBudget = Mathf.Max(0, budget);
        while (installCount < MAX_SIMULATED_INSTALL_COUNT)
        {
            int rootPlacementCoinCost = TurretEconomySimulationCalculator.GetCoinCost(rootShopEntry.GetPlacementCosts(installCount));
            int unitCoinCost = rootPlacementCoinCost + perUnitNonRootCoinCost;
            if (unitCoinCost <= 0)
            {
                installCount++;
                continue;
            }

            if (remainingBudget < unitCoinCost)
            {
                break;
            }

            remainingBudget -= unitCoinCost;
            installCount++;
        }

        return installCount;
    }

    // 웨이브 대표 행의 비고 문자열을 만든다
    private static string CombineWaveClearNotes(WaveSummaryRow wave, string simulationNote)
    {
        string rewardNote = wave.SpawnCount > 0 && wave.AverageCoinPerWave <= 0.0f ? "웨이브 획득 코인이 0입니다. 좀비 보상 프로필을 확인하세요." : string.Empty;
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
