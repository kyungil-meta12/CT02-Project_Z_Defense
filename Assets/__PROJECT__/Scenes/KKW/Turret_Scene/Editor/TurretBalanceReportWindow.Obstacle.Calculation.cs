using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

// 장애물 밸런스 계산에 사용하는 데이터 타입.

// ObstacleBuildEntrySO에서 수집한 장애물 항목 스냅샷.
internal struct ObstacleEntrySpec
{
    public string DisplayName;
    public ObstacleBuildSlotType SlotType;
    public Dictionary<RewardCurrencyType, int> BuildCosts;
    public ObstacleDefinitionSO Definition;
}

// 단일 설치 또는 최대 설치 시나리오의 결과.
internal struct ObstacleInstallSample
{
    public bool CanAfford;
    public int Count;
    public int Level;
    public float TotalHp;
}

// 탐욕 알고리즘으로 계산한 게이트+장애물 최적 예산 배분 결과.
internal struct ObstacleOptimalResult
{
    public bool HasValue;
    public float CombinedHp;
    public string Description;
}

// 웨이브별 장애물 밸런스 계산 결과 행.
internal struct ObstacleWaveRow
{
    public string WaveLabel;
    public Dictionary<RewardCurrencyType, float> Budget;
    public float ZombieDps;
    public ObstacleInstallSample[] SingleSamples;
    public ObstacleInstallSample[] MaxSamples;
    public ObstacleOptimalResult Optimal;
    public float DestructionTime;
    public float BestClearSeconds;
}

// 웨이브별 장애물 밸런스를 계산한다.
internal static class ObstacleBalanceCalculator
{
    private const int MAX_OBSTACLE_COUNT = 9;

    // ObstacleBuildEntrySO 에셋을 수집해 계산용 스냅샷 목록을 반환한다
    public static List<ObstacleEntrySpec> CollectEntries(List<ReportWarning> warnings)
    {
        List<ObstacleEntrySpec> entries = new List<ObstacleEntrySpec>();
        string[] guids = AssetDatabase.FindAssets("t:ObstacleBuildEntrySO");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ObstacleBuildEntrySO so = AssetDatabase.LoadAssetAtPath<ObstacleBuildEntrySO>(path);
            if (so == null)
            {
                continue;
            }

            ObstacleDefinitionSO def = so.ObstacleDefinition;
            if (def == null)
            {
                ReportWarning.Add(warnings, ReportWarningSeverity.Info, "ObstacleBuildEntrySO", path, "ObstacleDefinition이 없어 장애물 밸런스 계산에서 제외됩니다.");
                continue;
            }

            if (def.ObstacleSpec == null)
            {
                ReportWarning.Add(warnings, ReportWarningSeverity.Warning, "ObstacleBuildEntrySO", path, "ObstacleSpec이 없어 HP를 계산할 수 없어 제외됩니다.");
                continue;
            }

            entries.Add(new ObstacleEntrySpec
            {
                DisplayName = so.DisplayName,
                SlotType = so.SlotType,
                BuildCosts = ExtractCosts(so.BuildCosts),
                Definition = def
            });
        }

        // Gate 먼저, 그 다음 Obstacle 알파벳순
        entries.Sort((a, b) =>
        {
            int slotCompare = ((int)b.SlotType).CompareTo((int)a.SlotType);
            return slotCompare != 0 ? slotCompare : string.CompareOrdinal(a.DisplayName, b.DisplayName);
        });

