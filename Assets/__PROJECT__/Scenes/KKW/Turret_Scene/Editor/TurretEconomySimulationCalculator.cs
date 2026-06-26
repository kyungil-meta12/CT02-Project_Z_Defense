using System.Collections.Generic;
using UnityEngine;

// 터렛 설치/업그레이드 경제와 DPS 시나리오를 계산한다.
internal sealed class TurretEconomySimulationCalculator
{
    private const int MIN_LEVEL = 1;
    private const int FALLBACK_MAX_LEVEL = 100;
    private const int MAX_SIMULATED_TURRET_COUNT = 100;

    private readonly List<int> reusableLevels = new List<int>(MAX_SIMULATED_TURRET_COUNT);
    private readonly List<string> reusableNotes = new List<string>(8);

    // 특정 웨이브와 터렛 엔트리에 대한 세 가지 설치/성장 시나리오를 계산한다
    public TurretScenarioReport BuildTurretScenarioRows(WaveSummaryRow wave, TurretShopEntrySO entry)
    {
        int budget = Mathf.FloorToInt(Mathf.Max(0.0f, wave.AvailableBudgetCoin));
        int maxInstallCount = CalculateMaxInstallCount(entry, budget, out int maxOnlyPlacementCost, out string maxInstallNote);
        SimulationResult oneFocused = SimulateInstallAndUpgrade(entry, budget, 1, wave.TotalWaveHp, "1대 집중");
        SimulationResult maxInstalled = SimulateInstallAndUpgrade(entry, budget, maxInstallCount, wave.TotalWaveHp, "최대 설치");
        SimulationResult optimal = FindBestInstallScenario(entry, budget, maxInstallCount, wave.TotalWaveHp);
        optimal.ScenarioName = "최적";

        string turretName = GetShopEntryName(entry);
        TurretScenarioReport report = new TurretScenarioReport(optimal);
        report.ScenarioRows.Add(CreateScenarioDetailRow(wave, turretName, budget, oneFocused, oneFocused.Note));
        report.ScenarioRows.Add(CreateScenarioDetailRow(wave, turretName, budget, maxInstalled, CombineNotes(maxInstalled.Note, maxInstallNote, maxOnlyPlacementCost > budget ? "최대 설치 비용이 예산을 초과합니다." : string.Empty)));
        report.ScenarioRows.Add(CreateScenarioDetailRow(wave, turretName, budget, optimal, optimal.Note));
        return report;
    }

    // 두 시뮬레이션 결과 중 더 좋은 결과인지 판단한다
    public static bool IsBetterSimulationResult(SimulationResult candidate, SimulationResult currentBest)
    {
        if (candidate.TotalDps <= 0.0f)
        {
            return false;
        }

        if (currentBest.TotalDps <= 0.0f)
        {
            return true;
        }

        if (!Mathf.Approximately(candidate.TotalDps, currentBest.TotalDps))
        {
            return candidate.TotalDps > currentBest.TotalDps;
        }

        return candidate.InstallCount < currentBest.InstallCount;
    }

    // 시뮬레이션 결과의 레벨 요약 문자열을 반환한다
    public static string FormatLevelSummary(SimulationResult result)
    {
        return string.IsNullOrEmpty(result.LevelSummary) ? "None" : result.LevelSummary;
    }

    // 단일 시나리오의 전투력+경제 상세 행을 만든다
    private static TurretScenarioDetailRow CreateScenarioDetailRow(WaveSummaryRow wave, string turretName, int budget, SimulationResult result, string note)
    {
        return new TurretScenarioDetailRow
        {
            WaveLabel = wave.WaveLabel,
            TurretName = turretName,
            ScenarioName = result.ScenarioName,
            InstallCount = result.InstallCount,
            LevelSummary = FormatLevelSummary(result),
            TotalLevel = result.TotalLevel,
            TotalDps = result.TotalDps,
            ClearSeconds = result.ClearSeconds,
            BudgetCoin = budget,
            PlacementCost = result.PlacementCost,
            UpgradeCost = result.UpgradeCost,
            RemainingCoin = result.RemainingCoin,
            NextUpgradeShortage = result.NextUpgradeShortage,
            Note = note
        };
    }

