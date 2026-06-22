using UnityEngine;

/// <summary>
/// 상태이상 비주얼 인스턴스의 파티클 알파를 페이드 인/아웃으로 조절한다.
/// </summary>
public sealed class StatusEffectVisualAlphaFader : MonoBehaviour
{
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

    private ParticleSystem[] particleSystems;
    private ParticleSystem.MinMaxGradient[] originalStartColors;
    private ParticleSystem.Particle[][] particleBuffers;
    private byte[][] fadeStartParticleAlphas;
    private OverlayFX[] overlayFxComponents;
    private Material[] overlayMaterials;
    private Color[] originalOverlayColors;
    private float fadeStartAlpha;
    private float fadeTargetAlpha;
    private float fadeElapsed;
    private float fadeDuration;
    private float currentAlpha = 1.0f;
    private Renderer pendingRestoreRenderer;
    private Material[] pendingRestoreMaterials;
    private bool deactivateOnComplete;
    private bool isFading;

    // 컴포넌트 생성 시 파티클과 알파 버퍼를 캐시한다
    private void Awake()
    {
        CacheComponents();
    }

    // 페이드가 진행 중일 때 파티클 알파를 갱신한다
    private void Update()
    {
        if (!isFading)
        {
            return;
        }

        fadeElapsed += Time.deltaTime;
        float normalizedTime = fadeDuration <= 0.0f ? 1.0f : Mathf.Clamp01(fadeElapsed / fadeDuration);
        currentAlpha = Mathf.Lerp(fadeStartAlpha, fadeTargetAlpha, normalizedTime);
        ApplyParticleAlpha(currentAlpha);

        if (normalizedTime >= 1.0f)
        {
            CompleteFade();
        }
    }

    // 비주얼을 지정 시간 동안 0%에서 100% 알파로 표시한다
    public void PlayFadeIn(float duration, bool restartParticles)
    {
        EnsureCached();
        ClearPendingMaterialRestore();
        if (restartParticles)
        {
            RestoreParticleAlpha();
            RestartParticles();
            CaptureFadeStartAlpha();
            BeginFade(0.0f, 1.0f, duration, false);
            return;
        }

        CaptureFadeStartAlpha();
        BeginFade(currentAlpha, 1.0f, duration, false);
    }

    // 비주얼을 지정 시간 동안 100%에서 0% 알파로 숨긴 뒤 비활성화한다
    public void PlayFadeOut(float duration)
    {
        PlayFadeOut(duration, null, null);
    }

    // 비주얼을 숨긴 뒤 지정 렌더러의 원본 머티리얼을 복구한다
    public void PlayFadeOut(float duration, Renderer restoreRenderer, Material[] restoreMaterials)
    {
        EnsureCached();
        if (!gameObject.activeSelf)
        {
            return;
        }

        pendingRestoreRenderer = restoreRenderer;
        pendingRestoreMaterials = restoreMaterials;
        CaptureFadeStartAlpha();
        BeginFade(currentAlpha, 0.0f, duration, true);
    }

    // 페이드 상태를 시작하고 즉시 첫 알파 값을 적용한다
    private void BeginFade(float startAlpha, float targetAlpha, float duration, bool deactivateWhenComplete)
    {
        fadeStartAlpha = Mathf.Clamp01(startAlpha);
        fadeTargetAlpha = Mathf.Clamp01(targetAlpha);
        fadeDuration = Mathf.Max(0.0f, duration);
        fadeElapsed = 0.0f;
        deactivateOnComplete = deactivateWhenComplete;
        isFading = fadeDuration > 0.0f;
        currentAlpha = fadeStartAlpha;
        ApplyParticleAlpha(currentAlpha);

        if (!isFading)
        {
            currentAlpha = fadeTargetAlpha;
            ApplyParticleAlpha(currentAlpha);
            CompleteFade();
        }
    }

    // 페이드 완료 후 알파 복구와 비활성화 처리를 수행한다
    private void CompleteFade()
    {
        isFading = false;
        currentAlpha = fadeTargetAlpha;

        if (Mathf.Approximately(currentAlpha, 1.0f))
        {
            RestoreParticleAlpha();
        }

        if (deactivateOnComplete)
        {
            RestoreParticleAlpha();
            RestorePendingRendererMaterials();
            gameObject.SetActive(false);
        }
    }

