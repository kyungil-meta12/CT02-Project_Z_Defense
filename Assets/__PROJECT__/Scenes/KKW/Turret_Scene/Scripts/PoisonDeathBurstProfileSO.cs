using UnityEngine;

/// <summary>
/// Poison 처형 확정 대상 사망 시 발생하는 폭발 이펙트와 약한 범위 중독 값을 관리하는 프로필.
/// </summary>
[CreateAssetMenu(menuName = "Project Z Defense/Poison Death Burst Profile")]
public class PoisonDeathBurstProfileSO : ScriptableObject
{
    [Header("폭발 이펙트")]
    public GameObject burstEffectPrefab;
    [Min(0.0f)] public float effectDuration = 3.0f;
    public Vector3 effectPositionOffset;

    [Header("범위 중독")]
    [Min(0.0f)] public float radius = 2.5f;
    public LayerMask targetLayerMask = Physics.DefaultRaycastLayers;
    [Range(0.0f, 1.0f)] public float maxHpDamageRatioPerTick = 0.002f;
    [Min(0.01f)] public float tickInterval = 1.0f;
    [Min(0.0f)] public float duration = 2.0f;
    [Min(1)] public int maxStackCount = 1;
    public PoisonStackRefreshMode stackRefreshMode = PoisonStackRefreshMode.RefreshDurationOnly;

    [Header("보스 보정")]
    [Min(0.0f)] public float bossDamageMultiplier = 1.0f;

    [Header("연쇄 폭발")]
    public bool allowChainDeathBurst = true;

    public bool HasBurst
    {
        get
        {
            return burstEffectPrefab != null || HasWeakPoison;
        }
    }

    public bool HasWeakPoison
    {
        get
        {
            return radius > 0.0f && maxHpDamageRatioPerTick > 0.0f && tickInterval > 0.0f && duration > 0.0f && maxStackCount > 0;
        }
    }

    // 인스펙터에서 입력한 범위 중독 값을 안전한 범위로 보정한다
    private void OnValidate()
    {
        effectDuration = Mathf.Max(0.0f, effectDuration);
        radius = Mathf.Max(0.0f, radius);
        maxHpDamageRatioPerTick = Mathf.Clamp01(maxHpDamageRatioPerTick);
        tickInterval = Mathf.Max(0.01f, tickInterval);
        duration = Mathf.Max(0.0f, duration);
        maxStackCount = Mathf.Max(1, maxStackCount);
        bossDamageMultiplier = Mathf.Max(0.0f, bossDamageMultiplier);
    }
}
