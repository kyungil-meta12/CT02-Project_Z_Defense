using UnityEngine;

/// <summary>
/// 터렛 배치 UI에 표시할 터렛 정의, 프리팹, 아이콘, 설치 횟수별 배치 비용을 정의하는 ScriptableObject.
/// </summary>
[CreateAssetMenu(menuName = "Project Z Defense/Turret Placement Entry")]
public class TurretShopEntrySO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    [Header("Turret")]
    [SerializeField] private TurretDefinitionSO turretDefinition;
    [SerializeField] private GameObject overridePrefab;
    [SerializeField] private GameObject previewPrefab;

    [Header("Cost")]
    [SerializeField] private ResourceCost[] placementCosts;
    [SerializeField] private TurretPlacementCostTier[] placementCostTiers;

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            if (turretDefinition != null && !string.IsNullOrWhiteSpace(turretDefinition.displayName))
            {
                return turretDefinition.displayName;
            }

            return turretDefinition == null ? name : turretDefinition.name;
        }
    }

    public Sprite Icon
    {
        get
        {
            return icon;
        }
    }

    public TurretDefinitionSO TurretDefinition
    {
        get
        {
            return turretDefinition;
        }
    }

    public GameObject TurretPrefab
    {
        get
        {
            if (overridePrefab != null)
            {
                return overridePrefab;
            }

            return turretDefinition == null ? null : turretDefinition.basePrefab;
        }
    }

    public GameObject PreviewPrefab
    {
        get
        {
            return previewPrefab != null ? previewPrefab : TurretPrefab;
        }
    }

    public ResourceCost[] PlacementCosts
    {
        get
        {
            return GetPlacementCosts(0);
        }
    }

    // 현재까지 설치된 개수에 맞는 배치 비용을 반환한다
    public ResourceCost[] GetPlacementCosts(int placedCount)
    {
        ResourceCost[] tierCosts = GetTierCosts(placedCount);
        if (HasPayableCosts(tierCosts))
        {
            return tierCosts;
        }

        if (HasPayableCosts(placementCosts))
        {
            return placementCosts;
        }

        return System.Array.Empty<ResourceCost>();
    }

    // 인스펙터 입력값을 유효한 터렛 구매 비용 범위로 보정한다
    private void OnValidate()
    {
        ValidateCosts(placementCosts);

        if (placementCostTiers == null)
        {
            return;
        }

        for (int i = 0; i < placementCostTiers.Length; i++)
        {
            TurretPlacementCostTier tier = placementCostTiers[i];
            if (tier == null)
            {
                continue;
            }

            tier.minPlacedCount = Mathf.Max(0, tier.minPlacedCount);
            ValidateCosts(tier.costs);
        }
    }

    // 설치 개수 조건에 맞는 가장 높은 단계의 비용을 반환한다
    private ResourceCost[] GetTierCosts(int placedCount)
    {
        if (placementCostTiers == null || placementCostTiers.Length == 0)
        {
            return null;
        }

        int normalizedPlacedCount = Mathf.Max(0, placedCount);
        int bestMinPlacedCount = -1;
        ResourceCost[] bestCosts = null;

        for (int i = 0; i < placementCostTiers.Length; i++)
        {
            TurretPlacementCostTier tier = placementCostTiers[i];
            if (tier == null || tier.minPlacedCount > normalizedPlacedCount || tier.minPlacedCount < bestMinPlacedCount)
            {
                continue;
            }

            bestMinPlacedCount = tier.minPlacedCount;
            bestCosts = tier.costs;
        }

        return bestCosts;
    }

    // 비용 배열의 음수 수량을 0 이상으로 보정한다
    private static void ValidateCosts(ResourceCost[] costs)
    {
        if (costs == null)
        {
            return;
        }

        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost placementCost = costs[i];
            if (placementCost == null)
            {
                continue;
            }

            placementCost.amount = Mathf.Max(0, placementCost.amount);
        }
    }

    // 실제 소모할 비용 항목이 하나 이상 있는지 확인한다
    private static bool HasPayableCosts(ResourceCost[] costs)
    {
        if (costs == null)
        {
            return false;
        }

        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost resourceCost = costs[i];
            if (resourceCost != null && resourceCost.amount > 0)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// 터렛 배치 엔트리의 설치 개수 조건별 비용을 정의한다.
/// </summary>
[System.Serializable]
public class TurretPlacementCostTier
{
    [Min(0)] public int minPlacedCount;
    public ResourceCost[] costs;
}
