using System.Globalization;
using UnityEditor;
using UnityEngine;

// 터렛 웨이브 밸런스 리포트 결과를 화면/CSV 공유 표 모델로 변환한다.
internal sealed class TurretBalanceReportTableBuilder
{
    // 리포트 결과에서 전체 탭 표 모델을 만든다
    public ReportTableModel[] Build(TurretBalanceReportResult report)
    {
        ReportTableModel[] tables =
        {
            new ReportTableModel(),
            new ReportTableModel(),
            new ReportTableModel()
        };

        BuildWaveClearTableCache(report, tables[0]);
        BuildScenarioDetailTableCache(report, tables[1]);
        BuildSourceWarningTableCache(report, tables[2]);
        return tables;
    }

    // 웨이브 클리어 표 캐시를 만든다
    private static void BuildWaveClearTableCache(TurretBalanceReportResult report, ReportTableModel table)
    {
        table.Reset("wave_clear_simulation.csv", $"각 웨이브에서 같은 터렛 한 종류만 쓴다고 가정합니다. 시뮬레이션 예산 코인 = 초기 지갑 Coin {FormatInt(report.InitialWalletCoin)} + 직전 웨이브까지의 누적 웨이브 획득 Coin입니다(해당 웨이브 보상은 아직 받기 전이라 제외). 예상 클리어 초 = 웨이브 총 좀비 HP / 최적 터렛 총 DPS입니다.", "웨이브", "일반 좀비 수", "보스 좀비 수", "전체 좀비 수", "좀비 평균 HP", "웨이브 총 좀비 HP", "웨이브 획득 코인", "누적 웨이브 코인", "시뮬레이션 예산 코인", "최적 터렛", "최적 터렛 수", "최적 터렛 레벨", "최적 터렛 총 DPS", "예상 클리어 초", "비고");
        for (int i = 0; i < report.WaveClearRows.Count; i++)
        {
            WaveClearSimulationRow row = report.WaveClearRows[i];
            table.AddRow(row.WaveLabel, FormatInt(row.NormalSpawnCount), FormatInt(row.BossSpawnCount), FormatInt(row.SpawnCount), FormatFloat(row.AverageZombieHp), FormatFloat(row.TotalWaveHp), FormatFloat(row.AverageCoinPerWave), FormatFloat(row.CumulativeWaveRewardCoin), FormatFloat(row.AvailableBudgetCoin), row.BestTurretName, FormatInt(row.BestInstallCount), row.BestLevelText, FormatFloat(row.BestTotalDps), FormatFloat(row.BestClearSeconds), row.Note);
        }

        TurretBalanceReportTableRenderer.RecalculateColumnWidths(table);
    }

    // 터렛 시나리오 상세 표 캐시를 만든다
    private static void BuildScenarioDetailTableCache(TurretBalanceReportResult report, ReportTableModel table)
    {
        table.Reset("turret_scenario_details.csv", "터렛 상점 엔트리별로 1대 집중/최대 설치/예산 최적 시나리오의 전투력(레벨/DPS/클리어 시간)과 예산 사용 내역(설치비/업그레이드비/잔여/부족액)을 한 행에 보여줍니다.", "웨이브", "터렛 이름", "시나리오", "설치 터렛 수", "터렛 레벨", "설치 터렛 총 레벨", "터렛 총 DPS", "예상 클리어 초", "시뮬레이션 예산 코인", "터렛 설치 비용", "터렛 업그레이드 비용", "잔여 코인", "다음 업그레이드 부족 코인", "비고");
        for (int i = 0; i < report.ScenarioDetailRows.Count; i++)
        {
            TurretScenarioDetailRow row = report.ScenarioDetailRows[i];
            table.AddRow(row.WaveLabel, row.TurretName, row.ScenarioName, FormatInt(row.InstallCount), row.LevelSummary, FormatInt(row.TotalLevel), FormatFloat(row.TotalDps), FormatFloat(row.ClearSeconds), FormatInt(row.BudgetCoin), FormatInt(row.PlacementCost), FormatInt(row.UpgradeCost), FormatInt(row.RemainingCoin), FormatInt(row.NextUpgradeShortage), row.Note);
        }

        TurretBalanceReportTableRenderer.RecalculateColumnWidths(table);
    }

    // 원천 데이터 점검 표 캐시를 만든다
    private static void BuildSourceWarningTableCache(TurretBalanceReportResult report, ReportTableModel table)
    {
        table.Reset("source_data_warnings.csv", "시뮬레이션에 영향을 줄 수 있는 누락 참조, 0 Coin 보상, Coin 외 비용을 표시합니다. 심각도 경고는 계산 결과를 무효화할 수 있는 문제, 정보는 의도된 대체/생략 안내입니다.", "심각도", "데이터 종류", "에셋/프리팹 경로", "문제 또는 비고");
        for (int i = 0; i < report.Warnings.Count; i++)
        {
            ReportWarning row = report.Warnings[i];
            table.AddRow(FormatSeverity(row.Severity), row.SourceType, row.AssetPath, row.Note);
        }

        TurretBalanceReportTableRenderer.RecalculateColumnWidths(table);
    }

    // 경고 심각도를 표기용 문자열로 변환한다
    private static string FormatSeverity(ReportWarningSeverity severity)
    {
        return severity == ReportWarningSeverity.Warning ? "경고" : "정보";
    }

    // 정수 값을 표기용 문자열로 변환한다
    private static string FormatInt(int value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    // 실수 값을 표기용 문자열로 변환한다
    private static string FormatFloat(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
