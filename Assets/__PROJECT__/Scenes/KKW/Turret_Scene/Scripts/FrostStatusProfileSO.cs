using UnityEngine;

/// <summary>
/// Frost 터렛의 슬로우, 빙결, 폭발 효과 기본값과 체력비례 데미지 성장을 관리하는 상태이상 프로필.
/// </summary>
[CreateAssetMenu(menuName = "Project Z Defense/Frost Status Profile")]
public class FrostStatusProfileSO : ScriptableObject
{
    [Header("슬로우 기본값")]
    [Min(0.0f)] public float slowBuildUpDuration = 0.0f;
    [Range(0.0f, 1.0f)] public float maxSlowRatio = 0.0f;
    [Min(0.0f)] public float slowHoldDuration = 0.0f;
    [Range(0.0f, 1.0f)] public float freezeTriggerRatio = 0.9f;

    [Header("빙결 기본값")]
    [Min(0.0f)] public float freezeDuration = 0.0f;
    public GameObject freezeEffectPrefab;
    [Min(0.0f)] public float freezeEffectDuration = 5.5f;
    [Min(0.0f)] public float freezeExplosionDamageDelay = 2.2f;
    [Min(0.0f)] public float freezeCooldownPerTarget = 0.0f;

    [Header("폭발 기본값")]
    [Min(0.0f)] public float freezeExplosionRadius = 0.0f;
    [Min(0.0f)] public float freezeExplosionDamage = 0.0f;
    [Range(0.0f, 1.0f)] public float freezePrimaryTargetMaxHpDamageRatio = 0.1f;
    public LayerMask freezeExplosionLayerMask = Physics.DefaultRaycastLayers;
    [Range(0.0f, 1.0f)] public float freezeExplosionSlowRatio = 0.3f;
    [Min(0.0f)] public float freezeExplosionSlowDuration = 1.0f;

    [Header("체력비례 데미지 성장")]
    public float freezePrimaryTargetMaxHpDamageRatioPerLevel = 0.0f;
    [Range(0.0f, 1.0f)] public float maxFreezePrimaryTargetMaxHpDamageRatio = 1.0f;

    public bool HasFrostStatus
    {
        get
        {
            return maxSlowRatio > 0.0f && (slowBuildUpDuration > 0.0f || slowHoldDuration > 0.0f)
                || freezeDuration > 0.0f
                || freezeEffectPrefab != null
                || freezeExplosionDamage > 0.0f
                || freezePrimaryTargetMaxHpDamageRatio > 0.0f
                || freezePrimaryTargetMaxHpDamageRatioPerLevel > 0.0f
                || freezeExplosionSlowRatio > 0.0f && freezeExplosionSlowDuration > 0.0f;
        }
    }

    // 현재 터렛 레벨 기준으로 Frost 상태 전달 값을 계산한다
    public FrostStatusPayload CreatePayload(int level, float tickInterval)
    {
        int completedLevel = Mathf.Max(0, level - 1);

        FrostStatusPayload payload = new FrostStatusPayload
        {
            tickInterval = Mathf.Max(0.0f, tickInterval),
            slowBuildUpDuration = slowBuildUpDuration,
            maxSlowRatio = maxSlowRatio,
            slowHoldDuration = slowHoldDuration,
            freezeDuration = freezeDuration,
            freezeTriggerRatio = freezeTriggerRatio,
            canTriggerFreeze = true,
            freezeEffectPrefab = freezeEffectPrefab,
            freezeEffectDuration = freezeEffectDuration,
            freezeExplosionDamageDelay = freezeExplosionDamageDelay,
            freezeExplosionRadius = freezeExplosionRadius,
            freezeExplosionDamage = freezeExplosionDamage,
            freezePrimaryTargetMaxHpDamageRatio = Mathf.Clamp(freezePrimaryTargetMaxHpDamageRatio + freezePrimaryTargetMaxHpDamageRatioPerLevel * completedLevel, 0.0f, maxFreezePrimaryTargetMaxHpDamageRatio),
            freezeExplosionLayerMask = freezeExplosionLayerMask,
            freezeCooldownPerTarget = freezeCooldownPerTarget,
            freezeExplosionSlowRatio = freezeExplosionSlowRatio,
            freezeExplosionSlowDuration = freezeExplosionSlowDuration
        };

        return payload;
    }

    // 인스펙터에서 입력한 Frost 상태 값을 안전한 범위로 보정한다
    private void OnValidate()
    {
        slowBuildUpDuration = Mathf.Max(0.0f, slowBuildUpDuration);
        maxSlowRatio = Mathf.Clamp01(maxSlowRatio);
        slowHoldDuration = Mathf.Max(0.0f, slowHoldDuration);
        freezeTriggerRatio = Mathf.Clamp01(freezeTriggerRatio);
        freezeDuration = Mathf.Max(0.0f, freezeDuration);
        freezeEffectDuration = Mathf.Max(0.0f, freezeEffectDuration);
        freezeExplosionDamageDelay = Mathf.Max(0.0f, freezeExplosionDamageDelay);
        freezeCooldownPerTarget = Mathf.Max(0.0f, freezeCooldownPerTarget);
        freezeExplosionRadius = Mathf.Max(0.0f, freezeExplosionRadius);
        freezeExplosionDamage = Mathf.Max(0.0f, freezeExplosionDamage);
        freezePrimaryTargetMaxHpDamageRatio = Mathf.Clamp01(freezePrimaryTargetMaxHpDamageRatio);
        freezeExplosionSlowRatio = Mathf.Clamp01(freezeExplosionSlowRatio);
        freezeExplosionSlowDuration = Mathf.Max(0.0f, freezeExplosionSlowDuration);
        maxFreezePrimaryTargetMaxHpDamageRatio = Mathf.Clamp01(maxFreezePrimaryTargetMaxHpDamageRatio);
    }
}
