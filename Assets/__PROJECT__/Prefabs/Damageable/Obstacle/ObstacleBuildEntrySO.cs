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
    [Header("Identity")]
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    [Header("Obstacle")]
    [SerializeField] private ObstacleBuildSlotType slotType;
    [SerializeField] private GameObject obstaclePrefab;
    [SerializeField] private GameObject previewPrefab;
    [SerializeField] private Vector3 placementLocalEulerAngles;

    [Header("Cost")]
    [SerializeField] private ResourceCost[] buildCosts;

    public string DisplayName
    {
        get
        {
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

    public GameObject ObstaclePrefab
    {
        get
        {
            return obstaclePrefab;
        }
    }

    public GameObject PreviewPrefab
    {
        get
        {
            return previewPrefab != null ? previewPrefab : obstaclePrefab;
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
