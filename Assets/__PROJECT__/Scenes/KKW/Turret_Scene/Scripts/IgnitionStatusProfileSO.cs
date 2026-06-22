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
/// Ignition 터렛의 연소 틱데미지, 지속시간, 중첩 정책, 상호작용 플래그, 사망 연출 기본값을 관리하는 상태이상 프로필.
/// </summary>
[CreateAssetMenu(menuName = "Project Z Defense/Ignition Status Profile")]
public class IgnitionStatusProfileSO : ScriptableObject
{
    [Header("연소 기본값")]
    [Min(0.0f)] public float damageMultiplier = 0.0f;
    [Range(0.0f, 1.0f)] public float maxHpDamageRatioPerTick = 0.01f;
    [Min(0.01f)] public float tickInterval = 1.0f;
    [Min(0.0f)] public float duration = 10.0f;
    [Min(1)] public int maxStackCount = 1;
    public IgnitionStackRefreshMode stackRefreshMode = IgnitionStackRefreshMode.RefreshDurationOnly;

    [Header("보스 보정")]
    [Min(0.0f)] public float bossDamageMultiplier = 1.0f;

    [Header("사망 연출")]
    public GameObject burnDeathEffectPrefab;
    [Min(0.0f)] public float burnDeathEffectDuration = 2.0f;

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
        float safeMaxHpDamageRatioPerTick = Mathf.Clamp01(maxHpDamageRatioPerTick);
        IgnitionStatusPayload payload = new IgnitionStatusPayload
        {
            hasIgnitionStatus = HasIgnitionStatus && (safeDamagePerSecond > 0.0f || safeMaxHpDamageRatioPerTick > 0.0f),
            damagePerSecond = safeDamagePerSecond,
            maxHpDamageRatioPerTick = safeMaxHpDamageRatioPerTick,
            tickInterval = Mathf.Max(0.01f, tickInterval),
            duration = Mathf.Max(0.0f, duration),
            maxStackCount = Mathf.Max(1, maxStackCount),
            stackRefreshMode = stackRefreshMode,
            bossDamageMultiplier = Mathf.Max(0.0f, bossDamageMultiplier),
            burnDeathEffectPrefab = burnDeathEffectPrefab,
            burnDeathEffectDuration = Mathf.Max(0.0f, burnDeathEffectDuration),
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
        bossDamageMultiplier = Mathf.Max(0.0f, bossDamageMultiplier);
        burnDeathEffectDuration = Mathf.Max(0.0f, burnDeathEffectDuration);
    }
}
