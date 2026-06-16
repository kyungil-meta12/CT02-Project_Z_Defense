using UnityEngine;

/// <summary>
/// 터렛 배치 지점의 점유 상태와 실제 터렛 설치 처리를 관리한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretBaseSlot : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform buildPoint;
    [SerializeField] private Collider placementHitArea;

    [Header("런타임")]
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

    // 현재 슬롯 하위 터렛 상태를 갱신하고 터렛 컨트롤러를 반환한다
    public TurretDefinitionRuntimeController RefreshAndGetCurrentTurret()
    {
        RefreshCurrentTurret();
        return currentTurret;
    }

    // 컴포넌트 추가 시 배치 기준 참조를 자동으로 연결한다
    private void Reset()
    {
        AutoBindReferences();
    }

    // 런타임 시작 시 참조와 현재 점유 터렛을 갱신한다
    private void Awake()
    {
        AutoBindReferences();
        RefreshCurrentTurret();
    }

    // 지정한 콜라이더가 이 슬롯의 배치 판정 영역인지 확인한다
    public bool OwnsHitArea(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return false;
        }

        return hitCollider == placementHitArea || hitCollider.transform.IsChildOf(transform);
    }

    // 상점 엔트리의 기본 비용을 지불한 뒤 터렛을 배치한다
    public bool TryPlace(TurretShopEntrySO shopEntry, out TurretDefinitionRuntimeController placedTurret)
    {
        ResourceCost[] placementCosts = shopEntry == null ? null : shopEntry.PlacementCosts;
        return TryPlace(shopEntry, placementCosts, out placedTurret);
    }

    // 전달받은 설치 횟수 기준 비용을 지불한 뒤 터렛을 배치한다
    public bool TryPlace(TurretShopEntrySO shopEntry, ResourceCost[] placementCosts, out TurretDefinitionRuntimeController placedTurret)
    {
        placedTurret = null;

        if (!CanPlace || shopEntry == null || shopEntry.TurretPrefab == null)
        {
            TurretEconomyLogUtility.LogResult("설치", GetShopEntryName(shopEntry), placementCosts, false, this, "배치 슬롯 또는 터렛 프리팹이 유효하지 않습니다.");
            return false;
        }

        if (!TrySpendPlacementCosts(placementCosts))
        {
            TurretEconomyLogUtility.LogResult("설치", GetShopEntryName(shopEntry), placementCosts, false, this, "재화가 부족하거나 ItemManager가 없습니다.");
            return false;
        }

        GameObject turretObject = Instantiate(shopEntry.TurretPrefab, buildPoint);
        if (turretObject == null)
        {
            RefundPlacementCosts(placementCosts);
            TurretEconomyLogUtility.LogResult("설치", GetShopEntryName(shopEntry), placementCosts, false, this, "터렛 프리팹 생성에 실패해 비용을 환불했습니다.");
            return false;
        }

        turretObject.transform.localPosition = Vector3.zero;
        turretObject.transform.localRotation = Quaternion.identity;

        placedTurret = turretObject.GetComponent<TurretDefinitionRuntimeController>();
        currentTurretObject = turretObject;

        if (placedTurret != null && shopEntry.TurretDefinition != null)
        {
            placedTurret.SetDefinition(shopEntry.TurretDefinition, 1, 1);
        }

        currentTurret = placedTurret;
        TurretEconomyLogUtility.LogResult("설치", GetShopEntryName(shopEntry), placementCosts, true, this);
        return true;
    }

    // 지정한 터렛이 현재 점유 터렛이면 슬롯 상태를 비운다
    public void ClearCurrentTurret(TurretDefinitionRuntimeController turret)
    {
        if (currentTurret == turret)
        {
            currentTurret = null;
            currentTurretObject = null;
        }
    }

    // 외부에서 이미 배치된 터렛을 현재 슬롯 점유 대상으로 등록한다
    public void SetCurrentTurret(TurretDefinitionRuntimeController turret)
    {
        currentTurret = turret;
        currentTurretObject = turret == null ? null : turret.gameObject;
    }

    // 배치 비용을 지불할 수 있으면 실제로 소모한다
    private bool TrySpendPlacementCosts(ResourceCost[] placementCosts)
    {
        if (!HasPayableCosts(placementCosts))
        {
            return true;
        }

        if (InventorySystem.Inst == null)
        {
            Debug.LogError("[TurretBaseSlot] ItemManager가 없어 터렛 배치 비용을 사용할 수 없습니다.", this);
            return false;
        }

        return InventorySystem.Inst.TrySpend(placementCosts);
    }

    // 터렛 배치 실패 시 이미 지불한 배치 비용을 돌려준다
    private void RefundPlacementCosts(ResourceCost[] placementCosts)
    {
        if (!HasPayableCosts(placementCosts) || InventorySystem.Inst == null)
        {
            return;
        }

        InventorySystem.Inst.Refund(placementCosts);
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
            ResourceCost cost = costs[i];
            if (cost != null && cost.amount > 0)
            {
                return true;
            }
        }

        return false;
    }

    // 상점 엔트리의 표시 이름을 로그용으로 반환한다
    private static string GetShopEntryName(TurretShopEntrySO shopEntry)
    {
        return shopEntry == null ? string.Empty : shopEntry.DisplayName;
    }

    // 자식 오브젝트 이름 기준으로 기본 배치 참조를 자동 연결한다
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

    // 빌드 포인트 아래 이미 존재하는 터렛을 현재 점유 상태로 동기화한다
    private void RefreshCurrentTurret()
    {
        if (currentTurret != null)
        {
            currentTurretObject = currentTurret.gameObject;
            return;
        }

        if (currentTurretObject != null)
        {
            currentTurret = currentTurretObject.GetComponentInChildren<TurretDefinitionRuntimeController>(true);
            if (currentTurret != null)
            {
                return;
            }
        }

        if (buildPoint == null)
        {
            currentTurretObject = null;
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
