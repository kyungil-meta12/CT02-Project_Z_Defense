using System.Collections.Generic;
using UnityEngine;

// 터렛 비용/레벨 상한 계산에 쓰는 공유 정적 헬퍼 모음.
internal static class TurretEconomySimulationCalculator
{
    private const int MIN_LEVEL = 1;
    private const int FALLBACK_MAX_LEVEL = 100;

    // 터렛 정의의 시뮬레이션 최대 레벨을 결정한다
    internal static int ResolveMaxLevel(TurretDefinitionSO definition)
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

    // 터렛 런타임 스탯에서 단일 대상 DPS를 계산한다
    internal static float CalculateDps(TurretRuntimeStat stat)
    {
        return stat.fireInterval <= 0.0f ? 0.0f : stat.damage * Mathf.Max(1, stat.projectileCount) / stat.fireInterval;
    }

    // 비용 배열에서 Coin 비용 합계를 반환한다
    internal static int GetCoinCost(ResourceCost[] costs)
    {
        return GetCurrencyCost(costs, RewardCurrencyType.Coin);
    }

    // 비용 배열에서 Coin 외 재화가 있는지 확인한다
    internal static bool HasNonCoinCost(ResourceCost[] costs)
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

    // 누적 비용 표를 복사한다
    internal static Dictionary<RewardCurrencyType, int> CloneCosts(Dictionary<RewardCurrencyType, int> source)
    {
        return new Dictionary<RewardCurrencyType, int>(source);
    }

    // 비용 배열의 모든 재화를 누적 비용 표에 더한다
    internal static void AddCosts(Dictionary<RewardCurrencyType, int> target, ResourceCost[] costs)
    {
        if (costs == null)
        {
            return;
        }

        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null || cost.amount <= 0)
            {
                continue;
            }

            target.TryGetValue(cost.currencyType, out int existing);
            target[cost.currencyType] = existing + cost.amount;
        }
    }

    // 다른 누적 비용 표의 값을 누적 비용 표에 더한다
    internal static void AddCosts(Dictionary<RewardCurrencyType, int> target, Dictionary<RewardCurrencyType, int> costs)
    {
        foreach (KeyValuePair<RewardCurrencyType, int> pair in costs)
        {
            target.TryGetValue(pair.Key, out int existing);
            target[pair.Key] = existing + pair.Value;
        }
    }

    // 누적 비용 표에서 Coin 비용만 반환한다
    internal static int GetCoinAmount(Dictionary<RewardCurrencyType, int> costs)
    {
        return costs.TryGetValue(RewardCurrencyType.Coin, out int amount) ? amount : 0;
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
}
