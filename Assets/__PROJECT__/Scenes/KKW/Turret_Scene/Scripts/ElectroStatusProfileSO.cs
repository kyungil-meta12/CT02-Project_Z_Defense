using UnityEngine;
using ProjectZDefense.StatusEffects;

/// <summary>
/// Electro 터렛의 체인 라이트닝, 쇼크 스택, 과부하, 경직 기본값을 관리하는 상태이상 프로필.
/// </summary>
[CreateAssetMenu(menuName = "Project Z Defense/Electro Status Profile")]
public class ElectroStatusProfileSO : ScriptableObject
{
    [Header("체인 라이트닝")]
    [Min(1)] public int maxChainTargets = 5;
    [Min(0.0f)] public float chainRadius = 4.0f;
    [Range(0.0f, 1.0f)] public float chainDamageFalloffPerJump = 0.15f;
    public LayerMask chainTargetLayerMask = Physics.DefaultRaycastLayers;

    [Header("쇼크 스택")]
    [Min(1)] public int maxShockStackCount = 3;
    [Min(0.0f)] public float shockStackDuration = 15.0f;

    [Header("쇼크 스택 VFX")]
    public GameObject shockStackVisualPrefab;
    [Min(0.0f)] public float shockStackOrbitRadius = 0.55f;
    [Min(0.0f)] public float shockStackVerticalOffset = 0.1f;
    public float shockStackOrbitDegreesPerSecond = 180.0f;
    public Vector3 shockStackVisualScale = Vector3.one;
    public bool hideBackSideShockStackVisuals;
    [Range(-1.0f, 1.0f)] public float backSideHideDotThreshold;

    [Header("쇼크 스택 알파 페이드")]
    public bool useShockStackBackSideAlphaFade = true;
    [Range(0.0f, 1.0f)] public float shockStackFrontAlpha = 1.0f;
    [Range(0.0f, 1.0f)] public float shockStackBackAlpha = 0.16f;
    [Min(0.01f)] public float shockStackAlphaFadeSharpness = 1.25f;
    [Min(0.0f)] public float shockStackAlphaLerpSpeed = 14.0f;

    [Header("쇼크 스택 충전 연출")]
    public bool useShockStackChargedVisualMode = true;
    [Min(1)] public int chargedShockStackVisualThreshold = 3;
    public string[] subtleShockStackDisabledChildNames = { "TriangleShape", "Flare", "Volt" };

    [Header("보스 쇼크 스택 VFX")]
    public bool useBossShockStackOrbitRadius = true;
    [Min(0.0f)] public float bossShockStackOrbitRadius = 1.1f;
    public bool useBossShockStackVisualScale = true;
    public Vector3 bossShockStackVisualScale = Vector3.one;

    [Header("오버로드 발동 정책")]
    public bool canElectroHitTriggerOverload;
    public bool canNonElectroDamageTriggerOverload = true;

    [Header("과부하")]
    [Min(0.0f)] public float overloadRadius = 2.0f;
    [Min(0.0f)] public float overloadDamageMultiplier = 1.0f;

    [Header("경직")]
    [Min(0.0f)] public float stunDuration = 0.35f;
    [Range(0.0f, 1.0f)] public float stunDurationFalloffPerJump = 0.1f;
    [Min(0.0f)] public float minimumStunDuration = 0.1f;
    [Range(0.0f, 1.0f)] public float bossHitStunDurationMultiplier = 0.0f;
    [Range(0.0f, 1.0f)] public float bossStunDurationMultiplier = 0.0f;

    [Header("체인 링크 VFX")]
    public bool playChainLinkEffect = true;
    public GameObject chainLinkEffectPrefab;
    [Min(0.01f)] public float chainLinkEffectDuration = 0.65f;
    [Min(0.0f)] public float chainLinkVerticalOffset = 0.15f;

