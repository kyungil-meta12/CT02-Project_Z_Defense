using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// 장애물 밸런스 계산 결과를 화면/CSV 공유 표 모델로 변환한다.
internal static class ObstacleBalanceTableBuilder
{
    private const float WARN_RATIO_LOW = 0.8f;
    private const float WARN_RATIO_HIGH = 1.2f;

    // 장애물 밸런스 결과로 표 모델을 만든다
    public static ReportTableModel Build(List<ObstacleWaveRow> rows, List<ObstacleEntrySpec> entries, float targetClearSeconds, float targetClearSecondsIncrement, float obstacleTargetTimeMultiplier, float zombieArrivalSeconds)
    {
        ReportTableModel table = new ReportTableModel();

        if (entries.Count == 0)
        {
            table.Reset("obstacle_balance.csv", "ObstacleBuildEntrySO 에셋을 찾을 수 없습니다.", "안내");
            table.AddRow("ObstacleBuildEntrySO 에셋이 없어 장애물 밸런스를 계산할 수 없습니다.");
            return table;
        }

        string[] headers = BuildHeaders(entries);
        string info = "웨이브 시작 전 누적 재화 예산 기준(Coin은 초기 지갑 포함). "
                    + "누적 재화는 장애물 건설/업그레이드 비용과 아이템 데이터 CSV의 조합/분해 관계에 필요한 재화만 표시합니다. "
                    + "조합 가능 재화와 분해 결과는 기대 수량 기준의 가상 누적 재화로 표시하고 최적 장애물 계산에 사용합니다. "
                    + "총 좀비 DPS는 보스 제외 일반 좀비 전체 수 기준이며, 런타임 측정 DPS 또는 측정값을 웨이브 배율로 정규화한 DPS만 사용합니다. "
                    + "단일: 1개 설치 후 예산 전액으로 최대 업그레이드. "
                    + "최대 설치(Obstacle 전용): 1~9개 중 총 HP 최대 조합, 예산 전액 기준. "
                    + "최적 조합: 게이트+장애물 간 재화 효율(HP/재화) 기준 탐욕 배분, 합산 HP로 파괴시간 계산. "
                    + $"비고: 기준 파괴시간은 기준 클리어 시간×{FormatFloat(obstacleTargetTimeMultiplier)}배. 파괴시간이 기준 파괴시간 대비 ±20%를 벗어나면 경고.";
        table.Reset("obstacle_balance.csv", info, headers);

        for (int i = 0; i < rows.Count; i++)
        {
            float waveTargetSeconds = Mathf.Max(1.0f, targetClearSeconds) + i * Mathf.Max(0.0f, targetClearSecondsIncrement);
            float obstacleTargetSeconds = waveTargetSeconds * Mathf.Max(0.1f, obstacleTargetTimeMultiplier);
            table.AddRow(BuildRow(rows[i], entries, obstacleTargetSeconds, Mathf.Max(0.0f, zombieArrivalSeconds)));
        }

        TurretBalanceReportTableRenderer.RecalculateColumnWidths(table);
        return table;
    }

    // 장애물 항목 목록에 맞춰 동적으로 헤더를 만든다
    private static string[] BuildHeaders(List<ObstacleEntrySpec> entries)
    {
        List<string> headers = new List<string>();
        headers.Add("웨이브");
        headers.Add("예산");
        headers.Add("총 좀비 DPS\n(보스 제외)");

        for (int i = 0; i < entries.Count; i++)
        {
            string name = entries[i].DisplayName;
            headers.Add($"{name}\n단일");
            if (entries[i].SlotType == ObstacleBuildSlotType.Obstacle)
            {
                headers.Add($"{name}\n최대 설치");
            }
        }

        headers.Add("최적 조합\n(효율 배분)");
        headers.Add("파괴시간(초)");
        headers.Add("비고");
        return headers.ToArray();
    }

    // 웨이브 결과 행 하나를 문자열 배열로 변환한다
    private static string[] BuildRow(ObstacleWaveRow row, List<ObstacleEntrySpec> entries, float obstacleTargetSeconds, float zombieArrivalSeconds)
    {
        List<string> cols = new List<string>();
        cols.Add(row.WaveLabel);
        cols.Add(FormatBudget(row.Budget));
        cols.Add(FormatFloat(row.ZombieDps));

        for (int i = 0; i < entries.Count; i++)
        {
            ObstacleInstallSample single = row.SingleSamples[i];
            cols.Add(single.CanAfford
                ? $"Lv{single.Level}\n{FormatFloat(single.TotalHp)} HP"
                : "예산 부족");

            if (entries[i].SlotType == ObstacleBuildSlotType.Obstacle)
            {
                ObstacleInstallSample max = row.MaxSamples[i];
                cols.Add(max.CanAfford
                    ? $"{max.Count}개×Lv{max.Level}\n{FormatFloat(max.TotalHp)} HP"
                    : "예산 부족");
            }
        }

        cols.Add(row.Optimal.HasValue ? row.Optimal.Description : "-");

        bool hasDestructionTime = row.Optimal.HasValue && row.ZombieDps > 0f && row.DestructionTime > 0f;
        float adjustedDestructionTime = row.DestructionTime + zombieArrivalSeconds;
        cols.Add(hasDestructionTime ? FormatFloat(adjustedDestructionTime) : "-");

        cols.Add(BuildNote(adjustedDestructionTime, hasDestructionTime, obstacleTargetSeconds, row.DpsDataNote));

        return cols.ToArray();
    }

    // 파괴시간(도달시간 포함)과 장애물 기준 파괴시간을 비교해 비고 메세지를 만든다
    private static string BuildNote(float adjustedDestructionTime, bool hasDestructionTime, float obstacleTargetSeconds, string dpsDataNote)
    {
        string balanceNote = string.Empty;
        if (!hasDestructionTime || obstacleTargetSeconds <= 0f)
        {
            balanceNote = "-";
        }
        else
        {
            float ratio = adjustedDestructionTime / obstacleTargetSeconds;
            if (ratio < WARN_RATIO_LOW)
            {
                balanceNote = $"조기 파괴 위험\n파괴시간({FormatFloat(adjustedDestructionTime)}s) < 기준({FormatFloat(obstacleTargetSeconds)}s)×80%";
            }
            else if (ratio > WARN_RATIO_HIGH)
            {
                balanceNote = $"장애물 HP 과잉 — 업그레이드 수치 재검토 권장\n파괴시간({FormatFloat(adjustedDestructionTime)}s) > 기준({FormatFloat(obstacleTargetSeconds)}s)×120%";
            }

        }

        if (string.IsNullOrWhiteSpace(dpsDataNote))
        {
            return balanceNote;
        }

        if (string.IsNullOrWhiteSpace(balanceNote) || balanceNote == "-")
        {
            return dpsDataNote;
        }

        return dpsDataNote + "\n" + balanceNote;
    }

    // 재화별 예산을 재화 종류 수만큼 줄바꿈한 문자열로 만든다
    private static string FormatBudget(Dictionary<RewardCurrencyType, float> budget)
    {
        if (budget == null || budget.Count == 0)
        {
            return "0";
        }

        List<RewardCurrencyType> keys = new List<RewardCurrencyType>(budget.Keys);
        keys.Sort((a, b) => ((int)a).CompareTo((int)b));

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < keys.Count; i++)
        {
            if (budget[keys[i]] <= 0f)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(GetCurrencyLabel(keys[i]));
            builder.Append(' ');
            builder.Append(FormatFloat(budget[keys[i]]));
        }

        return builder.Length > 0 ? builder.ToString() : "0";
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

    private static string FormatFloat(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
