using System.Collections.Generic;
using UnityEngine;

// 터렛 종류별 단일 설치 진화/레벨 진행 표를 계산한다.
internal sealed class TurretSpeciesDetailCalculator
{
    private readonly TurretEvolutionGraphBuilder graphBuilder = new TurretEvolutionGraphBuilder();

    // 입력 스냅샷과 웨이브 행으로 터렛 종류별 상세 행과 기준 레벨 목록을 만들고, 사용한 진화 그래프 노드 목록을 반환한다
    public List<TurretEvolutionNode> BuildRows(TurretBalanceInputSnapshot snapshot, TurretBalanceReportResult result, TurretBalanceDpsSettings dpsSettings)
    {
        List<TurretEvolutionNode> nodes = graphBuilder.Build(snapshot.ShopEntries, result.Warnings);
        if (nodes.Count == 0)
        {
            ReportWarning.Add(result.Warnings, ReportWarningSeverity.Warning, "TurretEvolutionGraph", "InputSnapshot", "진화 그래프를 만들 수 있는 터렛이 없습니다.");
            return nodes;
        }

        BuildReferenceLevels(nodes, result.ScenarioReferenceLevels);

        for (int i = 0; i < nodes.Count; i++)
        {
            result.SpeciesDetailRows.Add(CreateRow(nodes[i], result.WaveRows, result.ScenarioReferenceLevels, dpsSettings));
        }

        return nodes;
    }

    // 그래프 전체에서 가장 높은 레벨 상한까지 모든 기준 레벨 목록을 만든다
    private static void BuildReferenceLevels(List<TurretEvolutionNode> nodes, List<int> referenceLevels)
    {
        referenceLevels.Clear();
        int maxCap = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            maxCap = Mathf.Max(maxCap, TurretEconomySimulationCalculator.ResolveMaxLevel(nodes[i].Definition));
        }

        if (maxCap <= 0)
        {
            return;
        }

        for (int level = 1; level <= maxCap; level++)
        {
            referenceLevels.Add(level);
        }
    }

    // 노드 하나가 다음 단계로 진화하는 시점/비용과 기준 레벨별 누적 비용·DPS·도달 웨이브를 계산한다
    private static TurretSpeciesDetailRow CreateRow(TurretEvolutionNode node, List<WaveSummaryRow> waveRowsAscending, List<int> referenceLevels, TurretBalanceDpsSettings dpsSettings)
    {
        TurretSpeciesDetailRow row = new TurretSpeciesDetailRow
        {
            TurretName = node.Definition == null ? "None" : node.Definition.displayName,
            Tier = node.Tier,
            HasNextEvolution = !node.IsTerminal,
            LevelSamples = new List<TurretLevelCostSample>(referenceLevels.Count)
        };

        if (!node.IsTerminal)
        {
            Dictionary<RewardCurrencyType, int> nextEvolutionCost = TurretEconomySimulationCalculator.CloneCosts(node.CumulativeReachCost);
            TurretEconomySimulationCalculator.AddCosts(nextEvolutionCost, node.UpgradeCostToRequiredLevel);
            row.NextEvolutionCumulativeCost = nextEvolutionCost;
            row.NextEvolutionWave = FindFirstAffordableWave(waveRowsAscending, TurretEconomySimulationCalculator.GetCoinAmount(nextEvolutionCost));
            row.NextEvolutionReached = row.NextEvolutionWave > 0;
        }

        int cap = TurretEconomySimulationCalculator.ResolveMaxLevel(node.Definition);
        for (int i = 0; i < referenceLevels.Count; i++)
        {
            row.LevelSamples.Add(CalculateLevelSample(node, referenceLevels[i], cap, waveRowsAscending, dpsSettings));
        }

        return row;
    }

    // 누적 예산이 지정 Coin 비용을 처음 넘는 웨이브를 찾는다
    private static int FindFirstAffordableWave(List<WaveSummaryRow> waveRowsAscending, int requiredCoin)
    {
        for (int i = 0; i < waveRowsAscending.Count; i++)
        {
            if (waveRowsAscending[i].AvailableBudgetCoin >= requiredCoin)
            {
                return waveRowsAscending[i].MinWave;
            }
        }

        return 0;
    }

    // 지정 레벨까지 단일 설치로 올리는 데 드는 재화별 누적 비용·DPS와 Coin 기준 도달 웨이브를 계산한다
    private static TurretLevelCostSample CalculateLevelSample(TurretEvolutionNode node, int level, int cap, List<WaveSummaryRow> waveRowsAscending, TurretBalanceDpsSettings dpsSettings)
    {
        TurretLevelCostSample sample = new TurretLevelCostSample { Level = level };
        if (level > cap)
        {
            return sample;
        }

        Dictionary<RewardCurrencyType, int> cumulativeCost = TurretEconomySimulationCalculator.CloneCosts(node.CumulativeReachCost);
        if (node.Definition.upgradeCostProfile != null)
        {
            TurretEconomySimulationCalculator.AddCosts(cumulativeCost, node.Definition.upgradeCostProfile.GetCosts(1, level));
        }

        sample.LevelAvailable = true;
        sample.CumulativeCost = cumulativeCost;
        sample.Wave = FindFirstAffordableWave(waveRowsAscending, TurretEconomySimulationCalculator.GetCoinAmount(cumulativeCost));
        sample.WaveReached = sample.Wave > 0;
        sample.Dps = TurretSpecialAbilityDpsCalculator.CalculateDps(node.Definition, level, FindRepresentativeWaveRow(waveRowsAscending, sample.Wave), dpsSettings);
        return sample;
    }

    // 지정 웨이브 번호에 해당하는 계산 행을 찾고 없으면 마지막 웨이브를 대표값으로 사용한다
    private static WaveSummaryRow FindRepresentativeWaveRow(List<WaveSummaryRow> waveRowsAscending, int wave)
    {
        if (waveRowsAscending == null || waveRowsAscending.Count == 0)
        {
            return default;
        }

        if (wave > 0)
        {
            for (int i = 0; i < waveRowsAscending.Count; i++)
            {
                if (waveRowsAscending[i].MinWave == wave)
                {
                    return waveRowsAscending[i];
                }
            }
        }

        return waveRowsAscending[waveRowsAscending.Count - 1];
    }
}
