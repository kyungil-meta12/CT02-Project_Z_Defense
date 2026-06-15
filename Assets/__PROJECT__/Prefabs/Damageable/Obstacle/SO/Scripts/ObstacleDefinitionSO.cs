using UnityEngine;

/// <summary>
/// 장애물의 표시 정보, 스펙, 업그레이드 비용, 레벨별 프리팹 교체 규칙을 정의한다.
/// </summary>
[CreateAssetMenu(fileName = "ObstacleDefinition", menuName = "Project Z Defense/Obstacle Definition")]
public class ObstacleDefinitionSO : ScriptableObject
{
    [Header("장애물 식별 정보 - 저장 ID, 표시 이름, UI 아이콘")]
    [SerializeField] private string obstacleId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    [Header("장애물 기본 구성 - 슬롯 타입, 체력 스펙, 기본/프리뷰 프리팹")]
    [SerializeField] private ObstacleBuildSlotType slotType;
    [SerializeField] private ObstacleSpec obstacleSpec;
    [SerializeField] private GameObject basePrefab;
    [SerializeField] private GameObject previewPrefab;
    [SerializeField] private Vector3 placementLocalEulerAngles;

    [Header("업그레이드 규칙 - 최대 레벨, 비용, 레벨별 프리팹 교체")]
    [SerializeField, Min(0)] private int maxLevel;
    [SerializeField] private ObstacleUpgradeCostProfileSO upgradeCostProfile;
    [SerializeField] private ObstaclePrefabProgressionSO prefabProgression;

    public string ObstacleId
    {
        get
        {
            return obstacleId;
        }
    }

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            if (basePrefab != null)
            {
                return basePrefab.name;
            }

            return name;
        }
    }

    public Sprite Icon
    {
        get
        {
            return icon;
        }
    }

    public ObstacleBuildSlotType SlotType
    {
        get
        {
            return slotType;
        }
    }

    public ObstacleSpec ObstacleSpec
    {
        get
        {
            return obstacleSpec;
        }
    }

    public int MaxLevel
    {
        get
        {
            return Mathf.Max(0, maxLevel);
        }
    }

    public ObstacleUpgradeCostProfileSO UpgradeCostProfile
    {
        get
        {
            return upgradeCostProfile;
        }
    }

    // 지정 레벨에 맞는 장애물 프리팹을 반환한다
    public GameObject GetPrefabForLevel(int level)
    {
        ObstaclePrefabProgressionEntry entry = prefabProgression == null ? null : prefabProgression.GetEntryForLevel(level);
        if (entry != null && entry.Prefab != null)
        {
            return entry.Prefab;
        }

        return basePrefab;
    }

    // 지정 레벨에 맞는 프리뷰 프리팹을 반환한다
    public GameObject GetPreviewPrefabForLevel(int level)
    {
        ObstaclePrefabProgressionEntry entry = prefabProgression == null ? null : prefabProgression.GetEntryForLevel(level);
        if (entry != null && entry.PreviewPrefab != null)
        {
            return entry.PreviewPrefab;
        }

        return previewPrefab != null ? previewPrefab : basePrefab;
    }

    // 지정 레벨에 맞는 배치 로컬 회전을 반환한다
    public Quaternion GetPlacementLocalRotationForLevel(int level)
    {
        ObstaclePrefabProgressionEntry entry = prefabProgression == null ? null : prefabProgression.GetEntryForLevel(level);
        return entry == null ? Quaternion.Euler(placementLocalEulerAngles) : entry.PlacementLocalRotation;
    }

    // 현재 레벨에서 목표 레벨까지 필요한 업그레이드 비용을 반환한다
    public ResourceCost[] GetUpgradeCosts(int currentLevel, int targetLevel)
    {
        if (upgradeCostProfile == null)
        {
            return System.Array.Empty<ResourceCost>();
        }

        return upgradeCostProfile.GetCosts(currentLevel, targetLevel);
    }

    // 인스펙터 입력값을 유효한 레벨 범위로 보정한다
    private void OnValidate()
    {
        maxLevel = Mathf.Max(0, maxLevel);
    }
}
