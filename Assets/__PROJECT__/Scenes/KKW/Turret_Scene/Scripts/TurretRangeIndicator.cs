using UnityEngine;

/// <summary>
/// 선택된 터렛의 현재 사거리를 Game 뷰에서 원형 라인으로 표시한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretRangeIndicator : MonoBehaviour
{
    private const int MIN_SEGMENTS = 12;
    private const float MIN_RADIUS = 0.01f;

    private LineRenderer lineRenderer;
    private Material runtimeMaterial;
    private Vector3[] circlePoints;

    // 컴포넌트 생성 시 라인 렌더러를 준비한다
    private void Awake()
    {
        EnsureLineRenderer();
        Hide();
    }

    // 파괴 시 런타임 머티리얼을 정리한다
    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }
    }

    // 지정한 중심과 반경으로 사거리 원을 표시한다
    public void Show(Vector3 center, float radius, int segments, float lineWidth, float yOffset, Color color)
    {
        EnsureLineRenderer();

        float safeRadius = Mathf.Max(MIN_RADIUS, radius);
        int safeSegments = Mathf.Max(MIN_SEGMENTS, segments);
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

        ApplyMaterialColor(color);
        lineRenderer.positionCount = pointCount;
        lineRenderer.startWidth = Mathf.Max(0.001f, lineWidth);
        lineRenderer.endWidth = lineRenderer.startWidth;
        lineRenderer.SetPositions(circlePoints);
        lineRenderer.enabled = true;
    }

    // 사거리 원 표시를 숨긴다
    public void Hide()
    {
        EnsureLineRenderer();
        lineRenderer.enabled = false;
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
