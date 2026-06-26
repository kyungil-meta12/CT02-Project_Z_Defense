using System.Globalization;
using UnityEngine;

// 입력 스냅샷을 숫자 중심 터렛 웨이브 밸런스 리포트 결과로 조립한다.
internal sealed class TurretBalanceReportCalculator
{
    private readonly TurretWaveRuntimeAnalyzer waveAnalyzer = new TurretWaveRuntimeAnalyzer();
    private readonly TurretEconomySimulationCalculator economyCalculator = new TurretEconomySimulationCalculator();

    // 입력 스냅샷으로 리포트 계산 결과를 만든다
    public TurretBalanceReportResult Build(TurretBalanceInputSnapshot snapshot)
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
        BuildSimulationRows(snapshot, result);
        return result;
    }

    // 웨이브와 터렛 상점 엔트리를 조합해 시뮬레이션 행을 만든다
    private void BuildSimulationRows(TurretBalanceInputSnapshot snapshot, TurretBalanceReportResult result)
    {
        if (snapshot.ShopEntries.Count == 0)
        {
            ReportWarning.Add(result.Warnings, ReportWarningSeverity.Warning, "TurretShopEntrySO", "InputSnapshot", "시뮬레이션 가능한 TurretShopEntrySO가 없습니다.");
            return;
        }

        for (int i = 0; i < result.WaveRows.Count; i++)
        {
            WaveSummaryRow wave = result.WaveRows[i];
            SimulationResult bestResult = new SimulationResult();
            bool hasBest = false;

            for (int j = 0; j < snapshot.ShopEntries.Count; j++)
            {
                TurretShopEntrySO entry = snapshot.ShopEntries[j];
                TurretEconomySimulationCalculator.TurretScenarioReport scenarioReport = economyCalculator.BuildTurretScenarioRows(wave, entry);
                result.ScenarioDetailRows.AddRange(scenarioReport.ScenarioRows);

                if (!hasBest || TurretEconomySimulationCalculator.IsBetterSimulationResult(scenarioReport.OptimalResult, bestResult))
                {
                    bestResult = scenarioReport.OptimalResult;
                    hasBest = true;
                }
            }

            result.WaveClearRows.Add(CreateWaveClearRow(wave, bestResult, hasBest));
        }
    }

    // 웨이브 대표 행을 최적 결과 기준으로 만든다
    private static WaveClearSimulationRow CreateWaveClearRow(WaveSummaryRow wave, SimulationResult bestResult, bool hasBest)
    {
        return new WaveClearSimulationRow
        {
            WaveLabel = wave.WaveLabel,
            SpawnCount = wave.SpawnCount,
            NormalSpawnCount = wave.NormalSpawnCount,
            BossSpawnCount = wave.BossSpawnCount,
            AverageZombieHp = wave.AverageZombieHp,
            TotalWaveHp = wave.TotalWaveHp,
            InitialWalletCoin = wave.InitialWalletCoin,
            AverageCoinPerWave = wave.AverageCoinPerWave,
            CumulativeWaveRewardCoin = wave.CumulativeWaveRewardCoin,
            AvailableBudgetCoin = wave.AvailableBudgetCoin,
            BestTurretName = hasBest ? bestResult.TurretName : "None",
            BestInstallCount = hasBest ? bestResult.InstallCount : 0,
            BestLevelText = hasBest ? TurretEconomySimulationCalculator.FormatLevelSummary(bestResult) : string.Empty,
            BestTotalDps = hasBest ? bestResult.TotalDps : 0.0f,
            BestClearSeconds = hasBest ? bestResult.ClearSeconds : 0.0f,
            Note = CombineWaveClearNotes(wave, hasBest ? bestResult.Note : "계산 가능한 터렛 결과가 없습니다.")
        };
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