    // 예산으로 설치 비용만 지불할 때 가능한 최대 설치 수를 계산한다
    private int CalculateMaxInstallCount(TurretShopEntrySO entry, int budget, out int placementCost, out string note)
    {
        placementCost = 0;
        note = string.Empty;
        int installCount = 0;
        int remainingBudget = Mathf.Max(0, budget);
        while (installCount < MAX_SIMULATED_TURRET_COUNT)
        {
            ResourceCost[] costs = entry.GetPlacementCosts(installCount);
            int coinCost = GetCoinCost(costs);
            if (HasNonCoinCost(costs))
            {
                note = "Coin 외 설치 비용은 계산에서 제외하고 Note로만 표시합니다.";
            }

            if (coinCost <= 0)
            {
                installCount++;
                note = CombineNotes(note, $"설치 비용이 0이라 최대 {MAX_SIMULATED_TURRET_COUNT}대까지만 계산합니다.");
                continue;
            }

            if (remainingBudget < coinCost)
            {
                break;
            }

            remainingBudget -= coinCost;
            placementCost += coinCost;
            installCount++;
        }

        return installCount;
    }

    // 지정 설치 수에서 남은 예산을 업그레이드에 배분해 결과를 계산한다
    private SimulationResult SimulateInstallAndUpgrade(TurretShopEntrySO entry, int budget, int installCount, float totalWaveHp, string scenarioName)
    {
        SimulationResult result = new SimulationResult
        {
            TurretName = GetShopEntryName(entry),
            ScenarioName = scenarioName,
            MaxLevel = entry == null ? FALLBACK_MAX_LEVEL : ResolveMaxLevel(entry.TurretDefinition),
            Note = string.Empty
        };

        if (entry == null || entry.TurretDefinition == null || installCount <= 0)
        {
            result.Note = "설치 가능한 터렛이 없습니다.";
            return result;
        }

        int normalizedInstallCount = Mathf.Min(Mathf.Max(0, installCount), MAX_SIMULATED_TURRET_COUNT);
        int remainingBudget = Mathf.Max(0, budget);
        int placementCost = CalculatePlacementCost(entry, normalizedInstallCount, ref remainingBudget, ref result.Note);
        if (normalizedInstallCount > 0 && placementCost > budget)
        {
            result.Note = CombineNotes(result.Note, "설치 비용이 예산을 초과합니다.");
            return result;
        }

        reusableLevels.Clear();
        for (int i = 0; i < normalizedInstallCount; i++)
        {
            reusableLevels.Add(MIN_LEVEL);
        }

        int upgradeCost = SpendUpgradeBudgetGreedy(entry.TurretDefinition, reusableLevels, ref remainingBudget, ref result.Note);
        FillSimulationResult(entry.TurretDefinition, reusableLevels, totalWaveHp, placementCost, upgradeCost, remainingBudget, ref result);
        return result;
    }

    // 지정 설치 수만큼 순차 설치 비용을 지불한다
    private int CalculatePlacementCost(TurretShopEntrySO entry, int installCount, ref int remainingBudget, ref string note)
    {
        int totalCost = 0;
        for (int placedCount = 0; placedCount < installCount; placedCount++)
        {
            ResourceCost[] costs = entry.GetPlacementCosts(placedCount);
            int coinCost = GetCoinCost(costs);
            if (HasNonCoinCost(costs))
            {
                note = CombineNotes(note, "Coin 외 설치 비용은 계산에서 제외했습니다.");
            }

            totalCost += coinCost;
            if (remainingBudget >= coinCost)
            {
                remainingBudget -= coinCost;
            }
            else
            {
                remainingBudget = -1;
            }
        }

        return totalCost;
    }