        return entries;
    }

    // 웨이브 요약 행 목록과 장애물 항목 목록으로 웨이브별 밸런스 결과를 계산한다
    public static List<ObstacleWaveRow> BuildRows(
        List<WaveSummaryRow> waveRows,
        List<ObstacleEntrySpec> entries,
        List<WaveClearSimulationRow> clearRows)
    {
        List<ObstacleWaveRow> rows = new List<ObstacleWaveRow>(waveRows.Count);

        for (int i = 0; i < waveRows.Count; i++)
        {
            WaveSummaryRow wave = waveRows[i];
            Dictionary<RewardCurrencyType, float> budget = BuildBudget(wave);
            float zombieDps = wave.AverageNormalZombieDps * wave.NormalSpawnCount;

            ObstacleInstallSample[] single = new ObstacleInstallSample[entries.Count];
            ObstacleInstallSample[] max = new ObstacleInstallSample[entries.Count];

            for (int j = 0; j < entries.Count; j++)
            {
                ObstacleEntrySpec entry = entries[j];
                single[j] = CalcSingle(entry, budget);
                max[j] = entry.SlotType == ObstacleBuildSlotType.Gate
                    ? single[j]
                    : CalcBestInstall(entry, budget);
            }

            ObstacleOptimalResult optimal = CalcOptimal(entries, budget);
            float destructionTime = zombieDps > 0f && optimal.HasValue && optimal.CombinedHp > 0f
                ? optimal.CombinedHp / zombieDps
                : 0f;

            float bestClearSeconds = clearRows != null && i < clearRows.Count
                ? clearRows[i].BestClearSeconds
                : 0f;

            rows.Add(new ObstacleWaveRow
            {
                WaveLabel = wave.WaveLabel,
                Budget = budget,
                ZombieDps = zombieDps,
                SingleSamples = single,
                MaxSamples = max,
                Optimal = optimal,
                DestructionTime = destructionTime,
                BestClearSeconds = bestClearSeconds
            });
        }

        return rows;
    }

    // 재화 효율(HP 증가 ÷ 재화 총량) 기반 탐욕 알고리즘으로 게이트+장애물 최적 예산 배분을 계산한다.
    // Gate는 초기 Lv1 설치 상태. 매 단계에서 효율이 가장 높은 업그레이드 또는 추가 설치를 선택한다.
    public static ObstacleOptimalResult CalcOptimal(List<ObstacleEntrySpec> entries, Dictionary<RewardCurrencyType, float> budget)
    {
        if (entries.Count == 0)
        {
            return default;
        }

        int n = entries.Count;
        int[] levels = new int[n];
        int[] counts = new int[n];
        for (int i = 0; i < n; i++)
        {
            levels[i] = 1;
            counts[i] = entries[i].SlotType == ObstacleBuildSlotType.Gate ? 1 : 0;
        }

        Dictionary<RewardCurrencyType, float> remaining = new Dictionary<RewardCurrencyType, float>(budget);

        while (true)
        {
            float bestEff = 0f;
            int bestIdx = -1;
            bool bestIsInstall = false;

            for (int i = 0; i < n; i++)
            {
                ObstacleDefinitionSO def = entries[i].Definition;
                int level = levels[i];
                int count = counts[i];

                // 현재 설치된 유닛 전체를 한 레벨 업그레이드
                if (count > 0 && level < def.MaxLevel)
                {
                    Dictionary<RewardCurrencyType, int> cost = ScaleCosts(
                        ExtractCosts(def.GetUpgradeCosts(level, level + 1)), count);
                    if (CanAfford(cost, remaining))
                    {
                        float hpGain = count * (CalcHp(def.ObstacleSpec, level + 1) - CalcHp(def.ObstacleSpec, level));
                        float eff = CalcEfficiency(hpGain, cost);
                        if (eff > bestEff)
                        {
                            bestEff = eff;
                            bestIdx = i;
                            bestIsInstall = false;
                        }
                    }
                }

                // Obstacle 추가 설치 (기존 유닛의 현재 레벨에 맞춰 즉시 업그레이드)
                if (entries[i].SlotType == ObstacleBuildSlotType.Obstacle && count < MAX_OBSTACLE_COUNT)
                {
                    Dictionary<RewardCurrencyType, int> installCost = BuildInstallAndCatchupCost(entries[i], def, level, count);
                    if (CanAfford(installCost, remaining))
                    {
                        float hpGain = CalcHp(def.ObstacleSpec, count == 0 ? 1 : level);
                        float eff = CalcEfficiency(hpGain, installCost);
                        if (eff > bestEff)
                        {
                            bestEff = eff;
                            bestIdx = i;
                            bestIsInstall = true;
                        }
                    }
                }
            }

            if (bestIdx == -1)
            {
                break;
            }

            // 선택한 행동 적용
            if (bestIsInstall)
            {
                int level = levels[bestIdx];
                int count = counts[bestIdx];
                Dictionary<RewardCurrencyType, int> cost = BuildInstallAndCatchupCost(entries[bestIdx], entries[bestIdx].Definition, level, count);
                remaining = SubtractCosts(remaining, cost);
                counts[bestIdx]++;
                if (counts[bestIdx] == 1)
                {
                    levels[bestIdx] = 1;
                }
            }
            else
            {
                Dictionary<RewardCurrencyType, int> cost = ScaleCosts(
                    ExtractCosts(entries[bestIdx].Definition.GetUpgradeCosts(levels[bestIdx], levels[bestIdx] + 1)),
                    counts[bestIdx]);
                remaining = SubtractCosts(remaining, cost);
                levels[bestIdx]++;
            }
        }

        // 총 HP 계산 및 설명 문자열 생성
        float totalHp = 0f;
        bool hasAny = false;
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < n; i++)
        {
            if (counts[i] == 0)
            {
                continue;
            }

            float unitHp = CalcHp(entries[i].Definition.ObstacleSpec, levels[i]);
            float hp = counts[i] * unitHp;
            totalHp += hp;
            hasAny = true;

            if (sb.Length > 0)
            {
                sb.Append('\n');
            }

            string countPrefix = counts[i] > 1 ? $"{counts[i]}×" : string.Empty;
            sb.Append($"{entries[i].DisplayName} {countPrefix}Lv{levels[i]} ({FormatHp(hp)} HP)");
        }

        if (!hasAny)
        {
            return default;
        }

        sb.Append($"\n합계 {FormatHp(totalHp)} HP");

        return new ObstacleOptimalResult
        {
            HasValue = true,
            CombinedHp = totalHp,
            Description = sb.ToString()
        };
    }

    // 웨이브 요약 행에서 전 재화별 누적 예산을 만든다. Coin은 AvailableBudgetCoin(초기 지갑 포함), 나머지는 CumulativeReward에서 가져온다.
    private static Dictionary<RewardCurrencyType, float> BuildBudget(WaveSummaryRow wave)
    {
        Dictionary<RewardCurrencyType, float> budget = new Dictionary<RewardCurrencyType, float>();
        budget[RewardCurrencyType.Coin] = wave.AvailableBudgetCoin;
        if (wave.CumulativeReward != null)
        {
            foreach (KeyValuePair<RewardCurrencyType, float> pair in wave.CumulativeReward)
            {
                if (pair.Key != RewardCurrencyType.Coin)
                {
                    budget[pair.Key] = pair.Value;
                }
            }
        }

        return budget;
    }

    // 단일 설치 시나리오: 설치 비용을 뺀 나머지 예산으로 최대 레벨을 계산한다
    private static ObstacleInstallSample CalcSingle(ObstacleEntrySpec entry, Dictionary<RewardCurrencyType, float> budget)
    {
        if (!CanAfford(entry.BuildCosts, budget))
        {
            return new ObstacleInstallSample { CanAfford = false };
        }

        Dictionary<RewardCurrencyType, float> remaining = SubtractCosts(budget, entry.BuildCosts);
        int level = FindMaxAffordableLevel(entry.Definition, remaining);
        float totalHp = CalcHp(entry.Definition.ObstacleSpec, level);
        return new ObstacleInstallSample { CanAfford = true, Count = 1, Level = level, TotalHp = totalHp };
    }

    // 최대 설치 시나리오(1~9개): 총 HP를 최대화하는 설치 수와 레벨 조합을 찾는다
    private static ObstacleInstallSample CalcBestInstall(ObstacleEntrySpec entry, Dictionary<RewardCurrencyType, float> budget)
    {
        ObstacleInstallSample best = new ObstacleInstallSample { CanAfford = false };

        for (int count = 1; count <= MAX_OBSTACLE_COUNT; count++)
        {
            Dictionary<RewardCurrencyType, int> totalInstallCost = ScaleCosts(entry.BuildCosts, count);
            if (!CanAfford(totalInstallCost, budget))
            {
                break;
            }

            Dictionary<RewardCurrencyType, float> afterInstall = SubtractCosts(budget, totalInstallCost);
            Dictionary<RewardCurrencyType, float> perUnit = DivideBudget(afterInstall, count);
            int level = FindMaxAffordableLevel(entry.Definition, perUnit);
            float unitHp = CalcHp(entry.Definition.ObstacleSpec, level);
            float totalHp = unitHp * count;

            if (!best.CanAfford || totalHp > best.TotalHp)
            {
                best = new ObstacleInstallSample { CanAfford = true, Count = count, Level = level, TotalHp = totalHp };
            }
        }

        return best;
    }

    // 주어진 예산으로 달성 가능한 최대 업그레이드 레벨을 이진 탐색으로 찾는다
    private static int FindMaxAffordableLevel(ObstacleDefinitionSO def, Dictionary<RewardCurrencyType, float> budget)
    {
        if (def == null)
        {
            return 1;
        }

        int maxLevel = def.MaxLevel;
        if (maxLevel <= 1)
        {
            return 1;
        }

        int lo = 1;
        int hi = maxLevel;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            Dictionary<RewardCurrencyType, int> cost = ExtractCosts(def.GetUpgradeCosts(1, mid));
            if (CanAfford(cost, budget))
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return lo;
    }

    // 추가 설치 1개 비용 = 설치비 + 기존 유닛 레벨까지의 업그레이드 누적 비용
    private static Dictionary<RewardCurrencyType, int> BuildInstallAndCatchupCost(
        ObstacleEntrySpec entry, ObstacleDefinitionSO def, int currentLevel, int currentCount)
    {
        if (currentCount == 0 || currentLevel <= 1)
        {
            return new Dictionary<RewardCurrencyType, int>(entry.BuildCosts);
        }

        return AddCosts(entry.BuildCosts, ExtractCosts(def.GetUpgradeCosts(1, currentLevel)));
    }

    // 업그레이드당 HP 증가 / 소모 재화 총량. 재화 종류에 관계없이 단위당 HP 효율로 비교한다.
    private static float CalcEfficiency(float hpGain, Dictionary<RewardCurrencyType, int> cost)
    {
        if (hpGain <= 0f)
        {
            return 0f;
        }

        float totalCost = 0f;
        foreach (KeyValuePair<RewardCurrencyType, int> pair in cost)
        {
            totalCost += pair.Value;
        }

        return totalCost > 0f ? hpGain / totalCost : float.MaxValue;
    }

    // 지정 레벨에서의 장애물 HP를 계산한다
    private static float CalcHp(ObstacleSpec spec, int level)
    {
        if (spec == null)
        {
            return 0f;
        }

        return spec.Hp + Mathf.Max(1, level) * spec.levelWeight;
    }

    // ResourceCost 배열을 재화 종류별 합산 Dictionary로 변환한다
    private static Dictionary<RewardCurrencyType, int> ExtractCosts(ResourceCost[] costs)
    {
        Dictionary<RewardCurrencyType, int> result = new Dictionary<RewardCurrencyType, int>();
        if (costs == null)
        {
            return result;
        }

        for (int i = 0; i < costs.Length; i++)
        {
            if (costs[i] == null)
            {
                continue;
            }

            int amount = Mathf.Max(0, costs[i].amount);
            if (amount == 0)
            {
                continue;
            }

            result.TryGetValue(costs[i].currencyType, out int existing);
            result[costs[i].currencyType] = existing + amount;
        }

        return result;
    }

    // 비용 Dictionary의 모든 재화가 예산 내에 들어오는지 확인한다
    private static bool CanAfford(Dictionary<RewardCurrencyType, int> cost, Dictionary<RewardCurrencyType, float> budget)
    {
        foreach (KeyValuePair<RewardCurrencyType, int> pair in cost)
        {
            budget.TryGetValue(pair.Key, out float available);
            if (pair.Value > available)
            {
                return false;
            }
        }

        return true;
    }

    // 예산에서 비용을 차감한 새 Dictionary를 반환한다
    private static Dictionary<RewardCurrencyType, float> SubtractCosts(
        Dictionary<RewardCurrencyType, float> budget,
        Dictionary<RewardCurrencyType, int> cost)
    {
        Dictionary<RewardCurrencyType, float> result = new Dictionary<RewardCurrencyType, float>(budget);
        foreach (KeyValuePair<RewardCurrencyType, int> pair in cost)
        {
            result.TryGetValue(pair.Key, out float current);
            result[pair.Key] = current - pair.Value;
        }

        return result;
    }

    // 두 비용 Dictionary를 합산해 반환한다
    private static Dictionary<RewardCurrencyType, int> AddCosts(
        Dictionary<RewardCurrencyType, int> a,
        Dictionary<RewardCurrencyType, int> b)
    {
        Dictionary<RewardCurrencyType, int> result = new Dictionary<RewardCurrencyType, int>(a);
        foreach (KeyValuePair<RewardCurrencyType, int> pair in b)
        {
            result.TryGetValue(pair.Key, out int existing);
            result[pair.Key] = existing + pair.Value;
        }

        return result;
    }

    // 비용 Dictionary를 정수 배수로 곱해 반환한다
    private static Dictionary<RewardCurrencyType, int> ScaleCosts(
        Dictionary<RewardCurrencyType, int> costs, int multiplier)
    {
        Dictionary<RewardCurrencyType, int> result = new Dictionary<RewardCurrencyType, int>();
        foreach (KeyValuePair<RewardCurrencyType, int> pair in costs)
        {
            result[pair.Key] = pair.Value * multiplier;
        }

        return result;
    }

    // 예산 Dictionary를 정수로 나눠 단위당 예산을 반환한다
    private static Dictionary<RewardCurrencyType, float> DivideBudget(
        Dictionary<RewardCurrencyType, float> budget, int divisor)
    {
        Dictionary<RewardCurrencyType, float> result = new Dictionary<RewardCurrencyType, float>();
        foreach (KeyValuePair<RewardCurrencyType, float> pair in budget)
        {
            result[pair.Key] = pair.Value / divisor;
        }

        return result;
    }

    private static string FormatHp(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
