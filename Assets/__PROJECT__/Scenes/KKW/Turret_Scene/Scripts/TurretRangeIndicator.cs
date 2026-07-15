using UnityEngine;

/// <summary>
/// 선택된 터렛의 현재 사거리를 Game 뷰에서 보정 가능한 프리팹 비주얼로 표시한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretRangeIndicator : MonoBehaviour
{
    private const int MIN_SEGMENTS = 12;
    private const float MIN_RADIUS = 0.01f;

    [Header("프리팹 표시")]
    [SerializeField] private GameObject indicatorPrefab;
    [SerializeField, Min(0.001f)] private float prefabRadiusAtScaleOne = 1.0f;
    [Tooltip("파티클 링처럼 실제 보이는 반경이 기준 반경보다 작을 때 추가로 키우는 배율입니다.")]
    [SerializeField, Min(0.01f)] private float prefabVisualScaleMultiplier = 1.0f;
    [Tooltip("Canvas 부모 스케일 영향을 피하기 위해 표시 프리팹을 월드 공간에 생성합니다.")]
    [SerializeField] private bool instantiatePrefabInWorldSpace = true;
    [SerializeField] private bool forcePrefabParticleLoop = true;
    [SerializeField] private bool restartPrefabParticlesOnShow = true;
    [Tooltip("표시 프리팹이 비어 있을 때만 LineRenderer 대체 표시를 사용합니다.")]
    [SerializeField] private bool useLineFallbackWhenPrefabMissing = true;

    [Header("라인 표시")]
    [Tooltip("프리팹 보정 중 정확한 사거리 경계선을 함께 표시합니다. 보정 후에는 끕니다.")]
    [SerializeField] private bool showLineWithPrefab;
    [SerializeField, Min(12)] private int lineSegments = 96;
    [SerializeField, Min(0.001f)] private float lineWidth = 0.08f;
    [SerializeField] private float yOffset = 0.05f;
    [SerializeField] private Color lineColor = new Color(0.2f, 0.85f, 1.0f, 0.65f);

    private LineRenderer lineRenderer;
    private Material runtimeMaterial;
    private Vector3[] circlePoints;
    private GameObject prefabInstance;
    private Transform prefabTransform;
    private ParticleSystem[] prefabParticles;
    private GameObject cachedPrefab;
    private bool cachedWorldSpaceMode;

    // 컴포넌트 생성 시 사거리 표시를 숨김 상태로 준비한다
    private void Awake()
    {
        Hide();
    }

    // 파괴 시 런타임 머티리얼과 프리팹 인스턴스를 정리한다
    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }

        if (prefabInstance != null)
        {
            Destroy(prefabInstance);
            prefabInstance = null;
        }
    }

    // 지정한 중심과 반경으로 사거리 원을 표시한다
    public void Show(Vector3 center, float radius)
    {
        if (indicatorPrefab != null)
        {
            ShowPrefab(center, radius, yOffset);
            if (showLineWithPrefab)
            {
                ShowLine(center, radius);
            }
            else
            {
                HideLineRenderer();
            }

            return;
        }

        if (!useLineFallbackWhenPrefabMissing)
        {
            Hide();
            return;
        }

        ShowLine(center, radius);
    }

    // 사거리 원 표시를 숨긴다
    public void Hide()
    {
        HidePrefab();
        HideLineRenderer();
    }

    // 프리팹 인스턴스를 현재 사거리 기준 위치와 스케일로 표시한다
    private void ShowPrefab(Vector3 center, float radius, float yOffset)
    {
        EnsurePrefabInstance();
        if (prefabInstance == null || prefabTransform == null)
        {
            return;
        }

        float safeRadius = Mathf.Max(MIN_RADIUS, radius);
        float safePrefabRadius = Mathf.Max(MIN_RADIUS, prefabRadiusAtScaleOne);
        float scale = safeRadius / safePrefabRadius * Mathf.Max(0.01f, prefabVisualScaleMultiplier);

        prefabTransform.position = center + Vector3.up * yOffset;
        ApplyPrefabWorldScale(scale);
        prefabInstance.SetActive(true);

        if (restartPrefabParticlesOnShow)
        {
            RestartPrefabParticles();
        }
    }

    // 프리팹 보정 확인 또는 프리팹 누락 대체용 사거리 원을 표시한다
    private void ShowLine(Vector3 center, float radius)
    {
        EnsureLineRenderer();

        float safeRadius = Mathf.Max(MIN_RADIUS, radius);
        int safeSegments = Mathf.Max(MIN_SEGMENTS, lineSegments);
        int pointCount = safeSegments + 1;
        EnsurePointBuffer(pointCount);

        Vector3 drawCenter = center + Vector3.up * yOffset;
        float angleStep = Mathf.PI * 2.0f / safeSegments;
        for (int i = 0; i < pointCount; i++)
        {
            float angle = angleStep * i;
            circlePoints[i] = new Vector3(
                drawCenter.x + Mathf.Cos(angle) * safeRadius,
                drawCenter.y,
                drawCenter.z + Mathf.Sin(angle) * safeRadius);
        }

        ApplyMaterialColor(lineColor);
        lineRenderer.positionCount = pointCount;
        lineRenderer.startWidth = Mathf.Max(0.001f, lineWidth);
        lineRenderer.endWidth = lineRenderer.startWidth;
        lineRenderer.SetPositions(circlePoints);
        lineRenderer.enabled = true;
    }

    // 프리팹 인스턴스와 파티클 참조를 준비한다
    private void EnsurePrefabInstance()
    {
        if (prefabInstance != null && cachedPrefab == indicatorPrefab && cachedWorldSpaceMode == instantiatePrefabInWorldSpace)
        {
            return;
        }

        DestroyPrefabInstance();
        if (indicatorPrefab == null)
        {
            return;
        }

        prefabInstance = instantiatePrefabInWorldSpace ? Instantiate(indicatorPrefab) : Instantiate(indicatorPrefab, transform);
        prefabInstance.name = indicatorPrefab.name;
        prefabTransform = prefabInstance.transform;
        prefabTransform.position = Vector3.zero;
        prefabTransform.localRotation = Quaternion.identity;
        prefabTransform.localScale = Vector3.one;
        prefabParticles = prefabInstance.GetComponentsInChildren<ParticleSystem>(true);
        cachedPrefab = indicatorPrefab;
        cachedWorldSpaceMode = instantiatePrefabInWorldSpace;
        ApplyPrefabParticleLoopSetting();
    }

    // 부모 Canvas 스케일과 무관하게 프리팹의 실제 월드 반경 스케일을 맞춘다
    private void ApplyPrefabWorldScale(float scale)
    {
        if (prefabTransform.parent == null)
        {
            prefabTransform.localScale = Vector3.one * scale;
            return;
        }

        Vector3 parentScale = prefabTransform.parent.lossyScale;
        prefabTransform.localScale = new Vector3(
            scale / Mathf.Max(MIN_RADIUS, Mathf.Abs(parentScale.x)),
            scale / Mathf.Max(MIN_RADIUS, Mathf.Abs(parentScale.y)),
            scale / Mathf.Max(MIN_RADIUS, Mathf.Abs(parentScale.z)));
    }

    // 라인 렌더러와 기본 표시 설정을 준비한다
    private void EnsureLineRenderer()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.alignment = LineAlignment.View;
    }

    // 프리팹 파티클 루프 설정을 런타임 표시 정책에 맞춘다
    private void ApplyPrefabParticleLoopSetting()
    {
        if (!forcePrefabParticleLoop || prefabParticles == null)
        {
            return;
        }

        for (int i = 0; i < prefabParticles.Length; i++)
        {
            ParticleSystem particle = prefabParticles[i];
            if (particle == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particle.main;
            main.loop = true;
        }
    }

    // 프리팹 파티클을 처음부터 다시 재생한다
    private void RestartPrefabParticles()
    {
        if (prefabParticles == null)
        {
            return;
        }

        for (int i = 0; i < prefabParticles.Length; i++)
        {
            ParticleSystem particle = prefabParticles[i];
            if (particle == null)
            {
                continue;
            }

            particle.Clear(true);
            particle.Play(true);
        }
    }

    // 프리팹 인스턴스를 비활성화한다
    private void HidePrefab()
    {
        if (prefabInstance != null)
        {
            prefabInstance.SetActive(false);
        }
    }

    // 라인 렌더러 표시를 비활성화한다
    private void HideLineRenderer()
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }

    // 생성된 프리팹 인스턴스와 캐시를 제거한다
    private void DestroyPrefabInstance()
    {
        if (prefabInstance != null)
        {
            Destroy(prefabInstance);
        }

        prefabInstance = null;
        prefabTransform = null;
        prefabParticles = null;
        cachedPrefab = null;
        cachedWorldSpaceMode = false;
    }

    // 원 좌표를 담을 버퍼 크기를 보장한다
    private void EnsurePointBuffer(int pointCount)
    {
        if (circlePoints == null || circlePoints.Length != pointCount)
        {
            circlePoints = new Vector3[pointCount];
        }
    }

    // 런타임 머티리얼을 만들고 색상을 적용한다
    private void ApplyMaterialColor(Color color)
    {
        if (runtimeMaterial == null)
        {
            runtimeMaterial = CreateLineMaterial(color);
            lineRenderer.sharedMaterial = runtimeMaterial;
        }

        runtimeMaterial.color = color;
        if (runtimeMaterial.HasProperty("_BaseColor"))
        {
            runtimeMaterial.SetColor("_BaseColor", color);
        }
    }

    // 투명 라인에 사용할 머티리얼을 생성한다
    private static Material CreateLineMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.name = "Runtime Turret Range Indicator";
        material.color = color;
        ConfigureTransparentMaterial(material, color);
        return material;
    }

    // 머티리얼을 투명 렌더링용으로 설정한다
    private static void ConfigureTransparentMaterial(Material material, Color color)
    {
        material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1.0f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0.0f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0.0f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}
