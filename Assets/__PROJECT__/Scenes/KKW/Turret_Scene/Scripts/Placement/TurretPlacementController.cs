using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class TurretPlacementController : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask turretBaseLayerMask = 1 << 11;
    [SerializeField, Min(1.0f)] private float maxRayDistance = 500.0f;

    [Header("Preview")]
    [SerializeField] private bool hidePreviewWhenNoBase = true;
    [SerializeField] private bool allowWorldClickPlacement = true;

    private TurretPlacementPreview preview;
    private TurretShopEntrySO activeShopEntry;
    private TurretBaseSlot hoveredSlot;

    public bool IsPlacing
    {
        get
        {
            return activeShopEntry != null;
        }
    }

    private void Awake()
    {
        preview = new TurretPlacementPreview();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void OnDisable()
    {
        CancelPlacement();
    }

    private void Update()
    {
        if (!IsPlacing || !allowWorldClickPlacement)
        {
            return;
        }

        TryGetPrimaryPointerPosition(out Vector2 pointerPosition);
        UpdatePlacement(pointerPosition);

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
            return;
        }

        if (WasPrimaryPointerPressed() && !IsPointerOverUI())
        {
            EndPlacement(pointerPosition);
        }
    }

    public void BeginPlacement(TurretShopEntrySO shopEntry, Vector2 screenPosition)
    {
        if (shopEntry == null || shopEntry.TurretPrefab == null)
        {
            CancelPlacement();
            return;
        }

        activeShopEntry = shopEntry;
        EnsurePreview();
        preview.Show(shopEntry.PreviewPrefab);
        UpdatePlacement(screenPosition);
    }

    public void UpdatePlacement(Vector2 screenPosition)
    {
        if (!IsPlacing)
        {
            return;
        }

        hoveredSlot = FindSlot(screenPosition, out RaycastHit hit);
        bool hasSlot = hoveredSlot != null && hoveredSlot.BuildPoint != null;
        bool canPlace = hasSlot && hoveredSlot.CanPlace;

        if (hasSlot)
        {
            preview.SetVisible(true);
            preview.SetPose(hoveredSlot.BuildPoint.position, hoveredSlot.BuildPoint.rotation);
            preview.SetValid(canPlace);
            return;
        }

        if (hidePreviewWhenNoBase)
        {
            preview.SetVisible(false);
            return;
        }

        preview.SetVisible(hit.collider != null);
        if (hit.collider != null)
        {
            preview.SetPose(hit.point, Quaternion.identity);
            preview.SetValid(false);
        }
    }

    public bool EndPlacement(Vector2 screenPosition)
    {
        if (!IsPlacing)
        {
            return false;
        }

        TurretBaseSlot targetSlot = FindSlot(screenPosition, out _);
        bool placed = targetSlot != null && targetSlot.TryPlace(activeShopEntry, out _);
        CancelPlacement();
        return placed;
    }

    public void CancelPlacement()
    {
        activeShopEntry = null;
        hoveredSlot = null;
        if (preview != null)
        {
            preview.Hide();
        }
    }

    private void EnsurePreview()
    {
        if (preview == null)
        {
            preview = new TurretPlacementPreview();
        }
    }

    private TurretBaseSlot FindSlot(Vector2 screenPosition, out RaycastHit hit)
    {
        hit = new RaycastHit();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return null;
        }

        Ray ray = targetCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out hit, maxRayDistance, turretBaseLayerMask, QueryTriggerInteraction.Collide))
        {
            return null;
        }

        TurretBaseSlot slot = hit.collider.GetComponentInParent<TurretBaseSlot>();
        if (slot != null)
        {
            return slot;
        }

        Transform root = hit.collider.transform.parent;
        while (root != null)
        {
            if (root.Find("BuildPoint") != null && root.Find("PlacementHitArea") != null)
            {
                return root.gameObject.AddComponent<TurretBaseSlot>();
            }

            root = root.parent;
        }

        return null;
    }

    private static bool TryGetPrimaryPointerPosition(out Vector2 pointerPosition)
    {
        if (Input.touchCount > 0)
        {
            pointerPosition = Input.GetTouch(0).position;
            return true;
        }

        pointerPosition = Input.mousePosition;
        return true;
    }

    private static bool WasPrimaryPointerPressed()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            return touch.phase == TouchPhase.Began;
        }

        return Input.GetMouseButtonDown(0);
    }

    private static bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            return EventSystem.current.IsPointerOverGameObject(touch.fingerId);
        }

        return EventSystem.current.IsPointerOverGameObject();
    }
}