    [Header("체인 링크 위치 보정")]
    public Vector3 chainLinkSourceAxis = Vector3.forward;
    public bool useChainLinkEndpointFit = true;
    public Vector3 chainLinkLocalStartPoint = Vector3.zero;
    public Vector3 chainLinkLocalEndPoint = new Vector3(0.0f, 0.0f, 15.0f);
    public Vector3 chainLinkLocalPositionOffset = Vector3.zero;
    public Vector3 chainLinkRotationEulerOffset = Vector3.zero;

    [Header("체인 링크 스케일")]
    [Min(0.01f)] public float chainLinkLengthScaleMultiplier = 0.55f;
    [Min(0.01f)] public float chainLinkThicknessScale = 0.55f;

    [Header("체인 코어 라인 VFX")]
    public bool playChainCoreLine = true;
    public Material chainCoreLineMaterial;
    public Color chainCoreLineStartColor = new Color(0.7f, 1.0f, 1.0f, 0.95f);
    public Color chainCoreLineEndColor = new Color(1.0f, 0.7f, 1.0f, 0.85f);
    [Min(0.001f)] public float chainCoreLineWidth = 0.06f;
    [Min(0.0f)] public float chainCoreLineStartDelay = 0.28f;
    [Min(0.0f)] public float chainCoreLineDuration = 0.25f;

    public bool HasElectroStatus
    {
        get
        {
            return maxChainTargets > 0 && chainRadius > 0.0f
                || maxShockStackCount > 0 && shockStackDuration > 0.0f
                || overloadRadius > 0.0f && overloadDamageMultiplier > 0.0f
                || stunDuration > 0.0f;
        }
    }

