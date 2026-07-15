using UnityEngine;

public enum ObstacleBuildSlotType
{
    Obstacle,
    Gate
}

/// <summary>
/// 장애물 또는 게이트 배치 UI와 배치 비용, 프리팹 정보를 정의한다.
/// 배치 비용은 legacy Coin 전용 값이 아니라 ResourceCost[]만 source of truth로 사용한다.
/// </summary>
[CreateAssetMenu(fileName = "ObstacleBuildEntry", menuName = "Project Z Defense/Obstacle Build Entry")]
public class ObstacleBuildEntrySO : ScriptableObject
{
    [Header("저장 식별자")]
    [SerializeField] private string saveId;

    [Header("배치 UI 표시 정보 - 이름과 아이콘")]
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    [Header("배치 대상 정보 - 정의 SO 또는 직접 지정 프리팹")]
    [SerializeField] private ObstacleBuildSlotType slotType;
    [SerializeField] private ObstacleDefinitionSO obstacleDefinition;
    [SerializeField] private GameObject obstaclePrefab;
    [SerializeField] private GameObject previewPrefab;
    [SerializeField] private Vector3 placementLocalEulerAngles;

    [Header("배치 비용 - 설치 시 소비할 재화 목록")]
    [SerializeField] private ResourceCost[] buildCosts;

    [Header("배치 비용 증가 - 슬롯 최초 점유 1회당 기본 비용 대비 증가율 (%)")]
    [SerializeField, Min(0f)] private float additionalCostPercentPerPlacement;

    [Header("재건 할인율 - 파괴 후 동일 슬롯 재설치 시 비용 할인 비율 (0=할인없음, 1=무료)")]
    [SerializeField, Range(0f, 1f)] private float rebuildCostDiscount = 0.5f;

    public string DisplayName
    {
        get
        {
            if (obstacleDefinition != null)
            {
                return obstacleDefinition.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            return obstaclePrefab == null ? name : obstaclePrefab.name;
        }
    }

    public Sprite Icon
    {
        get
        {
            if (icon != null)
            {
                return icon;
            }

            return obstacleDefinition == null ? null : obstacleDefinition.Icon;
        }
    }

    public ObstacleBuildSlotType SlotType
    {
        get
        {
            return obstacleDefinition != null ? obstacleDefinition.SlotType : slotType;
        }
    }

    public ObstacleDefinitionSO ObstacleDefinition
    {
        get
        {
            return obstacleDefinition;
        }
    }

    public GameObject ObstaclePrefab
    {
        get
        {
            return GetObstaclePrefabForLevel(1);
        }
    }

    public GameObject PreviewPrefab
    {
        get
        {
            return GetPreviewPrefabForLevel(1);
        }
    }

    public Quaternion PlacementLocalRotation
    {
        get
        {
            return Quaternion.Euler(placementLocalEulerAngles);
        }
    }

    public float AdditionalCostPercentPerPlacement => additionalCostPercentPerPlacement;
    public string SaveId => saveId;

    public ResourceCost[] BuildCosts
    {
        get
        {
            // 장애물 배치 비용은 터렛 배치/업그레이드/진화와 같은 다중 재화 파이프라인을 타야 하므로 legacy int Cost fallback을 두지 않는다.
            return buildCosts;
        }
    }

    // 현재 최초 점유 슬롯 수와 재건 여부를 반영한 실효 배치 비용을 반환한다
    public ResourceCost[] GetPlacementCosts(int firstPlacementCount, bool isRebuild)
    {
        if (buildCosts == null)
        {
            return System.Array.Empty<ResourceCost>();
        }

        float multiplier = 1.0f + Mathf.Max(0, firstPlacementCount) * additionalCostPercentPerPlacement * 0.01f;
        if (isRebuild)
        {
            multiplier *= 1.0f - Mathf.Clamp01(rebuildCostDiscount);
        }

        ResourceCost[] result = new ResourceCost[buildCosts.Length];
        for (int i = 0; i < buildCosts.Length; i++)
        {
            ResourceCost src = buildCosts[i];
            if (src == null || src.amount <= 0)
            {
                result[i] = new ResourceCost();
                continue;
            }

            result[i] = new ResourceCost(src.currencyType, Mathf.Max(0, Mathf.CeilToInt(src.amount * multiplier)));
        }

        return result;
    }

    // 지정 레벨에 맞는 설치 프리팹을 반환한다
    public GameObject GetObstaclePrefabForLevel(int level)
    {
        if (obstacleDefinition != null)
        {
            GameObject definitionPrefab = obstacleDefinition.GetPrefabForLevel(level);
            if (definitionPrefab != null)
            {
                return definitionPrefab;
            }
        }

        return obstaclePrefab;
    }

    // 지정 레벨에 맞는 프리뷰 프리팹을 반환한다
    public GameObject GetPreviewPrefabForLevel(int level)
    {
        if (obstacleDefinition != null)
        {
            GameObject definitionPreviewPrefab = obstacleDefinition.GetPreviewPrefabForLevel(level);
            if (definitionPreviewPrefab != null)
            {
                return definitionPreviewPrefab;
            }
        }

        return previewPrefab != null ? previewPrefab : obstaclePrefab;
    }

    // 지정 레벨에 맞는 배치 회전을 반환한다
    public Quaternion GetPlacementLocalRotationForLevel(int level)
    {
        return obstacleDefinition == null ? PlacementLocalRotation : obstacleDefinition.GetPlacementLocalRotationForLevel(level);
    }

    // 인스펙터 비용 값을 음수가 아닌 값으로 보정한다
    private void OnValidate()
    {
        saveId = saveId == null ? string.Empty : saveId.Trim();

        if (buildCosts == null)
        {
            return;
        }

        for (int i = 0; i < buildCosts.Length; i++)
        {
            ResourceCost buildCost = buildCosts[i];
            if (buildCost == null)
            {
                continue;
            }

            buildCost.amount = Mathf.Max(0, buildCost.amount);
        }
    }
}
