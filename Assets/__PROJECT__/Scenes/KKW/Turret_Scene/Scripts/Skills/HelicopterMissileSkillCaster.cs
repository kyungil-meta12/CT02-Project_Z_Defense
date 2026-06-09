using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class HelicopterMissileSkillCaster : MonoBehaviour
{
    private const float MAX_RAY_DISTANCE = 500f;

    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform runtimeContainer;

    [Header("Runtime Pool")]
    [Min(0)] [SerializeField] private int prewarmRuntimeCount = 1;

    private HelicopterMissileSkillDefinitionSO currentDefinition;
    private HelicopterMissileSkillLevelData currentLevelData;
    private readonly Queue<HelicopterMissileSkillRuntime> runtimePool = new Queue<HelicopterMissileSkillRuntime>(2);
    private GameObject activePreview;
    private Renderer[] activePreviewRenderers;
    private ParticleSystem[] activePreviewParticleSystems;
    private ParticleSystem.MinMaxGradient[] originalPreviewParticleStartColors;
    private MaterialPropertyBlock previewPropertyBlock;
    private Vector3 currentAreaCenter;
    private Quaternion currentAreaRotation;
    private float cooldownRemaining;
    private float currentCooldownDuration;
    private bool hasValidPlacement;

    public bool IsPlacing => currentDefinition != null;
    public float CooldownRemaining => cooldownRemaining;
    public float CooldownRatio => currentCooldownDuration > 0f ? cooldownRemaining / currentCooldownDuration : 0f;

    // 기본 카메라 참조를 캐시한다.
    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        PrewarmRuntimePool();
    }

    // 스킬 쿨타임을 진행한다.
    private void Update()
    {
        UpdateCooldown();
    }

    // 보관 중인 스킬 런타임을 정리한다.
    private void OnDestroy()
    {
        while (runtimePool.Count > 0)
        {
            HelicopterMissileSkillRuntime runtime = runtimePool.Dequeue();
            if (runtime != null)
            {
                Destroy(runtime.gameObject);
            }
        }
    }

    // 스킬 범위 지정 모드를 시작한다.
    public bool BeginPlacement(HelicopterMissileSkillDefinitionSO definition, int level, Vector2 screenPosition)
    {
        CancelPlacement();

        if (definition == null)
        {
            Debug.LogWarning("[헬기 스킬] 스킬 데이터가 없어 범위 지정을 시작할 수 없습니다.", this);
            return false;
        }

        if (!CanUseSkill())
        {
            return false;
        }

        currentDefinition = definition;
        currentLevelData = definition.GetLevelData(level);
        CreatePreview();
        UpdatePlacement(screenPosition);
        return true;
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
        StartCooldown();
        CancelPlacement();
        return true;
    }

    // 진행 중인 범위 지정을 취소한다.
    public void CancelPlacement()
    {
        if (activePreview != null)
        {
            RestoreOriginalPreviewVisualState();
            PooledObjectUtility.ReturnOrDestroy(activePreview);
            activePreview = null;
        }

        currentDefinition = null;
        currentLevelData = null;
        hasValidPlacement = false;
        activePreviewRenderers = null;
        activePreviewParticleSystems = null;
        originalPreviewParticleStartColors = null;
    }

    // 현재 스킬을 사용할 수 있는지 확인한다.
    public bool CanUseSkill()
    {
        return cooldownRemaining <= 0f;
    }

    // 현재 스킬 데이터 기준으로 쿨타임을 시작한다.
    private void StartCooldown()
    {
        currentCooldownDuration = currentDefinition != null ? Mathf.Max(0f, currentDefinition.Cooldown) : 0f;
        cooldownRemaining = currentCooldownDuration;
    }

    // 쿨타임 남은 시간을 감소시킨다.
    private void UpdateCooldown()
    {
        if (cooldownRemaining <= 0f)
        {
            return;
        }

        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
    }

    // 범위 표시 프리팹을 생성한다.
    private void CreatePreview()
    {
        if (currentDefinition.RangePreviewPrefab == null)
        {
            return;
        }

        activePreview = PooledObjectUtility.Spawn(currentDefinition.RangePreviewPrefab, Vector3.zero, Quaternion.identity);
        if (activePreview == null)
        {
            return;
        }

        activePreview.name = $"{currentDefinition.RangePreviewPrefab.name}_SkillPreview";
        activePreviewRenderers = activePreview.GetComponentsInChildren<Renderer>(true);
        activePreviewParticleSystems = activePreview.GetComponentsInChildren<ParticleSystem>(true);
        CacheOriginalPreviewParticleColors();
        DisablePreviewColliders();
        ApplyPreviewVisualState(false);
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
        ApplyPreviewVisualState(hasValidPlacement);
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
        areaRotation = ResolveAreaRotation();

        if (Physics.Raycast(ray, out RaycastHit hit, MAX_RAY_DISTANCE, ~0, QueryTriggerInteraction.Collide))
        {
            areaCenter = hit.point;
            return IsLayerInMask(hit.collider.gameObject.layer, currentDefinition.PlacementLayerMask);
        }

        if (!TryResolveFallbackPreviewPoint(ray, out areaCenter))
        {
            return false;
        }

        return false;
    }

    // Ground 미충돌 시 무효 프리뷰를 표시할 평면 위치를 계산한다.
    private bool TryResolveFallbackPreviewPoint(Ray ray, out Vector3 areaCenter)
    {
        areaCenter = Vector3.zero;

        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, currentDefinition.FallbackGroundY, 0f));
        if (!groundPlane.Raycast(ray, out float enter))
        {
            return false;
        }

        areaCenter = ray.GetPoint(enter);
        return true;
    }

    // 레이어가 지정된 마스크에 포함되는지 확인한다.
    private bool IsLayerInMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    // 프리뷰 오브젝트가 배치 판정을 가로막지 않도록 콜라이더를 끈다.
    private void DisablePreviewColliders()
    {
        if (activePreview == null)
        {
            return;
        }

        Collider[] colliders = activePreview.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }
    }

    // 배치 가능 여부에 따라 프리뷰 색상을 갱신한다.
    private void ApplyPreviewVisualState(bool isValid)
    {
        if (activePreviewRenderers == null || currentDefinition == null)
        {
            return;
        }

        Color previewColor = isValid ? currentDefinition.ValidPreviewColor : currentDefinition.InvalidPreviewColor;
        ApplyParticlePreviewColor(isValid, previewColor);

        for (int i = 0; i < activePreviewRenderers.Length; i++)
        {
            Renderer previewRenderer = activePreviewRenderers[i];
            if (previewRenderer != null)
            {
                ApplyRendererPreviewColor(previewRenderer, previewColor);
            }
        }
    }

    // 풀링된 프리뷰가 원래 파란 표시로 복구될 수 있도록 파티클 시작 색상을 보관한다.
    private void CacheOriginalPreviewParticleColors()
    {
        if (activePreviewParticleSystems == null)
        {
            return;
        }

        originalPreviewParticleStartColors = new ParticleSystem.MinMaxGradient[activePreviewParticleSystems.Length];
        for (int i = 0; i < activePreviewParticleSystems.Length; i++)
        {
            ParticleSystem particleSystemComp = activePreviewParticleSystems[i];
            if (particleSystemComp == null)
            {
                continue;
            }

            originalPreviewParticleStartColors[i] = particleSystemComp.main.startColor;
        }
    }

    // 파티클 기반 프리뷰의 원본 머티리얼과 형태를 유지한 채 유효/무효 색상만 변경한다.
    private void ApplyParticlePreviewColor(bool isValid, Color previewColor)
    {
        if (activePreviewParticleSystems == null)
        {
            return;
        }

        for (int i = 0; i < activePreviewParticleSystems.Length; i++)
        {
            ParticleSystem particleSystemComp = activePreviewParticleSystems[i];
            if (particleSystemComp == null)
            {
                continue;
            }

            ParticleSystem.MainModule mainModule = particleSystemComp.main;
            if (isValid && originalPreviewParticleStartColors != null && i < originalPreviewParticleStartColors.Length)
            {
                mainModule.startColor = originalPreviewParticleStartColors[i];
                continue;
            }

            mainModule.startColor = previewColor;
        }
    }

    // 일반 Renderer가 섞인 프리뷰도 원본 머티리얼을 교체하지 않고 색상 프로퍼티만 덮어쓴다.
    private void ApplyRendererPreviewColor(Renderer previewRenderer, Color previewColor)
    {
        if (previewRenderer is ParticleSystemRenderer)
        {
            return;
        }

        if (previewPropertyBlock == null)
        {
            previewPropertyBlock = new MaterialPropertyBlock();
        }

        previewRenderer.GetPropertyBlock(previewPropertyBlock);
        previewPropertyBlock.SetColor("_Color", previewColor);
        previewPropertyBlock.SetColor("_BaseColor", previewColor);
        previewRenderer.SetPropertyBlock(previewPropertyBlock);
    }

    // 풀 반환 전에 프리뷰 색상 오버라이드를 원래 상태로 되돌린다.
    private void RestoreOriginalPreviewVisualState()
    {
        if (activePreviewParticleSystems != null && originalPreviewParticleStartColors != null)
        {
            for (int i = 0; i < activePreviewParticleSystems.Length; i++)
            {
                ParticleSystem particleSystemComp = activePreviewParticleSystems[i];
                if (particleSystemComp == null || i >= originalPreviewParticleStartColors.Length)
                {
                    continue;
                }

                ParticleSystem.MainModule mainModule = particleSystemComp.main;
                mainModule.startColor = originalPreviewParticleStartColors[i];
            }
        }

        if (activePreviewRenderers == null)
        {
            return;
        }

        for (int i = 0; i < activePreviewRenderers.Length; i++)
        {
            Renderer previewRenderer = activePreviewRenderers[i];
            if (previewRenderer != null)
            {
                previewRenderer.SetPropertyBlock(null);
            }
        }
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
        HelicopterMissileSkillRuntime runtime = GetRuntimeFromPool();
        runtime.Initialize(currentDefinition, currentLevelData, targetCamera, currentAreaCenter, currentAreaRotation, ReturnRuntimeToPool);
    }

    // 스킬 런타임 풀을 초기 개수만큼 준비한다.
    private void PrewarmRuntimePool()
    {
        for (int i = 0; i < prewarmRuntimeCount; i++)
        {
            HelicopterMissileSkillRuntime runtime = CreateRuntimeInstance();
            ReturnRuntimeToPool(runtime);
        }
    }

    // 풀에서 사용할 스킬 런타임 인스턴스를 만든다.
    private HelicopterMissileSkillRuntime CreateRuntimeInstance()
    {
        GameObject runtimeObject = new GameObject("Helicopter Missile_Runtime");
        if (runtimeContainer != null)
        {
            runtimeObject.transform.SetParent(runtimeContainer, false);
        }

        return runtimeObject.AddComponent<HelicopterMissileSkillRuntime>();
    }

    // 풀에서 비활성 스킬 런타임을 꺼내거나 새로 만든다.
    private HelicopterMissileSkillRuntime GetRuntimeFromPool()
    {
        while (runtimePool.Count > 0)
        {
            HelicopterMissileSkillRuntime runtime = runtimePool.Dequeue();
            if (runtime != null)
            {
                runtime.gameObject.SetActive(true);
                return runtime;
            }
        }

        HelicopterMissileSkillRuntime newRuntime = CreateRuntimeInstance();
        newRuntime.gameObject.SetActive(true);
        return newRuntime;
    }

    // 사용이 끝난 스킬 런타임을 풀로 되돌린다.
    private void ReturnRuntimeToPool(HelicopterMissileSkillRuntime runtime)
    {
        if (runtime == null)
        {
            return;
        }

        if (runtimeContainer != null)
        {
            runtime.transform.SetParent(runtimeContainer, false);
        }

        runtime.gameObject.SetActive(false);
        runtimePool.Enqueue(runtime);
    }
}
