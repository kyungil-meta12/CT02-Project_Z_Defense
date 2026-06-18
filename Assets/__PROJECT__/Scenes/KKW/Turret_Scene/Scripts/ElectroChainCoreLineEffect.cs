using UnityEngine;
using ProjectZDefense.StatusEffects;

/// <summary>
/// Electro 체인 파티클의 시작점과 끝점을 정확히 이어주는 얇은 코어 라인을 렌더링한다.
/// </summary>
[DisallowMultipleComponent]
public class ElectroChainCoreLineEffect : MonoBehaviour
{
    private LineRenderer lineRenderer;

    // 비활성화될 때 코어 라인을 숨긴다
    private void OnDisable()
    {
        DisableLine();
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
        lineRenderer.sharedMaterial = ResolveCoreLineMaterial(payload);
        float lineWidth = Mathf.Max(0.001f, ResolveCoreLineWidth(payload));
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = ResolveCoreLineStartColor(payload);
        lineRenderer.endColor = ResolveCoreLineEndColor(payload);
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
