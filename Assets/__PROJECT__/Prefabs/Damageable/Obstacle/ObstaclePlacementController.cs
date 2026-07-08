using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 장애물/게이트 배치 입력, 프리뷰, 슬롯 확정을 관리한다.
/// </summary>
[DisallowMultipleComponent]
public class ObstaclePlacementController : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask obstacleSlotLayerMask = 1 << 9;
    [SerializeField, Min(1.0f)] private float maxRayDistance = 500.0f;

    [Header("Preview")]
    [SerializeField] private Material validPreviewMaterial;
    [SerializeField] private Material invalidPreviewMaterial;
    [SerializeField] private Color validPreviewColor = new Color(0.2f, 1.0f, 0.35f, 0.45f);
    [SerializeField] private Color invalidPreviewColor = new Color(1.0f, 0.12f, 0.08f, 0.45f);
    [SerializeField] private Vector3 previewLocalOffset = Vector3.zero;
    [SerializeField, Min(0.01f)] private float previewScaleMultiplier = 1.0f;
    [SerializeField] private bool createRuntimePreviewMaterials = true;

    [Header("Debug")]
    [SerializeField] private bool logPlacementResults = true;

    private TurretPlacementPreview preview;
    private ObstacleBuildEntrySO activeBuildEntry;
    private ObstacleBuildSlot hoveredSlot;
    private GameObject currentPreviewPrefab;
    private Material runtimeValidPreviewMaterial;
    private Material runtimeInvalidPreviewMaterial;

    // 설치 항목의 배치 성공 횟수가 바뀌어 표시 비용을 다시 계산해야 할 때 발생한다
    public event Action<ObstacleBuildEntrySO> OnPlacementCountChanged;

    public bool IsPlacing
    {
        get
        {
            return activeBuildEntry != null;
        }
    }

    // 배치 컨트롤러 시작 시 프리뷰와 카메라 참조를 준비한다
    private void Awake()
    {
        preview = new TurretPlacementPreview();
        EnsureRuntimePreviewMaterials();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    // 컨트롤러가 비활성화되면 진행 중인 배치를 취소한다
    private void OnDisable()
    {
        CancelPlacement();
    }

    // 런타임에 생성한 프리뷰 머티리얼을 정리한다
    private void OnDestroy()
    {
        DestroyRuntimePreviewMaterials();
    }

    // 배치 중 포인터 위치와 취소 입력을 처리한다
    private void Update()
    {
        if (!IsPlacing)
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

    // 선택한 빌드 항목으로 장애물 배치를 시작한다
    public void BeginPlacement(ObstacleBuildEntrySO buildEntry, Vector2 screenPosition)
    {
        if (buildEntry == null || buildEntry.ObstaclePrefab == null)
        {
            LogPlacementFailed(buildEntry, "배치 항목이 비어 있거나 장애물 프리팹이 없습니다.");
            CancelPlacement();
            return;
        }

        activeBuildEntry = buildEntry;
        EnsurePreview();
        currentPreviewPrefab = buildEntry.PreviewPrefab;
        preview.Show(currentPreviewPrefab);
        UpdatePlacement(screenPosition);
    }

    // 현재 포인터 위치에 맞춰 프리뷰와 설치 가능 상태를 갱신한다
    public void UpdatePlacement(Vector2 screenPosition)
    {
        if (!IsPlacing)
        {
            return;
        }

        hoveredSlot = FindSlot(screenPosition, out _);
        bool hasSlot = hoveredSlot != null && hoveredSlot.BuildPoint != null;
        bool canPlace = hasSlot && hoveredSlot.CanPlaceEntry(activeBuildEntry);

        if (!hasSlot)
        {
            preview.SetVisible(false);
            return;
        }

        int previewLevel = hoveredSlot.GetPlacementLevelForEntry(activeBuildEntry);
        GameObject previewPrefab = activeBuildEntry.GetPreviewPrefabForLevel(previewLevel);
        if (previewPrefab != currentPreviewPrefab)
        {
            currentPreviewPrefab = previewPrefab;
            preview.Show(currentPreviewPrefab);
        }

        preview.SetVisible(true);
        preview.SnapTo(hoveredSlot.BuildPoint, previewLocalOffset, activeBuildEntry.GetPlacementLocalRotationForLevel(previewLevel), previewScaleMultiplier);
        preview.SetVisualState(canPlace, GetValidPreviewMaterial(), GetInvalidPreviewMaterial(), validPreviewColor, invalidPreviewColor);
    }

    // 현재 포인터 위치의 슬롯에 배치를 확정한다
    public bool EndPlacement(Vector2 screenPosition)
    {
        if (!IsPlacing)
        {
            return false;
        }

        ObstacleBuildSlot targetSlot = FindSlot(screenPosition, out _);
        if (targetSlot == null)
        {
            // 슬롯을 찾지 못한 입력 단계 실패는 컨트롤러에서 기록하고, 슬롯 내부 조건 실패는 ObstacleBuildSlot.TryPlace에서 기록한다.
            LogPlacementFailed(activeBuildEntry, "포인터 위치에서 설치 슬롯을 찾을 수 없습니다.");
            CancelPlacement();
            return false;
        }

        ObstacleBuildEntrySO placedBuildEntry = activeBuildEntry;
        bool placed = targetSlot.TryPlace(activeBuildEntry, out _);
        CancelPlacement();

        if (placed)
        {
            // 설치 성공으로 다음 설치 비용이 올라갔을 수 있으므로 구독 중인 상점 UI에 알린다.
            OnPlacementCountChanged?.Invoke(placedBuildEntry);
        }

        return placed;
    }

    // 현재 설치 횟수 기준의 배치 비용을 반환한다
    public ResourceCost[] GetCurrentPlacementCosts(ObstacleBuildEntrySO buildEntry)
    {
        if (buildEntry == null)
        {
            return Array.Empty<ResourceCost>();
        }

        int firstPlacementCount = GameManager.Inst != null ? GameManager.Inst.GetFirstPlacementCount(buildEntry) : 0;
        return buildEntry.GetPlacementCosts(firstPlacementCount, isRebuild: false);
    }

    // 진행 중인 배치와 프리뷰 상태를 초기화한다
    public void CancelPlacement()
    {
        activeBuildEntry = null;
        hoveredSlot = null;
        currentPreviewPrefab = null;
        if (preview != null)
        {
            preview.Hide();
        }
    }

    // 프리뷰 객체와 런타임 머티리얼이 준비되었는지 확인한다
    private void EnsurePreview()
    {
        if (preview == null)
        {
            preview = new TurretPlacementPreview();
        }

        EnsureRuntimePreviewMaterials();
    }

    // 유효 슬롯 프리뷰에 사용할 머티리얼을 반환한다
    private Material GetValidPreviewMaterial()
    {
        return validPreviewMaterial != null ? validPreviewMaterial : runtimeValidPreviewMaterial;
    }

    // 무효 슬롯 프리뷰에 사용할 머티리얼을 반환한다
    private Material GetInvalidPreviewMaterial()
    {
        return invalidPreviewMaterial != null ? invalidPreviewMaterial : runtimeInvalidPreviewMaterial;
    }

    // 인스펙터 머티리얼이 없으면 런타임 프리뷰 머티리얼을 생성한다
    private void EnsureRuntimePreviewMaterials()
    {
        if (!createRuntimePreviewMaterials)
        {
            return;
        }

        if (runtimeValidPreviewMaterial == null)
        {
            runtimeValidPreviewMaterial = CreatePreviewMaterial("Runtime Valid Obstacle Preview", validPreviewColor);
        }

        if (runtimeInvalidPreviewMaterial == null)
        {
            runtimeInvalidPreviewMaterial = CreatePreviewMaterial("Runtime Invalid Obstacle Preview", invalidPreviewColor);
        }
    }

    // 컨트롤러가 만든 런타임 프리뷰 머티리얼을 제거한다
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

    // 지정 색상으로 투명 프리뷰 머티리얼을 생성한다
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

    // 생성한 프리뷰 머티리얼을 투명 렌더링 상태로 설정한다
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

    // 화면 좌표에서 설치 슬롯을 레이캐스트로 찾는다
    private ObstacleBuildSlot FindSlot(Vector2 screenPosition, out RaycastHit hit)
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
        if (!Physics.Raycast(ray, out hit, maxRayDistance, obstacleSlotLayerMask, QueryTriggerInteraction.Collide))
        {
            return null;
        }

        return hit.collider.GetComponentInParent<ObstacleBuildSlot>();
    }

    // 배치 컨트롤러 단계에서 발생한 배치 실패 사유를 콘솔에 출력하고, 플레이어에게도 경고 팝업으로 알린다
    private void LogPlacementFailed(ObstacleBuildEntrySO buildEntry, string reason)
    {
        if (logPlacementResults)
        {
            string entryName = buildEntry == null ? "없음" : buildEntry.DisplayName;
            Debug.LogWarning($"[ObstaclePlacementController] 배치 실패 - 항목: {entryName}, 사유: {reason}", this);
        }

        WarningPopupManager.ShowWarning("장애물 설치 실패");
    }

    // 마우스 또는 첫 번째 터치의 화면 좌표를 가져온다
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

    // 마우스 또는 터치의 기본 누름 입력이 시작됐는지 확인한다
    private static bool WasPrimaryPointerPressed()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            return touch.phase == TouchPhase.Began;
        }

        return Input.GetMouseButtonDown(0);
    }

    // 현재 포인터가 UI 위에 있는지 확인한다
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
