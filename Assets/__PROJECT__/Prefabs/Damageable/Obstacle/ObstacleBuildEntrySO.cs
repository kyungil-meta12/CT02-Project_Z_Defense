using UnityEngine;

public enum ObstacleBuildSlotType
{
    Obstacle,
    Gate
}

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
    [SerializeField, Min(0)] private int cost;

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

    public int Cost
    {
        get
        {
            return cost;
        }
    }
}
