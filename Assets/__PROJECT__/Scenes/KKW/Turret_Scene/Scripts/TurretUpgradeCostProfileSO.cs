using UnityEngine;

/// <summary>
/// 터렛 티어 레벨 업그레이드에 필요한 재화 비용을 계산하는 ScriptableObject.
/// </summary>
[CreateAssetMenu(menuName = "Project Z Defense/Turret Upgrade Cost Profile")]
public class TurretUpgradeCostProfileSO : ScriptableObject
{
    [SerializeField] private ResourceCost[] baseCostsPerLevel;
    [SerializeField, Min(0.0f)] private float additionalCostPercentPerTierLevel;

    // 현재 티어 레벨에서 목표 티어 레벨까지 필요한 총 비용을 계산한다
    public ResourceCost[] GetCosts(int currentTierLevel, int targetTierLevel)
    {
        if (baseCostsPerLevel == null || baseCostsPerLevel.Length == 0)
        {
            return System.Array.Empty<ResourceCost>();
        }

        int currentLevel = Mathf.Max(1, currentTierLevel);
        int targetLevel = Mathf.Max(currentLevel, targetTierLevel);
        if (targetLevel <= currentLevel)
        {
            return System.Array.Empty<ResourceCost>();
        }

        ResourceCost[] result = new ResourceCost[baseCostsPerLevel.Length];
        for (int i = 0; i < baseCostsPerLevel.Length; i++)
        {
            ResourceCost baseCost = baseCostsPerLevel[i];
            if (baseCost == null || baseCost.amount <= 0)
            {
                result[i] = new ResourceCost();
                continue;
            }

            int totalAmount = CalculateTotalAmount(baseCost.amount, currentLevel, targetLevel);
            result[i] = new ResourceCost(baseCost.currencyType, totalAmount);
        }

        return result;
    }

    // 지정한 레벨 구간의 누적 비용을 계산한다
    private int CalculateTotalAmount(int baseAmount, int currentLevel, int targetLevel)
    {
        float total = 0.0f;
        float percentPerLevel = additionalCostPercentPerTierLevel * 0.01f;

        for (int level = currentLevel + 1; level <= targetLevel; level++)
        {
            float multiplier = 1.0f + Mathf.Max(0.0f, level - 1) * percentPerLevel;
            total += baseAmount * multiplier;
        }

        return Mathf.CeilToInt(total);
    }

    // 인스펙터 입력값을 유효한 비용 범위로 보정한다
    private void OnValidate()
    {
        additionalCostPercentPerTierLevel = Mathf.Max(0.0f, additionalCostPercentPerTierLevel);
        if (baseCostsPerLevel == null)
        {
            return;
        }

        for (int i = 0; i < baseCostsPerLevel.Length; i++)
        {
            ResourceCost cost = baseCostsPerLevel[i];
            if (cost == null)
            {
                continue;
            }

            cost.amount = Mathf.Max(0, cost.amount);
        }
    }
}
