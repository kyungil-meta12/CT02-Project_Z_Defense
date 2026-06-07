using UnityEngine;

[CreateAssetMenu(menuName = "Project Z Defense/Turret Shop Entry")]
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
    [SerializeField, Min(0)] private int cost;

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
}
