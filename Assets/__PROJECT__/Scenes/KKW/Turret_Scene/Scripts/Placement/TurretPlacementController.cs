using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 터렛 배치 입력, 프리뷰, 설치 횟수 기반 비용 계산을 관리한다.
/// </summary>
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

    private readonly Dictionary<TurretShopEntrySO, int> placedCountsByEntry = new Dictionary<TurretShopEntrySO, int>();
    private TurretPlacementPreview preview;
    private TurretShopEntrySO activeShopEntry;
    private TurretBaseSlot hoveredSlot;
    private Material runtimeValidPreviewMaterial;
    private Material runtimeInvalidPreviewMaterial;

    public event Action<TurretShopEntrySO> OnPlacementCountChanged;

    public bool IsPlacing
    {
        get
        {
            return activeShopEntry != null;
        }
    }

    // 런타임 시작 시 프리뷰와 카메라 참조를 초기화한다
    private void Awake()
    {
        preview = new TurretPlacementPreview();
        EnsureRuntimePreviewMaterials();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    // 비활성화 시 진행 중인 배치를 취소한다
    private void OnDisable()
    {
        CancelPlacement();
    }

    // 파괴 시 런타임 프리뷰 머티리얼을 정리한다
    private void OnDestroy()
    {
        DestroyRuntimePreviewMaterials();
    }

    // 월드 클릭 배치 입력을 처리한다
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

    // 배치 엔트리로 터렛 배치 프리뷰를 시작한다
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

    // 포인터 위치에 맞춰 배치 프리뷰 상태를 갱신한다
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

    // 현재 포인터 위치의 슬롯에 터렛 배치를 시도한다
    public bool EndPlacement(Vector2 screenPosition)
    {
        if (!IsPlacing)
        {
            return false;
        }

        TurretShopEntrySO shopEntry = activeShopEntry;
        ResourceCost[] placementCosts = GetCurrentPlacementCosts(shopEntry);
        TurretBaseSlot targetSlot = FindSlot(screenPosition, out _);
        bool placed = targetSlot != null && targetSlot.TryPlace(shopEntry, placementCosts, out _);
        if (placed)
        {
            RegisterPlacement(shopEntry);
        }

        CancelPlacement();
        return placed;
    }

    // 진행 중인 배치 프리뷰를 취소한다
    public void CancelPlacement()
    {
        activeShopEntry = null;
        hoveredSlot = null;
        if (preview != null)
        {
            preview.Hide();
        }
    }

    // 현재 설치 횟수 기준의 배치 비용을 반환한다
    public ResourceCost[] GetCurrentPlacementCosts(TurretShopEntrySO shopEntry)
    {
        if (shopEntry == null)
        {
            return Array.Empty<ResourceCost>();
        }

        return shopEntry.GetPlacementCosts(GetPlacedCount(shopEntry));
    }

    // 지정한 엔트리로 성공 설치한 횟수를 반환한다
    public int GetPlacedCount(TurretShopEntrySO shopEntry)
    {
        if (shopEntry == null)
        {
            return 0;
        }

        return placedCountsByEntry.TryGetValue(shopEntry, out int count) ? count : 0;
    }

    // 성공한 배치 횟수를 기록하고 관련 UI에 변경을 알린다
    private void RegisterPlacement(TurretShopEntrySO shopEntry)
    {
        if (shopEntry == null)
        {
            return;
        }

        int nextCount = GetPlacedCount(shopEntry) + 1;
        placedCountsByEntry[shopEntry] = nextCount;
        OnPlacementCountChanged?.Invoke(shopEntry);
    }

    // 프리뷰 객체와 런타임 머티리얼을 준비한다
    private void EnsurePreview()
    {
        if (preview == null)
        {
            preview = new TurretPlacementPreview();
        }

        EnsureRuntimePreviewMaterials();
    }

    // 유효 배치 프리뷰 머티리얼을 반환한다
    private Material GetValidPreviewMaterial()
    {
        return validPreviewMaterial != null ? validPreviewMaterial : runtimeValidPreviewMaterial;
    }

    // 무효 배치 프리뷰 머티리얼을 반환한다
    private Material GetInvalidPreviewMaterial()
    {
        return invalidPreviewMaterial != null ? invalidPreviewMaterial : runtimeInvalidPreviewMaterial;
    }

    // 인스펙터 머티리얼이 없을 때 사용할 런타임 프리뷰 머티리얼을 생성한다
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

    // 런타임에 생성한 프리뷰 머티리얼을 제거한다
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

    // 프리뷰에 사용할 투명 머티리얼을 생성한다
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

    // 지정한 머티리얼을 알파 블렌딩 프리뷰용으로 설정한다
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

    // 화면 좌표에서 배치 가능한 터렛 베이스 슬롯을 찾는다
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

    // 배치 베이스가 없을 때 월드 프리뷰 위치를 계산한다
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

    // 현재 주 입력 포인터 위치를 반환한다
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

    // 현재 프레임에 주 입력 포인터가 눌렸는지 확인한다
    private static bool WasPrimaryPointerPressed()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            return touch.phase == TouchPhase.Began;
        }

        return Input.GetMouseButtonDown(0);
    }

    // 현재 주 입력 포인터가 UI 위에 있는지 확인한다
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
