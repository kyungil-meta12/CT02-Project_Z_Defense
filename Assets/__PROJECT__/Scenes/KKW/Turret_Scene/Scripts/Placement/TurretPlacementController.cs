using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class TurretPlacementController : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask turretBaseLayerMask = 1 << 11;
    [SerializeField] private LayerMask invalidPreviewLayerMask = ~0;
    [SerializeField, Min(1.0f)] private float maxRayDistance = 500.0f;

    [Header("Preview")]
    [SerializeField] private bool hidePreviewWhenNoBase = true;
    [SerializeField] private bool showInvalidPreviewOnWorld = true;
    [SerializeField] private bool allowWorldClickPlacement = true;
    [SerializeField] private Material validPreviewMaterial;
    [SerializeField] private Material invalidPreviewMaterial;
    [SerializeField] private Color validPreviewColor = new Color(0.2f, 1.0f, 0.35f, 0.45f);
    [SerializeField] private Color invalidPreviewColor = new Color(1.0f, 0.12f, 0.08f, 0.45f);
    [SerializeField] private Vector3 previewLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 invalidPreviewEulerAngles = new Vector3(0.0f, -90.0f, 0.0f);
    [SerializeField, Min(0.01f)] private float previewScaleMultiplier = 1.0f;
    [SerializeField, Min(0.01f)] private float invalidWorldPreviewScaleMultiplier = 1.0f;
    [SerializeField] private bool useInvalidPreviewPlacementPlane = true;
    [SerializeField] private float invalidPreviewPlaneY;
    [SerializeField, Range(0.0f, 0.45f)] private float invalidPreviewViewportPadding = 0.06f;
    [SerializeField] private bool createRuntimePreviewMaterials = true;

    private TurretPlacementPreview preview;
    private TurretShopEntrySO activeShopEntry;
    private TurretBaseSlot hoveredSlot;
    private Material runtimeValidPreviewMaterial;
    private Material runtimeInvalidPreviewMaterial;

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
        EnsureRuntimePreviewMaterials();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void OnDisable()
    {
        CancelPlacement();
    }

    private void OnDestroy()
    {
        DestroyRuntimePreviewMaterials();
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
            preview.SnapTo(hoveredSlot.BuildPoint, previewLocalOffset, previewScaleMultiplier);
            preview.SetVisualState(canPlace, GetValidPreviewMaterial(), GetInvalidPreviewMaterial(), validPreviewColor, invalidPreviewColor);
            return;
        }

        if (showInvalidPreviewOnWorld && TryFindInvalidPreviewPose(screenPosition, out Vector3 invalidPosition, out Quaternion invalidRotation))
        {
            preview.SetVisible(true);
            preview.SetPose(invalidPosition, invalidRotation, invalidWorldPreviewScaleMultiplier);
            preview.SetVisualState(false, GetValidPreviewMaterial(), GetInvalidPreviewMaterial(), validPreviewColor, invalidPreviewColor);
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
            preview.SetPose(hit.point, Quaternion.identity, previewScaleMultiplier);
            preview.SetVisualState(false, GetValidPreviewMaterial(), GetInvalidPreviewMaterial(), validPreviewColor, invalidPreviewColor);
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

        EnsureRuntimePreviewMaterials();
    }

    private Material GetValidPreviewMaterial()
    {
        return validPreviewMaterial != null ? validPreviewMaterial : runtimeValidPreviewMaterial;
    }

    private Material GetInvalidPreviewMaterial()
    {
        return invalidPreviewMaterial != null ? invalidPreviewMaterial : runtimeInvalidPreviewMaterial;
    }

    private void EnsureRuntimePreviewMaterials()
    {
        if (!createRuntimePreviewMaterials)
        {
            return;
        }

        if (runtimeValidPreviewMaterial == null)
        {
            runtimeValidPreviewMaterial = CreatePreviewMaterial("Runtime Valid Turret Preview", validPreviewColor);
        }

        if (runtimeInvalidPreviewMaterial == null)
        {
            runtimeInvalidPreviewMaterial = CreatePreviewMaterial("Runtime Invalid Turret Preview", invalidPreviewColor);
        }
    }

    private void DestroyRuntimePreviewMaterials()
    {
        if (runtimeValidPreviewMaterial != null)
        {
            Destroy(runtimeValidPreviewMaterial);
            runtimeValidPreviewMaterial = null;
        }

        if (runtimeInvalidPreviewMaterial != null)
        {
            Destroy(runtimeInvalidPreviewMaterial);
            runtimeInvalidPreviewMaterial = null;
        }
    }

    private static Material CreatePreviewMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.name = materialName;
        material.color = color;
        ConfigureTransparentMaterial(material, color);
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material, Color color)
    {
        material.SetColor("_Color", color);
        material.SetColor("_BaseColor", color);

        material.SetFloat("_Surface", 1.0f);
        material.SetFloat("_Blend", 0.0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0.0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
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

        return null;
    }

    private bool TryFindInvalidPreviewPose(Vector2 screenPosition, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.Euler(invalidPreviewEulerAngles);

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return false;
        }

        Vector3 viewportPoint = targetCamera.ScreenToViewportPoint(screenPosition);
        if (viewportPoint.x < invalidPreviewViewportPadding ||
            viewportPoint.x > 1.0f - invalidPreviewViewportPadding ||
            viewportPoint.y < invalidPreviewViewportPadding ||
            viewportPoint.y > 1.0f - invalidPreviewViewportPadding)
        {
            return false;
        }

        Ray ray = targetCamera.ScreenPointToRay(screenPosition);
        if (useInvalidPreviewPlacementPlane)
        {
            Plane placementPlane = new Plane(Vector3.up, new Vector3(0.0f, invalidPreviewPlaneY, 0.0f));
            if (!placementPlane.Raycast(ray, out float enter))
            {
                return false;
            }

            position = ray.GetPoint(enter) + previewLocalOffset;
            return true;
        }

        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, invalidPreviewLayerMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        position = hit.point + previewLocalOffset;
        return true;
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
