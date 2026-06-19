using UnityEngine;

/// <summary>
/// Electro Shock 스택 비주얼 인스턴스의 파티클 알파를 런타임에서 부드럽게 제어한다.
/// </summary>
public sealed class ElectroShockStackVisualFader : MonoBehaviour
{
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int TintColorPropertyId = Shader.PropertyToID("_TintColor");
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

    private ParticleSystemRenderer[] particleRenderers;
    private MaterialPropertyBlock propertyBlock;
    private Color[] baseColorValues;
    private Color[] baseTintColorValues;
    private Color[] baseBaseColorValues;
    private bool[] hasColorProperty;
    private bool[] hasTintColorProperty;
    private bool[] hasBaseColorProperty;
    private float currentAlpha = 1.0f;
    private bool isInitialized;

    // 자식 파티클 렌더러와 기본 색상 값을 캐시한다
    public void Initialize()
    {
        if (isInitialized)
        {
            return;
        }

        particleRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
        propertyBlock = new MaterialPropertyBlock();
        int rendererCount = particleRenderers.Length;
        baseColorValues = new Color[rendererCount];
        baseTintColorValues = new Color[rendererCount];
        baseBaseColorValues = new Color[rendererCount];
        hasColorProperty = new bool[rendererCount];
        hasTintColorProperty = new bool[rendererCount];
        hasBaseColorProperty = new bool[rendererCount];

        for (int i = 0; i < rendererCount; i++)
        {
            CacheRendererColorProperties(i);
        }

        isInitialized = true;
        ApplyAlphaImmediate(currentAlpha);
    }

    // 목표 알파를 현재 알파에서 부드럽게 따라가게 적용한다
    public void ApplyAlpha(float targetAlpha, float lerpSpeed, float deltaTime)
    {
        Initialize();

        float safeTargetAlpha = Mathf.Clamp01(targetAlpha);
        float safeLerpSpeed = Mathf.Max(0.0f, lerpSpeed);
        if (safeLerpSpeed <= 0.0f || deltaTime <= 0.0f)
        {
            ApplyAlphaImmediate(safeTargetAlpha);
            return;
        }

        float lerpFactor = 1.0f - Mathf.Exp(-safeLerpSpeed * deltaTime);
        float nextAlpha = Mathf.Lerp(currentAlpha, safeTargetAlpha, lerpFactor);
        ApplyAlphaImmediate(nextAlpha);
    }

    // 현재 알파를 즉시 지정한 값으로 초기화한다
    public void ApplyAlphaImmediate(float alpha)
    {
        Initialize();
        currentAlpha = Mathf.Clamp01(alpha);

        for (int i = 0; i < particleRenderers.Length; i++)
        {
            ApplyRendererAlpha(i, currentAlpha);
        }
    }

    // 지정한 렌더러가 사용하는 색상 프로퍼티와 기본 색상을 저장한다
    private void CacheRendererColorProperties(int rendererIndex)
    {
        ParticleSystemRenderer particleRenderer = particleRenderers[rendererIndex];
        if (particleRenderer == null)
        {
            return;
        }

        Material sharedMaterial = particleRenderer.sharedMaterial;
        if (sharedMaterial == null)
        {
            return;
        }

        hasColorProperty[rendererIndex] = sharedMaterial.HasProperty(ColorPropertyId);
        hasTintColorProperty[rendererIndex] = sharedMaterial.HasProperty(TintColorPropertyId);
        hasBaseColorProperty[rendererIndex] = sharedMaterial.HasProperty(BaseColorPropertyId);
        baseColorValues[rendererIndex] = hasColorProperty[rendererIndex] ? sharedMaterial.GetColor(ColorPropertyId) : Color.white;
        baseTintColorValues[rendererIndex] = hasTintColorProperty[rendererIndex] ? sharedMaterial.GetColor(TintColorPropertyId) : Color.white;
        baseBaseColorValues[rendererIndex] = hasBaseColorProperty[rendererIndex] ? sharedMaterial.GetColor(BaseColorPropertyId) : Color.white;
    }

    // 지정한 렌더러에 캐시한 기본 색상 기준 알파를 적용한다
    private void ApplyRendererAlpha(int rendererIndex, float alpha)
    {
        ParticleSystemRenderer particleRenderer = particleRenderers[rendererIndex];
        if (particleRenderer == null)
        {
            return;
        }

        particleRenderer.GetPropertyBlock(propertyBlock);
        if (hasColorProperty[rendererIndex])
        {
            propertyBlock.SetColor(ColorPropertyId, CreateAlphaColor(baseColorValues[rendererIndex], alpha));
        }

        if (hasTintColorProperty[rendererIndex])
        {
            propertyBlock.SetColor(TintColorPropertyId, CreateAlphaColor(baseTintColorValues[rendererIndex], alpha));
        }

        if (hasBaseColorProperty[rendererIndex])
        {
            propertyBlock.SetColor(BaseColorPropertyId, CreateAlphaColor(baseBaseColorValues[rendererIndex], alpha));
        }

        particleRenderer.SetPropertyBlock(propertyBlock);
    }

    // 기본 색상의 RGB는 유지하고 알파만 배율로 조정한다
    private static Color CreateAlphaColor(Color baseColor, float alpha)
    {
        baseColor.a *= alpha;
        return baseColor;
    }
}