    // 남은 예산을 DPS 증가 효율이 가장 높은 1레벨 업그레이드에 반복 배분한다
    private int SpendUpgradeBudgetGreedy(TurretDefinitionSO definition, List<int> levels, ref int remainingBudget, ref string note)
    {
        int totalUpgradeCost = 0;
        int maxLevel = ResolveMaxLevel(definition);
        if (definition == null || definition.upgradeCostProfile == null)
        {
            note = CombineNotes(note, "업그레이드 비용 프로필이 없어 Lv1로 계산했습니다.");
            return totalUpgradeCost;
        }

        while (remainingBudget > 0)
        {
            int bestIndex = -1;
            int bestCost = 0;
            float bestEfficiency = 0.0f;
            for (int i = 0; i < levels.Count; i++)
            {
                int currentLevel = levels[i];
                if (currentLevel >= maxLevel)
                {
                    continue;
                }

                int nextCost = GetCoinCost(definition.upgradeCostProfile.GetCosts(currentLevel, currentLevel + 1));
                if (nextCost <= 0 || nextCost > remainingBudget)
                {
                    continue;
                }

                float currentDps = CalculateDps(TurretStatCalculator.Calculate(definition, currentLevel));
                float nextDps = CalculateDps(TurretStatCalculator.Calculate(definition, currentLevel + 1));
                float efficiency = (nextDps - currentDps) / nextCost;
                if (bestIndex < 0 || efficiency > bestEfficiency)
                {
                    bestIndex = i;
                    bestCost = nextCost;
                    bestEfficiency = efficiency;
                }
            }

            if (bestIndex < 0)
            {
                break;
            }

            remainingBudget -= bestCost;
            totalUpgradeCost += bestCost;
            levels[bestIndex]++;
        }

        return totalUpgradeCost;
    }

    // 여러 설치 수 후보 중 총 DPS가 가장 높은 시나리오를 찾는다
    private SimulationResult FindBestInstallScenario(TurretShopEntrySO entry, int budget, int maxInstallCount, float totalWaveHp)
    {
        SimulationResult best = new SimulationResult();
        bool hasBest = false;
        int normalizedMaxInstallCount = Mathf.Min(Mathf.Max(0, maxInstallCount), MAX_SIMULATED_TURRET_COUNT);
        for (int installCount = 1; installCount <= normalizedMaxInstallCount; installCount++)
        {
            SimulationResult candidate = SimulateInstallAndUpgrade(entry, budget, installCount, totalWaveHp, "최적 후보");
            if (!hasBest || IsBetterSimulationResult(candidate, best))
            {
                best = candidate;
                hasBest = true;
            }
        }

        if (!hasBest)
        {
            best.TurretName = GetShopEntryName(entry);
            best.Note = "설치 가능한 후보가 없습니다.";
        }

        return best;
    }

    // 레벨 목록의 총 DPS와 경제 값을 결과 구조에 채운다
    private static void FillSimulationResult(TurretDefinitionSO definition, List<int> levels, float totalWaveHp, int placementCost, int upgradeCost, int remainingBudget, ref SimulationResult result)
    {
        float totalDps = 0.0f;
        int totalLevel = 0;
        for (int i = 0; i < levels.Count; i++)
        {
            int level = Mathf.Max(MIN_LEVEL, levels[i]);
            totalLevel += level;
            totalDps += CalculateDps(TurretStatCalculator.Calculate(definition, level));
        }

        result.InstallCount = levels.Count;
        result.TotalLevel = totalLevel;
        result.AverageLevel = levels.Count <= 0 ? 0.0f : (float)totalLevel / levels.Count;
        result.LevelSummary = BuildLevelSummary(levels);
        result.PlacementCost = placementCost;
        result.UpgradeCost = upgradeCost;
        result.RemainingCoin = Mathf.Max(0, remainingBudget);
        result.NextUpgradeShortage = CalculateNextUpgradeShortage(definition, levels, result.RemainingCoin);
        result.TotalDps = totalDps;
        result.ClearSeconds = totalDps <= 0.0f ? 0.0f : totalWaveHp / totalDps;
    }

    // 터렛 런타임 스탯에서 단일 대상 DPS를 계산한다
    private static float CalculateDps(TurretRuntimeStat stat)
    {
        return stat.fireInterval <= 0.0f ? 0.0f : stat.damage * Mathf.Max(1, stat.projectileCount) / stat.fireInterval;
    }

