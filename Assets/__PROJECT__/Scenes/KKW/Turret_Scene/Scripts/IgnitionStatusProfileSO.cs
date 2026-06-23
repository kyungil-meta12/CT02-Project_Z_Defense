using UnityEngine;
using ProjectZDefense.StatusEffects;

/// <summary>
/// Ignition 연소 상태를 다시 적용했을 때 지속시간과 중첩을 갱신하는 정책.
/// </summary>
public enum IgnitionStackRefreshMode
{
    RefreshDurationOnly,
    AddStackAndRefreshDuration
}

/// <summary>
/// Ignition 상태가 다른 3세대 속성과 상호작용할 수 있는 대상 속성 플래그.
/// </summary>
[System.Flags]
public enum IgnitionInteractionFlags
{
    None = 0,
    Frost = 1 << 0,
    Poison = 1 << 1,
    Electro = 1 << 2
}

/// <summary>
/// Ignition 터렛의 연소 틱데미지, 지속시간, 중첩 정책, 상호작용 플래그를 관리하는 상태이상 프로필.
/// </summary>
[CreateAssetMenu(menuName = "Project Z Defense/Ignition Status Profile")]
public class IgnitionStatusProfileSO : ScriptableObject
{
    [Header("연소 기본값")]
    [Tooltip("maxHpDamageRatioPerTick이 0일 때만 Combat.damage 기반 초당 데미지 fallback에 곱해집니다. 현재 Ignition_Turret 밸런스에서는 0으로 둡니다.")]
    [Min(0.0f)] public float damageMultiplier = 0.0f;
    [Tooltip("Ignition_Turret의 핵심 DPS 값입니다. 틱마다 대상 최대 체력에 이 비율을 곱해 피해를 줍니다.")]
    [Range(0.0f, 1.0f)] public float maxHpDamageRatioPerTick = 0.01f;
    [Tooltip("화상 틱 데미지 간격입니다. Combat.fireInterval이 아니라 이 값이 화상 DPS 주기를 결정합니다.")]
    [Min(0.01f)] public float tickInterval = 1.0f;
    [Tooltip("화상 지속시간입니다. RefreshDurationOnly 정책에서는 반복 접촉 시 이 시간이 갱신됩니다.")]
    [Min(0.0f)] public float duration = 10.0f;
    [Tooltip("화상 중첩 상한입니다. RefreshDurationOnly 정책에서는 1을 유지합니다.")]
    [Min(1)] public int maxStackCount = 1;
    public IgnitionStackRefreshMode stackRefreshMode = IgnitionStackRefreshMode.RefreshDurationOnly;

    [Header("속성 반응 연소")]
    [Tooltip("reactionMaxHpDamageRatioPerTick이 0일 때만 Combat.damage 기반 반응 초당 데미지 fallback에 곱해집니다.")]
    [Min(0.0f)] public float reactionDamageMultiplier = 0.0f;
    [Tooltip("Frost, Poison, Electro와 반응한 연소의 틱당 최대체력 비례 피해입니다.")]
    [Range(0.0f, 1.0f)] public float reactionMaxHpDamageRatioPerTick = 0.02f;
    [Tooltip("반응 연소의 틱 간격입니다.")]
    [Min(0.01f)] public float reactionTickInterval = 0.5f;

    [Header("보스 보정")]
    [Tooltip("보스 대상 Ignition 틱데미지 배율입니다.")]
    [Min(0.0f)] public float bossDamageMultiplier = 1.0f;

    [Header("속성 상호작용")]
    public IgnitionInteractionFlags interactionFlags = IgnitionInteractionFlags.Frost | IgnitionInteractionFlags.Poison | IgnitionInteractionFlags.Electro;

    public bool HasIgnitionStatus
    {
        get
        {
            return (damageMultiplier > 0.0f || maxHpDamageRatioPerTick > 0.0f) && tickInterval > 0.0f && duration > 0.0f && maxStackCount > 0;
        }
    }

    // 현재 터렛 레벨과 원본 초당 데미지 기준으로 Ignition 상태 전달 값을 계산한다
    public IgnitionStatusPayload CreatePayload(int level, float sourceDamagePerSecond)
    {
        return CreatePayload(level, sourceDamagePerSecond, null);
    }

    // 현재 터렛 레벨과 성장 프로필 기준으로 Ignition 상태 전달 값을 계산한다
    public IgnitionStatusPayload CreatePayload(int level, float sourceDamagePerSecond, TurretStatGrowthProfileSO growthProfile)
    {
        float safeDamagePerSecond = Mathf.Max(0.0f, sourceDamagePerSecond) * Mathf.Max(0.0f, damageMultiplier);
        float safeMaxHpDamageRatioPerTick = growthProfile == null
            ? Mathf.Clamp01(maxHpDamageRatioPerTick)
            : growthProfile.CalculateIgnitionMaxHpDamageRatioPerTick(maxHpDamageRatioPerTick, level);
        float safeReactionDamagePerSecond = Mathf.Max(0.0f, sourceDamagePerSecond) * Mathf.Max(0.0f, reactionDamageMultiplier);
        float safeReactionMaxHpDamageRatioPerTick = growthProfile == null
            ? Mathf.Clamp01(reactionMaxHpDamageRatioPerTick)
            : growthProfile.CalculateIgnitionReactionMaxHpDamageRatioPerTick(reactionMaxHpDamageRatioPerTick, level);
        float safeDuration = growthProfile == null
            ? Mathf.Max(0.0f, duration)
            : growthProfile.CalculateIgnitionDuration(duration, level);
        float safeReactionTickInterval = growthProfile == null
            ? Mathf.Max(0.01f, reactionTickInterval)
            : growthProfile.CalculateIgnitionReactionTickInterval(reactionTickInterval, level);
        IgnitionStatusPayload payload = new IgnitionStatusPayload
        {
            hasIgnitionStatus = HasIgnitionStatus && (safeDamagePerSecond > 0.0f || safeMaxHpDamageRatioPerTick > 0.0f),
            damagePerSecond = safeDamagePerSecond,
            maxHpDamageRatioPerTick = safeMaxHpDamageRatioPerTick,
            tickInterval = Mathf.Max(0.01f, tickInterval),
            duration = safeDuration,
            maxStackCount = Mathf.Max(1, maxStackCount),
            stackRefreshMode = stackRefreshMode,
            reactionDamagePerSecond = safeReactionDamagePerSecond,
            reactionMaxHpDamageRatioPerTick = safeReactionMaxHpDamageRatioPerTick,
            reactionTickInterval = safeReactionTickInterval,
            bossDamageMultiplier = Mathf.Max(0.0f, bossDamageMultiplier),
            interactionFlags = interactionFlags
        };

        return payload;
    }

    // 인스펙터에서 입력한 Ignition 상태 값을 안전한 범위로 보정한다
    private void OnValidate()
    {
        damageMultiplier = Mathf.Max(0.0f, damageMultiplier);
        maxHpDamageRatioPerTick = Mathf.Clamp01(maxHpDamageRatioPerTick);
        tickInterval = Mathf.Max(0.01f, tickInterval);
        duration = Mathf.Max(0.0f, duration);
        maxStackCount = Mathf.Max(1, maxStackCount);
        reactionDamageMultiplier = Mathf.Max(0.0f, reactionDamageMultiplier);
        reactionMaxHpDamageRatioPerTick = Mathf.Clamp01(reactionMaxHpDamageRatioPerTick);
        reactionTickInterval = Mathf.Max(0.01f, reactionTickInterval);
        bossDamageMultiplier = Mathf.Max(0.0f, bossDamageMultiplier);
    }
}
