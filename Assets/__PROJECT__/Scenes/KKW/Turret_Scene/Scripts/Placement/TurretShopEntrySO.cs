using UnityEngine;

/// <summary>
/// 터렛 배치 UI에 표시할 터렛 정의, 프리팹, 아이콘, 배치 비용을 정의하는 ScriptableObject.
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
    [SerializeField, HideInInspector, Min(0)] private int cost;

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

    public int Cost
    {
        get
        {
            return cost;
        }
    }

    public ResourceCost[] PlacementCosts
    {
        get
        {
            if (HasPayableCosts(placementCosts))
            {
                return placementCosts;
            }

            if (cost <= 0)
            {
                return System.Array.Empty<ResourceCost>();
            }

            return new[] { new ResourceCost(RewardCurrencyType.Coin, cost) };
        }
    }

    // 인스펙터 입력값을 유효한 터렛 구매 비용 범위로 보정한다
    private void OnValidate()
    {
        cost = Mathf.Max(0, cost);
        if (placementCosts == null)
        {
            return;
        }

        for (int i = 0; i < placementCosts.Length; i++)
        {
            ResourceCost placementCost = placementCosts[i];
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