    // 현재 레벨 분포에서 다음 1레벨 업그레이드 부족분을 계산한다
    private static int CalculateNextUpgradeShortage(TurretDefinitionSO definition, List<int> levels, int remainingCoin)
    {
        if (definition == null || definition.upgradeCostProfile == null || levels.Count == 0)
        {
            return 0;
        }

        int maxLevel = ResolveMaxLevel(definition);
        int minNextCost = int.MaxValue;
        for (int i = 0; i < levels.Count; i++)
        {
            int level = levels[i];
            if (level >= maxLevel)
            {
                continue;
            }

            int nextCost = GetCoinCost(definition.upgradeCostProfile.GetCosts(level, level + 1));
            if (nextCost > 0 && nextCost < minNextCost)
            {
                minNextCost = nextCost;
            }
        }

        return minNextCost == int.MaxValue ? 0 : Mathf.Max(0, minNextCost - remainingCoin);
    }

    // 터렛 정의의 시뮬레이션 최대 레벨을 결정한다
    private static int ResolveMaxLevel(TurretDefinitionSO definition)
    {
        if (definition == null)
        {
            return FALLBACK_MAX_LEVEL;
        }

        if (definition.evolutionProgressionProfile != null)
        {
            int nextEvolutionLevel = definition.evolutionProgressionProfile.GetNextRequiredEvolutionLevel(MIN_LEVEL);
            if (nextEvolutionLevel > 0)
            {
                return nextEvolutionLevel;
            }
        }

        return definition.maxLevel > 0 ? definition.maxLevel : FALLBACK_MAX_LEVEL;
    }

    // 비용 배열에서 Coin 비용 합계를 반환한다
    private static int GetCoinCost(ResourceCost[] costs)
    {
        return GetCurrencyCost(costs, RewardCurrencyType.Coin);
    }

    // 비용 배열에서 Coin 외 재화가 있는지 확인한다
    private static bool HasNonCoinCost(ResourceCost[] costs)
    {
        if (costs == null)
        {
            return false;
        }

        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost != null && cost.amount > 0 && cost.currencyType != RewardCurrencyType.Coin)
            {
                return true;
            }
        }

        return false;
    }

    // 비용 배열에서 지정한 재화 비용 합계를 반환한다
    private static int GetCurrencyCost(ResourceCost[] costs, RewardCurrencyType currencyType)
    {
        if (costs == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost != null && cost.currencyType == currencyType)
            {
                total += Mathf.Max(0, cost.amount);
            }
        }

        return total;
    }

    // 상점 엔트리 표시 이름을 반환한다
    private static string GetShopEntryName(TurretShopEntrySO entry)
    {
        return entry == null ? "None" : entry.DisplayName;
    }

    // 레벨 분포를 요약 문자열로 만든다
    private static string BuildLevelSummary(List<int> levels)
    {
        if (levels == null || levels.Count == 0)
        {
            return "None";
        }

        int minLevel = int.MaxValue;
        int maxLevel = 0;
        int totalLevel = 0;
        for (int i = 0; i < levels.Count; i++)
        {
            int level = levels[i];
            minLevel = Mathf.Min(minLevel, level);
            maxLevel = Mathf.Max(maxLevel, level);
            totalLevel += level;
        }

        float averageLevel = (float)totalLevel / levels.Count;
        return $"Lv{averageLevel:0.#} avg ({minLevel}~{maxLevel})";
    }

    // 여러 비고 문자열을 하나로 합친다
    private string CombineNotes(params string[] notes)
    {
        reusableNotes.Clear();
        for (int i = 0; i < notes.Length; i++)
        {
            string note = notes[i];
            if (!string.IsNullOrWhiteSpace(note) && !reusableNotes.Contains(note))
            {
                reusableNotes.Add(note);
            }
        }

        return string.Join(" / ", reusableNotes);
    }

    // 터렛 하나에 대한 시나리오 계산 결과 묶음.
    internal struct TurretScenarioReport
    {
        public SimulationResult OptimalResult;
        public List<TurretScenarioDetailRow> ScenarioRows;

        // 시나리오 행 목록을 포함한 결과를 초기화한다
        public TurretScenarioReport(SimulationResult optimalResult)
        {
            OptimalResult = optimalResult;
            ScenarioRows = new List<TurretScenarioDetailRow>(3);
        }
    }
}
