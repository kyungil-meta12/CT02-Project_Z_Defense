using UnityEngine;
using ProjectZDefense.StatusEffects;

/// <summary>
/// Electro 체인 파티클의 시작점과 끝점을 정확히 이어주는 얇은 코어 라인을 렌더링한다.
/// </summary>
[DisallowMultipleComponent]
public class ElectroChainCoreLineEffect : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Material sourceLineMaterial;
    private Material runtimeLineMaterial;

    // 비활성화될 때 코어 라인을 숨긴다
    private void OnDisable()
    {
        DisableLine();
    }

    // 파괴될 때 런타임 머티리얼 인스턴스를 정리한다
    private void OnDestroy()
    {
        if (runtimeLineMaterial != null)
        {
            Destroy(runtimeLineMaterial);
            runtimeLineMaterial = null;
            sourceLineMaterial = null;
        }
    }

    // 코어 라인 설정을 적용하고 시작점과 끝점을 갱신한다
    public void Apply(ElectroStatusPayload payload, Vector3 startPosition, Vector3 endPosition)
    {
        Apply(payload, startPosition, endPosition, 0.0f);
    }

    // 코어 라인 타이밍을 확인한 뒤 설정과 시작점 및 끝점을 갱신한다
    public void Apply(ElectroStatusPayload payload, Vector3 startPosition, Vector3 endPosition, float elapsedTime)
    {
        if (!CanShowCoreLine(payload, elapsedTime))
        {
            DisableLine();
            return;
        }

        EnsureLineRenderer();
        if (lineRenderer == null)
        {
            return;
        }

        float verticalOffset = ResolveCoreLineVerticalOffset(payload);
        ConfigureLineRenderer(payload);
        lineRenderer.SetPosition(0, startPosition + Vector3.up * verticalOffset);
        lineRenderer.SetPosition(1, endPosition + Vector3.up * verticalOffset);
        lineRenderer.enabled = true;
    }

    // 코어 라인을 비활성화한다
    public void DisableLine()
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }

    // LineRenderer 컴포넌트를 캐시하거나 생성한다
    private void EnsureLineRenderer()
    {
        if (lineRenderer != null)
        {
            return;
        }

        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.numCapVertices = 2;
        lineRenderer.numCornerVertices = 0;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
    }

    // LineRenderer의 색상, 굵기, 머티리얼을 현재 프로필 값으로 갱신한다
    private void ConfigureLineRenderer(ElectroStatusPayload payload)
    {
        lineRenderer.sharedMaterial = ResolveRuntimeLineMaterial(payload);
        float lineWidth = Mathf.Max(0.001f, ResolveCoreLineWidth(payload));
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = ResolveCoreLineStartColor(payload);
        lineRenderer.endColor = ResolveCoreLineEndColor(payload);
        lineRenderer.sortingOrder = 100;
    }

    // 현재 체인 코어 라인을 재생할지 반환한다
    private static bool ResolvePlayCoreLine(ElectroStatusPayload payload)
    {
        return payload.sourceProfile != null ? payload.sourceProfile.playChainCoreLine : payload.playChainCoreLine;
    }

    // 현재 시간에 코어 라인을 보여줄 수 있는지 확인한다
    private static bool CanShowCoreLine(ElectroStatusPayload payload, float elapsedTime)
    {
        if (!ResolvePlayCoreLine(payload))
        {
            return false;
        }

        float startDelay = ResolveCoreLineStartDelay(payload);
        if (elapsedTime < startDelay)
        {
            return false;
        }

        float duration = ResolveCoreLineDuration(payload);
        return duration <= 0.0f || elapsedTime <= startDelay + duration;
    }

    // 현재 사용할 코어 라인 머티리얼을 반환한다
    private static Material ResolveCoreLineMaterial(ElectroStatusPayload payload)
    {
        return payload.sourceProfile != null ? payload.sourceProfile.chainCoreLineMaterial : payload.chainCoreLineMaterial;
    }

    // 코어라인이 씬 지형에 가려지지 않도록 런타임 전용 머티리얼을 반환한다
    private Material ResolveRuntimeLineMaterial(ElectroStatusPayload payload)
    {
        Material resolvedMaterial = ResolveCoreLineMaterial(payload);
        if (resolvedMaterial == null)
        {
            return null;
        }

        if (runtimeLineMaterial == null || sourceLineMaterial != resolvedMaterial)
        {
            if (runtimeLineMaterial != null)
            {
                Destroy(runtimeLineMaterial);
            }

            sourceLineMaterial = resolvedMaterial;
            runtimeLineMaterial = new Material(resolvedMaterial);
            runtimeLineMaterial.name = resolvedMaterial.name + "_RuntimeOverlay";
        }

        ConfigureOverlayMaterial(runtimeLineMaterial);
        return runtimeLineMaterial;
    }

    // 머티리얼의 depth test를 항상 통과하도록 런타임 값만 보정한다
    private static void ConfigureOverlayMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_ZTest"))
        {
            material.SetFloat("_ZTest", 8.0f);
        }

        if (material.HasProperty("_Zwrite"))
        {
            material.SetFloat("_Zwrite", 0.0f);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0.0f);
        }

        if (material.HasProperty("_SoftParticles"))
        {
            material.SetFloat("_SoftParticles", 0.0f);
        }

        material.renderQueue = 5000;
    }

    // 현재 사용할 코어 라인 시작 색상을 반환한다
    private static Color ResolveCoreLineStartColor(ElectroStatusPayload payload)
    {
        return payload.sourceProfile != null ? payload.sourceProfile.chainCoreLineStartColor : payload.chainCoreLineStartColor;
    }

    // 현재 사용할 코어 라인 끝 색상을 반환한다
    private static Color ResolveCoreLineEndColor(ElectroStatusPayload payload)
    {
        return payload.sourceProfile != null ? payload.sourceProfile.chainCoreLineEndColor : payload.chainCoreLineEndColor;
    }

    // 현재 사용할 코어 라인 굵기를 반환한다
    private static float ResolveCoreLineWidth(ElectroStatusPayload payload)
    {
        return payload.sourceProfile != null ? payload.sourceProfile.chainCoreLineWidth : payload.chainCoreLineWidth;
    }

    // 현재 사용할 코어 라인 시작 지연을 반환한다
    private static float ResolveCoreLineStartDelay(ElectroStatusPayload payload)
    {
        return payload.sourceProfile != null ? payload.sourceProfile.chainCoreLineStartDelay : payload.chainCoreLineStartDelay;
    }

    // 현재 사용할 코어 라인 유지 시간을 반환한다
    private static float ResolveCoreLineDuration(ElectroStatusPayload payload)
    {
        return payload.sourceProfile != null ? payload.sourceProfile.chainCoreLineDuration : payload.chainCoreLineDuration;
    }

    // 현재 사용할 코어 라인 높이 보정을 반환한다
    private static float ResolveCoreLineVerticalOffset(ElectroStatusPayload payload)
    {
        return payload.sourceProfile != null ? payload.sourceProfile.chainLinkVerticalOffset : payload.chainLinkVerticalOffset;
    }
}
