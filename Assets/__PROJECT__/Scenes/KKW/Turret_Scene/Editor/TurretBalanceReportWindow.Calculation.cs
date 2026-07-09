using System.Collections.Generic;

// 입력 스냅샷을 숫자 중심 터렛 웨이브 밸런스 리포트 결과로 조립한다.
internal sealed class TurretBalanceReportCalculator
{
    private readonly TurretWaveRuntimeAnalyzer waveAnalyzer = new TurretWaveRuntimeAnalyzer();
    private readonly TurretSpeciesDetailCalculator speciesDetailCalculator = new TurretSpeciesDetailCalculator();
    private readonly TurretWaveClearRankingCalculator waveClearRankingCalculator = new TurretWaveClearRankingCalculator();

    // 입력 스냅샷으로 리포트 계산 결과를 만든다
    public TurretBalanceReportResult Build(TurretBalanceInputSnapshot snapshot, TurretBalanceDpsSettings dpsSettings)
    {
        TurretBalanceReportResult result = new TurretBalanceReportResult();
        if (snapshot == null)
        {
            ReportWarning.Add(result.Warnings, ReportWarningSeverity.Warning, "Report", "InputSnapshot", "입력 스냅샷이 없어 빈 리포트를 생성했습니다.");
            return result;
        }

        result.InitialWalletCoin = snapshot.InitialWalletCoin;
        result.Warnings.AddRange(snapshot.Warnings);
        waveAnalyzer.BuildWaveRows(snapshot, result.WaveRows, result.Warnings);
        BuildItemBalanceRows(result);

        // 종류별 상세(터렛 시나리오 상세) 데이터를 먼저 만들고, 웨이브 클리어 순위 계산이 그 결과를 그대로 재사용한다.
        List<TurretEvolutionNode> evolutionNodes = speciesDetailCalculator.BuildRows(snapshot, result, dpsSettings);
        waveClearRankingCalculator.BuildRanks(result, evolutionNodes, dpsSettings);
        return result;
    }

    // 웨이브별 누적 보상에서 아이템 조합/분해 관계를 반영한 아이템 밸런스 행을 만든다
    private static void BuildItemBalanceRows(TurretBalanceReportResult result)
    {
        if (result == null)
        {
            return;
        }

        HashSet<RewardCurrencyType> allCurrencyScope = TurretBalanceReportCurrencyProjector.BuildAllCurrencyScope();
        for (int i = 0; i < result.WaveRows.Count; i++)
        {
            WaveSummaryRow wave = result.WaveRows[i];
            Dictionary<RewardCurrencyType, float> source = TurretBalanceReportCurrencyProjector.BuildWaveBudgetSource(wave);
            result.ItemBalanceRows.Add(new ItemBalanceRow
            {
                WaveLabel = wave.WaveLabel,
                MaxItemAmounts = TurretBalanceReportCurrencyProjector.ProjectRewards(source, allCurrencyScope)
            });
        }
    }
}
