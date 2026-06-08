using UnityEngine;

[DisallowMultipleComponent]
public class HelicopterMissileSkillCaster : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform runtimeContainer;

    private HelicopterMissileSkillDefinitionSO currentDefinition;
    private HelicopterMissileSkillLevelData currentLevelData;
    private GameObject activePreview;
    private Vector3 currentAreaCenter;
    private Quaternion currentAreaRotation;
    private bool hasValidPlacement;

    public bool IsPlacing => currentDefinition != null;

    // 기본 카메라 참조를 캐시한다.
    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    // 스킬 범위 지정 모드를 시작한다.
    public void BeginPlacement(HelicopterMissileSkillDefinitionSO definition, int level, Vector2 screenPosition)
    {
        CancelPlacement();

        if (definition == null)
        {
            Debug.LogWarning("[헬기 스킬] 스킬 데이터가 없어 범위 지정을 시작할 수 없습니다.", this);
            return;
        }

        currentDefinition = definition;
        currentLevelData = definition.GetLevelData(level);
        CreatePreview();
        UpdatePlacement(screenPosition);
    }

    // 드래그 위치 기준으로 범위 프리뷰를 갱신한다.
    public void UpdatePlacement(Vector2 screenPosition)
    {
        if (currentDefinition == null)
        {
            return;
        }

        hasValidPlacement = TryResolvePlacement(screenPosition, out currentAreaCenter, out currentAreaRotation);
        RefreshPreview();
    }

    // 현재 지정 위치에 스킬을 발동한다.
    public bool TryCast(Vector2 screenPosition)
    {
        if (currentDefinition == null)
        {
            return false;
        }

        UpdatePlacement(screenPosition);

        if (!hasValidPlacement)
        {
            Debug.LogWarning("[헬기 스킬] 유효한 지면 위치를 찾지 못해 스킬을 발동하지 않았습니다.", this);
            CancelPlacement();
            return false;
        }

        SpawnRuntime();
        CancelPlacement();
        return true;
    }

    // 진행 중인 범위 지정을 취소한다.
    public void CancelPlacement()
    {
        currentDefinition = null;
        currentLevelData = null;
        hasValidPlacement = false;

        if (activePreview != null)
        {
            Destroy(activePreview);
            activePreview = null;
        }
    }

    // 범위 표시 프리팹을 생성한다.
    private void CreatePreview()
    {
        if (currentDefinition.RangePreviewPrefab == null)
        {
            return;
        }

        activePreview = Instantiate(currentDefinition.RangePreviewPrefab);
        activePreview.name = $"{currentDefinition.RangePreviewPrefab.name}_SkillPreview";
    }

    // 범위 표시 위치와 스케일을 현재 데이터에 맞춘다.
    private void RefreshPreview()
    {
        if (activePreview == null || currentDefinition == null || currentLevelData == null)
        {
            return;
        }

        Vector3 previewPosition = currentAreaCenter + Vector3.up * currentDefinition.PreviewHeightOffset;
        activePreview.transform.SetPositionAndRotation(previewPosition, currentAreaRotation);
        activePreview.transform.localScale = new Vector3(
            currentLevelData.AreaWidth * currentDefinition.PreviewLocalScaleMultiplier.x,
            currentDefinition.PreviewLocalScaleMultiplier.y,
            currentLevelData.AreaLength * currentDefinition.PreviewLocalScaleMultiplier.z);
    }

    // 화면 좌표를 월드 범위 중심점과 회전으로 변환한다.
    private bool TryResolvePlacement(Vector2 screenPosition, out Vector3 areaCenter, out Quaternion areaRotation)
    {
        areaCenter = Vector3.zero;
        areaRotation = Quaternion.identity;

        if (targetCamera == null)
        {
            Debug.LogWarning("[헬기 스킬] 타겟 카메라가 없어 화면 좌표를 월드 좌표로 변환할 수 없습니다.", this);
            return false;
        }

        Ray ray = targetCamera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, currentDefinition.PlacementLayerMask, QueryTriggerInteraction.Collide))
        {
            areaCenter = hit.point;
        }
        else
        {
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, currentDefinition.FallbackGroundY, 0f));
            if (!groundPlane.Raycast(ray, out float enter))
            {
                return false;
            }

            areaCenter = ray.GetPoint(enter);
        }

        areaRotation = ResolveAreaRotation();
        return true;
    }

    // 카메라 기준 또는 고정값 기준으로 길쭉한 범위 방향을 계산한다.
    private Quaternion ResolveAreaRotation()
    {
        if (!currentDefinition.AlignAreaToCameraForward || targetCamera == null)
        {
            return Quaternion.Euler(currentDefinition.FixedAreaRotationEuler);
        }

        Vector3 forward = targetCamera.transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
        {
            return Quaternion.Euler(currentDefinition.FixedAreaRotationEuler);
        }

        return Quaternion.LookRotation(forward.normalized, Vector3.up) * Quaternion.Euler(currentDefinition.FixedAreaRotationEuler);
    }

    // 헬기 미사일 스킬 런타임 오브젝트를 생성한다.
    private void SpawnRuntime()
    {
        GameObject runtimeObject = new GameObject($"{currentDefinition.DisplayName}_Runtime");
        if (runtimeContainer != null)
        {
            runtimeObject.transform.SetParent(runtimeContainer);
        }

        HelicopterMissileSkillRuntime runtime = runtimeObject.AddComponent<HelicopterMissileSkillRuntime>();
        runtime.Initialize(currentDefinition, currentLevelData, targetCamera, currentAreaCenter, currentAreaRotation);
    }
}
