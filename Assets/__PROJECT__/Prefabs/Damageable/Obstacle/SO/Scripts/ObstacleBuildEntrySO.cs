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

    public ResourceCost[] BuildCosts
    {
        get
        {
            // 장애물 배치 비용은 터렛 배치/업그레이드/진화와 같은 다중 재화 파이프라인을 타야 하므로 legacy int Cost fallback을 두지 않는다.
            return buildCosts;
        }
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
