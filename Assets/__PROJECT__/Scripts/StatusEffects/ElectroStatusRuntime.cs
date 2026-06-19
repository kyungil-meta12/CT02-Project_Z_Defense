using ProjectZDefense.StatusEffects;
using UnityEngine;

/// <summary>
/// 대상 하나에 적용된 Electro Shock 스택과 스택 비주얼을 관리한다.
/// </summary>
public sealed class ElectroStatusRuntime : MonoBehaviour
{
    private const int MAX_VISUAL_STACK_COUNT = 3;

    private readonly GameObject[] shockStackVisualInstances = new GameObject[MAX_VISUAL_STACK_COUNT];
    private readonly ElectroShockStackVisualFader[] shockStackVisualFaders = new ElectroShockStackVisualFader[MAX_VISUAL_STACK_COUNT];
    private readonly ElectroShockStackVisualModeController[] shockStackVisualModeControllers = new ElectroShockStackVisualModeController[MAX_VISUAL_STACK_COUNT];
    private IDamageable damageable;
    private Collider cachedTargetCollider;
    private ElectroStatusPayload activePayload;
    private Transform visualRoot;
    private Transform cachedCameraTransform;
    private float shockStackRemainingDuration;
    private float orbitAngle;
    private int shockStackCount;
    private bool isBoss;

    public bool IsActive => shockStackCount > 0;
    public int ShockStackCount => shockStackCount;

    // Electro 런타임이 참조할 대상과 중심 위치 계산용 콜라이더를 초기화한다
    public void Initialize(IDamageable damageable_, bool isBoss_)
    {
        damageable = damageable_;
        isBoss = isBoss_;
        cachedTargetCollider = GetComponentInChildren<Collider>(true);
    }

    // Electro 투사체 또는 체인으로 전달된 Shock 스택 데이터를 갱신한다
    public void ApplyElectroStatus(ElectroStatusPayload payload, int chainIndex, float sourceDamage)
    {
        if (damageable == null || !damageable.IsAlive || !payload.hasElectroStatus)
        {
            return;
        }

        if (payload.maxShockStackCount <= 0 || payload.shockStackDuration <= 0.0f)
        {
            return;
        }

        activePayload = payload;
        int maxStackCount = Mathf.Min(MAX_VISUAL_STACK_COUNT, Mathf.Max(1, payload.maxShockStackCount));
        shockStackCount = Mathf.Min(maxStackCount, shockStackCount + 1);
        shockStackRemainingDuration = Mathf.Max(0.0f, payload.shockStackDuration);
        RefreshShockStackVisuals();
    }

    // Shock 스택 유지시간과 회전 비주얼을 갱신한다
    public void Tick(float deltaTime)
    {
        if (shockStackCount <= 0)
        {
            return;
        }

        if (damageable == null || !damageable.IsAlive)
        {
            ResetStatus();
            return;
        }

        shockStackRemainingDuration = Mathf.Max(0.0f, shockStackRemainingDuration - deltaTime);
        if (shockStackRemainingDuration <= 0.0f)
        {
            ResetStatus();
            return;
        }

        UpdateShockStackVisualPositions(deltaTime);
    }

    // 풀 재사용이나 사망 시 Shock 스택과 스택 비주얼을 초기화한다
    public void ResetStatus()
    {
        activePayload = default;
        shockStackRemainingDuration = 0.0f;
        orbitAngle = 0.0f;
        shockStackCount = 0;
        SetShockStackVisualCount(0);
    }

    // 현재 스택 수에 맞춰 비주얼 인스턴스를 준비하고 활성 개수를 맞춘다
    private void RefreshShockStackVisuals()
    {
        EnsureVisualRoot();
        EnsureShockStackVisualInstances();
        SetShockStackVisualCount(shockStackCount);
        ApplyShockStackVisualMode();
        UpdateShockStackVisualPositions(0.0f);
    }

    // 현재 스택 수에 맞춰 약한 전하 또는 완전 충전 비주얼 모드를 적용한다
    private void ApplyShockStackVisualMode()
    {
        bool shouldUseChargedMode = ShouldUseChargedShockStackVisualMode();
        for (int i = 0; i < shockStackVisualModeControllers.Length; i++)
        {
            ElectroShockStackVisualModeController modeController = shockStackVisualModeControllers[i];
            if (modeController == null)
            {
                continue;
            }

            modeController.ApplyChargedMode(shouldUseChargedMode);
        }
    }

    // 현재 Shock 스택이 완전 충전 연출 조건을 만족하는지 확인한다
    private bool ShouldUseChargedShockStackVisualMode()
    {
        if (!activePayload.useShockStackChargedVisualMode)
        {
            return true;
        }

        int threshold = Mathf.Max(1, activePayload.chargedShockStackVisualThreshold);
        return shockStackCount >= threshold;
    }

