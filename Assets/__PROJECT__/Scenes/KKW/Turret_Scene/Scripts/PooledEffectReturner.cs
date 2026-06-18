using System.Collections;
using UnityEngine;

/// <summary>
/// 풀링된 이펙트의 파티클 재생, 선택적 페이드아웃, 지연 반환을 담당한다.
/// </summary>
[DisallowMultipleComponent]
public class PooledEffectReturner : MonoBehaviour
{
    private PoolObject poolObject;
    private ParticleSystem[] particleSystems;
    private ParticleSystem.MinMaxGradient[] originalStartColors;
    private ParticleSystem.Particle[][] particleBuffers;
    private byte[][] fadeStartParticleAlphas;
    private Coroutine returnRoutine;
    private bool isReturning;

    // 시작 시 풀과 파티클 참조를 캐시한다
    private void Awake()
    {
        CacheComponents();
    }

    // 활성화될 때 반환 상태와 코루틴을 초기화한다
    private void OnEnable()
    {
        isReturning = false;
        StopReturnRoutine();
    }

    // 비활성화될 때 반환 코루틴을 정리한다
    private void OnDisable()
    {
        StopReturnRoutine();
        RestoreParticleAlpha();
    }

    // 지정 시간 뒤 페이드 없이 이펙트를 반환하도록 초기화한다
    public void Init(float duration)
    {
        Init(duration, 0.0f);
    }

    // 지정 시간 뒤 선택적 페이드아웃을 거쳐 이펙트를 반환하도록 초기화한다
    public void Init(float duration, float fadeOutDuration)
    {
        isReturning = false;
        StopReturnRoutine();
        RestoreParticleAlpha();
        PlayParticles();

        if (duration <= 0.0f)
        {
            ReturnNow();
            return;
        }

        returnRoutine = StartCoroutine(ReturnAfterRoutine(duration, fadeOutDuration));
    }

    // 지정된 생존 시간 동안 대기하고 마지막 구간에 알파 페이드를 적용한다
    private IEnumerator ReturnAfterRoutine(float duration, float fadeOutDuration)
    {
        float elapsedTime = 0.0f;
        float safeFadeOutDuration = Mathf.Clamp(fadeOutDuration, 0.0f, duration);
        float fadeStartTime = duration - safeFadeOutDuration;
        bool hasCapturedFadeStartAlpha = false;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            if (safeFadeOutDuration > 0.0f && elapsedTime >= fadeStartTime)
            {
                if (!hasCapturedFadeStartAlpha)
                {
                    CaptureFadeStartAlpha();
                    hasCapturedFadeStartAlpha = true;
                }

                float fadeProgress = Mathf.Clamp01((elapsedTime - fadeStartTime) / safeFadeOutDuration);
                ApplyParticleAlpha(1.0f - fadeProgress);
            }

            yield return null;
        }

        returnRoutine = null;
        ReturnNow();
    }

    // 이펙트를 풀로 반환하거나 풀 정보가 없으면 제거한다
    private void ReturnNow()
    {
        if (isReturning)
        {
            return;
        }

        isReturning = true;
        CachePoolObject();

        if (poolObject != null && poolObject.OriginStack != null)
        {
            poolObject.ReturnToPool();
            return;
        }

        Destroy(gameObject);
    }

    // 캐시된 모든 파티클 시스템을 처음부터 재생한다
    private void PlayParticles()
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

    // 풀과 파티클 시스템 및 페이드 버퍼를 캐시한다
    private void CacheComponents()
    {
        CachePoolObject();
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        originalStartColors = new ParticleSystem.MinMaxGradient[particleSystems.Length];
        particleBuffers = new ParticleSystem.Particle[particleSystems.Length][];
        fadeStartParticleAlphas = new byte[particleSystems.Length][];

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

    // 현재 오브젝트의 PoolObject 컴포넌트를 캐시한다
    private void CachePoolObject()
    {
        if (poolObject == null)
        {
            poolObject = GetComponent<PoolObject>();
        }
    }

    // 실행 중인 반환 코루틴을 중지한다
    private void StopReturnRoutine()
    {
        if (returnRoutine == null)
        {
            return;
        }

        StopCoroutine(returnRoutine);
        returnRoutine = null;
    }

    // 파티클 시스템의 시작 색상 알파를 원래 값으로 복원한다
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
    }

    // 모든 파티클 시스템의 시작 색상과 살아있는 파티클 알파를 지정 비율로 낮춘다
    private void ApplyParticleAlpha(float alphaMultiplier)
    {
        if (particleSystems == null)
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

    // MinMaxGradient 모드에 맞춰 알파만 낮춘 색상 데이터를 반환한다
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
}
