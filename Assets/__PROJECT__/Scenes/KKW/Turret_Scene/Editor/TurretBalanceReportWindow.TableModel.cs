using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

// 터렛 웨이브 밸런스 리포트 결과를 화면/CSV 공유 표 모델로 변환한다.
internal sealed class TurretBalanceReportTableBuilder
{
    // 리포트 결과에서 전체 탭 표 모델을 만든다
    public ReportTableModel[] Build(TurretBalanceReportResult report, float targetClearSeconds, float targetClearSecondsIncrement, float zombieArrivalSeconds)
    {
        ReportTableModel[] tables =
        {
            new ReportTableModel(),
            new ReportTableModel(),
            new ReportTableModel(),
            new ReportTableModel()
        };

        BuildItemBalanceTableCache(report, tables[0]);
        BuildWaveClearTableCache(report, tables[1], targetClearSeconds, targetClearSecondsIncrement, zombieArrivalSeconds);
        BuildScenarioDetailTableCache(report, tables[2]);
        BuildSourceWarningTableCache(report, tables[3]);
        return tables;
    }

    // 아이템 밸런스 표 캐시를 만든다
    private static void BuildItemBalanceTableCache(TurretBalanceReportResult report, ReportTableModel table)
    {
        RewardCurrencyType[] currencyTypes = GetSortedCurrencyTypes();
        string[] headers = new string[1 + currencyTypes.Length];
        headers[0] = "웨이브";
        for (int i = 0; i < currencyTypes.Length; i++)
        {
            headers[1 + i] = currencyTypes[i].ToString();
        }

        table.Reset("item_balance.csv", "각 웨이브 시작 시점의 누적 보상에 드랍 기대값, 웨이브 보상 배율, 아이템 데이터 CSV의 조합/분해 기대값을 반영한 아이템별 최대 참조 수량입니다. 각 열은 해당 아이템을 목표로 했을 때의 독립 참조값이며 실제 자동 조합/분해 실행을 의미하지 않습니다.", headers);
        for (int rowIndex = 0; rowIndex < report.ItemBalanceRows.Count; rowIndex++)
        {
            ItemBalanceRow row = report.ItemBalanceRows[rowIndex];
            string[] columns = new string[headers.Length];
            columns[0] = row.WaveLabel;
            for (int currencyIndex = 0; currencyIndex < currencyTypes.Length; currencyIndex++)
            {
                row.MaxItemAmounts.TryGetValue(currencyTypes[currencyIndex], out float amount);
                columns[1 + currencyIndex] = FormatFloat(amount);
            }

            table.AddRow(columns);
        }

        TurretBalanceReportTableRenderer.RecalculateColumnWidths(table);
    }

    // 웨이브 클리어 표 캐시를 만든다
    private static void BuildWaveClearTableCache(TurretBalanceReportResult report, ReportTableModel table, float targetClearSeconds, float targetClearSecondsIncrement, float zombieArrivalSeconds)
    {
        string incrementDesc = targetClearSecondsIncrement > 0f
            ? $", 웨이브당 +{FormatFloat(targetClearSecondsIncrement)}초 증가"
            : string.Empty;
        string arrivalDesc = zombieArrivalSeconds > 0f ? $", 좀비 도달 {FormatFloat(zombieArrivalSeconds)}초 포함" : string.Empty;
        HashSet<RewardCurrencyType> currencyScope = TurretBalanceReportCurrencyProjector.BuildTurretCurrencyScope(report);
        table.Reset("wave_clear_simulation.csv", $"각 웨이브에서, 터렛 시나리오 상세의 종류별 레벨 체크포인트 데이터(누적 재화·DPS)를 참고해 그 웨이브 시작 시점의 Coin 예산(직전 웨이브까지의 총 누적 재화 중 Coin, 첫 웨이브는 초기 지갑 Coin {FormatInt(report.InitialWalletCoin)})으로 설치 가능한 총 DPS가 가장 높은 1~3순위 종류를 보여줍니다. 설치 수는 현재 터렛 슬롯 상한인 최대 8대까지 계산합니다. 웨이브 획득 재화·총 누적 재화는 터렛 설치/업그레이드/진화 비용과 아이템 데이터 CSV의 조합/분해 관계에 필요한 재화만 표시합니다. 예상 클리어 초 = 웨이브 총 좀비 HP / 1순위 총 DPS + 좀비 도달 시간입니다. 비고: 기준 클리어 {FormatFloat(targetClearSeconds)}초{incrementDesc}{arrivalDesc} 대비 ±20% 초과 시 경고.", "웨이브", "일반 좀비 수", "보스 좀비 수", "웨이브 총 좀비 HP", "웨이브 획득 재화", "총 누적 재화", "1순위 터렛", "2순위 터렛", "3순위 터렛", "예상 클리어 초", "비고");
        for (int i = 0; i < report.WaveClearRows.Count; i++)
        {
            WaveClearSimulationRow row = report.WaveClearRows[i];
            float waveTarget = targetClearSeconds + i * targetClearSecondsIncrement;
            float adjustedClearSeconds = row.BestClearSeconds + zombieArrivalSeconds;
            string note = BuildWaveClearNote(adjustedClearSeconds, waveTarget, row.Note);
            Dictionary<RewardCurrencyType, float> averageRewards = GetItemBalanceDelta(report, i, currencyScope);
            Dictionary<RewardCurrencyType, float> cumulativeRewards = GetItemBalanceRewards(report, i, currencyScope);
            table.AddRow(row.WaveLabel, FormatInt(row.NormalSpawnCount), FormatInt(row.BossSpawnCount), FormatFloat(row.TotalWaveHp), FormatRewardBreakdown(averageRewards), FormatRewardBreakdown(cumulativeRewards), FormatRankCell(row.TopRanks, 0), FormatRankCell(row.TopRanks, 1), FormatRankCell(row.TopRanks, 2), FormatFloat(adjustedClearSeconds), note);
        }

        TurretBalanceReportTableRenderer.RecalculateColumnWidths(table);
    }