    // 현재 프로필 값을 Electro 상태 전달 값으로 변환한다
    public ElectroStatusPayload CreatePayload()
    {
        ElectroStatusPayload payload = new ElectroStatusPayload
        {
            hasElectroStatus = HasElectroStatus,
            maxChainTargets = Mathf.Max(1, maxChainTargets),
            chainRadius = Mathf.Max(0.0f, chainRadius),
            chainDamageFalloffPerJump = Mathf.Clamp01(chainDamageFalloffPerJump),
            chainTargetLayerMask = chainTargetLayerMask,
            maxShockStackCount = Mathf.Max(1, maxShockStackCount),
            shockStackDuration = Mathf.Max(0.0f, shockStackDuration),
            shockStackVisualPrefab = shockStackVisualPrefab,
            shockStackOrbitRadius = Mathf.Max(0.0f, shockStackOrbitRadius),
            shockStackVerticalOffset = Mathf.Max(0.0f, shockStackVerticalOffset),
            shockStackOrbitDegreesPerSecond = shockStackOrbitDegreesPerSecond,
            shockStackVisualScale = GetSafeShockStackVisualScale(),
            useBossShockStackOrbitRadius = useBossShockStackOrbitRadius,
            bossShockStackOrbitRadius = Mathf.Max(0.0f, bossShockStackOrbitRadius),
            useBossShockStackVisualScale = useBossShockStackVisualScale,
            bossShockStackVisualScale = GetSafeBossShockStackVisualScale(),
            hideBackSideShockStackVisuals = hideBackSideShockStackVisuals,
            backSideHideDotThreshold = Mathf.Clamp(backSideHideDotThreshold, -1.0f, 1.0f),
            useShockStackBackSideAlphaFade = useShockStackBackSideAlphaFade,
            shockStackFrontAlpha = Mathf.Clamp01(shockStackFrontAlpha),
            shockStackBackAlpha = Mathf.Clamp01(shockStackBackAlpha),
            shockStackAlphaFadeSharpness = Mathf.Max(0.01f, shockStackAlphaFadeSharpness),
            shockStackAlphaLerpSpeed = Mathf.Max(0.0f, shockStackAlphaLerpSpeed),
            useShockStackChargedVisualMode = useShockStackChargedVisualMode,
            chargedShockStackVisualThreshold = Mathf.Max(1, chargedShockStackVisualThreshold),
            subtleShockStackDisabledChildNames = subtleShockStackDisabledChildNames,
            canElectroHitTriggerOverload = canElectroHitTriggerOverload,
            canNonElectroDamageTriggerOverload = canNonElectroDamageTriggerOverload,
            overloadRadius = Mathf.Max(0.0f, overloadRadius),
            overloadDamageMultiplier = Mathf.Max(0.0f, overloadDamageMultiplier),
            stunDuration = Mathf.Max(0.0f, stunDuration),
            stunDurationFalloffPerJump = Mathf.Clamp01(stunDurationFalloffPerJump),
            minimumStunDuration = Mathf.Max(0.0f, minimumStunDuration),
            bossHitStunDurationMultiplier = Mathf.Clamp01(bossHitStunDurationMultiplier),
            bossStunDurationMultiplier = Mathf.Clamp01(bossStunDurationMultiplier),
            playChainLinkEffect = playChainLinkEffect,
            sourceProfile = this,
            chainLinkEffectPrefab = chainLinkEffectPrefab,
            chainLinkEffectDuration = Mathf.Max(0.01f, chainLinkEffectDuration),
            chainLinkVerticalOffset = Mathf.Max(0.0f, chainLinkVerticalOffset),
            chainLinkSourceAxis = GetSafeChainLinkSourceAxis(),
            useChainLinkEndpointFit = useChainLinkEndpointFit,
            chainLinkLocalStartPoint = chainLinkLocalStartPoint,
            chainLinkLocalEndPoint = chainLinkLocalEndPoint,
            chainLinkLocalPositionOffset = chainLinkLocalPositionOffset,
            chainLinkRotationEulerOffset = chainLinkRotationEulerOffset,
            chainLinkLengthScaleMultiplier = Mathf.Max(0.01f, chainLinkLengthScaleMultiplier),
            chainLinkThicknessScale = Mathf.Max(0.01f, chainLinkThicknessScale),
            playChainCoreLine = playChainCoreLine,
            chainCoreLineMaterial = chainCoreLineMaterial,
            chainCoreLineStartColor = chainCoreLineStartColor,
            chainCoreLineEndColor = chainCoreLineEndColor,
            chainCoreLineWidth = Mathf.Max(0.001f, chainCoreLineWidth),
            chainCoreLineStartDelay = Mathf.Max(0.0f, chainCoreLineStartDelay),
            chainCoreLineDuration = Mathf.Max(0.0f, chainCoreLineDuration)
        };

        return payload;
    }

    // 지정한 체인 순번에서 적용할 데미지 배율을 계산한다
    public float CalculateChainDamageMultiplier(int chainIndex)
    {
        int safeChainIndex = Mathf.Max(0, chainIndex);
        float multiplier = 1.0f - chainDamageFalloffPerJump * safeChainIndex;
        return Mathf.Max(0.0f, multiplier);
    }

    // 지정한 체인 순번에서 적용할 경직 시간을 계산한다
    public float CalculateStunDuration(int chainIndex, bool isBoss)
    {
        int safeChainIndex = Mathf.Max(0, chainIndex);
        float stunMultiplier = 1.0f - stunDurationFalloffPerJump * safeChainIndex;
        float calculatedDuration = stunDuration * Mathf.Max(0.0f, stunMultiplier);
        float clampedDuration = Mathf.Max(minimumStunDuration, calculatedDuration);
        return isBoss ? clampedDuration * bossStunDurationMultiplier : clampedDuration;
    }