    // 예약된 RendererOverlay 원본 머티리얼 복구 정보를 초기화한다
    private void ClearPendingMaterialRestore()
    {
        pendingRestoreRenderer = null;
        pendingRestoreMaterials = null;
    }

    // 페이드 완료 후 RendererOverlay 대상의 원본 머티리얼을 복구한다
    private void RestorePendingRendererMaterials()
    {
        if (pendingRestoreRenderer == null || pendingRestoreMaterials == null)
        {
            ClearPendingMaterialRestore();
            return;
        }

        pendingRestoreRenderer.sharedMaterials = pendingRestoreMaterials;
        ClearPendingMaterialRestore();
    }

    // 파티클 시스템과 페이드 버퍼가 없으면 다시 캐시한다
    private void EnsureCached()
    {
        if (particleSystems == null || originalStartColors == null)
        {
            CacheComponents();
        }
    }

    // 자식 파티클 시스템과 원본 시작 색상 및 살아있는 파티클 버퍼를 캐시한다
    private void CacheComponents()
    {
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        originalStartColors = new ParticleSystem.MinMaxGradient[particleSystems.Length];
        particleBuffers = new ParticleSystem.Particle[particleSystems.Length][];
        fadeStartParticleAlphas = new byte[particleSystems.Length][];
        CacheOverlayMaterials();

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule mainModule = particleSystem.main;
            originalStartColors[i] = mainModule.startColor;
            int maxParticles = Mathf.Max(1, mainModule.maxParticles);
            particleBuffers[i] = new ParticleSystem.Particle[maxParticles];
            fadeStartParticleAlphas[i] = new byte[maxParticles];
        }
    }

    // OverlayFX 머티리얼과 원본 색상을 캐시한다
    private void CacheOverlayMaterials()
    {
        overlayFxComponents = GetComponentsInChildren<OverlayFX>(true);
        overlayMaterials = new Material[overlayFxComponents.Length];
        originalOverlayColors = new Color[overlayFxComponents.Length];

        for (int i = 0; i < overlayFxComponents.Length; i++)
        {
            OverlayFX overlayFx = overlayFxComponents[i];
            if (overlayFx == null || overlayFx.overlayMaterial == null)
            {
                continue;
            }

            overlayMaterials[i] = overlayFx.overlayMaterial;
            originalOverlayColors[i] = GetMaterialColor(overlayMaterials[i]);
        }
    }

    // 모든 파티클 시스템을 초기화하고 다시 재생한다
    private void RestartParticles()
    {
        if (particleSystems == null)
        {
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }

    // 파티클 시스템 시작 색상 알파를 원본 값으로 복구한다
    private void RestoreParticleAlpha()
    {
        if (particleSystems == null || originalStartColors == null)
        {
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule mainModule = particleSystem.main;
            mainModule.startColor = originalStartColors[i];
        }

        RestoreOverlayMaterialAlpha();
    }

    // 모든 파티클 시스템의 시작 색상과 살아있는 파티클 알파를 지정 비율로 조절한다
    private void ApplyParticleAlpha(float alphaMultiplier)
    {
        if (particleSystems == null || originalStartColors == null)
        {
            return;
        }

        float safeAlphaMultiplier = Mathf.Clamp01(alphaMultiplier);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule mainModule = particleSystem.main;
            mainModule.startColor = ScaleGradientAlpha(originalStartColors[i], safeAlphaMultiplier);
            ApplyLiveParticleAlpha(particleSystem, i, safeAlphaMultiplier);
        }

        ApplyOverlayMaterialAlpha(safeAlphaMultiplier);
    }

    // OverlayFX 머티리얼 알파를 원본 값으로 복구한다
    private void RestoreOverlayMaterialAlpha()
    {
        if (overlayMaterials == null || originalOverlayColors == null)
        {
            return;
        }

        int count = Mathf.Min(overlayMaterials.Length, originalOverlayColors.Length);
        for (int i = 0; i < count; i++)
        {
            Material material = overlayMaterials[i];
            if (material == null)
            {
                continue;
            }

            SetMaterialColor(material, originalOverlayColors[i]);
        }
    }

    // OverlayFX 머티리얼의 원본 알파를 지정 비율로 조절한다
    private void ApplyOverlayMaterialAlpha(float alphaMultiplier)
    {
        if (overlayMaterials == null || originalOverlayColors == null)
        {
            return;
        }

        int count = Mathf.Min(overlayMaterials.Length, originalOverlayColors.Length);
        for (int i = 0; i < count; i++)
        {
            Material material = overlayMaterials[i];
            if (material == null)
            {
                continue;
            }

            Color color = originalOverlayColors[i];
            color.a *= alphaMultiplier;
            SetMaterialColor(material, color);
        }
    }

    // 페이드 시작 순간 살아있는 파티클의 알파를 저장한다
    private void CaptureFadeStartAlpha()
    {
        if (particleSystems == null || particleBuffers == null || fadeStartParticleAlphas == null)
        {
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            ParticleSystem.Particle[] particles = particleBuffers[i];
            byte[] alphas = fadeStartParticleAlphas[i];
            if (particleSystem == null || particles == null || alphas == null)
            {
                continue;
            }

            int particleCount = particleSystem.GetParticles(particles);
            for (int particleIndex = 0; particleIndex < particleCount; particleIndex++)
            {
                alphas[particleIndex] = particles[particleIndex].startColor.a;
            }
        }
    }

    // 현재 살아있는 파티클 색상 알파를 페이드 시작 알파 기준으로 보정한다
    private void ApplyLiveParticleAlpha(ParticleSystem particleSystem, int particleSystemIndex, float alphaMultiplier)
    {
        if (particleBuffers == null || fadeStartParticleAlphas == null || particleSystemIndex < 0 || particleSystemIndex >= particleBuffers.Length)
        {
            return;
        }

        ParticleSystem.Particle[] particles = particleBuffers[particleSystemIndex];
        byte[] alphas = fadeStartParticleAlphas[particleSystemIndex];
        if (particles == null || alphas == null)
        {
            return;
        }

        int particleCount = particleSystem.GetParticles(particles);
        for (int i = 0; i < particleCount; i++)
        {
            Color32 startColor = particles[i].startColor;
            startColor.a = (byte)Mathf.RoundToInt(alphas[i] * alphaMultiplier);
            particles[i].startColor = startColor;
        }

        particleSystem.SetParticles(particles, particleCount);
    }

    // MinMaxGradient 모드에 맞춰 알파만 조절한 색상 데이터를 반환한다
    private ParticleSystem.MinMaxGradient ScaleGradientAlpha(ParticleSystem.MinMaxGradient sourceGradient, float alphaMultiplier)
    {
        switch (sourceGradient.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(ScaleColorAlpha(sourceGradient.color, alphaMultiplier));
            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(
                    ScaleColorAlpha(sourceGradient.colorMin, alphaMultiplier),
                    ScaleColorAlpha(sourceGradient.colorMax, alphaMultiplier));
            default:
                Color fallbackColor = ScaleColorAlpha(sourceGradient.color, alphaMultiplier);
                return new ParticleSystem.MinMaxGradient(fallbackColor);
        }
    }

    // 색상의 RGB는 유지하고 알파만 지정 비율로 낮춘다
    private Color ScaleColorAlpha(Color color, float alphaMultiplier)
    {
        color.a *= alphaMultiplier;
        return color;
    }

    // 머티리얼에서 사용하는 색상 프로퍼티 값을 반환한다
    private static Color GetMaterialColor(Material material)
    {
        if (material.HasProperty(BaseColorPropertyId))
        {
            return material.GetColor(BaseColorPropertyId);
        }

        if (material.HasProperty(ColorPropertyId))
        {
            return material.GetColor(ColorPropertyId);
        }

        return Color.white;
    }

    // 머티리얼에서 사용하는 색상 프로퍼티 값을 설정한다
    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty(BaseColorPropertyId))
        {
            material.SetColor(BaseColorPropertyId, color);
        }

        if (material.HasProperty(ColorPropertyId))
        {
            material.SetColor(ColorPropertyId, color);
        }
    }
}
