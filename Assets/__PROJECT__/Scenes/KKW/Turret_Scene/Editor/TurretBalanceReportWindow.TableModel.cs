using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
        table.Reset("wave_clear_simulation.csv", $"각 웨이브에서, 터렛 시나리오 상세의 종류별 레벨 체크포인트 데이터(누적 재화·DPS)를 참고해 그 웨이브 시작 시점의 Coin 예산(직전 웨이브까지의 총 누적 재화 중 Coin, 첫 웨이브는 초기 지갑 Coin {FormatInt(report.InitialWalletCoin)})으로 설치 가능한 총 DPS가 가장 높은 1~3순위 종류를 보여줍니다. 설치 수는 현재 터렛 슬롯 상한인 최대 8대까지 계산합니다. 웨이브 획득 재화·총 누적 재화는 좀비 드랍 전체(Coin 외 재화 포함)와 웨이브 클리어 Coin 보너스까지 반영합니다. 재화 줄은 실제로 등장하는 재화 종류 수만큼 늘어납니다. 예상 클리어 초 = 웨이브 총 좀비 HP / 1순위 총 DPS입니다.", "웨이브", "일반 좀비 수", "보스 좀비 수", "웨이브 총 좀비 HP", "웨이브 획득 재화", "총 누적 재화", "1순위 터렛", "2순위 터렛", "3순위 터렛", "예상 클리어 초", "비고");
        for (int i = 0; i < report.WaveClearRows.Count; i++)
        {
            WaveClearSimulationRow row = report.WaveClearRows[i];
            table.AddRow(row.WaveLabel, FormatInt(row.NormalSpawnCount), FormatInt(row.BossSpawnCount), FormatFloat(row.TotalWaveHp), FormatRewardBreakdown(row.AverageRewardPerWave), FormatRewardBreakdown(row.CumulativeReward), FormatRankCell(row.TopRanks, 0), FormatRankCell(row.TopRanks, 1), FormatRankCell(row.TopRanks, 2), FormatFloat(row.BestClearSeconds), row.Note);
        }

        TurretBalanceReportTableRenderer.RecalculateColumnWidths(table);
    }

    // 순위 목록에서 지정 순위의 셀 텍스트를 만든다
    private static string FormatRankCell(List<WaveClearRankEntry> topRanks, int rankIndex)
    {
        if (topRanks == null || rankIndex >= topRanks.Count)
        {
            return "해당없음";
        }

        WaveClearRankEntry entry = topRanks[rankIndex];
        return $"{entry.TurretName}\n{FormatInt(entry.InstallCount)}대 Lv{FormatInt(entry.Level)}\n{FormatFloat(entry.TotalDps)} DPS";
    }

    // 터렛 시나리오 상세 표 캐시를 만든다
    private static void BuildScenarioDetailTableCache(TurretBalanceReportResult report, ReportTableModel table)
    {
        table.Reset("turret_species_detail.csv", "진화 그래프의 터렛 종류(노드)별로, 단일 설치 기준 다음 진화에 필요한 누적 재화와 그것이 가능해지는 웨이브(Coin 기준), 그리고 1레벨부터 최대 레벨까지 해당 종류 안에서만(진화하지 않고) 레벨업하는 데 드는 누적 재화·DPS·그 Coin이 모이는 웨이브를 보여줍니다. 재화 줄은 실제로 비용이 든 재화 종류 수만큼 늘어납니다.", BuildSpeciesDetailHeaders(report.ScenarioReferenceLevels));
        for (int i = 0; i < report.SpeciesDetailRows.Count; i++)
        {
            table.AddRow(BuildSpeciesDetailRowColumns(report.SpeciesDetailRows[i]));
        }

        TurretBalanceReportTableRenderer.RecalculateColumnWidths(table);
    }

    // 터렛 종류별 상세 표의 헤더를 기준 레벨 목록에 맞춰 동적으로 만든다
    private static string[] BuildSpeciesDetailHeaders(List<int> referenceLevels)
    {
        string[] headers = new string[3 + referenceLevels.Count];
        headers[0] = "터렛 이름";
        headers[1] = "다음 진화 가능 웨이브";
        headers[2] = "다음 진화까지 누적 소모 재화";
        for (int i = 0; i < referenceLevels.Count; i++)
        {
            headers[3 + i] = $"레벨 {referenceLevels[i]}";
        }

        return headers;
    }

    // 터렛 종류별 상세 표의 행 문자열을 만든다
    private static string[] BuildSpeciesDetailRowColumns(TurretSpeciesDetailRow row)
    {
        string[] columns = new string[3 + row.LevelSamples.Count];
        columns[0] = row.TurretName;
        columns[1] = !row.HasNextEvolution ? "해당없음" : row.NextEvolutionReached ? FormatInt(row.NextEvolutionWave) : "미도달";
        columns[2] = !row.HasNextEvolution ? "해당없음" : FormatCostBreakdown(row.NextEvolutionCumulativeCost);
        for (int i = 0; i < row.LevelSamples.Count; i++)
        {
            TurretLevelCostSample sample = row.LevelSamples[i];
            columns[3 + i] = sample.LevelAvailable
                ? $"{FormatFloat(sample.Dps)} DPS\n{FormatCostBreakdown(sample.CumulativeCost)}\n{(sample.WaveReached ? $"W{FormatInt(sample.Wave)}" : "미도달")}"
                : "해당없음";
        }

        return columns;
    }

    // 재화별 누적 비용을 재화 종류 수만큼 줄바꿈한 문자열로 만든다
    private static string FormatCostBreakdown(Dictionary<RewardCurrencyType, int> costs)
    {
        if (costs == null || costs.Count == 0)
        {
            return "0";
        }

        List<RewardCurrencyType> currencyTypes = new List<RewardCurrencyType>(costs.Keys);
        currencyTypes.Sort((left, right) => ((int)left).CompareTo((int)right));

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < currencyTypes.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append(GetCurrencyLabel(currencyTypes[i]));
            builder.Append(' ');
            builder.Append(FormatInt(costs[currencyTypes[i]]));
        }

        return builder.ToString();
    }

    // 재화별 기대 보상을 재화 종류 수만큼 줄바꿈한 문자열로 만든다
    private static string FormatRewardBreakdown(Dictionary<RewardCurrencyType, float> rewards)
    {
        if (rewards == null || rewards.Count == 0)
        {
            return "0";
        }

        List<RewardCurrencyType> currencyTypes = new List<RewardCurrencyType>(rewards.Keys);
        currencyTypes.Sort((left, right) => ((int)left).CompareTo((int)right));

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < currencyTypes.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append(GetCurrencyLabel(currencyTypes[i]));
            builder.Append(' ');
            builder.Append(FormatFloat(rewards[currencyTypes[i]]));
        }

        return builder.ToString();
    }

    // 재화 종류의 표기용 짧은 이름을 반환한다
    private static string GetCurrencyLabel(RewardCurrencyType currencyType)
    {
        switch (currencyType)
        {
            case RewardCurrencyType.Coin:
                return "코인";
            default:
                return currencyType.ToString();
        }
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
