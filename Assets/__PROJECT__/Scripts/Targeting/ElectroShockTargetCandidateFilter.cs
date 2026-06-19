using UnityEngine;

/// <summary>
/// Electro 터렛이 Shock 스택을 더 쌓기 어려운 대상을 첫 타겟 후보에서 제외한다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Project Z Defense/Targeting/Electro Shock Target Candidate Filter")]
public sealed class ElectroShockTargetCandidateFilter : MonoBehaviour, ITargetCandidateFilter, ITargetCandidateFallbackFilter
{
    [Header("Electro 타겟 제외 조건")]
    [SerializeField, Tooltip("켜면 과부하 긴 기절 중인 대상을 첫 타겟 후보에서 제외합니다.")]
    private bool excludeOverloadStunnedTargets = true;
    [SerializeField, Tooltip("켜면 이미 최대 Shock 스택에 도달한 대상을 첫 타겟 후보에서 제외합니다.")]
    private bool excludeFullShockStackTargets = true;
    [SerializeField, Min(1), Tooltip("최대 Shock 스택으로 판단할 기준값입니다. 현재 Electro 기본값은 3입니다.")]
    private int fullShockStackCount = 3;

    public string DebugExcludeReason => "Electro Shock 스택 추가 효율이 낮은 타겟은 탐색에서 제외합니다.";

    // Electro Shock 스택 추가가 비효율적인 타겟 후보를 제외한다
    public bool ShouldExcludeTarget(Transform targetTransform, IDamageable damageable)
    {
        if (targetTransform == null)
        {
            return false;
        }

        ElectroStatusRuntime electroStatusRuntime = targetTransform.GetComponentInParent<ElectroStatusRuntime>();
        if (electroStatusRuntime == null)
        {
            return false;
        }

        if (excludeOverloadStunnedTargets && electroStatusRuntime.IsOverloadStunActive)
        {
            return true;
        }

        return excludeFullShockStackTargets &&
               electroStatusRuntime.ShockStackCount >= Mathf.Max(1, fullShockStackCount);
    }

    // 일반 후보가 없을 때 3스택 대상은 타이머 갱신용 fallback 타겟으로 허용한다
    public bool ShouldAllowFallbackTarget(Transform targetTransform, IDamageable damageable)
    {
        if (targetTransform == null)
        {
            return false;
        }

        ElectroStatusRuntime electroStatusRuntime = targetTransform.GetComponentInParent<ElectroStatusRuntime>();
        if (electroStatusRuntime == null)
        {
            return false;
        }

        if (excludeOverloadStunnedTargets && electroStatusRuntime.IsOverloadStunActive)
        {
            return false;
        }

        return excludeFullShockStackTargets &&
               electroStatusRuntime.ShockStackCount >= Mathf.Max(1, fullShockStackCount);
    }

    // 인스펙터 입력값을 안전한 범위로 보정한다
    private void OnValidate()
    {
        fullShockStackCount = Mathf.Max(1, fullShockStackCount);
    }
}
