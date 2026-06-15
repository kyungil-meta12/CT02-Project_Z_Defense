using UnityEngine;

/// <summary>
/// 장애물 레벨 업그레이드에 필요한 재화 비용을 계산하는 ScriptableObject.
/// </summary>
[CreateAssetMenu(fileName = "ObstacleUpgradeCostProfile", menuName = "Project Z Defense/Obstacle Upgrade Cost Profile")]
public class ObstacleUpgradeCostProfileSO : ScriptableObject
{
    [Header("업그레이드 비용 계산 - 레벨당 기본 비용과 레벨 상승 비용 증가율")]
    [SerializeField] private ResourceCost[] baseCostsPerLevel;
    [SerializeField, Min(0.0f)] private float additionalCostPercentPerLevel;

    // 현재 레벨에서 목표 레벨까지 필요한 총 비용을 계산한다
    public ResourceCost[] GetCosts(int currentLevel, int targetLevel)
    {
        if (baseCostsPerLevel == null || baseCostsPerLevel.Length == 0)
        {
            return System.Array.Empty<ResourceCost>();
        }

        int safeCurrentLevel = Mathf.Max(1, currentLevel);
        int safeTargetLevel = Mathf.Max(safeCurrentLevel, targetLevel);
        if (safeTargetLevel <= safeCurrentLevel)
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

            result[i] = new ResourceCost(baseCost.currencyType, CalculateTotalAmount(baseCost.amount, safeCurrentLevel, safeTargetLevel));
        }

        return result;
    }

    // 지정한 레벨 구간의 누적 비용을 계산한다
    private int CalculateTotalAmount(int baseAmount, int currentLevel, int targetLevel)
    {
        float total = 0.0f;
        float percentPerLevel = additionalCostPercentPerLevel * 0.01f;

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
        additionalCostPercentPerLevel = Mathf.Max(0.0f, additionalCostPercentPerLevel);
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