    // 스택 비주얼을 대상 중심에 배치할 루트 트랜스폼을 준비한다
    private void EnsureVisualRoot()
    {
        if (visualRoot != null)
        {
            return;
        }

        GameObject visualRootObject = new GameObject("ElectroShockStackVisualRoot");
        visualRoot = visualRootObject.transform;
        visualRoot.SetParent(transform, false);
    }

    // Volt Sphere 프리팹을 최대 스택 수만큼 지연 생성한다
    private void EnsureShockStackVisualInstances()
    {
        if (activePayload.shockStackVisualPrefab == null)
        {
            return;
        }

        for (int i = 0; i < MAX_VISUAL_STACK_COUNT; i++)
        {
            if (shockStackVisualInstances[i] != null)
            {
                continue;
            }

            GameObject visualInstance = Instantiate(activePayload.shockStackVisualPrefab, visualRoot);
            visualInstance.name = activePayload.shockStackVisualPrefab.name + "_ShockStack_" + (i + 1);
            visualInstance.transform.localScale = ResolveShockStackVisualScale();
            shockStackVisualFaders[i] = EnsureShockStackVisualFader(visualInstance);
            shockStackVisualModeControllers[i] = EnsureShockStackVisualModeController(visualInstance, activePayload.subtleShockStackDisabledChildNames);
            visualInstance.SetActive(false);
            shockStackVisualInstances[i] = visualInstance;
        }
    }

    // 현재 스택 수만큼 Volt Sphere 비주얼을 켜고 나머지는 끈다
    private void SetShockStackVisualCount(int activeCount)
    {
        for (int i = 0; i < shockStackVisualInstances.Length; i++)
        {
            GameObject visualInstance = shockStackVisualInstances[i];
            if (visualInstance == null)
            {
                continue;
            }

            SetVisualActive(visualInstance, i < activeCount, true);
        }
    }

    // 대상 몸 중앙 주변으로 스택 비주얼을 분산 배치하고 회전시킨다
    private void UpdateShockStackVisualPositions(float deltaTime)
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.position = ResolveVisualCenterPosition();
        orbitAngle += activePayload.shockStackOrbitDegreesPerSecond * deltaTime;

