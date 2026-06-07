using UnityEngine;

[DisallowMultipleComponent]
public class TurretBaseSlot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform buildPoint;
    [SerializeField] private Collider placementHitArea;

    [Header("Runtime")]
    [SerializeField] private TurretDefinitionRuntimeController currentTurret;
    [SerializeField] private GameObject currentTurretObject;

    public Transform BuildPoint
    {
        get
        {
            return buildPoint;
        }
    }

    public Collider PlacementHitArea
    {
        get
        {
            return placementHitArea;
        }
    }

    public TurretDefinitionRuntimeController CurrentTurret
    {
        get
        {
            return currentTurret;
        }
    }

    public bool CanPlace
    {
        get
        {
            return buildPoint != null && currentTurret == null && currentTurretObject == null;
        }
    }

    private void Reset()
    {
        AutoBindReferences();
    }

    private void Awake()
    {
        AutoBindReferences();
        RefreshCurrentTurret();
    }

    public bool OwnsHitArea(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return false;
        }

        return hitCollider == placementHitArea || hitCollider.transform.IsChildOf(transform);
    }

    public bool TryPlace(TurretShopEntrySO shopEntry, out TurretDefinitionRuntimeController placedTurret)
    {
        placedTurret = null;

        if (!CanPlace || shopEntry == null || shopEntry.TurretPrefab == null)
        {
            return false;
        }

        GameObject turretObject = Instantiate(shopEntry.TurretPrefab, buildPoint);
        turretObject.transform.localPosition = Vector3.zero;
        turretObject.transform.localRotation = Quaternion.identity;

        placedTurret = turretObject.GetComponent<TurretDefinitionRuntimeController>();
        currentTurretObject = turretObject;

        if (placedTurret != null && shopEntry.TurretDefinition != null)
        {
            placedTurret.SetDefinition(shopEntry.TurretDefinition, 1, 1);
        }

        currentTurret = placedTurret;
        return true;
    }

    public void ClearCurrentTurret(TurretDefinitionRuntimeController turret)
    {
        if (currentTurret == turret)
        {
            currentTurret = null;
            currentTurretObject = null;
        }
    }

    public void SetCurrentTurret(TurretDefinitionRuntimeController turret)
    {
        currentTurret = turret;
        currentTurretObject = turret == null ? null : turret.gameObject;
    }

    private void AutoBindReferences()
    {
        if (buildPoint == null)
        {
            Transform foundBuildPoint = transform.Find("BuildPoint");
            if (foundBuildPoint != null)
            {
                buildPoint = foundBuildPoint;
            }
        }

        if (placementHitArea == null)
        {
            Transform foundHitArea = transform.Find("PlacementHitArea");
            if (foundHitArea != null)
            {
                placementHitArea = foundHitArea.GetComponent<Collider>();
            }
        }
    }

    private void RefreshCurrentTurret()
    {
        if (currentTurret != null || buildPoint == null)
        {
            return;
        }

        currentTurret = buildPoint.GetComponentInChildren<TurretDefinitionRuntimeController>(true);
        if (currentTurret != null)
        {
            currentTurretObject = currentTurret.gameObject;
            return;
        }

        if (buildPoint.childCount > 0)
        {
            currentTurretObject = buildPoint.GetChild(0).gameObject;
        }
    }
}