    // 인스펙터에서 입력한 Electro 상태 값을 안전한 범위로 보정한다
    private void OnValidate()
    {
        maxChainTargets = Mathf.Max(1, maxChainTargets);
        chainRadius = Mathf.Max(0.0f, chainRadius);
        chainDamageFalloffPerJump = Mathf.Clamp01(chainDamageFalloffPerJump);
        maxShockStackCount = Mathf.Max(1, maxShockStackCount);
        shockStackDuration = Mathf.Max(0.0f, shockStackDuration);
        shockStackOrbitRadius = Mathf.Max(0.0f, shockStackOrbitRadius);
        shockStackVerticalOffset = Mathf.Max(0.0f, shockStackVerticalOffset);
        shockStackVisualScale = GetSafeShockStackVisualScale();
        bossShockStackOrbitRadius = Mathf.Max(0.0f, bossShockStackOrbitRadius);
        bossShockStackVisualScale = GetSafeBossShockStackVisualScale();
        backSideHideDotThreshold = Mathf.Clamp(backSideHideDotThreshold, -1.0f, 1.0f);
        shockStackFrontAlpha = Mathf.Clamp01(shockStackFrontAlpha);
        shockStackBackAlpha = Mathf.Clamp01(shockStackBackAlpha);
        shockStackAlphaFadeSharpness = Mathf.Max(0.01f, shockStackAlphaFadeSharpness);
        shockStackAlphaLerpSpeed = Mathf.Max(0.0f, shockStackAlphaLerpSpeed);
        chargedShockStackVisualThreshold = Mathf.Max(1, chargedShockStackVisualThreshold);
        overloadRadius = Mathf.Max(0.0f, overloadRadius);
        overloadDamageMultiplier = Mathf.Max(0.0f, overloadDamageMultiplier);
        stunDuration = Mathf.Max(0.0f, stunDuration);
        stunDurationFalloffPerJump = Mathf.Clamp01(stunDurationFalloffPerJump);
        minimumStunDuration = Mathf.Clamp(minimumStunDuration, 0.0f, stunDuration);
        bossHitStunDurationMultiplier = Mathf.Clamp01(bossHitStunDurationMultiplier);
        bossStunDurationMultiplier = Mathf.Clamp01(bossStunDurationMultiplier);
        chainLinkEffectDuration = Mathf.Max(0.01f, chainLinkEffectDuration);
        chainLinkVerticalOffset = Mathf.Max(0.0f, chainLinkVerticalOffset);
        chainLinkSourceAxis = NormalizeSafeAxis(chainLinkSourceAxis);
        chainLinkLengthScaleMultiplier = Mathf.Max(0.01f, chainLinkLengthScaleMultiplier);
        chainLinkThicknessScale = Mathf.Max(0.01f, chainLinkThicknessScale);
        chainCoreLineWidth = Mathf.Max(0.001f, chainCoreLineWidth);
        chainCoreLineStartDelay = Mathf.Max(0.0f, chainCoreLineStartDelay);
        chainCoreLineDuration = Mathf.Max(0.0f, chainCoreLineDuration);
    }

    // 체인 링크 프리팹의 기준 축을 안전한 단위 벡터로 반환한다
    private Vector3 GetSafeChainLinkSourceAxis()
    {
        return NormalizeSafeAxis(chainLinkSourceAxis);
    }

    // 쇼크 스택 비주얼 스케일을 0 이하로 눌리지 않게 보정한다
    private Vector3 GetSafeShockStackVisualScale()
    {
        return new Vector3(
            Mathf.Max(0.01f, shockStackVisualScale.x),
            Mathf.Max(0.01f, shockStackVisualScale.y),
            Mathf.Max(0.01f, shockStackVisualScale.z));
    }

    // 보스 쇼크 스택 비주얼 스케일을 0 이하로 눌리지 않게 보정한다
    private Vector3 GetSafeBossShockStackVisualScale()
    {
        return new Vector3(
            Mathf.Max(0.01f, bossShockStackVisualScale.x),
            Mathf.Max(0.01f, bossShockStackVisualScale.y),
            Mathf.Max(0.01f, bossShockStackVisualScale.z));
    }

    // 입력 축이 너무 작으면 전기 프리팹 기본 길이 방향인 Z축을 사용한다
    private static Vector3 NormalizeSafeAxis(Vector3 axis)
    {
        if (axis.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return axis.normalized;
    }
}
