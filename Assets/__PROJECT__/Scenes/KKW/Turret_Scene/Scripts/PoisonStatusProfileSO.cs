using UnityEngine;

public enum PoisonStackRefreshMode
{
    RefreshDurationOnly,
    AddStackAndRefreshDuration
}

/// <summary>
/// Poison 터렛의 중독 틱데미지, 지속시간, 중첩 정책, 보스 보정값을 관리하는 상태이상 프로필.
/// </summary>
[CreateAssetMenu(menuName = "Project Z Defense/Poison Status Profile")]
public class PoisonStatusProfileSO : ScriptableObject
{
    [Header("중독 기본값")]
    [Range(0.0f, 1.0f)] public float maxHpDamageRatioPerTick = 0.01f;
    [Min(0.01f)] public float tickInterval = 1.0f;
    [Min(0.0f)] public float duration = 4.0f;
    [Min(1)] public int maxStackCount = 1;
    public PoisonStackRefreshMode stackRefreshMode = PoisonStackRefreshMode.AddStackAndRefreshDuration;

    [Header("보스 보정")]
    [Min(0.0f)] public float bossDamageMultiplier = 1.0f;

    public bool HasPoisonStatus
    {
        get
        {
            return maxHpDamageRatioPerTick > 0.0f && tickInterval > 0.0f && duration > 0.0f && maxStackCount > 0;
        }
    }

    // 현재 터렛 레벨 기준으로 Poison 상태 전달 값을 계산한다
    public PoisonStatusPayload CreatePayload(int level)
    {
        PoisonStatusPayload payload = new PoisonStatusPayload
        {
            hasPoisonStatus = HasPoisonStatus,
            maxHpDamageRatioPerTick = Mathf.Clamp01(maxHpDamageRatioPerTick),
            tickInterval = Mathf.Max(0.01f, tickInterval),
            duration = Mathf.Max(0.0f, duration),
            maxStackCount = Mathf.Max(1, maxStackCount),
            stackRefreshMode = stackRefreshMode,
            bossDamageMultiplier = Mathf.Max(0.0f, bossDamageMultiplier)
        };

        return payload;
    }

    // 인스펙터에서 입력한 Poison 상태 값을 안전한 범위로 보정한다
    private void OnValidate()
    {
        maxHpDamageRatioPerTick = Mathf.Clamp01(maxHpDamageRatioPerTick);
        tickInterval = Mathf.Max(0.01f, tickInterval);
        duration = Mathf.Max(0.0f, duration);
        maxStackCount = Mathf.Max(1, maxStackCount);
        bossDamageMultiplier = Mathf.Max(0.0f, bossDamageMultiplier);
    }
}