        float orbitRadius = ResolveShockStackOrbitRadius();
        float angleStep = shockStackCount <= 0 ? 0.0f : 360.0f / shockStackCount;
        for (int i = 0; i < shockStackVisualInstances.Length; i++)
        {
            GameObject visualInstance = shockStackVisualInstances[i];
            if (visualInstance == null || i >= shockStackCount)
            {
                continue;
            }

            float angle = orbitAngle + angleStep * i;
            float angleRadians = angle * Mathf.Deg2Rad;
            Vector3 orbitOffset = new Vector3(
                Mathf.Cos(angleRadians) * orbitRadius,
                0.0f,
                Mathf.Sin(angleRadians) * orbitRadius);

            Transform visualTransform = visualInstance.transform;
            visualTransform.position = visualRoot.position + orbitOffset;
            visualTransform.rotation = Quaternion.LookRotation(orbitOffset.sqrMagnitude > 0.0001f ? orbitOffset.normalized : transform.forward, Vector3.up);
            SetVisualActive(visualInstance, ShouldShowShockStackVisual(orbitOffset), false);
            ApplyShockStackVisualAlpha(i, orbitOffset, deltaTime);
        }
    }

    // 대상 종류에 맞는 쇼크 스택 회전 반지름을 반환한다
    private float ResolveShockStackOrbitRadius()
    {
        if (isBoss && activePayload.useBossShockStackOrbitRadius)
        {
            return Mathf.Max(0.0f, activePayload.bossShockStackOrbitRadius);
        }

        return Mathf.Max(0.0f, activePayload.shockStackOrbitRadius);
    }

    // 대상 종류에 맞는 쇼크 스택 비주얼 스케일을 반환한다
    private Vector3 ResolveShockStackVisualScale()
    {
        if (isBoss && activePayload.useBossShockStackVisualScale)
        {
            return activePayload.bossShockStackVisualScale;
        }

        return activePayload.shockStackVisualScale;
    }

    // 카메라 기준 뒤쪽 반원에 있는 쇼크 스택 비주얼을 숨길지 계산한다
    private bool ShouldShowShockStackVisual(Vector3 orbitOffset)
    {
        if (!activePayload.hideBackSideShockStackVisuals || activePayload.useShockStackBackSideAlphaFade)
        {
            return true;
        }

        float cameraFacingDot;
        if (!TryCalculateCameraFacingDot(orbitOffset, out cameraFacingDot))
        {
            return true;
        }

        return cameraFacingDot >= activePayload.backSideHideDotThreshold;
    }

    // 카메라 기준 앞뒤 위치에 따라 쇼크 스택 비주얼 알파를 부드럽게 적용한다
    private void ApplyShockStackVisualAlpha(int visualIndex, Vector3 orbitOffset, float deltaTime)
    {
        ElectroShockStackVisualFader visualFader = shockStackVisualFaders[visualIndex];
        if (visualFader == null)
        {
            return;
        }

        float targetAlpha = CalculateShockStackVisualAlpha(orbitOffset);
        visualFader.ApplyAlpha(targetAlpha, activePayload.shockStackAlphaLerpSpeed, deltaTime);
    }

    // 카메라 기준 앞뒤 위치에서 목표 알파를 계산한다
    private float CalculateShockStackVisualAlpha(Vector3 orbitOffset)
    {
        if (!activePayload.useShockStackBackSideAlphaFade)
        {
            return activePayload.shockStackFrontAlpha;
        }

        float cameraFacingDot;
        if (!TryCalculateCameraFacingDot(orbitOffset, out cameraFacingDot))
        {
            return activePayload.shockStackFrontAlpha;
        }

        float normalizedFacing = Mathf.Clamp01((cameraFacingDot + 1.0f) * 0.5f);
        float shapedFacing = Mathf.Pow(normalizedFacing, activePayload.shockStackAlphaFadeSharpness);
        float backAlpha = Mathf.Min(activePayload.shockStackBackAlpha, activePayload.shockStackFrontAlpha);
        float frontAlpha = Mathf.Max(activePayload.shockStackBackAlpha, activePayload.shockStackFrontAlpha);
        return Mathf.Lerp(backAlpha, frontAlpha, shapedFacing);
    }

    // 카메라 방향과 궤도 오프셋의 내적을 계산한다
    private bool TryCalculateCameraFacingDot(Vector3 orbitOffset, out float cameraFacingDot)
    {
        cameraFacingDot = 1.0f;

        Transform cameraTransform = ResolveCameraTransform();
        if (cameraTransform == null)
        {
            return false;
        }

        Vector3 horizontalOffset = orbitOffset;
        horizontalOffset.y = 0.0f;
        if (horizontalOffset.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 toCamera = cameraTransform.position - visualRoot.position;
        toCamera.y = 0.0f;
        if (toCamera.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        cameraFacingDot = Vector3.Dot(horizontalOffset.normalized, toCamera.normalized);
        return true;
    }

    // 쇼크 스택 가림 판정에 사용할 메인 카메라 트랜스폼을 캐시한다
    private Transform ResolveCameraTransform()
    {
        if (cachedCameraTransform != null)
        {
            return cachedCameraTransform;
        }

        Camera mainCamera = Camera.main;
        cachedCameraTransform = mainCamera != null ? mainCamera.transform : null;
        return cachedCameraTransform;
    }

    // 비주얼 활성 상태를 바꾸고 필요하면 파티클을 재시작한다
    private static void SetVisualActive(GameObject visualInstance, bool shouldBeActive, bool restartOnEnable)
    {
        if (visualInstance == null || visualInstance.activeSelf == shouldBeActive)
        {
            return;
        }

        visualInstance.SetActive(shouldBeActive);
        if (restartOnEnable)
        {
            RestartParticles(visualInstance);
        }
    }

    // 쇼크 스택 비주얼 인스턴스에 알파 페이드 컴포넌트를 준비한다
    private static ElectroShockStackVisualFader EnsureShockStackVisualFader(GameObject visualInstance)
    {
        ElectroShockStackVisualFader visualFader = visualInstance.GetComponent<ElectroShockStackVisualFader>();
        if (visualFader == null)
        {
            visualFader = visualInstance.AddComponent<ElectroShockStackVisualFader>();
        }

        visualFader.Initialize();
        return visualFader;
    }

    // 쇼크 스택 비주얼 인스턴스에 충전 단계 컨트롤러를 준비한다
    private static ElectroShockStackVisualModeController EnsureShockStackVisualModeController(GameObject visualInstance, string[] subtleModeDisabledChildNames)
    {
        ElectroShockStackVisualModeController modeController = visualInstance.GetComponent<ElectroShockStackVisualModeController>();
        if (modeController == null)
        {
            modeController = visualInstance.AddComponent<ElectroShockStackVisualModeController>();
        }

        modeController.Initialize(subtleModeDisabledChildNames);
        return modeController;
    }

    // 대상 콜라이더 기준 몸 중앙 위치를 계산한다
    private Vector3 ResolveVisualCenterPosition()
    {
        Vector3 centerPosition = cachedTargetCollider != null
            ? TurretAimPointUtility.GetAimPosition(cachedTargetCollider, 0.5f)
            : transform.position + Vector3.up;

        centerPosition.y += activePayload.shockStackVerticalOffset;
        return centerPosition;
    }

    // 활성화된 파티클을 초기화하고 다시 재생한다
    private static void RestartParticles(GameObject visualInstance)
    {
        if (visualInstance == null || !visualInstance.activeSelf)
        {
            return;
        }

        ParticleSystem[] particleSystems = visualInstance.GetComponentsInChildren<ParticleSystem>(true);
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
}