    // 웨이브 클리어 비고를 만든다. 예상 클리어 초(도달시간 포함)가 해당 웨이브 기준 대비 ±20% 초과 시 경고를 추가한다.
    private static string BuildWaveClearNote(float adjustedClearSeconds, float waveTarget, string baseNote)
    {
        baseNote = baseNote ?? string.Empty;
        if (adjustedClearSeconds <= 0f || waveTarget <= 0f)
        {
            return baseNote;
        }

        float ratio = adjustedClearSeconds / waveTarget;
        string flagNote = string.Empty;
        if (ratio < 0.8f)
        {
            flagNote = $"DPS 과잉 (클리어 {FormatFloat(adjustedClearSeconds)}s < 기준 {FormatFloat(waveTarget)}s×80%)";
        }
        else if (ratio > 1.2f)
        {
            flagNote = $"DPS 부족 위험 (클리어 {FormatFloat(adjustedClearSeconds)}s > 기준 {FormatFloat(waveTarget)}s×120%)";
        }

        if (string.IsNullOrEmpty(flagNote))
        {
            return baseNote;
        }

        return string.IsNullOrEmpty(baseNote) ? flagNote : baseNote + "\n" + flagNote;
    }

    // 순위 목록에서 지정 순위의 셀 텍스트를 만든다
    private static string FormatRankCell(List<WaveClearRankEntry> topRanks, int rankIndex)
    {
        if (topRanks == null || rankIndex >= topRanks.Count)
        {
            return "해당없음";
        }

        WaveClearRankEntry entry = topRanks[rankIndex];
        return $"{entry.TurretName}\n{FormatInt(entry.InstallCount)}대 Lv{FormatInt(entry.Level)}\n{FormatDpsWithCritical(entry.TotalDps, entry.CriticalExpectedTotalDps)} DPS";
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
                ? $"{FormatDpsWithCritical(sample.Dps, sample.CriticalExpectedDps)} DPS\n{FormatCostBreakdown(sample.CumulativeCost)}\n{(sample.WaveReached ? $"W{FormatInt(sample.Wave)}" : "미도달")}"
                : "해당없음";
        }

        return columns;
    }

    // 기본 DPS와 치명타/강타 기대 DPS를 함께 표시한다
    private static string FormatDpsWithCritical(float baseDps, float criticalExpectedDps)
    {
        return $"{FormatFloat(baseDps)} ({FormatFloat(criticalExpectedDps)})";
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

    // 아이템 밸런스 행에서 필요한 재화만 가져온다
    private static Dictionary<RewardCurrencyType, float> GetItemBalanceRewards(TurretBalanceReportResult report, int index, HashSet<RewardCurrencyType> currencyScope)
    {
        if (report == null || index < 0 || index >= report.ItemBalanceRows.Count)
        {
            return new Dictionary<RewardCurrencyType, float>();
        }

        return TurretBalanceReportCurrencyProjector.FilterItemAmounts(report.ItemBalanceRows[index], currencyScope);
    }

    // 아이템 밸런스 누적 행의 차이를 웨이브 획득량으로 만든다
    private static Dictionary<RewardCurrencyType, float> GetItemBalanceDelta(TurretBalanceReportResult report, int index, HashSet<RewardCurrencyType> currencyScope)
    {
        Dictionary<RewardCurrencyType, float> current = GetItemBalanceRewards(report, index, currencyScope);
        if (index <= 0)
        {
            return current;
        }

        Dictionary<RewardCurrencyType, float> previous = GetItemBalanceRewards(report, index - 1, currencyScope);
        Dictionary<RewardCurrencyType, float> delta = new Dictionary<RewardCurrencyType, float>();
        foreach (KeyValuePair<RewardCurrencyType, float> pair in current)
        {
            previous.TryGetValue(pair.Key, out float previousAmount);
            float amount = pair.Value - previousAmount;
            if (amount > 0f)
            {
                delta[pair.Key] = amount;
            }
        }

        return delta;
    }

    // RewardCurrencyType enum 값을 숫자 순서대로 반환한다
    private static RewardCurrencyType[] GetSortedCurrencyTypes()
    {
        Array values = Enum.GetValues(typeof(RewardCurrencyType));
        RewardCurrencyType[] currencyTypes = new RewardCurrencyType[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            currencyTypes[i] = (RewardCurrencyType)values.GetValue(i);
        }

        Array.Sort(currencyTypes, (left, right) => ((int)left).CompareTo((int)right));
        return currencyTypes;
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
